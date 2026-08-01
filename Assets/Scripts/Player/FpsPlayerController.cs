using ProjectSun.FPS.Input;
using UnityEngine;

namespace ProjectSun.FPS.Player
{
    [RequireComponent(typeof(CharacterController), typeof(FpsInput))]
    public sealed class FpsPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 5.2f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -25f;
        [Header("View")]
        [SerializeField, Range(50f, 89f)] private float verticalLookLimit = 82f;

        private CharacterController characterController;
        private Transform cameraPivot;
        private Camera playerCamera;
        private FpsInput input;
        private float pitch;
        private float verticalVelocity;
        private float speedMultiplier = 1f;
        private bool gameplayInputEnabled = true;

        public Vector2 MoveInput { get; private set; }
        public FpsInput Input => GetInput();
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsSprinting { get; private set; }
        public Vector3 FlatForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        public void Configure(Transform viewPivot, Camera viewCamera)
        {
            cameraPivot = viewPivot;
            if (input != null) input.FieldOfViewChanged -= SetFieldOfView;
            playerCamera = viewCamera;
            input = GetInput();
            input.FieldOfViewChanged += SetFieldOfView;
            SetFieldOfView(input.FieldOfView);
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            gameplayInputEnabled = enabled;
            GetInput().SetGameplayEnabled(enabled);
            if (!enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            input = GetInput();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!gameplayInputEnabled)
            {
                MoveInput = Vector2.zero;
                return;
            }
            Look();
            Move();

            if (input.WasPressed(FpsBinding.Menu))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (input.WasPressed(FpsBinding.Fire) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Look()
        {
            if (cameraPivot == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            Vector2 lookDelta = input.ReadLookDelta() * input.LookSensitivity;
            float mouseX = lookDelta.x;
            float mouseY = lookDelta.y;
            pitch = Mathf.Clamp(pitch - mouseY, -verticalLookLimit, verticalLookLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        private void Move()
        {
            MoveInput = input.ReadMove();
            MoveInput = Vector2.ClampMagnitude(MoveInput, 1f);
            bool crouching = input.IsHeld(FpsBinding.Crouch);
            IsSprinting = input.IsHeld(FpsBinding.Sprint) && MoveInput.y > 0.1f && !crouching;

            float speed = crouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 move = (transform.right * MoveInput.x + transform.forward * MoveInput.y) * (speed * speedMultiplier);
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            if (characterController.isGrounded && input.WasPressed(FpsBinding.Jump))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;
            characterController.Move(move * Time.deltaTime);
        }

        private FpsInput GetInput()
        {
            if (input == null) input = GetComponent<FpsInput>();
            if (input == null) input = gameObject.AddComponent<FpsInput>();
            return input;
        }

        private void SetFieldOfView(float value)
        {
            if (playerCamera != null) playerCamera.fieldOfView = value;
        }

        private void OnDestroy()
        {
            if (input != null) input.FieldOfViewChanged -= SetFieldOfView;
        }
    }
}
