using UnityEngine;
using UnityEngine.InputSystem;

namespace ZombieShooter
{
    /// <summary>
    /// Single place that touches the Input System. Polls devices directly rather than
    /// binding an .inputactions asset so nothing needs wiring in the inspector; swap the
    /// bodies for action-map lookups later without touching any caller.
    /// </summary>
    public static class InputReader
    {
        /// <summary>World-space-agnostic movement on the XZ plane. Magnitude clamped to 1.</summary>
        public static Vector2 Move
        {
            get
            {
                var move = Vector2.zero;

                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                }

                var pad = Gamepad.current;
                if (pad != null)
                {
                    var stick = pad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > move.sqrMagnitude) move = stick;
                }

                return Vector2.ClampMagnitude(move, 1f);
            }
        }

        /// <summary>Right-stick aim. Zero when the pad is idle, so callers fall back to the mouse.</summary>
        public static Vector2 AimStick
        {
            get
            {
                var pad = Gamepad.current;
                if (pad == null) return Vector2.zero;

                var stick = pad.rightStick.ReadValue();
                return stick.sqrMagnitude < 0.04f ? Vector2.zero : stick;
            }
        }

        public static Vector2 MouseScreenPosition =>
            Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        public static bool FireHeld =>
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Gamepad.current != null && Gamepad.current.rightTrigger.isPressed);

        public static bool ReloadPressed =>
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

        public static bool RestartPressed =>
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
    }
}
