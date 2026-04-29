using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElmanGameDevTools.TreeCutting
{
    public class PlayerAxeSwing : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform axePivot;
        [SerializeField] private GameObject axeRoot;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string swingActionName = "Cut";

        [Header("Swing Rotation")]
        [SerializeField] private Vector3 idleLocalEuler = new Vector3(0f, 0f, 0f);
        [SerializeField] private Vector3 windupLocalEuler = new Vector3(-35f, 15f, 0f);
        [SerializeField] private Vector3 impactLocalEuler = new Vector3(45f, -10f, 0f);

        [Header("Swing Timing")]
        [SerializeField] private float windupTime = 0.12f;
        [SerializeField] private float strikeTime = 0.16f;
        [SerializeField] private float returnTime = 0.18f;

        [Header("Behaviour")]
        [SerializeField] private bool hideAxeOnStart = false;

        private InputActionMap _playerMap;
        private InputAction _swingAction;

        private bool _isSwinging;

        public event Action ImpactMoment;

        private void Awake()
        {
            if (axePivot == null)
            {
                Debug.LogError($"{nameof(PlayerAxeSwing)}: Axe Pivot is missing.", this);
                enabled = false;
                return;
            }

            if (inputActionAsset == null)
            {
                Debug.LogError($"{nameof(PlayerAxeSwing)}: Input Action Asset is missing.", this);
                enabled = false;
                return;
            }

            _playerMap = inputActionAsset.FindActionMap(playerMapName, true);
            _swingAction = _playerMap.FindAction(swingActionName, true);

            axePivot.localRotation = Quaternion.Euler(idleLocalEuler);

            if (axeRoot != null)
                axeRoot.SetActive(!hideAxeOnStart);
        }

        private void OnEnable()
        {
            if (_swingAction == null) return;

            _swingAction.performed += OnSwingPerformed;
            _playerMap.Enable();
        }

        private void OnDisable()
        {
            if (_swingAction == null) return;

            _swingAction.performed -= OnSwingPerformed;
        }

        private void OnSwingPerformed(InputAction.CallbackContext context)
        {
            TrySwing();
        }

        public void SetAxeVisible(bool visible)
        {
            if (axeRoot != null)
                axeRoot.SetActive(visible);
        }

        public void TrySwing()
        {
            if (_isSwinging) return;

            if (axeRoot != null && !axeRoot.activeInHierarchy)
                axeRoot.SetActive(true);

            StartCoroutine(SwingRoutine());
        }

        private IEnumerator SwingRoutine()
        {
            _isSwinging = true;

            Quaternion idleRotation = Quaternion.Euler(idleLocalEuler);
            Quaternion windupRotation = Quaternion.Euler(windupLocalEuler);
            Quaternion impactRotation = Quaternion.Euler(impactLocalEuler);

            yield return RotateAxe(idleRotation, windupRotation, windupTime);

            yield return RotateAxe(windupRotation, impactRotation, strikeTime);

            ImpactMoment?.Invoke();

            yield return RotateAxe(impactRotation, idleRotation, returnTime);

            _isSwinging = false;
        }

        private IEnumerator RotateAxe(Quaternion from, Quaternion to, float duration)
        {
            if (duration <= 0f)
            {
                axePivot.localRotation = to;
                yield break;
            }

            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.Clamp01(timer / duration);
                t = Mathf.SmoothStep(0f, 1f, t);

                axePivot.localRotation = Quaternion.Slerp(from, to, t);

                yield return null;
            }

            axePivot.localRotation = to;
        }
    }
}