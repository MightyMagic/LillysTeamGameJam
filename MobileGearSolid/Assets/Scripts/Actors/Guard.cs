using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MobileGearSolid
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
        const float LookAroundTime = 2.5f;

        public GuardState state;
        public bool bodyFound;
        public float suspicion;
        public int hp, maxHp;
        SpriteRenderer[] hpPips;
        Transform hpBar;
        /// After a non-lethal hit the guard staggers; if not finished off in time he raises the alarm.
        public const float WoundStun = 1.3f;
        float stunTimer;

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
        float? fixedLook;
        float speedMul = 1f;
        List<Vector2> path = new List<Vector2>();
        int pathIdx;
        Vector2 velocity;

        float facingAngle = -90f, targetAngle = -90f, lookBase;
        float waitTimer, stateTimer, lostSightTimer, repathTimer, shootCooldown;
        bool seesPlayer, searchArrived;
        Vector2 lastSeen, investigatePos;
        float iconPop;

        public bool Dead => state == GuardState.Dead;
        public Vector2 Pos => rb != null ? rb.position : (Vector2)transform.position;
        public Vector2 Center => Pos + Vector2.up * 0.45f;
        Vector2 Facing => new Vector2(Mathf.Cos(facingAngle * Mathf.Deg2Rad), Mathf.Sin(facingAngle * Mathf.Deg2Rad));

        public static Guard Spawn(Level level, List<Vector2> route, GuardDef def = null)
        {
            var go = new GameObject("Guard");
            go.transform.SetParent(level.transform, false);
            go.transform.position = route[0];
            go.layer = Level.CharacterLayer;
            var g = go.AddComponent<Guard>();
            g.level = level;
            g.route = route;
            g.hp = Meta.GuardHp;
            if (def != null)
            {
                g.fixedLook = def.look;
                g.speedMul = Mathf.Max(0.2f, def.speed);
                if (def.hp > 0) g.hp = def.hp;
            }
            g.rb = go.AddComponent<Rigidbody2D>();
            g.rb.bodyType = RigidbodyType2D.Kinematic;
            g.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.28f;
            col.offset = new Vector2(0, 0.22f);

            g.sr = go.AddComponent<SpriteRenderer>();
            g.sr.sharedMaterial = Art.Lit;
            if (g.hp > Meta.GuardHp) g.sr.color = new Color(0.72f, 0.8f, 1f); // armoured: bluish tint
            g.anim = new CharAnim("guard");
            g.icon = Art.MakeSprite(go.transform, "StateIcon", "fx_suspect", new Vector3(0, 1.6f), Art.OrderIcons);
            g.icon.enabled = false;
            g.BuildHpBar();
            g.cone = VisionCone.Create(level.transform, "GuardCone");

            g.flashlight = Level.MakeLight(go.transform, g.Center, new Color(1f, 0.93f, 0.7f), 0.9f, ViewDist);
            g.flashlight.transform.localPosition = new Vector3(0, 0.45f);
            g.flashlight.pointLightInnerAngle = FOV * 0.6f;
            g.flashlight.pointLightOuterAngle = FOV + 10f;
            g.flashlight.pointLightInnerRadius = 0.5f;

            g.tracer = MakeTracer(go.transform, new Color(1f, 0.5f, 0.3f));
            g.ResetToStart();
            return g;
        }

        /// Back to the first waypoint, calm. Used on spawn and by tutorial checkpoints.
        public void ResetToStart()
        {
            if (Dead) return;
            rb.position = route[0];
            transform.position = route[0];
            state = GuardState.Patrol;
            suspicion = 0f;
            waitTimer = 0f;
            routeIdx = 0;
            float a = fixedLook ?? (route.Count > 1 ? AngleOf(route[1] - route[0]) : -90f);
            facingAngle = targetAngle = lookBase = a;
            GoTo(route[0]);
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

            if (stunTimer > 0f)
            {
                stunTimer -= dt;
                velocity = Vector2.zero;
                if (stunTimer <= 0f && Game.I.Player != null)
                    Game.I.RaiseAlert(Loc.T("reason.wounded"), Game.I.Player.Pos);
                UpdateVisuals(dt);
                return;
            }

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
            seesPlayer = p != null && !p.dead && !p.HiddenFromGuards && CanSee(p.Center, range);
            var m = Game.I.Mission;

            // an alerted guard bumping into the box kicks it over
            if (p != null && p.InBox && state == GuardState.Alert && Vector2.Distance(Pos, p.Pos) < 1.1f)
            {
                p.ExitBox();
                Game.I.ReportSighting(p.Pos);
                Game.I.UI.ShowBanner(Loc.T("reason.box"), UIKit.Red, 2.5f);
                seesPlayer = true;
            }

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
                    if (p.BoxMoving) rate *= p.boxMaster ? 0.25f : 0.5f; // "was that box... moving?" (Box Master card: harder to notice)
                    rate *= p.stealthMul;
                    suspicion += rate * dt;
                    if (suspicion >= 1f)
                    {
                        Game.I.RaiseAlert(Loc.T(p.InBox ? "reason.box_moving" : "reason.guard"), p.Pos);
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
                        Game.I.RaiseAlert(Loc.T("reason.body"), other.Pos);
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

        /// Moves along the current path; returns true when the end is reached (or unreachable).
        bool FollowPath(float speed)
        {
            while (pathIdx < path.Count && Vector2.Distance(Pos, path[pathIdx]) < 0.12f) pathIdx++;
            if (pathIdx >= path.Count)
            {
                velocity = Vector2.zero;
                return true;
            }
            Vector2 d = path[pathIdx] - Pos;
            velocity = d.normalized * speed * speedMul;
            targetAngle = AngleOf(d);
            return false;
        }

        /// Calm left-right head sweep around lookBase (never a full spin).
        void LookAround(float t, float amplitude) => targetAngle = lookBase + Mathf.Sin(t * 2.2f) * amplitude;

        // ------------------------------------------------------------------ states

        void UpdatePatrol(float dt)
        {
            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                velocity = Vector2.zero;
                if (fixedLook.HasValue && route.Count == 1) targetAngle = fixedLook.Value;
                else LookAround(waitTimer, 60f);
                return;
            }
            if (FollowPath(WalkSpeed))
            {
                lookBase = fixedLook ?? facingAngle;
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
            searchArrived = false;
            GoTo(pos);
        }

        void UpdateSearch(float dt)
        {
            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                velocity = Vector2.zero;
                targetAngle = AngleOf(investigatePos - Pos);
                return;
            }
            if (!searchArrived)
            {
                if (!FollowPath(SearchSpeed)) return;
                searchArrived = true;
                lookBase = facingAngle;
            }
            stateTimer += dt;
            LookAround(stateTimer, 85f);
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
            if (state != GuardState.Alert)
            {
                iconPop = 1f;
                searchArrived = false;
                stateTimer = 0f;
            }
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
                searchArrived = false;
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
                return;
            }

            if (!searchArrived)
            {
                // head for the last known position; re-plan when it changes
                if (repathTimer <= 0f) { GoTo(m.lastKnownPos); repathTimer = 0.6f; }
                if (FollowPath(ChaseSpeed))
                {
                    searchArrived = true;
                    stateTimer = 0f;
                    lookBase = facingAngle;
                }
                return;
            }

            // at the spot: look around, then sweep nearby points instead of spinning in place
            stateTimer += dt;
            if (stateTimer < LookAroundTime)
            {
                velocity = Vector2.zero;
                LookAround(stateTimer, 80f);
                return;
            }
            if (FollowPath(SearchSpeed))
            {
                GoTo(level.RandomWalkableNear(m.lastKnownPos, 4));
                stateTimer = 0f;
                lookBase = facingAngle;
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
                Game.I.Toast(Loc.T("toast.power_fixed"));
                ReturnToPatrol();
            }
        }

        public void OnNoise(Vector2 pos)
        {
            if (Dead || state == GuardState.Alert) return;
            if (state != GuardState.Search) Sfx.Play("suspect", 0.6f);
            StartSearch(pos, 0.7f);
        }

        /// A pistol hit: dies at 0 hp, otherwise staggers and will call for help.
        public void TakeHit(int damage)
        {
            if (Dead) return;
            hp -= damage;
            if (hp <= 0) { Kill(); return; }
            stunTimer = WoundStun;
            velocity = Vector2.zero;
            iconPop = 1f;
            Sfx.Play("kill", 0.5f);
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
            hpBar.gameObject.SetActive(false);
            cone.Hide();
            flashlight.enabled = false;
            tracer.enabled = false;
            GetComponent<Collider2D>().enabled = false;
            Sfx.Play("kill");
            Game.I.OnGuardKilled(this);
        }

        // ------------------------------------------------------------------ visuals

        // ------------------------------------------------------------------ health bar

        const float BarWidth = 0.62f, BarHeight = 0.09f, PipGap = 0.04f;

        /// Small segmented bar over the head (one segment per hit point).
        void BuildHpBar()
        {
            maxHp = Mathf.Max(1, hp);
            hpBar = new GameObject("HpBar").transform;
            hpBar.SetParent(transform, false);
            hpBar.localPosition = new Vector3(0, 1.22f);
            var bg = Art.MakeSprite(hpBar, "Bg", "fx_white", Vector3.zero, Art.OrderIcons - 2);
            bg.color = new Color(0, 0, 0, 0.75f);
            bg.transform.localScale = new Vector3((BarWidth + 0.06f) / 0.125f, (BarHeight + 0.06f) / 0.125f, 1f);
            hpPips = new SpriteRenderer[maxHp];
            float w = (BarWidth - PipGap * (maxHp - 1)) / maxHp;
            for (int i = 0; i < maxHp; i++)
            {
                var pip = Art.MakeSprite(hpBar, "Pip", "fx_white", Vector3.zero, Art.OrderIcons - 1);
                pip.transform.localPosition = new Vector3(-BarWidth / 2 + w / 2 + i * (w + PipGap), 0);
                pip.transform.localScale = new Vector3(w / 0.125f, BarHeight / 0.125f, 1f);
                hpPips[i] = pip;
            }
            hpBar.gameObject.SetActive(false);
        }

        /// Shown when the guard is hurt or the player is aiming at him.
        void UpdateHpBar()
        {
            var p = Game.I.Player;
            bool show = !Dead && (hp < maxHp || (p != null && p.Target == this));
            hpBar.gameObject.SetActive(show);
            if (!show) return;
            Color full = maxHp > Meta.GuardHp ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.25f, 0.2f);
            for (int i = 0; i < hpPips.Length; i++)
                hpPips[i].color = i < hp ? full : new Color(0.25f, 0.25f, 0.28f, 0.9f);
        }

        void UpdateVisuals(float dt)
        {
            anim.Apply(sr, Facing, velocity.magnitude, dt);
            sr.sortingOrder = Art.SortY(Pos.y);

            Color c;
            string iconName = null;
            if (stunTimer > 0f)
            {
                c = new Color(1f, 0.15f, 0.1f, 0.15f);
                iconName = Mathf.Repeat(Time.time * 6f, 1f) > 0.4f ? "fx_alert" : null;
            }
            else switch (state)
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
                float s = iconName == "fx_alert" ? 1f : 1f + iconPop * 0.8f;
                icon.transform.localScale = new Vector3(s, s, 1f);
                icon.color = state == GuardState.Patrol ? new Color(1, 1, 1, 0.4f + suspicion) : Color.white;
            }

            UpdateHpBar();

            if (tracer.enabled)
            {
                tracerT -= dt;
                if (tracerT <= 0f) tracer.enabled = false;
            }
        }
    }
}
