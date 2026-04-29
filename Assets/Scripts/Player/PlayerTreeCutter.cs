using UnityEngine;

namespace ElmanGameDevTools.TreeCutting
{
    public class PlayerTreeCutter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerAxeSwing axeSwing;

        [Header("Cut Settings")]
        [SerializeField] private float cutRayDistance = 3f;
        [SerializeField] private LayerMask cuttableLayerMask = ~0;

        [Header("Detection Debug")]
        [SerializeField] private bool detectTreeContinuously = true;
        [SerializeField] private bool logTreeDetected = true;
        [SerializeField] private bool logAxeHitTree = true;

        [Header("Debug Ray")]
        [SerializeField] private bool drawDebugRay = true;

        private TreeCuttable _currentDetectedTree;

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>();

            if (axeSwing == null)
                axeSwing = GetComponent<PlayerAxeSwing>();

            if (playerCamera == null)
                Debug.LogError($"{nameof(PlayerTreeCutter)}: Player camera is missing.", this);

            if (axeSwing == null)
                Debug.LogError($"{nameof(PlayerTreeCutter)}: PlayerAxeSwing is missing.", this);
        }

        private void OnEnable()
        {
            if (axeSwing != null)
                axeSwing.ImpactMoment += TryCutTree;
        }

        private void OnDisable()
        {
            if (axeSwing != null)
                axeSwing.ImpactMoment -= TryCutTree;
        }

        private void Update()
        {
            if (!detectTreeContinuously) return;

            DetectTreeInFront();
        }

        private void DetectTreeInFront()
        {
            TreeCuttable detectedTree = GetTreeFromCameraRay(out _);

            if (detectedTree == _currentDetectedTree)
                return;

            _currentDetectedTree = detectedTree;

            if (_currentDetectedTree != null && logTreeDetected)
            {
                Debug.Log($"Tree detected: {_currentDetectedTree.name}", _currentDetectedTree);
            }
        }

        private void TryCutTree()
        {
            TreeCuttable cuttableTree = GetTreeFromCameraRay(out RaycastHit hit);

            if (cuttableTree == null)
                return;

            if (logAxeHitTree)
            {
                Debug.Log($"Axe impact hit tree: {cuttableTree.name}", cuttableTree);
            }

            cuttableTree.TryCut(transform, hit.point);
        }

        private TreeCuttable GetTreeFromCameraRay(out RaycastHit hit)
        {
            hit = default;

            if (playerCamera == null)
                return null;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (drawDebugRay)
                Debug.DrawRay(ray.origin, ray.direction * cutRayDistance, Color.green, 0.05f);

            if (!Physics.Raycast(
                    ray,
                    out hit,
                    cutRayDistance,
                    cuttableLayerMask,
                    QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<TreeCuttable>();
        }
    }
}