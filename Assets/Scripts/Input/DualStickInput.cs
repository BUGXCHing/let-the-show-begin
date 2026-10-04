using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HaoxiKaiyan
{
    public sealed class DualStickInput : MonoBehaviour
    {
        [SerializeField] private VirtualStick moveStick;
        [SerializeField] private VirtualStick weaponStick;
        private Transform player;
        private Camera stageCamera;
        private Vector2 keyboardWeapon;
        private bool mouseStageDrag;

        public Vector2 Move { get; private set; }
        public Vector2 Weapon { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool PausePressed { get; private set; }
        public bool WeaponDoubleTapped { get; private set; }

        public void Configure(VirtualStick move, VirtualStick weapon, Transform actor, Camera camera)
        {
            moveStick = move;
            weaponStick = weapon;
            player = actor;
            stageCamera = camera;
        }

        private void Update()
        {
            // Button edges belong to this frame even if a keyboard is disconnected.
            RestartPressed = false;
            PausePressed = false;
            Vector2 move = moveStick != null ? moveStick.Value : Vector2.zero;
            Vector2 weapon = weaponStick != null ? weaponStick.Value : Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                move += new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                Vector2 arrows = new Vector2(
                    (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                    (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f));
                float ramp = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? 11f : 3.3f;
                keyboardWeapon = Vector2.MoveTowards(keyboardWeapon, Vector2.ClampMagnitude(arrows, 1f), ramp * Time.unscaledDeltaTime);
                weapon += keyboardWeapon;
                RestartPressed = keyboard.rKey.wasPressedThisFrame;
                PausePressed = keyboard.escapeKey.wasPressedThisFrame;
            }

            // A held drag in the stage is a desktop analogue of the right stick. Mouse speed
            // changes the requested grip direction; actual blade speed still decides the hit.
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 screen = mouse.position.ReadValue();
                // The owner of a pointer is decided at press time. A drag that starts on
                // either HUD stick must never turn into a stage weapon drag mid-gesture.
                if (mouse.leftButton.wasPressedThisFrame)
                    mouseStageDrag = screen.x > Screen.width * .15f && screen.x < Screen.width * .85f &&
                        screen.y > Screen.height * .33f && screen.y < Screen.height * .82f &&
                        (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
                if (!mouse.leftButton.isPressed) mouseStageDrag = false;
                if (mouseStageDrag && player != null && stageCamera != null)
                {
                    Ray ray = stageCamera.ScreenPointToRay(screen);
                    var plane = new Plane(Vector3.up, player.position + Vector3.up * 1.30f);
                    if (plane.Raycast(ray, out float distance))
                    {
                        Vector3 displacement = ray.GetPoint(distance) - player.position;
                        Vector2 pull = Vector2.ClampMagnitude(new Vector2(displacement.x, displacement.z) / 1.7f, 1f);
                        if (pull.sqrMagnitude > .01f) weapon = pull;
                    }
                }
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                weapon += pad.rightStick.ReadValue();
                RestartPressed |= pad.buttonSouth.wasPressedThisFrame;
            }

            Move = Vector2.ClampMagnitude(move, 1f);
            Weapon = Vector2.ClampMagnitude(weapon, 1f);
            WeaponDoubleTapped = weaponStick != null && weaponStick.ConsumeDoubleTap();
        }
    }
}
