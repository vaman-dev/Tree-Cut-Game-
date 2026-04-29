using System.Collections;
using UnityEngine;

namespace ElmanGameDevTools.TreeCutting
{
    public class TreeCuttable : MonoBehaviour
    {
        private enum TreeState
        {
            Idle,
            Cutting,
            Warning,
            Falling,
            Fallen
        }

        [Header("Tree References")]
        [SerializeField] private Transform fallPivot;
        [SerializeField] private Transform notchVisual;
        [SerializeField] private Transform predictionArrow;

        [Header("Cut Settings")]
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float trunkRadius = 0.45f;
        [SerializeField] private float cutHeight = 1.1f;

        [Header("Fall Settings")]
        [SerializeField] private float warningDelay = 1.2f;
        [SerializeField] private float fallDuration = 2f;
        [SerializeField] private float maxFallAngle = 90f;

        [Header("Feedback")]
        [SerializeField] private ParticleSystem woodChipParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cutClip;
        [SerializeField] private AudioClip crackClip;
        [SerializeField] private AudioClip impactClip;

        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private TreeState _state = TreeState.Idle;
        private int _currentHealth;
        private bool _notchLocked;
        private Vector3 _notchDirection;
        private Quaternion _initialPivotRotation;

        private Coroutine _fallRoutine;

        private void Awake()
        {
            _currentHealth = maxHealth;

            if (fallPivot == null)
                fallPivot = transform;

            _initialPivotRotation = fallPivot.rotation;

            if (notchVisual != null)
                notchVisual.gameObject.SetActive(false);

            if (predictionArrow != null)
                predictionArrow.gameObject.SetActive(false);
        }

        public void TryCut(Transform playerTransform, Vector3 hitPoint)
        {
            if (playerTransform == null) return;

            if (_state == TreeState.Warning ||
                _state == TreeState.Falling ||
                _state == TreeState.Fallen)
            {
                return;
            }

            if (!_notchLocked)
            {
                LockNotchDirection(playerTransform);
                ShowNotchVisual();
                ShowPredictionArrow();
                _state = TreeState.Cutting;
            }

            ApplyCutDamage();
            PlayCutFeedback(hitPoint);

            if (_currentHealth <= 0)
            {
                StartWarningAndFall();
            }
        }

        private void LockNotchDirection(Transform playerTransform)
        {
            _notchDirection = playerTransform.position - transform.position;
            _notchDirection.y = 0f;

            if (_notchDirection.sqrMagnitude <= 0.001f)
                _notchDirection = transform.forward;

            _notchDirection.Normalize();
            _notchLocked = true;

            if (logDebug)
                Debug.Log($"Tree notch locked. Fall direction: {_notchDirection}", this);
        }

        private void ApplyCutDamage()
        {
            _currentHealth--;

            if (logDebug)
                Debug.Log($"Tree cut. Health left: {_currentHealth}", this);
        }

        private void ShowNotchVisual()
        {
            if (notchVisual == null) return;

            Vector3 notchPosition =
                transform.position +
                _notchDirection * trunkRadius +
                Vector3.up * cutHeight;

            notchVisual.position = notchPosition;
            notchVisual.rotation = Quaternion.LookRotation(_notchDirection, Vector3.up);
            notchVisual.gameObject.SetActive(true);
        }

        private void ShowPredictionArrow()
        {
            if (predictionArrow == null) return;

            Vector3 arrowPosition =
                transform.position +
                _notchDirection * 1.5f +
                Vector3.up * 0.05f;

            predictionArrow.position = arrowPosition;
            predictionArrow.rotation = Quaternion.LookRotation(_notchDirection, Vector3.up);
            predictionArrow.gameObject.SetActive(true);
        }

        private void PlayCutFeedback(Vector3 hitPoint)
        {
            if (audioSource != null && cutClip != null)
                audioSource.PlayOneShot(cutClip);

            if (woodChipParticles != null)
            {
                woodChipParticles.transform.position = hitPoint;
                woodChipParticles.transform.rotation = Quaternion.LookRotation(_notchDirection, Vector3.up);
                woodChipParticles.Play();
            }
        }

        private void StartWarningAndFall()
        {
            if (_fallRoutine != null) return;

            _state = TreeState.Warning;

            if (audioSource != null && crackClip != null)
                audioSource.PlayOneShot(crackClip);

            _fallRoutine = StartCoroutine(WarningThenFallRoutine());
        }

        private IEnumerator WarningThenFallRoutine()
        {
            float timer = 0f;

            Vector3 originalPosition = fallPivot.localPosition;

            while (timer < warningDelay)
            {
                timer += Time.deltaTime;

                float shake = Mathf.Sin(Time.time * 35f) * 0.015f;
                fallPivot.localPosition = originalPosition + new Vector3(shake, 0f, 0f);

                yield return null;
            }

            fallPivot.localPosition = originalPosition;

            yield return FallRoutine();
        }

        private IEnumerator FallRoutine()
        {
            _state = TreeState.Falling;

            if (predictionArrow != null)
                predictionArrow.gameObject.SetActive(false);

            Vector3 rotationAxis = Vector3.Cross(Vector3.up, _notchDirection);

            if (rotationAxis.sqrMagnitude <= 0.001f)
                rotationAxis = transform.right;

            rotationAxis.Normalize();

            Quaternion startRotation = fallPivot.rotation;
            Quaternion targetRotation = Quaternion.AngleAxis(maxFallAngle, rotationAxis) * _initialPivotRotation;

            float timer = 0f;

            while (timer < fallDuration)
            {
                timer += Time.deltaTime;

                float t = timer / fallDuration;
                t = Mathf.SmoothStep(0f, 1f, t);

                fallPivot.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

                yield return null;
            }

            fallPivot.rotation = targetRotation;

            _state = TreeState.Fallen;

            if (audioSource != null && impactClip != null)
                audioSource.PlayOneShot(impactClip);

            if (logDebug)
                Debug.Log("Tree fallen.", this);
        }
    }
}