using UnityEngine;

namespace NightShift
{
    /// Base for anything the context button can act on. Shows a pulsing cyan marker
    /// so interactive objects read differently from decor (GDD §5).
    public abstract class Interactable : MonoBehaviour
    {
        public Vector2 Pos;
        public float range = 1.3f;
        public bool autoPickup;

        SpriteRenderer marker;
        float markerHeight;

        public virtual bool Available => true;
        public abstract string Prompt { get; }
        public abstract void Interact(Player p);

        protected void SetupMarker(float height, Color color)
        {
            markerHeight = height;
            marker = Art.MakeSprite(transform, "Marker", "fx_marker", Vector3.zero, Art.OrderIcons - 1);
            marker.color = color;
            marker.transform.position = Pos + Vector2.up * height;
        }

        protected virtual void Update()
        {
            if (marker == null) return;
            marker.enabled = Available;
            float t = Time.time * 3f + Pos.x;
            marker.transform.position = Pos + Vector2.up * (markerHeight + Mathf.Sin(t) * 0.08f);
            var c = marker.color;
            c.a = 0.55f + 0.45f * Mathf.Sin(t * 1.3f);
            marker.color = c;
        }

        protected virtual void OnEnable()
        {
            if (Game.I != null) Game.I.Interactables.Add(this);
        }

        protected virtual void OnDisable()
        {
            if (Game.I != null) Game.I.Interactables.Remove(this);
        }
    }
}
