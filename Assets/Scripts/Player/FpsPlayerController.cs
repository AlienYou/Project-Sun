using UnityEngine;

namespace ProjectSun.FPS.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FpsPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 5.2f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0.1f)] private float crouchSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -25f;
        [Header("View")]
        [SerializeField, Range(0.1f, 10f)] private float mouseSensitivity = 2.2f;
        [SerializeField, Range(50f, 89f)] private float verticalLookLimit = 82f;

        private CharacterController characterController;
        private Transform cameraPivot;
        private float pitch;
        private float verticalVelocity;
        private float speedMultiplier = 1f;
        private bool gameplayInputEnabled = true;

        public Vector2 MoveInput { get; private set; }
        public bool IsGrounded => characterController != null && characterController.isGrounded;
        public bool IsSprinting { get; private set; }
        public Vector3 FlatForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        public void Configure(Transform viewPivot)
        {
            cameraPivot = viewPivot;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void SetGameplayInputEnabled(bool enabled)
        {
            gameplayInputEnabled = enabled;
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Look()
        {
            if (cameraPivot == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - mouseY, -verticalLookLimit, verticalLookLimit);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }

        private void Move()
        {
            MoveInput = new Vector2(
                (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            MoveInput = Vector2.ClampMagnitude(MoveInput, 1f);
            bool crouching = Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.LeftControl);
            IsSprinting = Input.GetKey(KeyCode.LeftShift) && MoveInput.y > 0.1f && !crouching;

            float speed = crouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 move = (transform.right * MoveInput.x + transform.forward * MoveInput.y) * (speed * speedMultiplier);
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;
            if (characterController.isGrounded && Input.GetKeyDown(KeyCode.Space))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;
            characterController.Move(move * Time.deltaTime);
        }
    }
}
