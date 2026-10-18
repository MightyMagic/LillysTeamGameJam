using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    /// Presentation only: preserve the world frame's aspect and keep the map clear of controls.
    public static class GameViewport
    {
        public const float TargetArea = .4f;
        const float ControlGap = 15f;

        public static float CanvasScale(int width, int height) =>
            Mathf.Sqrt(width / 1920f * height / 1080f);

        // These are the unchanged compact-HUD home positions, not the stick's moving touch position.
        public static void AddControlHomes(List<Rect> rectangles, int width, int height)
        {
            float s = CanvasScale(width, height);
            rectangles.Add(Normalize(new Rect(80 * s, 80 * s, 300 * s, 300 * s), width, height));
            rectangles.Add(Normalize(new Rect(width - 275 * s, 85 * s, 210 * s, 210 * s), width, height));
            rectangles.Add(Normalize(new Rect(width - 515 * s, 35 * s, 190 * s, 190 * s), width, height));
            rectangles.Add(Normalize(new Rect(width - 220 * s, 350 * s, 140 * s, 140 * s), width, height));
        }
        public static Rect Normalize(Rect r, int width, int height) =>
            new Rect(r.x / width, r.y / height, r.width / width, r.height / height);

        public static Rect Calculate(Rect worldFrame, int width, int height, Rect safePixels, IList<Rect> exclusions)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            float screenAspect = width / (float)height;
            float ratio = worldFrame.width / worldFrame.height / screenAspect;
            float scale = CanvasScale(width, height);
            var gutter = new Vector2(ControlGap * scale / width, ControlGap * scale / height);
            var safe = Normalize(safePixels, width, height);
            safe.xMin = Mathf.Clamp01(safe.xMin + gutter.x); safe.xMax = Mathf.Clamp01(safe.xMax - gutter.x);
            safe.yMin = Mathf.Clamp01(safe.yMin + gutter.y); safe.yMax = Mathf.Clamp01(safe.yMax - gutter.y);
            float preferredCenter = Mathf.Clamp(.48f, safe.xMin, safe.xMax), top = Mathf.Min(.86f, safe.yMax);
            float maxHeight = Mathf.Min(.82f, Mathf.Sqrt(TargetArea / ratio), top - safe.yMin, safe.width / ratio);
            Rect At(float center, float h) => new Rect(center - h * ratio * .5f, top - h, h * ratio, h);
            bool Clear(Rect candidate)
            {
                if (exclusions == null) return true;
                foreach (var source in exclusions)
                {
                    var obstacle = source;
                    obstacle.min -= gutter; obstacle.max += gutter;
                    if (candidate.Overlaps(obstacle)) return false;
                }
                return true;
            }
            bool Place(float h, out float center)
            {
                float half = h * ratio * .5f, left = safe.xMin + half, right = safe.xMax - half;
                float best = preferredCenter, distance = float.PositiveInfinity;
                void Consider(float x)
                {
                    if (x < left || x > right || !Clear(At(x, h))) return;
                    float d = Mathf.Abs(x - preferredCenter);
                    if (d < distance) { distance = d; best = x; }
                }
                Consider(Mathf.Clamp(preferredCenter, left, right));
                Consider(left); Consider(right);
                if (exclusions != null)
                    foreach (var obstacle in exclusions)
                    {
                        Consider(obstacle.xMin - gutter.x - half - .000001f);
                        Consider(obstacle.xMax + gutter.x + half + .000001f);
                    }
                center = best;
                return !float.IsPositiveInfinity(distance);
            }
            // Prefer the approved centre. A small sideways adjustment can retain a larger map
            // between the unchanged controls; only then reduce the target area if it cannot fit.
            float low = 0, high = Mathf.Max(0, maxHeight), fittedCenter;
            if (Place(high, out fittedCenter)) return At(fittedCenter, high);
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * .5f;
                if (Place(middle, out fittedCenter)) low = middle; else high = middle;
            }
            Place(low, out fittedCenter);
            return At(fittedCenter, low);
        }
    }
}
