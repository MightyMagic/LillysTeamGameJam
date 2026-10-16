using UnityEngine;

namespace MobileGearSolid
{
    /// The hero: free 8-way movement, close-range silenced pistol with auto-targeting,
    /// context interaction, the cardboard box and placeable noise traps.
    public class Player : MonoBehaviour
    {
        public const float Speed = 3.6f, BoxSpeed = 1.8f;
        public const float TargetRange = 4.5f; // the suppressed pistol only works up close
        const float TargetAngle = 65f, SpotRange = 9f;
        public const float ShotNoiseRadius = 4.5f;

        public int hp, maxHp, ammo, decoys;
        public bool dead;
        public Vector2 facing = Vector2.down;
        public Interactable CurrentInteractable { get; private set; }
        public Guard Target { get; private set; }

        public bool InBox => box != null;
        /// A still box is invisible to guards; a moving one only looks suspicious.
        public bool HiddenFromGuards => InBox && stillTime > 0.2f;
        public bool HiddenFromCameras => InBox;
        public bool BoxMoving => InBox && stillTime <= 0.2f;

        /// Text for the action button (null = nothing to do).
        public string ActionPrompt => InBox ? Loc.T("prompt.box_exit") : CurrentInteractable != null ? CurrentInteractable.Prompt : null;

        Rigidbody2D rb;
        SpriteRenderer sr, reticle;
        CharAnim anim;
        LineRenderer tracer;
        HideBox box;
        float tracerT, hurtFlash, invuln, stillTime, boxBob;

        public Vector2 Pos => rb.position;
        public Vector2 Center => Pos + Vector2.up * 0.45f;

        public static Player Spawn(Level level, Vector2 pos, int hp, int ammo, int decoys)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(level.transform, false);
            go.transform.position = pos;
            go.layer = Level.CharacterLayer;
            var p = go.AddComponent<Player>();
            p.rb = go.AddComponent<Rigidbody2D>();
            p.rb.gravityScale = 0f;
            p.rb.freezeRotation = true;
            p.rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            p.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.28f;
            col.offset = new Vector2(0, 0.22f);
            // no friction: slide along walls instead of sticking when pushing diagonally into them
            col.sharedMaterial = new PhysicsMaterial2D("PlayerSlide") { friction = 0f, bounciness = 0f };
            p.sr = go.AddComponent<SpriteRenderer>();
            p.anim = new CharAnim("player");
            p.hp = p.maxHp = hp;
            p.ammo = ammo;
            p.decoys = decoys;

            p.reticle = Art.MakeSprite(level.transform, "Reticle", "ui_ring", Vector3.zero, Art.OrderIcons - 2);
            p.reticle.color = new Color(1f, 0.25f, 0.2f, 0.85f);
            p.reticle.transform.localScale = Vector3.one * 0.45f;
            p.reticle.enabled = false;
            p.tracer = Guard.MakeTracer(go.transform, new Color(1f, 0.95f, 0.6f));

            // small personal light so the player is always readable in the dark
            var l = Level.MakeLight(go.transform, pos, new Color(0.6f, 0.75f, 0.9f), 0.35f, 2.2f);
            l.transform.localPosition = new Vector3(0, 0.4f);
            p.anim.Apply(p.sr, Vector2.down, 0f, 0f);
            return p;
        }

        void Update()
        {
            if (dead) return;
            float dt = Time.deltaTime;
            if (Game.I.phase != Game.Phase.Playing)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 move = GameInput.Move;
            if (move.magnitude < 0.15f) move = Vector2.zero;
            rb.linearVelocity = move * (InBox ? BoxSpeed : Speed);
            if (move != Vector2.zero) facing = move.normalized;
            stillTime = move == Vector2.zero ? stillTime + dt : 0f;

            if (InBox)
            {
                sr.sprite = Art.S("player_box");
                sr.flipX = false;
                boxBob = move == Vector2.zero ? 0f : boxBob + dt * 10f;
                sr.transform.localScale = new Vector3(1f, 1f + Mathf.Abs(Mathf.Sin(boxBob)) * 0.06f, 1f);
            }
            else anim.Apply(sr, facing, rb.linearVelocity.magnitude, dt);
            sr.sortingOrder = Art.SortY(Pos.y);

            FindInteractable();

            Target = InBox ? null : FindTarget(TargetRange);
            reticle.enabled = Target != null;
            if (Target != null)
            {
                reticle.transform.position = Target.Center;
                reticle.transform.Rotate(0, 0, 90f * dt);
            }

            if (GameInput.ConsumeShoot()) Shoot();
            if (GameInput.ConsumeInteract())
            {
                if (InBox) ExitBox();
                else if (CurrentInteractable != null) CurrentInteractable.Interact(this);
            }
            if (GameInput.ConsumeDecoy()) PlaceTrap();

            if (tracer.enabled)
            {
                tracerT -= dt;
                if (tracerT <= 0f) tracer.enabled = false;
            }
            invuln -= dt;
            hurtFlash = Mathf.Max(0f, hurtFlash - dt * 4f);
            sr.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), hurtFlash);
        }

        void FindInteractable()
        {
            CurrentInteractable = null;
            float best = float.MaxValue;
            var list = Game.I.Interactables;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue; // a pickup may have removed itself
                var it = list[i];
                if (!it.Available) continue;
                float d = Vector2.Distance(Pos + Vector2.up * 0.3f, it.Pos);
                if (it.autoPickup)
                {
                    if (d < 0.7f && !InBox) it.Interact(this);
                    continue;
                }
                if (d <= it.range && d < best)
                {
                    best = d;
                    CurrentInteractable = it;
                }
            }
        }

        /// Nearest living guard in a forward cone with clear line of fire (GDD §5).
        Guard FindTarget(float range)
        {
            Guard best = null;
            float bestD = range;
            foreach (var g in Game.I.Guards)
            {
                if (g.Dead) continue;
                Vector2 d = g.Center - Center;
                float dist = d.magnitude;
                if (dist > bestD) continue;
                if (dist > 1.5f && Vector2.Angle(facing, d) > TargetAngle) continue;
                if (Physics2D.Linecast(Center, g.Center, Level.ObstacleMask)) continue;
                best = g;
                bestD = dist;
            }
            return best;
        }

        void Shoot()
        {
            if (InBox) { Game.I.Toast(Loc.T("toast.in_box")); return; }
            if (ammo <= 0)
            {
                Game.I.Toast(Loc.T("toast.no_ammo"));
                Sfx.Play("empty");
                return;
            }
            var t = Target;
            if (t == null)
            {
                // don't waste a bullet: explain why there is no shot
                Game.I.Toast(Loc.T(FindTarget(SpotRange) != null ? "toast.too_far" : "toast.no_target"));
                Sfx.Play("empty");
                return;
            }
            ammo--;
            Vector2 end = t.Center;
            facing = (end - Center).normalized;
            t.Kill();
            tracer.SetPosition(0, Center + facing * 0.4f);
            tracer.SetPosition(1, end);
            tracer.enabled = true;
            tracerT = 0.06f;
            Sfx.Play("shot");
            // the suppressor shrinks the audible radius, but the shot is not silent
            Game.I.EmitNoise(Pos, ShotNoiseRadius);
        }

        /// Drops a noise trap at your feet; it goes off a moment later, so place it and run.
        void PlaceTrap()
        {
            if (InBox) { Game.I.Toast(Loc.T("toast.in_box")); return; }
            if (decoys <= 0)
            {
                Game.I.Toast(Loc.T("toast.no_traps"));
                Sfx.Play("empty");
                return;
            }
            decoys--;
            Game.I.Mission.trapsUsed++;
            NoiseTrap.Spawn(transform.parent, Pos + Vector2.up * 0.2f);
            Game.I.Toast(Loc.T("toast.trap_placed"));
        }

        public void EnterBox(HideBox b)
        {
            box = b;
            b.SetWorn(true, Pos);
            stillTime = 0f;
            Game.I.Mission.hides++;
            Sfx.Play("door", 0.6f);
        }

        public void ExitBox()
        {
            if (box == null) return;
            box.SetWorn(false, Pos + Vector2.up * 0.45f);
            box = null;
            sr.transform.localScale = Vector3.one;
            anim.Apply(sr, facing, 0f, 0f);
            Sfx.Play("door", 0.6f);
        }

        public void Teleport(Vector2 pos)
        {
            ExitBox();
            rb.position = pos;
            transform.position = pos;
            rb.linearVelocity = Vector2.zero;
        }

        public void Hurt(int dmg)
        {
            if (dead || invuln > 0f) return;
            ExitBox();
            hp -= dmg;
            hurtFlash = 1f;
            invuln = 0.7f;
            Sfx.Play("hurt");
            Game.I.Shake(0.25f);
            Game.I.UI.OnPlayerHurt();
            if (hp <= 0)
            {
                hp = 0;
                dead = true;
                rb.linearVelocity = Vector2.zero;
                sr.sprite = Art.S("player_dead");
                sr.color = Color.white;
                sr.sortingOrder = Art.OrderBodies;
                reticle.enabled = false;
                Game.I.PlayerDied();
            }
        }
    }
}
