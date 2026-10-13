using UnityEngine;

namespace NightShift
{
    /// Extraction point. Locked until every non-extract objective is done.
    public class ExitDoor : Interactable
    {
        UnityEngine.Rendering.Universal.Light2D glow;

        public void Init(Vector2 pos)
        {
            Pos = pos;
            range = 1.8f;
            SetupMarker(1.0f, new Color(0.4f, 1f, 0.5f));
            glow = Level.MakeLight(transform, pos + Vector2.down * 0.6f, new Color(0.3f, 1f, 0.45f), 0.8f, 3.5f);
        }

        public override string Prompt => Game.I.Mission.ReadyToExtract ? "Эвакуироваться" : "Выход заблокирован";

        public override void Interact(Player p)
        {
            if (Game.I.Mission.ReadyToExtract)
            {
                Game.I.CompleteMission();
                return;
            }
            Game.I.Toast("Сначала: " + MissionDef.ObjectiveLabel(Game.I.Mission.Current));
            Sfx.Play("empty");
        }

        protected override void Update()
        {
            base.Update();
            bool ready = Game.I.Mission != null && Game.I.Mission.ReadyToExtract;
            glow.color = ready ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.3f, 0.25f);
            glow.intensity = ready ? 0.9f + Mathf.Sin(Time.time * 4f) * 0.3f : 0.5f;
        }
    }
}
