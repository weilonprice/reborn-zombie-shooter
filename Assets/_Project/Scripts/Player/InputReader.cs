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

        /// <summary>Trigger pulled this frame. Semi-automatic weapons read this, not FireHeld.</summary>
        public static bool FirePressed =>
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame);

        public static bool ReloadPressed =>
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);

        /// <summary>Weapon slot requested this frame, 0-3, or -1 for none.</summary>
        public static int WeaponSlotPressed
        {
            get
            {
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame) return 0;
                    if (kb.digit2Key.wasPressedThisFrame) return 1;
                    if (kb.digit3Key.wasPressedThisFrame) return 2;
                    if (kb.digit4Key.wasPressedThisFrame) return 3;
                }

                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (pad.dpad.up.wasPressedThisFrame) return 0;
                    if (pad.dpad.right.wasPressedThisFrame) return 1;
                    if (pad.dpad.down.wasPressedThisFrame) return 2;
                    if (pad.dpad.left.wasPressedThisFrame) return 3;
                }

                return -1;
            }
        }

        /// <summary>Continue past a win into endless. Separate key from restart so the
        /// victory screen can offer both without one shadowing the other.</summary>
        public static bool EndlessPressed =>
            (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        public static bool RestartPressed =>
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        public static bool ArmoryPressed =>
            (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

        public static bool DeployPressed =>
            (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.leftShoulder.wasPressedThisFrame);

        public static bool RotateDeployablePressed =>
            (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);

        /// <summary>
        /// Fires the pistol's ultimate. V rather than Space, which is already the restart
        /// key on the death screen, and rather than Q/F/R/E/B which the deployables, reload
        /// and shop have taken.
        /// </summary>
        public static bool UltimatePressed =>
            (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

        /// <summary>Cycles which deployable is selected. Q was freed up from rotate for this.</summary>
        public static bool CycleDeployablePressed =>
            (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.leftStickButton.wasPressedThisFrame);
    }
}
