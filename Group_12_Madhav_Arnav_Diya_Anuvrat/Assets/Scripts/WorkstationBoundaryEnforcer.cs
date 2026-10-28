using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Enforces physical room-scale boundaries for the VR cybersecurity workstation.
    /// Moves TrackingSpace relative to the stationary room/rig so that moving the player camera
    /// NEVER moves the room walls, and hooks into Application.onBeforeRender and Camera.onPreCull
    /// so the player camera can NEVER physically exit the 4 walls or see the grey exterior void!
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public class WorkstationBoundaryEnforcer : MonoBehaviour
    {
        [Header("Tracking References")]
        [SerializeField] private Transform headCamera;
        [SerializeField] private Transform trackingSpace;
        [SerializeField] private Transform rigRoot;
        [SerializeField] private Transform roomOrigin;

        [Header("Workstation Seated Volume Constraints (Meters)")]
        [SerializeField] private float minX = -1.8f;
        [SerializeField] private float maxX = 1.8f;
        [SerializeField] private float minZ = -0.5f;
        [SerializeField] private float maxZ = 2.4f;
        [SerializeField] private float minY = 0.5f;
        [SerializeField] private float maxY = 2.2f;

        [Header("Hard Interior Wall Boundaries (Office Room: X [-3.5, 3.5], Y [0, 3.4], Z [-1.3, 4.9])")]
        [SerializeField] private float roomMinX = -3.05f;
        [SerializeField] private float roomMaxX = 3.05f;
        [SerializeField] private float roomMinY = 0.55f;
        [SerializeField] private float roomMaxY = 2.95f;
        [SerializeField] private float roomMinZ = -0.85f;
        [SerializeField] private float roomMaxZ = 4.45f;

        private void OnEnable()
        {
            Application.onBeforeRender += EnforceBoundariesBeforeRender;
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= EnforceBoundariesBeforeRender;
            Camera.onPreCull -= OnCameraPreCull;
        }

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
            roomOrigin = transform;
            SetupHeadCollider();
        }

        private void ResolveReferences()
        {
            if (roomOrigin == null)
                roomOrigin = transform;

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
        private bool isSeatedLocked = false;
        private Vector3 seatedEyeLocalTarget = new Vector3(0f, 1.18f, 0.22f);

        public bool IsSeatedLocked => isSeatedLocked;

        public void SetAnchorPosition(Vector3 pos)
        {
            referenceRigPosition = pos;
            hasInitializedReference = true;
        }

        public void SetSeatedLocked(bool locked, Vector3 eyeLocalTarget)
        {
            isSeatedLocked = locked;
            seatedEyeLocalTarget = eyeLocalTarget;
            EnforceRoomAndSeatConstraints();
        }

        private void Update()
        {
            EnforceRoomAndSeatConstraints();
        }

        private void LateUpdate()
        {
            EnforceRoomAndSeatConstraints();
        }

        private void EnforceBoundariesBeforeRender()
        {
            EnforceRoomAndSeatConstraints();
        }

        private void OnCameraPreCull(Camera cam)
        {
            EnforceRoomAndSeatConstraints();
        }

        private void EnforceRoomAndSeatConstraints()
        {
            if (headCamera == null || trackingSpace == null)
            {
                ResolveReferences();
                if (headCamera == null) return;
            }

            // IMPORTANT: Phishing Simulation (transform) is a sibling of TrackingSpace under [BuildingBlock] Camera Rig!
            // Moving rigRoot would move BOTH the player AND the room walls together!
            // Moving trackingSpace moves ONLY the player (eyes + hands) relative to the room!
            Transform playerMover = trackingSpace != null ? trackingSpace : headCamera;
            Transform room = roomOrigin != null ? roomOrigin : transform;

            // Compute head position in the room's local space so wall coordinates are ALWAYS exact
            Vector3 headLocal = room.InverseTransformPoint(headCamera.position);

            // 1. When seated in the chair ('K' toggle), lock body translation to the chair while allowing full 360° head/eye rotation!
            if (isSeatedLocked)
            {
                Vector3 localDelta = seatedEyeLocalTarget - headLocal;
                if (localDelta.sqrMagnitude > 0.000001f)
                {
                    Vector3 worldDelta = room.TransformVector(localDelta);
                    playerMover.position += worldDelta;
                }
                return;
            }

            // 2. Hard interior wall/floor/ceiling clamp in room local coordinates:
            // Walls are at X: [-3.55, 3.55], Y: [0, 3.4], Z: [-1.35, 4.95].
            // Keep player's head strictly inside with a 0.45m safety margin from every wall!
            const float HardMinX = -3.05f;
            const float HardMaxX = 3.05f;
            const float HardMinY = 0.55f;
            const float HardMaxY = 2.95f;
            const float HardMinZ = -0.85f;
            const float HardMaxZ = 4.45f;

            float clampedX = Mathf.Clamp(headLocal.x, HardMinX, HardMaxX);
            float clampedY = Mathf.Clamp(headLocal.y, HardMinY, HardMaxY);
            float clampedZ = Mathf.Clamp(headLocal.z, HardMinZ, HardMaxZ);

            // Also prevent walking inside the solid executive desk footprint (X in [-1.15, 1.15], Z in [0.52, 1.55])
            if (clampedZ > 0.52f && clampedZ < 1.55f && clampedX > -1.15f && clampedX < 1.15f)
            {
                float dFront = Mathf.Abs(clampedZ - 0.52f);
                float dBack = Mathf.Abs(1.55f - clampedZ);
                float dLeft = Mathf.Abs(clampedX - (-1.15f));
                float dRight = Mathf.Abs(1.15f - clampedX);
                float minD = Mathf.Min(Mathf.Min(dFront, dBack), Mathf.Min(dLeft, dRight));
                if (minD == dFront) clampedZ = 0.52f;
                else if (minD == dBack) clampedZ = 1.55f;
                else if (minD == dLeft) clampedX = -1.15f;
                else clampedX = 1.15f;
            }

            Vector3 localCorrection = new Vector3(clampedX - headLocal.x, clampedY - headLocal.y, clampedZ - headLocal.z);
            if (localCorrection.sqrMagnitude > 0.000001f)
            {
                Vector3 worldCorrection = room.TransformVector(localCorrection);
                playerMover.position += worldCorrection;
            }
        }
    }
}

