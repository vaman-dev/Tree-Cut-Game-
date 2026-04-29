using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElmanGameDevTools.PlayerSystem
{
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Input Reader")]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("INPUT ACTION ASSET")]
        [SerializeField] private InputActionAsset inputActionAsset;

        [Header("ACTION MAP")]
        [SerializeField] private string playerMapName = "Player";

        [Header("ACTION NAMES")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string lookActionName = "Look";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string runActionName = "Run";
        [SerializeField] private string crouchActionName = "Crouch";

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _runAction;
        private InputAction _crouchAction;

        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool IsRunningHeld { get; private set; }
        public bool IsCrouchingHeld { get; private set; }

        public event Action JumpPressed;

        private void Awake()
        {
            if (inputActionAsset == null)
            {
                Debug.LogError($"{nameof(PlayerInputReader)}: Input Action Asset is missing.", this);
                enabled = false;
                return;
            }

            _playerMap = inputActionAsset.FindActionMap(playerMapName, true);

            _moveAction = _playerMap.FindAction(moveActionName, true);
            _lookAction = _playerMap.FindAction(lookActionName, true);
            _jumpAction = _playerMap.FindAction(jumpActionName, true);
            _runAction = _playerMap.FindAction(runActionName, true);
            _crouchAction = _playerMap.FindAction(crouchActionName, true);
        }

        private void OnEnable()
        {
            if (_playerMap == null) return;

            SubscribeCallbacks();
            _playerMap.Enable();
        }

        private void OnDisable()
        {
            if (_playerMap == null) return;

            UnsubscribeCallbacks();
            _playerMap.Disable();

            MoveInput = Vector2.zero;
            LookInput = Vector2.zero;
            IsRunningHeld = false;
            IsCrouchingHeld = false;
        }

        private void SubscribeCallbacks()
        {
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;

            _lookAction.performed += OnLookPerformed;
            _lookAction.canceled += OnLookCanceled;

            _jumpAction.performed += OnJumpPerformed;

            _runAction.performed += OnRunPerformed;
            _runAction.canceled += OnRunCanceled;

            _crouchAction.performed += OnCrouchPerformed;
            _crouchAction.canceled += OnCrouchCanceled;
        }

        private void UnsubscribeCallbacks()
        {
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;

            _lookAction.performed -= OnLookPerformed;
            _lookAction.canceled -= OnLookCanceled;

            _jumpAction.performed -= OnJumpPerformed;

            _runAction.performed -= OnRunPerformed;
            _runAction.canceled -= OnRunCanceled;

            _crouchAction.performed -= OnCrouchPerformed;
            _crouchAction.canceled -= OnCrouchCanceled;
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            MoveInput = context.ReadValue<Vector2>();
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            MoveInput = Vector2.zero;
        }

        private void OnLookPerformed(InputAction.CallbackContext context)
        {
            LookInput = context.ReadValue<Vector2>();
        }

        private void OnLookCanceled(InputAction.CallbackContext context)
        {
            LookInput = Vector2.zero;
        }

        private void OnJumpPerformed(InputAction.CallbackContext context)
        {
            JumpPressed?.Invoke();
        }

        private void OnRunPerformed(InputAction.CallbackContext context)
        {
            IsRunningHeld = context.ReadValueAsButton();
        }

        private void OnRunCanceled(InputAction.CallbackContext context)
        {
            IsRunningHeld = false;
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context)
        {
            IsCrouchingHeld = context.ReadValueAsButton();
        }

        private void OnCrouchCanceled(InputAction.CallbackContext context)
        {
            IsCrouchingHeld = false;
        }
    }
}