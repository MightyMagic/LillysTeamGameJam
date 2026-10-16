using UnityEngine;

namespace NightShift
{
    /// Keycard door. Blocks movement, sight and guard paths while closed.
    public class LockedDoor : Interactable
    {
        Level level;
        SpriteRenderer sr;
        BoxCollider2D col;
        public bool IsOpen { get; private set; }

        public void Init(Level lvl, int x, int y)
        {
            level = lvl;
            Pos = Level.Center(x, y);
            range = 1.4f;
            transform.position = Pos;
            sr = Art.MakeSprite(transform, "Door", "prop_door_locked", Vector3.zero, Art.OrderWalls + 1);
            sr.transform.localPosition = Vector3.zero;
            col = gameObject.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            gameObject.layer = 0;
            SetupMarker(0.8f, new Color(1f, 0.6f, 0.5f));
        }

        public override bool Available => !IsOpen;
        public override string Prompt => Game.I.Mission.hasKeycard ? Loc.T("prompt.door") : Loc.T("prompt.door_locked");

        public override void Interact(Player p)
        {
            if (IsOpen) return;
            if (!Game.I.Mission.hasKeycard)
            {
                Game.I.Toast(Loc.T("toast.door_locked"));
                Sfx.Play("empty");
                return;
            }
            level.OpenDoorsNear(Pos);
            Sfx.Play("door");
        }

        public void SetOpen()
        {
            IsOpen = true;
            col.enabled = false;
            sr.enabled = false;
        }
    }
}
