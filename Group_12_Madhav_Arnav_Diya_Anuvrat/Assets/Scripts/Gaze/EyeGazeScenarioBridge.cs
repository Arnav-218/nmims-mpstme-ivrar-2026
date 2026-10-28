using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Integration adapter bridging Person 2's PhishingThreatSimulationManager
    /// to the EyeGazeAttentionTracker subsystem.
    /// Works with both the real simulation manager and the test mock manager.
    /// </summary>
    public class EyeGazeScenarioBridge : MonoBehaviour
    {
        [Tooltip("Reference to the central EyeGazeAttentionTracker component.")]
        [SerializeField] private EyeGazeAttentionTracker _tracker;

        [Tooltip("Participant pseudonym identifier (e.g. 'P01' or 'SYNTH-01').")]
        [SerializeField] private string _participantPseudoId = "P01";

        [Tooltip("Active session identifier (e.g. 'S01').")]
        [SerializeField] private string _sessionId = "S01";

        [Tooltip("Experimental condition label (e.g. 'VR_Immersive' or 'Web_2D').")]
        [SerializeField] private string _conditionLabel = "VR_Immersive";

        public EyeGazeAttentionTracker Tracker => _tracker;
        public string ParticipantPseudoId => _participantPseudoId;
        public string SessionId => _sessionId;
        public string ConditionLabel => _conditionLabel;

        private void Awake()
        {
            if (_tracker == null)
            {
                _tracker = GetComponent<EyeGazeAttentionTracker>();
                if (_tracker == null)
                {
                    _tracker = UnityEngine.Object.FindAnyObjectByType<EyeGazeAttentionTracker>();
                }
            }
        }

        public void SetParticipant(string participantPseudoId, string sessionId, string conditionLabel = "VR_Immersive")
        {
            _participantPseudoId = participantPseudoId;
            _sessionId = sessionId;
            _conditionLabel = conditionLabel;
        }

        public void SetTracker(EyeGazeAttentionTracker tracker)
        {
            _tracker = tracker;
        }

        public void IncludeAoiLayer(int layerId)
        {
            if (ResolveTracker())
                _tracker.IncludeAoiLayer(layerId);
        }

        public bool ResolveTracker()
        {
            EyeGazeAttentionTracker[] trackers =
                UnityEngine.Object.FindObjectsByType<EyeGazeAttentionTracker>(FindObjectsSortMode.None);
            if (trackers.Length > 1)
            {
                Debug.LogError("[EyeGazeScenarioBridge] Multiple EyeGazeAttentionTracker components exist. Keep one authoritative tracker.");
                return false;
            }

            _tracker = trackers.Length == 1 ? trackers[0] : null;
            return _tracker != null;
        }

        public bool HasMultipleTrackers()
        {
            return UnityEngine.Object.FindObjectsByType<EyeGazeAttentionTracker>(FindObjectsSortMode.None).Length > 1;
        }

        /// <summary>
        /// Invoked when Person 2's scenario manager signals a new trial starting.
        /// </summary>
        public void OnScenarioTrialStarted(string trialId, string scenarioId)
        {
            if (_tracker == null)
            {
                Debug.LogError("[EyeGazeScenarioBridge] Cannot begin trial: EyeGazeAttentionTracker reference missing!");
                return;
            }

            var context = new TrialContext(
                _participantPseudoId,
                _sessionId,
                trialId,
                scenarioId,
                _conditionLabel
            );

            _tracker.BeginTrial(context);
        }

        /// <summary>
        /// Invoked when Person 2's scenario manager signals trial completion with outcome.
        /// </summary>
        public void OnScenarioTrialEnded(PhishingTrialOutcome outcome, double responseTimeMs, string reportingLeverId = "", string notes = "")
        {
            if (_tracker == null) return;

            var outcomeInfo = new TrialOutcomeInfo(outcome, responseTimeMs, reportingLeverId, notes);
            _tracker.EndTrial(outcomeInfo);
        }

        /// <summary>
        /// Invoked when Person 2's scenario manager aborts a trial.
        /// </summary>
        public void OnScenarioTrialAborted(string reason)
        {
            if (_tracker == null) return;
            _tracker.AbortTrial(reason);
        }
    }
}
