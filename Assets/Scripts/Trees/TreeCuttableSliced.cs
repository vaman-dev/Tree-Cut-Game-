using System.Collections;
using Hanzzz.MeshSlicerFree;
using UnityEngine;
using System.Threading.Tasks;
namespace ElmanGameDevTools.TreeCutting
{
    public class TreeCuttableSliced : MonoBehaviour
    {
        private enum TreeState
        {
            Idle,
            Cutting,
            Warning,
            Sliced,
            Fallen
        }

        [Header("Tree Slice References")]
        [SerializeField] private GameObject sliceTarget;
        [SerializeField] private Transform slicePlane;
        [SerializeField] private Material intersectionMaterial;

        [Header("Visual References")]
        [SerializeField] private Transform notchVisual;
        [SerializeField] private Transform predictionArrow;

        [Header("Cut Settings")]
        [SerializeField] private int maxHealth = 5;
        [SerializeField] private float trunkRadius = 0.45f;
        [SerializeField] private float cutHeight = 1.1f;

        [Header("Warning Settings")]
        [SerializeField] private float warningDelay = 1.2f;

        [Header("Physics Fall Settings")]
        [SerializeField] private float fallForce = 4f;
        [SerializeField] private float fallTorque = 8f;
        [SerializeField] private float upperPartMass = 25f;
        // Push the falling sliced part slightly away from the stump
        // using the already calculated notch/fall direction.
        [SerializeField] private float separationDistance = 0.25f;
        [SerializeField] private float initialPushForce = 3f;

        [Header("Falling Collider Settings")]
        [SerializeField] private bool usePrimitiveColliderForFallingPart = true;
        [SerializeField] private bool removeExistingMeshColliders = true;

        [SerializeField] private float fallingCapsuleRadius = 0.25f;
        [SerializeField] private float fallingCapsuleHeight = 3.5f;
        [SerializeField] private Vector3 fallingCapsuleCenter = new Vector3(0f, 1.75f, 0f);

        [Header("Feedback")]
        [SerializeField] private ParticleSystem woodChipParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip cutClip;
        [SerializeField] private AudioClip crackClip;
        [SerializeField] private AudioClip impactClip;


        [Header("Debug")]
        [SerializeField] private bool logDebug = true;

        private readonly MeshSlicer _meshSlicer = new MeshSlicer();

        private TreeState _state = TreeState.Idle;
        private int _currentHealth;
        private bool _notchLocked;
        private Vector3 _notchDirection;
        private Coroutine _sliceRoutine;

        private void Awake()
        {
            _currentHealth = maxHealth;

            if (notchVisual != null)
                notchVisual.gameObject.SetActive(false);

            if (predictionArrow != null)
                predictionArrow.gameObject.SetActive(false);
        }

        public void TryCut(Transform playerTransform, Vector3 hitPoint)
        {
            if (playerTransform == null) return;

            if (_state == TreeState.Warning ||
                _state == TreeState.Sliced ||
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
                StartWarningAndSlice();
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
                Debug.Log($"Notch direction locked: {_notchDirection}", this);
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

        private void StartWarningAndSlice()
        {
            if (_sliceRoutine != null) return;

            _state = TreeState.Warning;

            if (audioSource != null && crackClip != null)
                audioSource.PlayOneShot(crackClip);

            _sliceRoutine = StartCoroutine(WarningThenSliceRoutine());
        }

        private IEnumerator WarningThenSliceRoutine()
        {
            yield return new WaitForSeconds(warningDelay);

            SliceTree();
        }

        private async void SliceTree()
        {
            if (sliceTarget == null)
            {
                Debug.LogError("Slice Target is missing.", this);
                return;
            }

            if (slicePlane == null)
            {
                Debug.LogError("Slice Plane is missing.", this);
                return;
            }

            Plane plane = new Plane(slicePlane.up, slicePlane.position);

            if (logDebug)
                Debug.Log("Async slicing started...", this);

            (GameObject, GameObject) result;

            try
            {
                result = await _meshSlicer.SliceAsync(
                    sliceTarget,
                    Get3PointsOnPlane(plane),
                    intersectionMaterial
                );
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"Async slice failed: {exception.Message}", this);
                return;
            }

            if (result.Item1 == null || result.Item2 == null)
            {
                Debug.LogWarning("Slice failed. Slice plane may not intersect tree.", this);
                return;
            }

            result.Item1.transform.SetParent(transform, true);
            result.Item2.transform.SetParent(transform, true);

            sliceTarget.SetActive(false);

            GameObject upperPart;
            GameObject stumpPart;

            DecideUpperAndStump(result.Item1, result.Item2, out upperPart, out stumpPart);

            SetupStump(stumpPart);
            SetupFallingUpperPart(upperPart);

            if (predictionArrow != null)
                predictionArrow.gameObject.SetActive(false);

            _state = TreeState.Sliced;

            if (logDebug)
                Debug.Log("Async tree sliced successfully.", this);
        }

        private void DecideUpperAndStump(
            GameObject partA,
            GameObject partB,
            out GameObject upperPart,
            out GameObject stumpPart)
        {
            float heightA = GetRendererCenterY(partA);
            float heightB = GetRendererCenterY(partB);

            if (heightA >= heightB)
            {
                upperPart = partA;
                stumpPart = partB;
            }
            else
            {
                upperPart = partB;
                stumpPart = partA;
            }
        }

        private float GetRendererCenterY(GameObject obj)
        {
            Renderer renderer = obj.GetComponentInChildren<Renderer>();

            if (renderer != null)
                return renderer.bounds.center.y;

            return obj.transform.position.y;
        }

        private void SetupStump(GameObject stumpPart)
        {
            stumpPart.name = "Tree_Stump_Sliced";

            Rigidbody rb = stumpPart.GetComponent<Rigidbody>();
            if (rb != null)
                Destroy(rb);

            Collider collider = stumpPart.GetComponent<Collider>();
            if (collider == null)
                stumpPart.AddComponent<MeshCollider>();
            collider.enabled = false; 
        }

        private void SetupFallingUpperPart(GameObject upperPart)
        {
            upperPart.name = "Tree_Upper_Falling_Sliced";

            upperPart.transform.position += _notchDirection * separationDistance;

            if (removeExistingMeshColliders)
            {
                MeshCollider[] meshColliders = upperPart.GetComponentsInChildren<MeshCollider>();

                foreach (MeshCollider meshCollider in meshColliders)
                {
                    Destroy(meshCollider);
                }
            }

            if (usePrimitiveColliderForFallingPart)
            {
                CapsuleCollider capsuleCollider = upperPart.GetComponent<CapsuleCollider>();

                if (capsuleCollider == null)
                    capsuleCollider = upperPart.AddComponent<CapsuleCollider>();

                capsuleCollider.direction = 1;
                capsuleCollider.radius = fallingCapsuleRadius;
                capsuleCollider.height = fallingCapsuleHeight;
                capsuleCollider.center = fallingCapsuleCenter;
            }
            else
            {
                MeshCollider meshCollider = upperPart.GetComponent<MeshCollider>();

                if (meshCollider == null)
                    meshCollider = upperPart.AddComponent<MeshCollider>();

                meshCollider.convex = true;
            }

            Rigidbody rb = upperPart.GetComponent<Rigidbody>();

            if (rb == null)
                rb = upperPart.AddComponent<Rigidbody>();

            rb.mass = upperPartMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            rb.AddForce(_notchDirection * initialPushForce, ForceMode.Impulse);
            rb.AddForce(_notchDirection * fallForce, ForceMode.Impulse);

            Vector3 torqueAxis = Vector3.Cross(Vector3.up, _notchDirection).normalized;
            rb.AddTorque(torqueAxis * fallTorque, ForceMode.Impulse);

            if (audioSource != null && impactClip != null)
                StartCoroutine(PlayImpactLater());
        }

        private IEnumerator PlayImpactLater()
        {
            yield return new WaitForSeconds(1.2f);

            if (audioSource != null && impactClip != null)
                audioSource.PlayOneShot(impactClip);

            _state = TreeState.Fallen;
        }

        private (Vector3, Vector3, Vector3) Get3PointsOnPlane(Plane p)
        {
            Vector3 xAxis;

            if (0f != p.normal.x)
            {
                xAxis = new Vector3(-p.normal.y / p.normal.x, 1f, 0f);
            }
            else if (0f != p.normal.y)
            {
                xAxis = new Vector3(0f, -p.normal.z / p.normal.y, 1f);
            }
            else
            {
                xAxis = new Vector3(1f, 0f, -p.normal.x / p.normal.z);
            }

            Vector3 yAxis = Vector3.Cross(p.normal, xAxis);

            return (
                -p.distance * p.normal,
                -p.distance * p.normal + xAxis,
                -p.distance * p.normal + yAxis
            );
        }
    }
}