using UnityEngine;

namespace ElmanGameDevTools.PlayerSystem
{
    [RequireComponent(typeof(CharacterController))]
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Movement Controller")]
    public class PlayerMovementController : MonoBehaviour
    {
        [Header("REFERENCES")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Transform playerCamera;

        [Header("MOVEMENT SETTINGS")]
        [SerializeField] private float speed = 6f;
        [SerializeField] private float runSpeed = 9f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -25f;

        [Header("CROUCH SETTINGS")]
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchSmoothTime = 0.1f;

        [Header("STANDING DETECTION & GROUND CHECK")]
        [SerializeField] private GameObject standingHeightMarker;
        [SerializeField] private float standingCheckRadius = 0.2f;
        [SerializeField] private LayerMask obstacleLayerMask = ~0;
        [SerializeField] private float minStandingClearance = 0.01f;
        [SerializeField] private LayerMask groundLayer = 1;
        [SerializeField] private float groundCheckDistance = 0.5f;

        private Vector3 _velocity;
        private float _originalHeight;
        private float _targetHeight;
        private float _currentMovementSpeed;
        private float _cameraBaseHeight;
        private float _markerHeightOffset;

        private bool _isGrounded;
        private bool _isCrouching;
        private bool _hasJumped;

        private MovementState _currentMovementState = MovementState.Walking;

        public enum MovementState
        {
            Walking,
            Running,
            Crouching,
            Jumping
        }

        public bool IsGrounded => _isGrounded;
        public bool IsCrouching => _isCrouching;
        public bool HasJumped => _hasJumped;
        public float CurrentMovementSpeed => _currentMovementSpeed;
        public MovementState CurrentState => _currentMovementState;
        public Vector2 MoveInput => inputReader != null ? inputReader.MoveInput : Vector2.zero;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();

            if (controller == null)
            {
                Debug.LogError($"{nameof(PlayerMovementController)}: CharacterController missing.", this);
                enabled = false;
                return;
            }

            if (inputReader == null)
            {
                Debug.LogError($"{nameof(PlayerMovementController)}: PlayerInputReader missing.", this);
                enabled = false;
                return;
            }

            if (playerCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }
        }

        private void Start()
        {
            _originalHeight = controller.height;
            _targetHeight = _originalHeight;

            if (playerCamera != null)
                _cameraBaseHeight = playerCamera.localPosition.y;

            if (standingHeightMarker != null)
                _markerHeightOffset = standingHeightMarker.transform.position.y - transform.position.y;
        }

        private void OnEnable()
        {
            if (inputReader != null)
                inputReader.JumpPressed += HandleJumpPressed;
        }

        private void OnDisable()
        {
            if (inputReader != null)
                inputReader.JumpPressed -= HandleJumpPressed;
        }

        private void Update()
        {
            CheckGroundStatus();
            HandleCrouchLogic();
            UpdateMovementState();
            HandleMovement();
            HandleHeightAndCamera();
            UpdateStandingMarkerPosition();
        }

        private void CheckGroundStatus()
        {
            Vector3 origin = transform.position + Vector3.up * controller.radius;

            bool groundHit = Physics.SphereCast(
                origin,
                controller.radius * 0.8f,
                Vector3.down,
                out _,
                groundCheckDistance,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            _isGrounded = groundHit || controller.isGrounded;

            if (_isGrounded && _velocity.y < 0f)
            {
                _hasJumped = false;
                _velocity.y = -5f;
            }
        }

        private void HandleCrouchLogic()
        {
            _isCrouching = inputReader.IsCrouchingHeld || !CanStandUp();
            _targetHeight = _isCrouching ? crouchHeight : _originalHeight;
        }

        private void UpdateMovementState()
        {
            bool wantsToRun = inputReader.IsRunningHeld && inputReader.MoveInput.y > 0.1f;

            if (!_isGrounded)
            {
                _currentMovementState = MovementState.Jumping;
                _currentMovementSpeed = wantsToRun ? runSpeed : speed;
                return;
            }

            if (_isCrouching)
            {
                _currentMovementState = MovementState.Crouching;
                _currentMovementSpeed = speed * 0.5f;
            }
            else
            {
                _currentMovementState = wantsToRun ? MovementState.Running : MovementState.Walking;
                _currentMovementSpeed = wantsToRun ? runSpeed : speed;
            }
        }

        private void HandleMovement()
        {
            Vector2 input = inputReader.MoveInput;

            Vector3 moveInput =
                transform.right * input.x +
                transform.forward * input.y;

            if (moveInput.magnitude > 1f)
                moveInput.Normalize();

            controller.Move(moveInput * _currentMovementSpeed * Time.deltaTime);

            _velocity.y += gravity * Time.deltaTime;
            controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleJumpPressed()
        {
            if (!_isGrounded || _isCrouching) return;

            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _hasJumped = true;
            _isGrounded = false;
        }

        private void HandleHeightAndCamera()
        {
            float previousHeight = controller.height;

            controller.height = Mathf.Lerp(
                controller.height,
                _targetHeight,
                Time.deltaTime * (1f / crouchSmoothTime)
            );

            if (_isGrounded)
            {
                float heightDifference = controller.height - previousHeight;

                if (heightDifference > 0f)
                    controller.Move(Vector3.up * heightDifference);
            }

            if (playerCamera == null) return;

            float currentRelativeHeight = _cameraBaseHeight * (controller.height / _originalHeight);

            Vector3 camPosition = playerCamera.localPosition;
            camPosition.y = Mathf.Lerp(
                camPosition.y,
                currentRelativeHeight,
                Time.deltaTime * (1f / crouchSmoothTime)
            );

            playerCamera.localPosition = camPosition;
        }

        private void UpdateStandingMarkerPosition()
        {
            if (standingHeightMarker == null) return;

            standingHeightMarker.transform.position = new Vector3(
                transform.position.x,
                transform.position.y + _markerHeightOffset,
                transform.position.z
            );
        }

        public bool CanStandUp()
        {
            if (standingHeightMarker == null) return true;

            Collider[] hits = Physics.OverlapSphere(
                standingHeightMarker.transform.position,
                standingCheckRadius,
                obstacleLayerMask,
                QueryTriggerInteraction.Ignore
            );

            foreach (Collider col in hits)
            {
                if (col.transform.IsChildOf(transform)) continue;
                if (col.transform == transform) continue;
                if (col.isTrigger) continue;

                if (col.bounds.min.y < standingHeightMarker.transform.position.y + minStandingClearance)
                    return false;
            }

            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (standingHeightMarker == null) return;

            Gizmos.color = CanStandUp() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(standingHeightMarker.transform.position, standingCheckRadius);
        }
    }
}