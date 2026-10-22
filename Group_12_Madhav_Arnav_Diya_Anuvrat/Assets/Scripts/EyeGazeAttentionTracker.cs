using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Tracks participant eye-gaze fixations, visual attention heatmaps, and Area-of-Interest (AOI) dwell times
    /// across suspicious email components (sender header, domain URL, padlock badge, subject line, body text,
    /// and hyperlink call-to-action).
    /// Correlates visual attention with threat recognition latency and 14-day recall.
    /// 
    /// Student Roles:
    /// - D065 Diya Shah (Eye-Gaze & Attention Tracking Specialist): AOI raycasting, fixation thresholding, and pupil dynamics
    /// - I080 Anuvrat Tripathi (Human Factors & Retention Analyst): Attention telemetry export, Threat Inspection Ratio (TIR)
    ///   formulation, active-fixation flushing on trial completion, and CSV schema export
    /// </summary>
    public class EyeGazeAttentionTracker : MonoBehaviour
    {
        public enum AOICategory
        {
            SenderDomainHeader,
            SecurityPadlockBadge,
            SubjectLineUrgency,
            BodyTextContent,
            HyperlinkCallToAction,
            IrrelevantBackground
        }

        /// <summary>
        /// Selects which Threat Inspection Ratio (TIR) operational definition is used when computing
        /// attention allocation across email security indicators:
        /// - SenderDomainAndHyperlink (Primary default):
        ///   (Dwell_SenderDomain + Dwell_Hyperlink) / Total_Email_AOI_Dwell.
        ///   Matches RESEARCH_AND_IMPLEMENTATION_GUIDE.md Section 1.1 and the original I080 code specification.
        /// - AllSecurityCuesIncludePadlock (Sensitivity variant):
        ///   (Dwell_SenderDomain + Dwell_SecurityBadge + Dwell_Hyperlink) / Total_Email_AOI_Dwell.
        ///   Matches README.md abstract description including SSL padlock badges.
        /// - SenderDomainOnly (Sensitivity variant):
        ///   Dwell_SenderDomain / Total_Email_AOI_Dwell.
        ///   Matches RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md Section 3.4.
        /// </summary>
        public enum ThreatInspectionFormula
        {
            SenderDomainAndHyperlink,
            AllSecurityCuesIncludePadlock,
            SenderDomainOnly
        }

        [System.Serializable]
        public struct AOIFixationRecord
        {
            public AOICategory aoi;
            public float totalDwellTimeSeconds;
            public int fixationCount;
            public float firstFixationLatencySeconds;
        }

        public const string TelemetrySchemaVersion = "v2.0";

        [Header("Gaze Raycast Configuration")]
        [SerializeField] private Transform gazeOriginCamera;
        [SerializeField] private float maxGazeDistance = 5.0f;
        [SerializeField] private LayerMask aoiLayerMask;
        [SerializeField] private float fixationDwellThreshold = 0.15f; // Minimum 150ms for cognitive fixation

        [Header("Threat Inspection Ratio (TIR) Configuration (I080)")]
        [SerializeField] private ThreatInspectionFormula primaryTirFormula = ThreatInspectionFormula.SenderDomainAndHyperlink;

        [Header("Runtime Dwell Telemetry")]
        [SerializeField] private AOICategory activeAOI = AOICategory.IrrelevantBackground;
        [SerializeField] private float currentAOIDwellTimer = 0f;
        [SerializeField] private float validGazeSampleSeconds = 0f;
        [SerializeField] private float invalidGazeSampleSeconds = 0f;
        [SerializeField] private int currentTrialIndex = 0;

        private Dictionary<AOICategory, AOIFixationRecord> fixationDatabase = new Dictionary<AOICategory, AOIFixationRecord>();
        private float trialElapsedTimer = 0f;

        private void Awake()
        {
            InitializeAOIDatabase();
        }

        private void InitializeAOIDatabase()
        {
            fixationDatabase.Clear();
            foreach (AOICategory cat in Enum.GetValues(typeof(AOICategory)))
            {
                fixationDatabase[cat] = new AOIFixationRecord
                {
                    aoi = cat,
                    totalDwellTimeSeconds = 0f,
                    fixationCount = 0,
                    firstFixationLatencySeconds = -1f
                };
            }
        }

        private void Update()
        {
            trialElapsedTimer += Time.deltaTime;
            ProcessGazeRaycast();
        }

        private void ProcessGazeRaycast()
        {
            if (gazeOriginCamera == null)
            {
                gazeOriginCamera = Camera.main != null ? Camera.main.transform : null;
                if (gazeOriginCamera == null)
                {
                    RecordMissingGazeSample(Time.deltaTime);
                    return;
                }
            }

            Ray gazeRay = new Ray(gazeOriginCamera.position, gazeOriginCamera.forward);
            RaycastHit hit;

            AOICategory detectedAOI = AOICategory.IrrelevantBackground;

            if (Physics.Raycast(gazeRay, out hit, maxGazeDistance, aoiLayerMask))
            {
                // =========================================================================
                // TODO [D065 - Diya Shah - Eye-Gaze & Attention Tracking Specialist]:
                // 1. Query the hit collider's AOI identifier tag (e.g., "AOI_SenderDomain", "AOI_Hyperlink").
                // 2. Filter out high-frequency saccadic eye tremors using a moving-average gaze window.
                // 3. Detect pupil dilation changes under induced cognitive stress when encountering phishing urgency cues.
                // =========================================================================

                if (hit.collider.CompareTag("AOI_SenderDomain"))
                    detectedAOI = AOICategory.SenderDomainHeader;
                else if (hit.collider.CompareTag("AOI_Hyperlink"))
                    detectedAOI = AOICategory.HyperlinkCallToAction;
                else if (hit.collider.CompareTag("AOI_SubjectLine"))
                    detectedAOI = AOICategory.SubjectLineUrgency;
                else if (hit.collider.CompareTag("AOI_BodyText"))
                    detectedAOI = AOICategory.BodyTextContent;
                else if (hit.collider.CompareTag("AOI_SecurityBadge"))
                    detectedAOI = AOICategory.SecurityPadlockBadge;
            }

            validGazeSampleSeconds += Mathf.Max(0f, Time.deltaTime);
            UpdateAOIDwellTimes(detectedAOI);
        }

        /// <summary>
        /// Records a missing or invalid eye-tracker sample (e.g., blink or tracking loss) and flushes
        /// any ongoing fixation that already met the 150 ms cognitive fixation threshold.
        /// </summary>
        public void RecordMissingGazeSample(float sampleDeltaSeconds)
        {
            if (sampleDeltaSeconds > 0f && ! float.IsNaN(sampleDeltaSeconds) && !float.IsInfinity(sampleDeltaSeconds))
            {
                invalidGazeSampleSeconds += sampleDeltaSeconds;
            }
            FlushActiveFixation();
            activeAOI = AOICategory.IrrelevantBackground;
        }

        private void UpdateAOIDwellTimes(AOICategory detectedAOI)
        {
            if (detectedAOI == activeAOI)
            {
                currentAOIDwellTimer += Time.deltaTime;
            }
            else
            {
                CommitPendingFixationIfEligible();
                activeAOI = detectedAOI;
                currentAOIDwellTimer = 0f;
            }
        }

        private void CommitPendingFixationIfEligible()
        {
            if (currentAOIDwellTimer >= fixationDwellThreshold && fixationDatabase.ContainsKey(activeAOI))
            {
                AOIFixationRecord rec = fixationDatabase[activeAOI];
                rec.totalDwellTimeSeconds += currentAOIDwellTimer;
                rec.fixationCount++;
                if (rec.firstFixationLatencySeconds < 0f)
                {
                    rec.firstFixationLatencySeconds = Mathf.Max(0f, trialElapsedTimer - currentAOIDwellTimer);
                }
                fixationDatabase[activeAOI] = rec;
            }
            currentAOIDwellTimer = 0f;
        }

        /// <summary>
        /// Flushes the currently active gaze interval into fixationDatabase when a trial ends.
        /// Without flushing, if a participant is still looking at the sender domain or hyperlink
        /// at the moment they click or report, that final fixation interval would be lost.
        /// </summary>
        public void FlushActiveFixation()
        {
            if ( fixationDatabase == null || fixationDatabase.Count == 0)
            {
                InitializeAOIDatabase();
            }
            CommitPendingFixationIfEligible();
        }

        /// <summary>
        /// Pure, static calculation of the Threat Inspection Ratio (TIR).
        /// Returns -1f when total email AOI dwell is zero or inputs are invalid (undefined ratio),
        /// never dividing by zero.
        /// Note: TIR quantifies visual attention allocation across defined AOIs; it does not prove
        /// cognitive comprehension or guarantee safe decision-making.
        /// </summary>
        public static float CalculateThreatInspectionRatio(
            float senderDomainDwellSec,
            float securityBadgeDwellSec,
            float subjectLineDwellSec,
            float bodyTextDwellSec,
            float hyperlinkDwellSec,
            ThreatInspectionFormula formula = ThreatInspectionFormula.SenderDomainAndHyperlink)
        {
            if (float.IsNaN(senderDomainDwellSec) || float.IsNaN(securityBadgeDwellSec) ||
                float.IsNaN(subjectLineDwellSec) || float.IsNaN(bodyTextDwellSec) ||
                float.IsNaN(hyperlinkDwellSec))
            {
                return -1f;
            }

            if (senderDomainDwellSec < 0f || securityBadgeDwellSec < 0f ||
                subjectLineDwellSec < 0f || bodyTextDwellSec < 0f || hyperlinkDwellSec < 0f)
            {
                return -1f;
            }

            float totalEmailDwell =
                senderDomainDwellSec +
                securityBadgeDwellSec +
                subjectLineDwellSec +
                bodyTextDwellSec +
                hyperlinkDwellSec;

            if (totalEmailDwell <= 0f || float.IsInfinity(totalEmailDwell))
            {
                return -1f;
            }

            float cueDwell = 0f;
            switch (formula)
            {
                case ThreatInspectionFormula.AllSecurityCuesIncludePadlock:
                    cueDwell = senderDomainDwellSec + securityBadgeDwellSec + hyperlinkDwellSec;
                    break;
                case ThreatInspectionFormula.SenderDomainOnly:
                    cueDwell = senderDomainDwellSec;
                    break;
                case ThreatInspectionFormula.SenderDomainAndHyperlink:
                default:
                    cueDwell = senderDomainDwellSec + hyperlinkDwellSec;
                    break;
            }

            return Mathf.Clamp01(cueDwell / totalEmailDwell);
        }

        /// <summary>
        /// Returns the header line for the basic 8-column legacy CSV row format.
        /// </summary>
        public static string GetLegacyTelemetryCsvHeader()
        {
            return "participant_id,day_interval,scenario_id,dwell_sender_domain_sec,dwell_hyperlink_sec,fixations_sender_domain,fixations_hyperlink,threat_inspection_ratio";
        }

        /// <summary>
        /// Returns the header line for the full schema v2.0 CSV consumed by telemetry/gaze_metrics.py.
        /// </summary>
        public static string GetTelemetryCsvHeader()
        {
            return "schema_version,participant_id,training_condition,day_interval,trial_index,scenario_id," +
                   "trial_duration_sec,valid_gaze_sec,invalid_gaze_sec," +
                   "dwell_sender_domain_sec,dwell_security_badge_sec,dwell_subject_line_sec,dwell_body_text_sec,dwell_hyperlink_sec," +
                   "fixations_sender_domain,fixations_security_badge,fixations_subject_line,fixations_body_text,fixations_hyperlink," +
                   "tir_sender_domain_hyperlink,tir_all_security_cues,tir_sender_domain_only";
        }

        private static string EscapeCsvField(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            string cleaned = raw.Trim();
            if (cleaned.Contains(",") || cleaned.Contains("\"") || cleaned.Contains("\n") || cleaned.Contains("\r"))
            {
                return "\"" + cleaned.Replace("\"", "\"\"") + "\"";
            }
            return cleaned;
        }

        private static string FormatFloatOrBlank(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                return "";
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Preserves the original 3-argument signature for compatibility with existing callers.
        /// Flushes any active fixation, computes the primary Threat Inspection Ratio across all 5 email
        /// AOIs, and formats the 8-column CSV row using invariant "." decimal separators.
        /// When total email dwell is zero, the TIR field is left blank ("") so Python parsers treat it as
        /// undefined rather than 0.000.
        /// </summary>
        public string ExportTelemetryCsvRow(string participantId, int scenarioId, int dayInterval)
        {
            // =========================================================================
            // Implemented [I080 - Anuvrat Tripathi - Human Factors & Retention Analyst]:
            // 1. Flush the final ongoing fixation if the trial ended while the participant was gazing at an AOI.
            // 2. Compute Threat Inspection Ratio using all 5 email AOIs in the denominator.
            // 3. Format telemetry string with CultureInfo.InvariantCulture and safe CSV escaping.
            // =========================================================================
            FlushActiveFixation();

            AOIFixationRecord domainRec = fixationDatabase[AOICategory.SenderDomainHeader];
            AOIFixationRecord badgeRec = fixationDatabase[AOICategory.SecurityPadlockBadge];
            AOIFixationRecord subjectRec = fixationDatabase[AOICategory.SubjectLineUrgency];
            AOIFixationRecord bodyRec = fixationDatabase[AOICategory.BodyTextContent];
            AOIFixationRecord linkRec = fixationDatabase[AOICategory.HyperlinkCallToAction];

            float inspectionRatio = CalculateThreatInspectionRatio(
                domainRec.totalDwellTimeSeconds,
                badgeRec.totalDwellTimeSeconds,
                subjectRec.totalDwellTimeSeconds,
                bodyRec.totalDwellTimeSeconds,
                linkRec.totalDwellTimeSeconds,
                primaryTirFormula);

            string safeId = EscapeCsvField(string.IsNullOrWhiteSpace(participantId) ? "UNKNOWN" : participantId);
            string domainDwellStr = domainRec.totalDwellTimeSeconds.ToString("F3", CultureInfo.InvariantCulture);
            string linkDwellStr = linkRec.totalDwellTimeSeconds.ToString("F3", CultureInfo.InvariantCulture);
            string tirStr = FormatFloatOrBlank(inspectionRatio);

            return $"{safeId},{dayInterval},{scenarioId},{domainDwellStr},{linkDwellStr},{domainRec.fixationCount},{linkRec.fixationCount},{tirStr}";
        }

        /// <summary>
        /// Exports a complete Schema v2.0 telemetry row containing all 5 email AOI dwell times,
        /// fixation counts, valid/invalid gaze durations, and all 3 TIR formula variants for direct
        /// ingestion by telemetry/gaze_metrics.py.
        /// </summary>
        public string ExportDetailedTelemetryCsvRow(
            string participantId,
            string trainingCondition,
            int dayInterval,
            int trialIndex,
            string scenarioId)
        {
            FlushActiveFixation();

            AOIFixationRecord domainRec = fixationDatabase[AOICategory.SenderDomainHeader];
            AOIFixationRecord badgeRec = fixationDatabase[AOICategory.SecurityPadlockBadge];
            AOIFixationRecord subjectRec = fixationDatabase[AOICategory.SubjectLineUrgency];
            AOIFixationRecord bodyRec = fixationDatabase[AOICategory.BodyTextContent];
            AOIFixationRecord linkRec = fixationDatabase[AOICategory.HyperlinkCallToAction];

            float tirPrimary = CalculateThreatInspectionRatio(
                domainRec.totalDwellTimeSeconds, badgeRec.totalDwellTimeSeconds,
                subjectRec.totalDwellTimeSeconds, bodyRec.totalDwellTimeSeconds,
                linkRec.totalDwellTimeSeconds, ThreatInspectionFormula.SenderDomainAndHyperlink);

            float tirAllCues = CalculateThreatInspectionRatio(
                domainRec.totalDwellTimeSeconds, badgeRec.totalDwellTimeSeconds,
                subjectRec.totalDwellTimeSeconds, bodyRec.totalDwellTimeSeconds,
                linkRec.totalDwellTimeSeconds, ThreatInspectionFormula.AllSecurityCuesIncludePadlock);

            float tirDomainOnly = CalculateThreatInspectionRatio(
                domainRec.totalDwellTimeSeconds, badgeRec.totalDwellTimeSeconds,
                subjectRec.totalDwellTimeSeconds, bodyRec.totalDwellTimeSeconds,
                linkRec.totalDwellTimeSeconds, ThreatInspectionFormula.SenderDomainOnly);

            StringBuilder sb = new StringBuilder(256);
            sb.Append(TelemetrySchemaVersion).Append(',');
            sb.Append(EscapeCsvField(string.IsNullOrWhiteSpace(participantId) ? "UNKNOWN" : participantId)).Append(',');
            sb.Append(EscapeCsvField(string.IsNullOrWhiteSpace(trainingCondition) ? "UNSPECIFIED" : trainingCondition)).Append(',');
            sb.Append(dayInterval.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(trialIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsvField(string.IsNullOrWhiteSpace(scenarioId) ? "SCEN_UNKNOWN" : scenarioId)).Append(',');
            sb.Append(FormatFloatOrBlank(trialElapsedTimer)).Append(',');
            sb.Append(FormatFloatOrBlank(validGazeSampleSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(invalidGazeSampleSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(domainRec.totalDwellTimeSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(badgeRec.totalDwellTimeSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(subjectRec.totalDwellTimeSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(bodyRec.totalDwellTimeSeconds)).Append(',');
            sb.Append(FormatFloatOrBlank(linkRec.totalDwellTimeSeconds)).Append(',');
            sb.Append(domainRec.fixationCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(badgeRec.fixationCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(subjectRec.fixationCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(bodyRec.fixationCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(linkRec.fixationCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(FormatFloatOrBlank(tirPrimary)).Append(',');
            sb.Append(FormatFloatOrBlank(tirAllCues)).Append(',');
            sb.Append(FormatFloatOrBlank(tirDomainOnly));
            return sb.ToString();
        }

        /// <summary>
        /// Safely appends a detailed Schema v2.0 telemetry row to a CSV file on disk, writing the
        /// header row automatically when creating a new file.
        /// </summary>
        public bool TryAppendDetailedTelemetryToFile(
            string filePath,
            string participantId,
            string trainingCondition,
            int dayInterval,
            string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
                string row = ExportDetailedTelemetryCsvRow(
                    participantId, trainingCondition, dayInterval, currentTrialIndex, scenarioId);

                using (StreamWriter writer = new StreamWriter(filePath, append: true, encoding: new UTF8Encoding(false)))
                {
                    if (writeHeader)
                    {
                        writer.WriteLine(GetTelemetryCsvHeader());
                    }
                    writer.WriteLine(row);
                }
                currentTrialIndex++;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EyeGazeAttentionTracker] Failed to write telemetry CSV '{filePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Testing/Simulation helper: injects synthetic dwell time into an AOI category so unit tests
        /// or offline replays can verify TIR and CSV formatting without a physical VR headset.
        /// </summary>
        public void InjectSyntheticAOIFixation(AOICategory category, float dwellSeconds, float latencySeconds = 0.25f)
        {
            if (dwellSeconds <= 0f || float.IsNaN(dwellSeconds) || float.IsInfinity(dwellSeconds))
                return;

            if (fixationDatabase == null || fixationDatabase.Count == 0)
            {
                InitializeAOIDatabase();
            }

            AOIFixationRecord rec = fixationDatabase[category];
            rec.totalDwellTimeSeconds += dwellSeconds;
            rec.fixationCount++;
            if (rec.firstFixationLatencySeconds < 0f)
            {
                rec.firstFixationLatencySeconds = Mathf.Max(0f, latencySeconds);
            }
            fixationDatabase[category] = rec;
            validGazeSampleSeconds += dwellSeconds;
            trialElapsedTimer += dwellSeconds;
        }

        public void ResetGazeTracker()
        {
            trialElapsedTimer = 0f;
            currentAOIDwellTimer = 0f;
            validGazeSampleSeconds = 0f;
            invalidGazeSampleSeconds = 0f;
            activeAOI = AOICategory.IrrelevantBackground;
            InitializeAOIDatabase();
        }
    }
}
