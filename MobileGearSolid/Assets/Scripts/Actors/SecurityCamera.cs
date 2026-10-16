using UnityEngine;

namespace NightShift
{
    /// Wall camera sweeping a red sector. Disabled while the power is cut.
    public class SecurityCamera : MonoBehaviour
    {
        const float Range = 6.5f, FOV = 50f, Sweep = 40f, Period = 7f;

        float baseAngle, angle, t;
        public float suspicion;
        public bool seesPlayer;
        Vector2 eye;
        VisionCone cone;
        SpriteRenderer sr;

        public void Init(Vector2 tileCenter, Vector2 dir)
        {
            baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            eye = tileCenter + dir * 0.6f;
            t = Random.value * Period;
            transform.position = tileCenter + dir * 0.35f + (dir == Vector2.down ? Vector2.up * 0.1f : Vector2.zero);
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = Art.S("prop_camera");
            sr.sortingOrder = Art.OrderWallDecor + 1;
            cone = VisionCone.Create(transform.parent, "CameraCone");
        }

        void Update()
        {
            if (Game.I.phase != Game.Phase.Playing) return;
            if (!Game.I.Level.PowerOn)
            {
                cone.Hide();
                sr.color = new Color(0.4f, 0.4f, 0.4f);
                suspicion = 0f;
                return;
            }

            float dt = Time.deltaTime;
            t += dt;
            angle = baseAngle + Mathf.Sin(t * 2f * Mathf.PI / Period) * Sweep;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            var p = Game.I.Player;
            bool sees = false;
            if (p != null && !p.dead && !p.HiddenFromCameras)
            {
                Vector2 d = p.Center - eye;
                sees = d.magnitude < Range && Vector2.Angle(new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)), d) < FOV * 0.5f
                       && !Physics2D.Linecast(eye, p.Center, Level.ObstacleMask);
            }

            seesPlayer = sees;
            var m = Game.I.Mission;
            if (sees)
            {
                if (m.alertActive) Game.I.ReportSighting(p.Pos);
                else
                {
                    if (suspicion <= 0f) Sfx.Play("beep", 0.5f);
                    suspicion += 1.3f * dt;
                    if (suspicion >= 1f)
                    {
                        Game.I.RaiseAlert(Loc.T("reason.camera"), p.Pos);
                        suspicion = 0f;
                    }
                }
            }
            else suspicion = Mathf.Max(0f, suspicion - 0.5f * dt);

            bool blink = sees && Mathf.Repeat(Time.time * 8f, 1f) > 0.5f;
            sr.color = blink ? new Color(1f, 0.5f, 0.5f) : Color.white;
            cone.Draw(eye, angle, FOV, Range, new Color(1f, 0.12f, 0.1f, 0.22f + suspicion * 0.25f));
        }
    }
}
