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
        [SerializeField] private float minX = -1.8f;
        [SerializeField] private float maxX = 1.8f;
        [SerializeField] private float minZ = -0.5f;
        [SerializeField] private float maxZ = 2.4f;
        [SerializeField] private float minY = 0.5f;
        [SerializeField] private float maxY = 2.2f;

        [Header("Stay In Room Boundaries (Office Cabin Room Size 7.2m x 6.4m)")]
        [SerializeField] private float roomMinX = -3.2f;
        [SerializeField] private float roomMaxX = 3.2f;
        [SerializeField] private float roomMinZ = -1.0f;
        [SerializeField] private float roomMaxZ = 4.6f;

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
            if (headCamera == null)
            {
                ResolveReferences();
                if (headCamera == null) return;
            }

            Transform rig = rigRoot != null ? rigRoot : transform;

            // Prevent player from clipping out of the room or walking far away in VR/simulator.
            // If the head position leaves the room interior boundaries, clamp the rig back inside.
            Vector3 headPos = headCamera.position;

            float clampedX = Mathf.Clamp(headPos.x, roomMinX, roomMaxX);
            float clampedZ = Mathf.Clamp(headPos.z, roomMinZ, roomMaxZ);

            if (!Mathf.Approximately(clampedX, headPos.x) || !Mathf.Approximately(clampedZ, headPos.z))
            {
                Vector3 correction = new Vector3(clampedX - headPos.x, 0f, clampedZ - headPos.z);
                rig.position += correction;
            }

            // In VR simulator / seated testing: keep user seated in front of workstation if drastically displaced (> 4.5m)
            if (Vector3.Distance(new Vector3(headPos.x, 0f, headPos.z), new Vector3(0f, 0f, 0.18f)) > 4.5f)
            {
                Vector3 recenterCorrection = new Vector3(0f - headPos.x, 0f, 0.18f - headPos.z);
                rig.position += recenterCorrection;
            }
        }
    }
}

