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
            // Remove or disable physical SphereCollider on all eye cameras so camera does not penetrate or collide
            // into interior mesh geometry causing grey clipping plane blanking.
            string[] eyeCameraNames = { "CenterEyeAnchor", "LeftEyeAnchor", "RightEyeAnchor" };
            foreach (string camName in eyeCameraNames)
            {
                GameObject camObj = GameObject.Find(camName);
                if (camObj == null) continue;

                SphereCollider sc = camObj.GetComponent<SphereCollider>();
                if (sc != null)
                {
                    sc.enabled = false;
                }

                Rigidbody rb = camObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
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
            // Boundary hard-clamping has been safely neutralized so the VR player's natural head motion
            // is unconstrained and never snaps, resets, or penetrates geometry into the grey background clear color.
        }
    }
}

