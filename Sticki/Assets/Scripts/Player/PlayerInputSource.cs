using Sticki.Core.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sticki.Player
{
    public class PlayerInputSource : MonoBehaviour, IInputSource
    {
        [SerializeField] private bool useRawMouseInput = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool FireHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool InspectPressed { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool SelectArPressed { get; private set; }
        public bool SelectPistolPressed { get; private set; }
        public bool SelectLmgPressed { get; private set; }
        public bool SelectShotgunPressed { get; private set; }

        private void Update()
        {
            Move = ReadMove();
            Look = ReadLook();
            JumpPressed = ReadJumpPressed();
            FireHeld = ReadFireHeld();
            ReloadPressed = ReadReloadPressed();
            SprintHeld = ReadSprintHeld();
            InspectPressed = ReadInspectPressed();
            InteractPressed = ReadInteractPressed();
            SelectArPressed = ReadSelectArPressed();
            SelectPistolPressed = ReadSelectPistolPressed();
            SelectLmgPressed = ReadSelectLmgPressed();
            SelectShotgunPressed = ReadSelectShotgunPressed();
        }

        private Vector2 ReadMove()
        {
            Vector2 move = Vector2.zero;
            if (Keyboard.current != null)
            {
                float x = (Keyboard.current.dKey.isPressed ? 1f : 0f) - (Keyboard.current.aKey.isPressed ? 1f : 0f);
                float y = (Keyboard.current.wKey.isPressed ? 1f : 0f) - (Keyboard.current.sKey.isPressed ? 1f : 0f);
                move = new Vector2(x, y);
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude)
                {
                    move = stick;
                }
            }

            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            if (move.sqrMagnitude > 0f)
            {
                return move;
            }
            return Vector2.zero;
        }

        private Vector2 ReadLook()
        {
            Vector2 look = Vector2.zero;

            if (Mouse.current != null)
            {
                look = Mouse.current.delta.ReadValue();
                if (!useRawMouseInput)
                {
                    look *= Time.unscaledDeltaTime * 60f;
                }
            }

            if (Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.rightStick.ReadValue();
                if (stick.sqrMagnitude > look.sqrMagnitude)
                {
                    look = stick;
                }
            }

            if (look.sqrMagnitude > 0f)
            {
                return look;
            }
            return Vector2.zero;
        }

        private bool ReadJumpPressed()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
            return false;
        }

        private bool ReadFireHeld()
        {
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.rightTrigger.isPressed)
            {
                return true;
            }
            return false;
        }

        private bool ReadSprintHeld()
        {
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed)
            {
                return true;
            }
            return false;
        }

        private bool ReadReloadPressed()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private bool ReadInspectPressed()
        {
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            {
                return true;
            }
            return false;
        }

        private bool ReadInteractPressed()
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private bool ReadSelectArPressed()
        {
            if (Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.dpad.left.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private bool ReadSelectPistolPressed()
        {
            if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                return true;
            }

            if (Gamepad.current != null && Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private bool ReadSelectLmgPressed()
        {
            if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }

        private bool ReadSelectShotgunPressed()
        {
            if (Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }
    }
}
