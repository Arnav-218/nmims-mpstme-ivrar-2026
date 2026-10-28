using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Experimental context passed to BeginTrial().
    /// Must only contain pseudonymous identifiers; no direct personal identifiers permitted.
    /// </summary>
    [Serializable]
    public struct TrialContext
    {
        public string participantPseudoId;
        public string sessionId;
        public string trialId;
        public string scenarioId;
        public string conditionLabel; // e.g. "VR_Immersive", "Web_2D", or empty if unknown

        public TrialContext(
            string participantPseudoId,
            string sessionId,
            string trialId,
            string scenarioId,
            string conditionLabel = "")
        {
            this.participantPseudoId = string.IsNullOrEmpty(participantPseudoId) ? "P00" : participantPseudoId;
            this.sessionId = string.IsNullOrEmpty(sessionId) ? "S01" : sessionId;
            this.trialId = string.IsNullOrEmpty(trialId) ? "T01" : trialId;
            this.scenarioId = string.IsNullOrEmpty(scenarioId) ? "SCENARIO_DEFAULT" : scenarioId;
            this.conditionLabel = conditionLabel ?? "";
        }

        public bool IsSynthetic => participantPseudoId != null &&
            (participantPseudoId.StartsWith("SYNTH", StringComparison.OrdinalIgnoreCase) ||
             participantPseudoId.IndexOf("SIM", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    /// <summary>
    /// Phishing detection behavioral outcome for a trial.
    /// </summary>
    public enum PhishingTrialOutcome
    {
        None,
        CorrectReport,   // Phishing email correctly identified and reported
        MissedThreat,    // Phishing email treated as legitimate / link clicked
        CorrectDismiss,  // Legitimate email correctly passed
        FalseAlarm,      // Legitimate email falsely reported as phishing
        Timeout          // Trial timed out without decision
    }

    /// <summary>
    /// Outcome information submitted by the scenario manager when a trial concludes.
    /// </summary>
    [Serializable]
    public struct TrialOutcomeInfo
    {
        public PhishingTrialOutcome outcome;
        public double responseTimeMs;
        public string reportingLeverId;
        public string notes;

        public TrialOutcomeInfo(
            PhishingTrialOutcome outcome,
            double responseTimeMs = 0.0,
            string reportingLeverId = "",
            string notes = "")
        {
            this.outcome = outcome;
            this.responseTimeMs = responseTimeMs;
            this.reportingLeverId = reportingLeverId ?? "";
            this.notes = notes ?? "";
        }

        public static TrialOutcomeInfo Empty => new TrialOutcomeInfo(PhishingTrialOutcome.None);
    }

    /// <summary>
    /// Summary metrics computed for a single Area of Interest within a trial.
    /// </summary>
    [Serializable]
    public class AOIMetricSummary
    {
        public AOIType aoiType;
        public string aoiId;
        public bool isDiagnostic;

        // Fixation-based dwell (primary)
        public double fixationDwellMs;
        public int fixationCount;
        public double meanFixationDurationMs;
        public double maxFixationDurationMs;
        public double timeToFirstFixationMs; // -1 if never fixated
        public int firstFixationOrder;       // 1-indexed, 0 if never fixated
        public int revisitCount;

        // Raw sample dwell (secondary)
        public double rawSampleDwellMs;
        public int rawSampleCount;

        public AOIMetricSummary(AOIType aoiType, string aoiId, bool isDiagnostic)
        {
            this.aoiType = aoiType;
            this.aoiId = aoiId ?? "unknown";
            this.isDiagnostic = isDiagnostic;
            this.timeToFirstFixationMs = -1.0;
            this.firstFixationOrder = 0;
            this.revisitCount = 0;
        }
    }

    /// <summary>
    /// Complete summary metrics for a trial session.
    /// </summary>
    [Serializable]
    public class GazeTrialSummary
    {
        // Identification
        public string schemaVersion = "1.0.0";
        public TrialContext context;
        public GazeProvenance provenance;
        public string inputSourceName;
        public bool isSynthetic;

        // Timing
        public string startUtcIso8601;
        public string endUtcIso8601;
        public double totalObservationTimeMs;
        public double validTrackingTimeMs;
        public double trackingLossTimeMs;
        public double bridgedGapTimeMs;
        public float trackingValidityRatio;

        // Sampling
        public int totalSampleCount;
        public double estimatedSamplingRateHz;

        // Fixations
        public int totalFixationCount;
        public double meanFixationDurationMs;
        public DwellMode dwellMode;

        // AOI specific dwell breakdown (primary fixation-based ms)
        public double tSenderHeaderMs;
        public double tSenderDomainMs;
        public double tHyperlinkTargetMs;
        public double tSecurityBadgeMs;
        public double tSubjectLineMs;
        public double tEmailBodyMs;
        public double tTotalDwellMs;
        public double tDiagnosticDwellMs;

        // Threat Inspection Ratio (R_inspect)
        public float rInspect;
        public string rInspectFlag; // "OK", "NA_NO_DWELL", "LOW_VALIDITY"
        public float rInspectOverTTotal;
        public bool includeSecurityBadgeInDiagnostic;

        // Scan sequence
        public string aoiScanSequence;

        // Outcome
        public PhishingTrialOutcome outcome;
        public bool outcomeAvailable;
        public double responseTimeMs;
        public string abortReason;

        // Per-AOI collection
        public List<AOIMetricSummary> aoiSummaries = new List<AOIMetricSummary>();
        public List<FixationRecord> fixations = new List<FixationRecord>();
    }
}
