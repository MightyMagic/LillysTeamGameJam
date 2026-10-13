using System.Collections.Generic;
using UnityEngine;

namespace NightShift
{
    /// Cached access to generated sprites (Assets/Resources/Sprites) and shared materials.
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material unlit;
        static Font font;

        public static Sprite S(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = Resources.Load<Sprite>("Sprites/" + name);
            if (s == null) Debug.LogWarning("[NightShift] Missing sprite: " + name);
            cache[name] = s;
            return s;
        }

        public static Material Unlit
        {
            get
            {
                if (unlit == null)
                {
                    unlit = Resources.Load<Material>("Materials/Unlit");
                    if (unlit == null) unlit = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                }
                return unlit;
            }
        }

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        // Sorting: everything that stands on the floor is y-sorted, flat things sit below.
        public const int OrderFloor = -32000;
        public const int OrderWalls = -31000;
        public const int OrderWallDecor = -30900;
        public const int OrderCones = -30000;
        public const int OrderFloorItems = -29000;
        public const int OrderBodies = -28000;
        public const int OrderFx = 9000;
        public const int OrderIcons = 10000;

        public static int SortY(float y) => Mathf.RoundToInt(-y * 32f);

        public static SpriteRenderer MakeSprite(Transform parent, string name, string sprite, Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(sprite);
            sr.sortingOrder = order;
            // gameplay markers ignore 2D lighting so they stay readable in the dark
            if (sprite.StartsWith("fx_") || sprite.StartsWith("ui_")) sr.sharedMaterial = Unlit;
            return sr;
        }
    }
}
