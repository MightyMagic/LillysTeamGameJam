using UnityEngine;
using UnityEngine.InputSystem;

namespace NightShift
{
    /// Merges the on-screen controls (joystick + buttons) with keyboard input.
    public static class GameInput
    {
        public static Vector2 Joystick;
        static bool shoot, interact, decoy;

        public static void PressShoot() => shoot = true;
        public static void PressInteract() => interact = true;
        public static void PressDecoy() => decoy = true;

        public static void Clear()
        {
            shoot = interact = decoy = false;
            Joystick = Vector2.zero;
        }

        public static Vector2 Move
        {
            get
            {
                Vector2 v = Joystick;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        static bool Key(Key key)
        {
            var k = Keyboard.current;
            return k != null && k[key].wasPressedThisFrame;
        }

        public static bool ConsumeShoot()
        {
            bool r = shoot || Key(UnityEngine.InputSystem.Key.Space) || Key(UnityEngine.InputSystem.Key.J);
            shoot = false;
            return r;
        }

        public static bool ConsumeInteract()
        {
            bool r = interact || Key(UnityEngine.InputSystem.Key.E) || Key(UnityEngine.InputSystem.Key.K);
            interact = false;
            return r;
        }

        public static bool ConsumeDecoy()
        {
            bool r = decoy || Key(UnityEngine.InputSystem.Key.Q) || Key(UnityEngine.InputSystem.Key.L);
            decoy = false;
            return r;
        }

        public static bool PausePressed() => Key(UnityEngine.InputSystem.Key.Escape) || Key(UnityEngine.InputSystem.Key.P);
    }
}
