using UnityEngine;

namespace NightShift
{
    /// 3-direction (down / up / side, side is flipped for left) 4-frame walk cycle.
    public class CharAnim
    {
        readonly Sprite[,] frames = new Sprite[3, 4];
        float walkT;

        public CharAnim(string prefix)
        {
            string[] dirs = { "down", "up", "side" };
            for (int d = 0; d < 3; d++)
                for (int f = 0; f < 4; f++)
                    frames[d, f] = Art.S($"{prefix}_{dirs[d]}_{f}");
        }

        public void Apply(SpriteRenderer sr, Vector2 facing, float speed, float dt)
        {
            int dir;
            bool flip = false;
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y) * 0.9f)
            {
                dir = 2;
                flip = facing.x < 0;
            }
            else dir = facing.y > 0 ? 1 : 0;

            int frame = 0;
            if (speed > 0.05f)
            {
                walkT += dt * (4f + speed * 2.2f);
                frame = Mathf.FloorToInt(walkT) % 4;
            }
            else walkT = 0;

            sr.sprite = frames[dir, frame];
            sr.flipX = flip;
        }
    }
}
