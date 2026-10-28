using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Classification of Area of Interest (AOI) within the phishing simulation email stimulus.
    /// </summary>
    public enum AOIType
    {
        /// <summary>Diagnostic: Sender display name / header element.</summary>
        SenderHeader,

        /// <summary>Diagnostic: Actual sending domain address (crucial indicator of spoofing).</summary>
        SenderDomain,

        /// <summary>Diagnostic: Target URL of embedded hyperlink.</summary>
        HyperlinkTarget,

        /// <summary>Security indicator: SSL/TLS lock, digital signature, or warning badge. Configurable in R_inspect.</summary>
        SecurityBadge,

        /// <summary>Non-diagnostic: Email subject line.</summary>
        SubjectLine,

        /// <summary>Non-diagnostic: General body text of the email.</summary>
        EmailBody,

        /// <summary>Non-AOI surface or raycast miss. Excluded from T_total_dwell.</summary>
        IrrelevantBackground
    }

    public static class AOITypeExtensions
    {
        /// <summary>
        /// Returns whether this AOI type is considered diagnostic for phishing threat inspection.
        /// </summary>
        public static bool IsDiagnostic(this AOIType type, bool includeSecurityBadge = false)
        {
            switch (type)
            {
                case AOIType.SenderHeader:
                case AOIType.SenderDomain:
                case AOIType.HyperlinkTarget:
                    return true;
                case AOIType.SecurityBadge:
                    return includeSecurityBadge;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns whether this AOI type counts towards T_total_dwell (all valid email elements).
        /// Excludes IrrelevantBackground.
        /// </summary>
        public static bool CountsTowardsTotalDwell(this AOIType type)
        {
            return type != AOIType.IrrelevantBackground;
        }
    }

    /// <summary>
    /// Component attached to any GameObject representing an Area of Interest.
    /// Requires a Collider to participate in Physics.RaycastNonAlloc detection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class GazeAOITarget : MonoBehaviour
    {
        [Header("AOI Properties")]
        [Tooltip("Semantic category of this email stimulus region.")]
        [SerializeField] private AOIType _aoiType = AOIType.EmailBody;

        [Tooltip("Stable, unique string ID for data export (e.g., 'sender_domain_01').")]
        [SerializeField] private string _aoiId = "aoi_element";

        [Tooltip("Raycast priority. Higher priority wins when multiple colliders overlap (e.g. hyperlink inside email body).")]
        [SerializeField] private int _priority = 0;

        [Tooltip("Optional scenario or email stimulus identifier this AOI belongs to.")]
        [SerializeField] private string _scenarioBinding = string.Empty;

        [Header("Collider Reference")]
        [SerializeField] private Collider _targetCollider;

        public AOIType Type => _aoiType;
        public string AoiId => _aoiId;
        public int Priority => _priority;
        public string ScenarioBinding => _scenarioBinding;
        public Collider TargetCollider => _targetCollider;

        private void Reset()
        {
            _targetCollider = GetComponent<Collider>();
            if (string.IsNullOrEmpty(_aoiId))
            {
                _aoiId = gameObject.name.ToLowerInvariant().Replace(" ", "_");
            }
        }

        private void Awake()
        {
            if (_targetCollider == null)
            {
                _targetCollider = GetComponent<Collider>();
            }
        }

        private void OnEnable()
        {
            if (_targetCollider == null)
            {
                _targetCollider = GetComponent<Collider>();
            }

            GazeAOIRegistry.Register(this);
        }

        private void OnDisable()
        {
            GazeAOIRegistry.Unregister(this);
        }

        public void Configure(AOIType aoiType, string aoiId, int priority = 0, string scenarioBinding = "")
        {
            if (isActiveAndEnabled)
                GazeAOIRegistry.Unregister(this);

            _aoiType = aoiType;
            _aoiId = aoiId;
            _priority = priority;
            _scenarioBinding = scenarioBinding;

            if (_targetCollider == null)
            {
                _targetCollider = GetComponent<Collider>();
            }

            if (isActiveAndEnabled)
            {
                GazeAOIRegistry.Register(this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_targetCollider != null)
            {
                Gizmos.color = _aoiType.IsDiagnostic(true) ? Color.cyan : Color.yellow;
                Gizmos.DrawWireCube(_targetCollider.bounds.center, _targetCollider.bounds.size);
            }
        }
    }
}
