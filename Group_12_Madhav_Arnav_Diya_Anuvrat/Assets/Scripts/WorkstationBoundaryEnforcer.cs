using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Enforces physical room-scale boundaries for the VR cybersecurity workstation.
    /// Prevents player camera / XR rig from clipping or walking through desks,
    /// triple-monitor displays, and office room walls.
    /// </summary>
    public class WorkstationBoundaryEnforcer : MonoBehaviour
    {
        [Header("Tracking References")]
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Transform rigRoot;

        [Header("Workstation Seated Volume Constraints (Meters)")]
        [SerializeField] private float minX = -0.65f;
        [SerializeField] private float maxX = 0.65f;
        [SerializeField] private float minZ = -0.85f;
        [SerializeField] private float maxZ = 0.45f;
        [SerializeField] private float minY = 0.90f;
        [SerializeField] private float maxY = 1.75f;

        [Header("Physical Collision Detection")]
        [SerializeField] private float headColliderRadius = 0.15f;
        private SphereCollider headCollider;

        private void Start()
        {
            ResolveReferences();
            SetupHeadCollider();
        }

        public void Configure(Transform head, Transform tracking, Transform root)
        {
            headCamera = head;
            trackingSpace = tracking;
            rigRoot = root;
            SetupHeadCollider();
        }

        private void ResolveReferences()
        {
            if (headCamera == null)
            {
                GameObject head = GameObject.Find("CenterEyeAnchor");
                if (head != null)
                    headCamera = head.transform;
            }

            if (headCamera != null)
            {
                if (trackingSpace == null && headCamera.parent != null)
                    trackingSpace = headCamera.parent;
                if (rigRoot == null)
                    rigRoot = (trackingSpace != null && trackingSpace.parent != null) ? trackingSpace.parent : transform;
            }
        }

        private void SetupHeadCollider()
        {
            if (headCamera == null) return;

            headCollider = headCamera.GetComponent<SphereCollider>();
            if (headCollider == null)
            {
                headCollider = headCamera.gameObject.AddComponent<SphereCollider>();
                headCollider.radius = headColliderRadius;
                headCollider.isTrigger = false;
            }

            Rigidbody rb = headCamera.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = headCamera.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private Vector3 referenceRigPosition;
        private bool hasInitializedReference = false;

        public void SetAnchorPosition(Vector3 pos)
        {
            referenceRigPosition = pos;
            hasInitializedReference = true;
        }

        private void LateUpdate()
        {
            if (headCamera == null)
            {
                ResolveReferences();
                if (headCamera == null) return;
            }

            Transform moveTarget = rigRoot != null ? rigRoot : (trackingSpace != null ? trackingSpace : transform);
            if (!hasInitializedReference)
            {
                referenceRigPosition = moveTarget.position;
                hasInitializedReference = true;
            }

            // Head offset relative to the current moveTarget position
            Vector3 localHeadOffset = headCamera.position - moveTarget.position;
            // The world position the head would occupy if moveTarget remained at referenceRigPosition
            Vector3 unconstrainedHeadPos = referenceRigPosition + localHeadOffset;

            Vector3 clampedHeadPos = unconstrainedHeadPos;
            clampedHeadPos.x = Mathf.Clamp(unconstrainedHeadPos.x, minX, maxX);
            clampedHeadPos.z = Mathf.Clamp(unconstrainedHeadPos.z, minZ, maxZ);
            clampedHeadPos.y = Mathf.Clamp(unconstrainedHeadPos.y, minY, maxY);

            // Corrective displacement needed to keep head strictly within physical boundaries
            Vector3 correctiveOffset = clampedHeadPos - unconstrainedHeadPos;
            moveTarget.position = referenceRigPosition + correctiveOffset;
        }
    }
}
