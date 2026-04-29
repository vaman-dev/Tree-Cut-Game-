using UnityEngine;

namespace ElmanGameDevTools.PlayerSystem
{
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Camera Controller")]
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("REFERENCES")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMovementController movementController;
        [SerializeField] private Transform playerCamera;

        [Header("LOOK SETTINGS")]
        [SerializeField] private float sensitivity = 2f;
        [SerializeField] private float mouseLookScale = 0.08f;
        [SerializeField] private float maxLookUpAngle = 90f;
        [SerializeField] private float maxLookDownAngle = -90f;

        [Header("CAMERA INERTIA & WEIGHT")]
        [Range(1f, 30f)]
        [SerializeField] private float cameraWeight = 12f;

        [Header("HEAD BOB SETTINGS")]
        [SerializeField] private bool enableHeadBob = true;
        [Range(0.01f, 0.15f)][SerializeField] private float bobAmountX = 0.04f;
        [Range(0.01f, 0.15f)][SerializeField] private float bobAmountY = 0.05f;
        [SerializeField] private float walkBobFrequency = 12f;
        [SerializeField] private float runBobFrequency = 16f;
        [SerializeField] private float crouchBobFrequency = 8f;
        [SerializeField] private float bobSmoothness = 10f;

        [Header("CAMERA TILT")]
        [SerializeField] private bool enableCameraTilt = true;
        [SerializeField] private float tiltAmount = 2f;
        [SerializeField] private float tiltSmoothness = 8f;
        [SerializeField] private float runTiltMultiplier = 1.2f;
        [SerializeField] private float crouchTiltMultiplier = 0.5f;
        [SerializeField] private float turnTiltAmount = 1.5f;
        [SerializeField] private float maxTotalTilt = 5f;

        [Header("FOV SETTINGS")]
        [SerializeField] private bool enableRunFov = true;
        [SerializeField] private float normalFov = 60f;
        [SerializeField] private float runFov = 70f;
        [SerializeField] private float fovChangeSpeed = 8f;

        private Camera _cameraComponent;

        private float _targetYaw;
        private float _targetPitch;
        private float _currentYaw;
        private float _currentPitch;

        private float _smoothInputX;
        private float _currentTilt;
        private float _timer;
        private float _cameraBaseHeight;

        private void Awake()
        {
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
            if (movementController == null) movementController = GetComponent<PlayerMovementController>();

            if (playerCamera == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam != null) playerCamera = cam.transform;
            }

            if (inputReader == null)
            {
                Debug.LogError($"{nameof(PlayerCameraController)}: PlayerInputReader missing.", this);
                enabled = false;
                return;
            }

            if (movementController == null)
            {
                Debug.LogError($"{nameof(PlayerCameraController)}: PlayerMovementController missing.", this);
                enabled = false;
                return;
            }

            if (playerCamera == null)
            {
                Debug.LogError($"{nameof(PlayerCameraController)}: Player camera missing.", this);
                enabled = false;
                return;
            }

            _cameraComponent = playerCamera.GetComponent<Camera>();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _cameraBaseHeight = playerCamera.localPosition.y;

            _targetYaw = transform.eulerAngles.y;
            _targetPitch = NormalizePitch(playerCamera.localEulerAngles.x);

            _currentYaw = _targetYaw;
            _currentPitch = _targetPitch;

            if (_cameraComponent != null)
                _cameraComponent.fieldOfView = normalFov;
        }

        private void Update()
        {
            HandleCameraControl();
            HandleCameraTilt();
            HandleFovChange();

            if (enableHeadBob)
                HandleHeadBob();
        }

        private void HandleCameraControl()
        {
            Vector2 lookInput = inputReader.LookInput;

            float mouseX = lookInput.x * sensitivity * mouseLookScale;
            float mouseY = lookInput.y * sensitivity * mouseLookScale;

            _smoothInputX = Mathf.Lerp(
                _smoothInputX,
                mouseX,
                Time.deltaTime * cameraWeight
            );

            _targetYaw += mouseX;
            _targetPitch -= mouseY;
            _targetPitch = Mathf.Clamp(_targetPitch, maxLookDownAngle, maxLookUpAngle);

            float smoothFactor = Mathf.Clamp01(Time.deltaTime * cameraWeight);

            _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, smoothFactor);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, smoothFactor);

            transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
            playerCamera.localRotation = Quaternion.Euler(_currentPitch, 0f, _currentTilt);
        }

        private void HandleCameraTilt()
        {
            if (!enableCameraTilt)
            {
                _currentTilt = Mathf.Lerp(_currentTilt, 0f, Time.deltaTime * tiltSmoothness);
                return;
            }

            Vector2 moveInput = inputReader.MoveInput;

            float keyboardTilt = -moveInput.x * tiltAmount;
            float mouseTilt = -_smoothInputX * turnTiltAmount;

            float targetTiltTotal = keyboardTilt + mouseTilt;

            if (movementController.CurrentState == PlayerMovementController.MovementState.Running)
                targetTiltTotal *= runTiltMultiplier;

            if (movementController.IsCrouching)
                targetTiltTotal *= crouchTiltMultiplier;

            targetTiltTotal = Mathf.Clamp(targetTiltTotal, -maxTotalTilt, maxTotalTilt);

            _currentTilt = Mathf.Lerp(
                _currentTilt,
                targetTiltTotal,
                Time.deltaTime * tiltSmoothness
            );
        }

        private void HandleFovChange()
        {
            if (!enableRunFov || _cameraComponent == null) return;

            bool isActuallyRunning =
                movementController.CurrentState == PlayerMovementController.MovementState.Running;

            float targetFov = isActuallyRunning ? runFov : normalFov;

            _cameraComponent.fieldOfView = Mathf.Lerp(
                _cameraComponent.fieldOfView,
                targetFov,
                Time.deltaTime * fovChangeSpeed
            );
        }

        private void HandleHeadBob()
        {
            Vector2 moveInput = inputReader.MoveInput;
            float moveMagnitude = moveInput.magnitude;

            float currentCameraHeight = _cameraBaseHeight;

            if (!movementController.IsGrounded || moveMagnitude <= 0.1f)
            {
                _timer = 0f;

                playerCamera.localPosition = Vector3.Lerp(
                    playerCamera.localPosition,
                    new Vector3(0f, currentCameraHeight, 0f),
                    Time.deltaTime * bobSmoothness
                );

                return;
            }

            float frequency = walkBobFrequency;

            if (movementController.CurrentState == PlayerMovementController.MovementState.Running)
                frequency = runBobFrequency;
            else if (movementController.IsCrouching)
                frequency = crouchBobFrequency;

            _timer += Time.deltaTime * frequency;

            Vector3 targetPosition = new Vector3(
                Mathf.Cos(_timer * 0.5f) * bobAmountX,
                currentCameraHeight + Mathf.Sin(_timer) * bobAmountY,
                0f
            );

            playerCamera.localPosition = Vector3.Lerp(
                playerCamera.localPosition,
                targetPosition,
                Time.deltaTime * bobSmoothness
            );
        }

        private float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
                pitch -= 360f;

            return pitch;
        }
    }
}