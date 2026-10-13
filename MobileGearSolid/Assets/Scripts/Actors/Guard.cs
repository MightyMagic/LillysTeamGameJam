using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NightShift
{
    public enum GuardState { Patrol, Suspicious, Search, Alert, CheckPanel, Dead }

    /// Guard AI from GDD §6: Patrol -> Suspicious -> Search -> Alert, plus "check the power panel".
    /// Suspicion fills gradually while the player is in the cone, giving time to react.
    public class Guard : MonoBehaviour
    {
        public const float FOV = 80f;
        public const float ViewDist = 6.2f;
        const float WalkSpeed = 1.5f, SearchSpeed = 2.2f, ChaseSpeed = 3.0f;
        const float TurnSpeed = 300f;

        public GuardState state;
        public bool bodyFound;
        public float suspicion;

        Level level;
        Rigidbody2D rb;
        SpriteRenderer sr, icon;
        CharAnim anim;
        VisionCone cone;
        Light2D flashlight;
        LineRenderer tracer;
        float tracerT;

        List<Vector2> route;
        int routeIdx;
        List<Vector2> path = new List<Vector2>();
        int pathIdx;
        Vector2 velocity;

        float facingAngle = -90f, targetAngle = -90f, lookBase;
        float waitTimer, stateTimer, lostSightTimer, repathTimer, shootCooldown;
        bool seesPlayer;
        Vector2 lastSeen, investigatePos;
        float iconPop;

        public bool Dead => state == GuardState.Dead;
        public Vector2 Pos => rb != null ? rb.position : (Vector2)transform.position;
        public Vector2 Center => Pos + Vector2.up * 0.45f;
        Vector2 Facing => new Vector2(Mathf.Cos(facingAngle * Mathf.Deg2Rad), Mathf.Sin(facingAngle * Mathf.Deg2Rad));

        public static Guard Spawn(Level level, List<Vector2> route)
        {
            var go = new GameObject("Guard");
            go.transform.SetParent(level.transform, false);
            go.transform.position = route[0];
            go.layer = Level.CharacterLayer;
            var g = go.AddComponent<Guard>();
            g.level = level;
            g.route = route;
            g.rb = go.AddComponent<Rigidbody2D>();
            g.rb.bodyType = RigidbodyType2D.Kinematic;
            g.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.28f;
            col.offset = new Vector2(0, 0.22f);

            g.sr = go.AddComponent<SpriteRenderer>();
            g.anim = new CharAnim("guard");
            g.icon = Art.MakeSprite(go.transform, "StateIcon", "fx_suspect", new Vector3(0, 1.45f), Art.OrderIcons);
            g.icon.enabled = false;
            g.cone = VisionCone.Create(level.transform, "GuardCone");

            g.flashlight = Level.MakeLight(go.transform, g.Center, new Color(1f, 0.93f, 0.7f), 0.9f, ViewDist);
            g.flashlight.transform.localPosition = new Vector3(0, 0.45f);
            g.flashlight.pointLightInnerAngle = FOV * 0.6f;
            g.flashlight.pointLightOuterAngle = FOV + 10f;
            g.flashlight.pointLightInnerRadius = 0.5f;

            g.tracer = MakeTracer(go.transform, new Color(1f, 0.5f, 0.3f));

            g.routeIdx = 0;
            if (route.Count > 1) g.facingAngle = g.targetAngle = AngleOf(route[1] - route[0]);
            g.GoTo(route[0]);
            return g;
        }

        public static LineRenderer MakeTracer(Transform parent, Color c)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Art.Unlit;
            lr.widthMultiplier = 0.06f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, 0.2f);
            lr.sortingOrder = Art.OrderFx;
            lr.enabled = false;
            return lr;
        }

        static float AngleOf(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ main loop

        void Update()
        {
            if (Dead) return;
            if (Game.I.phase != Game.Phase.Playing)
            {
                velocity = Vector2.zero;
                return;
            }
            float dt = Time.deltaTime;

            Perceive(dt);

            switch (state)
            {
                case GuardState.Patrol: UpdatePatrol(dt); break;
                case GuardState.Suspicious: UpdateSuspicious(dt); break;
                case GuardState.Search: UpdateSearch(dt); break;
                case GuardState.Alert: UpdateAlert(dt); break;
                case GuardState.CheckPanel: UpdateCheckPanel(dt); break;
            }

            facingAngle = Mathf.MoveTowardsAngle(facingAngle, targetAngle, TurnSpeed * dt);
            UpdateVisuals(dt);
        }

        void FixedUpdate()
        {
            if (Dead || rb == null) return;
            rb.MovePosition(rb.position + velocity * Time.fixedDeltaTime);
        }

        // ------------------------------------------------------------------ perception

        float CurrentRange
        {
            get
            {
                float r = ViewDist * (level.PowerOn ? 1f : 0.65f);
                if (state == GuardState.Alert) r *= 1.15f;
                return r;
            }
        }

        public bool CanSee(Vector2 target, float range)
        {
            Vector2 d = target - Center;
            float dist = d.magnitude;
            if (dist > range) return false;
            if (dist > 0.9f && Vector2.Angle(Facing, d) > FOV * 0.5f) return false;
            return !Physics2D.Linecast(Center, target, Level.ObstacleMask);
        }

        void Perceive(float dt)
        {
            var p = Game.I.Player;
            float range = CurrentRange;
            seesPlayer = p != null && !p.dead && CanSee(p.Center, range);
            var m = Game.I.Mission;

            if (seesPlayer)
            {
                lastSeen = p.Pos;
                if (m.alertActive)
                {
                    Game.I.ReportSighting(p.Pos);
                    if (state != GuardState.Alert) EnterAlert();
                }
                else
                {
                    float d = Vector2.Distance(Center, p.Center);
                    float rate = Mathf.Lerp(3.2f, 0.9f, d / range);
                    if (state == GuardState.Search || state == GuardState.Suspicious) rate *= 1.4f;
                    suspicion += rate * dt;
                    if (suspicion >= 1f)
                    {
                        Game.I.RaiseAlert("Тебя заметил охранник", p.Pos);
                        return;
                    }
                    if (suspicion > 0.12f && state != GuardState.Suspicious) EnterSuspicious();
                }
            }
            else suspicion = Mathf.Max(0f, suspicion - 0.3f * dt);

            // discovering neutralised colleagues
            if (!m.alertActive)
            {
                foreach (var other in Game.I.Guards)
                {
                    if (other == this || !other.Dead || other.bodyFound) continue;
                    if (CanSee(other.Pos + Vector2.up * 0.2f, range * 0.85f))
                    {
                        other.bodyFound = true;
                        Game.I.RaiseAlert("Охранник обнаружил тело", other.Pos);
                        return;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ movement

        void GoTo(Vector2 dest)
        {
            path = level.FindPath(Pos, dest);
            pathIdx = 0;
        }

        bool Arrived => pathIdx >= path.Count;

        /// Moves along the current path; returns true when the end is reached.
        bool FollowPath(float speed)
        {
            while (pathIdx < path.Count && Vector2.Distance(Pos, path[pathIdx]) < 0.12f) pathIdx++;
            if (pathIdx >= path.Count)
            {
                velocity = Vector2.zero;
                return true;
            }
            Vector2 d = path[pathIdx] - Pos;
            velocity = d.normalized * speed;
            targetAngle = AngleOf(d);
            return false;
        }

        // ------------------------------------------------------------------ states

        void UpdatePatrol(float dt)
        {
            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                velocity = Vector2.zero;
                targetAngle = lookBase + Mathf.Sin(waitTimer * 2.4f) * 60f;
                return;
            }
            if (FollowPath(WalkSpeed))
            {
                lookBase = facingAngle;
                waitTimer = route.Count > 1 ? 1.8f : 3.5f;
                routeIdx = (routeIdx + 1) % route.Count;
                GoTo(route[routeIdx]);
            }
        }

        void EnterSuspicious()
        {
            if (state != GuardState.Suspicious) Sfx.Play("suspect", 0.6f);
            state = GuardState.Suspicious;
            stateTimer = 0f;
            lostSightTimer = 0f;
            velocity = Vector2.zero;
            iconPop = 1f;
        }

        void UpdateSuspicious(float dt)
        {
            velocity = Vector2.zero;
            targetAngle = AngleOf(lastSeen - Pos);
            stateTimer += dt;
            if (seesPlayer) lostSightTimer = 0f;
            else
            {
                lostSightTimer += dt;
                if (lostSightTimer > 0.9f) StartSearch(lastSeen, 0.2f);
            }
        }

        public void StartSearch(Vector2 pos, float delay)
        {
            if (Dead) return;
            if (state != GuardState.Search) iconPop = 1f;
            state = GuardState.Search;
            investigatePos = pos;
            stateTimer = 0f;
            waitTimer = delay;
            GoTo(pos);
        }

        void UpdateSearch(float dt)
        {
            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                velocity = Vector2.zero;
                targetAngle = AngleOf(investigatePos - Pos);
                lookBase = targetAngle;
                return;
            }
            if (!FollowPath(SearchSpeed)) { lookBase = facingAngle; return; }
            stateTimer += dt;
            targetAngle = lookBase + Mathf.Sin(stateTimer * 2f) * 85f;
            if (stateTimer > 4.5f) ReturnToPatrol();
        }

        void ReturnToPatrol()
        {
            state = GuardState.Patrol;
            suspicion = 0f;
            waitTimer = 0f;
            GoTo(route[routeIdx]);
        }

        public void EnterAlert()
        {
            if (Dead) return;
            if (state != GuardState.Alert) iconPop = 1f;
            state = GuardState.Alert;
            suspicion = 1f;
            shootCooldown = 1.3f; // give the player a moment to react
            repathTimer = 0f;
        }

        void UpdateAlert(float dt)
        {
            var m = Game.I.Mission;
            if (!m.alertActive)
            {
                StartSearch(m.lastKnownPos, 0f);
                return;
            }
            var p = Game.I.Player;
            shootCooldown -= dt;
            repathTimer -= dt;

            if (!seesPlayer) shootCooldown = Mathf.Max(shootCooldown, 0.7f);
            if (seesPlayer)
            {
                float d = Vector2.Distance(Pos, p.Pos);
                if (d > 3.5f)
                {
                    if (repathTimer <= 0f) { GoTo(p.Pos); repathTimer = 0.4f; }
                    FollowPath(ChaseSpeed);
                }
                else velocity = Vector2.zero;
                targetAngle = AngleOf(p.Pos - Pos);
                if (shootCooldown <= 0f && d < 7.5f)
                {
                    Fire(p);
                    shootCooldown = 1.6f;
                }
            }
            else
            {
                if (repathTimer <= 0f) { GoTo(m.lastKnownPos); repathTimer = 0.6f; }
                if (FollowPath(ChaseSpeed))
                {
                    stateTimer += dt;
                    targetAngle = facingAngle + 120f * dt * 3f;
                }
            }
        }

        void Fire(Player p)
        {
            Sfx.Play("gun", 0.7f);
            float d = Vector2.Distance(Pos, p.Pos);
            bool hit = Random.value < Mathf.Lerp(0.6f, 0.25f, d / 7.5f);
            Vector2 end = hit ? p.Center : p.Center + Random.insideUnitCircle * 0.9f;
            tracer.SetPosition(0, Center);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerT = 0.07f;
            if (hit) p.Hurt(1);
        }

        public void SendToPanel()
        {
            if (Dead || state == GuardState.Alert) return;
            state = GuardState.CheckPanel;
            stateTimer = 0f;
            GoTo(level.panel.ServicePoint);
        }

        void UpdateCheckPanel(float dt)
        {
            if (level.PowerOn) { ReturnToPatrol(); return; }
            if (!FollowPath(SearchSpeed)) return;
            targetAngle = 90f;
            stateTimer += dt;
            if (stateTimer > 2.5f)
            {
                level.RestorePower();
                Game.I.Toast("Охранник включил питание");
                ReturnToPatrol();
            }
        }

        public void OnNoise(Vector2 pos)
        {
            if (Dead || state == GuardState.Alert) return;
            if (state != GuardState.Search) Sfx.Play("suspect", 0.6f);
            StartSearch(pos, 0.7f);
        }

        public void Kill()
        {
            if (Dead) return;
            state = GuardState.Dead;
            velocity = Vector2.zero;
            sr.sprite = Art.S("guard_dead");
            sr.flipX = Facing.x < 0;
            sr.sortingOrder = Art.OrderBodies;
            icon.enabled = false;
            cone.Hide();
            flashlight.enabled = false;
            tracer.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            Sfx.Play("kill");
            Game.I.OnGuardKilled(this);
        }

        // ------------------------------------------------------------------ visuals

        void UpdateVisuals(float dt)
        {
            anim.Apply(sr, Facing, velocity.magnitude, dt);
            sr.sortingOrder = Art.SortY(Pos.y);

            Color c;
            string iconName = null;
            switch (state)
            {
                case GuardState.Suspicious: c = new Color(1f, 0.75f, 0.2f, 0.3f); iconName = "fx_suspect"; break;
                case GuardState.Search: c = new Color(1f, 0.6f, 0.2f, 0.26f); iconName = "fx_search"; break;
                case GuardState.Alert: c = new Color(1f, 0.15f, 0.1f, 0.32f); iconName = "fx_alert"; break;
                default:
                    c = Color.Lerp(new Color(1f, 0.95f, 0.6f, 0.16f), new Color(1f, 0.7f, 0.2f, 0.3f), suspicion);
                    if (suspicion > 0.05f) iconName = "fx_suspect";
                    break;
            }
            cone.Draw(Center, facingAngle, FOV, CurrentRange, c);
            flashlight.transform.rotation = Quaternion.Euler(0, 0, facingAngle - 90f);
            flashlight.pointLightOuterRadius = CurrentRange;
            flashlight.color = state == GuardState.Alert ? new Color(1f, 0.6f, 0.5f) : new Color(1f, 0.93f, 0.7f);

            icon.enabled = iconName != null;
            if (iconName != null)
            {
                icon.sprite = Art.S(iconName);
                iconPop = Mathf.Max(0f, iconPop - dt * 4f);
                float s = 1f + iconPop * 0.8f;
                icon.transform.localScale = new Vector3(s, s, 1f);
                icon.color = state == GuardState.Patrol ? new Color(1, 1, 1, 0.4f + suspicion) : Color.white;
            }

            if (tracer.enabled)
            {
                tracerT -= dt;
                if (tracerT <= 0f) tracer.enabled = false;
            }
        }
    }
}
