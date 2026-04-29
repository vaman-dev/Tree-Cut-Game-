using UnityEngine;

namespace ElmanGameDevTools.PlayerSystem
{
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Animator Controller")]
    public class PlayerAnimatorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerMovementController movementController;

        [Header("Animation Settings")]
        [SerializeField] private float animationSmoothTime = 10f;

        [Header("Animator Parameter Names")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string isRunningParameter = "IsRunning";
        [SerializeField] private string isGroundedParameter = "IsGrounded";
        [SerializeField] private string isCrouchingParameter = "IsCrouching";

        private int _speedHash;
        private int _isMovingHash;
        private int _isRunningHash;
        private int _isGroundedHash;
        private int _isCrouchingHash;

        private float _currentAnimSpeed;

        private void Awake()
        {
            if (movementController == null)
                movementController = GetComponentInParent<PlayerMovementController>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            _speedHash = Animator.StringToHash(speedParameter);
            _isMovingHash = Animator.StringToHash(isMovingParameter);
            _isRunningHash = Animator.StringToHash(isRunningParameter);
            _isGroundedHash = Animator.StringToHash(isGroundedParameter);
            _isCrouchingHash = Animator.StringToHash(isCrouchingParameter);
        }

        private void Update()
        {
            if (animator == null || movementController == null)
                return;

            UpdateAnimator();
        }

        private void UpdateAnimator()
        {
            Vector2 moveInput = movementController.MoveInput;
            bool isMoving = moveInput.magnitude > 0.1f;

            bool isRunning =
                movementController.CurrentState == PlayerMovementController.MovementState.Running;

            bool isCrouching = movementController.IsCrouching;
            bool isGrounded = movementController.IsGrounded;

            float targetSpeed = 0f;

            if (isMoving)
            {
                if (isRunning)
                    targetSpeed = 1f;
                else
                    targetSpeed = 0.5f;
            }

            _currentAnimSpeed = Mathf.Lerp(
                _currentAnimSpeed,
                targetSpeed,
                Time.deltaTime * animationSmoothTime
            );

            animator.SetFloat(_speedHash, _currentAnimSpeed);
            animator.SetBool(_isMovingHash, isMoving);
            animator.SetBool(_isRunningHash, isRunning);
            animator.SetBool(_isGroundedHash, isGrounded);
            animator.SetBool(_isCrouchingHash, isCrouching);
        }
    }
}