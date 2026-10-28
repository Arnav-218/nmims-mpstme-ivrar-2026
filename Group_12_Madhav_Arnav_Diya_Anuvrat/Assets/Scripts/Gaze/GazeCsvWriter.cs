using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Handles RFC 4180 compliant CSV serialisation and companion JSON metadata generation.
    /// Uses InvariantCulture for all numbers, guarantees no-overwrite appending, and enforces
    /// rigorous separation between genuine eye-tracking data and synthetic/simulated data.
    ///
    /// Privacy guarantee: No SystemInfo.deviceName, deviceUniqueIdentifier, device model,
    /// graphics device name, usernames, or absolute local paths are written to any output file.
    /// </summary>
    public static class GazeCsvWriter
    {
        private static readonly CultureInfo s_Culture = CultureInfo.InvariantCulture;

        // ─── RFC 4180 ────────────────────────────────────────────────────────────

        /// <summary>
        /// Escapes a CSV field per RFC 4180.
        /// Encloses in double-quotes if the string contains commas, quotes, or newlines.
        /// Internal double-quotes are doubled.
        /// </summary>
        public static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            bool needsQuotes = field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (needsQuotes)
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }

        public static string FormatFloat(float value, string format = "F4")
        {
            if (float.IsNaN(value))      return "NA";
            if (float.IsInfinity(value)) return "NA";
            return value.ToString(format, s_Culture);
        }

        public static string FormatDouble(double value, string format = "F4")
        {
            if (double.IsNaN(value))      return "NA";
            if (double.IsInfinity(value)) return "NA";
            return value.ToString(format, s_Culture);
        }

        // ─── Path Resolution ─────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the session storage folder.
        /// Real data → EyeGazeData/&lt;sessionId&gt;/
        /// Synthetic/simulated → EyeGazeData_Synthetic/&lt;sessionId&gt;/
        /// Application.persistentDataPath is never read in a field initialiser or constructor.
        /// </summary>
        public static string GetSessionDirectory(string sessionId, bool isSynthetic, string customRootPath = null)
        {
            string root = !string.IsNullOrEmpty(customRootPath)
                ? customRootPath
                : Application.persistentDataPath;

            string folder      = isSynthetic ? "EyeGazeData_Synthetic" : "EyeGazeData";
            string safeSession = string.IsNullOrEmpty(sessionId) ? "S01" : sessionId;
            string fullPath    = Path.Combine(root, folder, safeSession);

            if (!Directory.Exists(fullPath))
            {
                try   { Directory.CreateDirectory(fullPath); }
                catch (Exception ex)
                {
                    Debug.LogError($"[GazeCsvWriter] Failed to create data directory: {ex.Message}");
                }
            }
            return fullPath;
        }

        // ─── Safe IO ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends text to a file.  If the file is locked (e.g. open in Excel),
        /// one retry is attempted to a timestamp-unique sibling file.
        /// Never overwrites existing data.
        /// </summary>
        private static bool SafeAppendAllText(string filePath, string content)
        {
            try
            {
                File.AppendAllText(filePath, content, Encoding.UTF8);
                return true;
            }
            catch (IOException ioEx)
            {
                try
                {
                    string dir    = Path.GetDirectoryName(filePath);
                    string stem   = Path.GetFileNameWithoutExtension(filePath);
                    string ext    = Path.GetExtension(filePath);
                    string stamp  = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", s_Culture);
                    string retry  = Path.Combine(dir!, $"{stem}_{stamp}{ext}");
                    File.AppendAllText(retry, content, Encoding.UTF8);
                    Debug.LogWarning($"[GazeCsvWriter] Original file locked ({ioEx.Message}). Data redirected to sibling file.");
                    return true;
                }
                catch (Exception ex2)
                {
                    Debug.LogError($"[GazeCsvWriter] IO failure even on retry: {ex2.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GazeCsvWriter] IO failure: {ex.Message}");
                return false;
            }
        }

        public static bool WritePhishingDecision(
            TrialContext context,
            GazeProvenance provenance,
            string selectedAction,
            bool isCorrect,
            double responseTimeMs,
            string customRootPath = null)
        {
            try
            {
                bool isSynthetic = context.IsSynthetic ||
                    provenance == GazeProvenance.Simulated_Interactive ||
                    provenance == GazeProvenance.Simulated_Scripted ||
                    provenance == GazeProvenance.SyntheticTest;
                string directory = GetSessionDirectory(context.sessionId, isSynthetic, customRootPath);
                string filePath = Path.Combine(directory, "phishing_decisions.csv");
                bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

                var row = new StringBuilder();
                if (writeHeader)
                    row.AppendLine("timestamp_utc,participant_pseudo_id,session_id,trial_id,scenario_id,condition_label,provenance,selected_action,correct,response_time_ms");

                row.Append(EscapeCsvField(DateTime.UtcNow.ToString("o", s_Culture))).Append(',')
                    .Append(EscapeCsvField(context.participantPseudoId)).Append(',')
                    .Append(EscapeCsvField(context.sessionId)).Append(',')
                    .Append(EscapeCsvField(context.trialId)).Append(',')
                    .Append(EscapeCsvField(context.scenarioId)).Append(',')
                    .Append(EscapeCsvField(context.conditionLabel)).Append(',')
                    .Append(EscapeCsvField(provenance.ToString())).Append(',')
                    .Append(EscapeCsvField(selectedAction)).Append(',')
                    .Append(isCorrect ? "true" : "false").Append(',')
                    .Append(FormatDouble(responseTimeMs, "F2")).AppendLine();

                return SafeAppendAllText(filePath, row.ToString());
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GazeCsvWriter] Error writing phishing decision record: {exception.Message}");
                return false;
            }
        }

        // ─── trial_summary.csv ───────────────────────────────────────────────────

        public static bool WriteTrialSummary(GazeTrialSummary summary, string customRootPath = null)
        {
            if (summary == null) return false;
            try
            {
                string dir      = GetSessionDirectory(summary.context.sessionId, summary.isSynthetic, customRootPath);
                string filePath = Path.Combine(dir, "trial_summary.csv");
                bool   isNew    = !File.Exists(filePath);

                var sb = new StringBuilder();
                if (isNew)
                {
                    sb.AppendLine(string.Join(",",
                        "schema_version",
                        "participant_pseudo_id",
                        "session_id",
                        "trial_id",
                        "scenario_id",
                        "condition_label",
                        "provenance",
                        "input_source",
                        "is_synthetic",
                        "start_utc_iso8601",
                        "end_utc_iso8601",
                        "t_total_observation_ms",
                        "valid_tracking_time_ms",
                        "tracking_loss_time_ms",
                        "bridged_gap_time_ms",
                        "tracking_validity_ratio",
                        "total_sample_count",
                        "estimated_sampling_rate_hz",
                        "total_fixation_count",
                        "mean_fixation_duration_ms",
                        "dwell_mode",
                        "t_sender_header_ms",
                        "t_sender_domain_ms",
                        "t_hyperlink_target_ms",
                        "t_security_badge_ms",
                        "t_subject_line_ms",
                        "t_email_body_ms",
                        "t_total_dwell_ms",
                        "t_diagnostic_dwell_ms",
                        "r_inspect",
                        "r_inspect_flag",
                        "r_inspect_over_t_total",
                        "include_security_badge_in_diagnostic",
                        "aoi_scan_sequence",
                        "outcome",
                        "outcome_available",
                        "response_time_ms",
                        "abort_reason"
                    ));
                }

                sb.AppendLine(string.Join(",",
                    EscapeCsvField(summary.schemaVersion),
                    EscapeCsvField(summary.context.participantPseudoId),
                    EscapeCsvField(summary.context.sessionId),
                    EscapeCsvField(summary.context.trialId),
                    EscapeCsvField(summary.context.scenarioId),
                    EscapeCsvField(summary.context.conditionLabel),
                    EscapeCsvField(summary.provenance.ToString()),
                    EscapeCsvField(summary.inputSourceName),
                    summary.isSynthetic ? "true" : "false",
                    EscapeCsvField(summary.startUtcIso8601),
                    EscapeCsvField(summary.endUtcIso8601),
                    FormatDouble(summary.totalObservationTimeMs,    "F2"),
                    FormatDouble(summary.validTrackingTimeMs,        "F2"),
                    FormatDouble(summary.trackingLossTimeMs,         "F2"),
                    FormatDouble(summary.bridgedGapTimeMs,           "F2"),
                    FormatFloat (summary.trackingValidityRatio,      "F4"),
                    summary.totalSampleCount.ToString(s_Culture),
                    FormatDouble(summary.estimatedSamplingRateHz,    "F2"),
                    summary.totalFixationCount.ToString(s_Culture),
                    FormatDouble(summary.meanFixationDurationMs,     "F2"),
                    EscapeCsvField(summary.dwellMode.ToString()),
                    FormatDouble(summary.tSenderHeaderMs,            "F2"),
                    FormatDouble(summary.tSenderDomainMs,            "F2"),
                    FormatDouble(summary.tHyperlinkTargetMs,         "F2"),
                    FormatDouble(summary.tSecurityBadgeMs,           "F2"),
                    FormatDouble(summary.tSubjectLineMs,             "F2"),
                    FormatDouble(summary.tEmailBodyMs,               "F2"),
                    FormatDouble(summary.tTotalDwellMs,              "F2"),
                    FormatDouble(summary.tDiagnosticDwellMs,         "F2"),
                    FormatFloat (summary.rInspect,                   "F4"),
                    EscapeCsvField(summary.rInspectFlag),
                    FormatFloat (summary.rInspectOverTTotal,         "F4"),
                    summary.includeSecurityBadgeInDiagnostic ? "true" : "false",
                    EscapeCsvField(summary.aoiScanSequence),
                    EscapeCsvField(summary.outcome.ToString()),
                    summary.outcomeAvailable ? "true" : "false",
                    FormatDouble(summary.responseTimeMs,             "F2"),
                    EscapeCsvField(summary.abortReason ?? "")
                ));

                return SafeAppendAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GazeCsvWriter] Error writing trial summary CSV: {ex.Message}");
                return false;
            }
        }

        // ─── aoi_metrics.csv ─────────────────────────────────────────────────────

        public static bool WriteAoiMetrics(GazeTrialSummary summary, string customRootPath = null)
        {
            if (summary?.aoiSummaries == null) return false;
            try
            {
                string dir      = GetSessionDirectory(summary.context.sessionId, summary.isSynthetic, customRootPath);
                string filePath = Path.Combine(dir, "aoi_metrics.csv");
                bool   isNew    = !File.Exists(filePath);

                var sb = new StringBuilder();
                if (isNew)
                {
                    sb.AppendLine(string.Join(",",
                        "schema_version",
                        "participant_pseudo_id",
                        "session_id",
                        "trial_id",
                        "scenario_id",
                        "provenance",
                        "is_synthetic",
                        "aoi_type",
                        "aoi_id",
                        "is_diagnostic",
                        "fixation_dwell_ms",
                        "fixation_count",
                        "mean_fixation_duration_ms",
                        "max_fixation_duration_ms",
                        "time_to_first_fixation_ms",
                        "first_fixation_order",
                        "revisit_count",
                        "raw_sample_dwell_ms",
                        "raw_sample_count"
                    ));
                }

                foreach (var aoi in summary.aoiSummaries)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsvField(summary.schemaVersion),
                        EscapeCsvField(summary.context.participantPseudoId),
                        EscapeCsvField(summary.context.sessionId),
                        EscapeCsvField(summary.context.trialId),
                        EscapeCsvField(summary.context.scenarioId),
                        EscapeCsvField(summary.provenance.ToString()),
                        summary.isSynthetic ? "true" : "false",
                        EscapeCsvField(aoi.aoiType.ToString()),
                        EscapeCsvField(aoi.aoiId),
                        aoi.isDiagnostic ? "true" : "false",
                        FormatDouble(aoi.fixationDwellMs,       "F2"),
                        aoi.fixationCount.ToString(s_Culture),
                        FormatDouble(aoi.meanFixationDurationMs,"F2"),
                        FormatDouble(aoi.maxFixationDurationMs, "F2"),
                        aoi.timeToFirstFixationMs >= 0 ? FormatDouble(aoi.timeToFirstFixationMs, "F2") : "NA",
                        aoi.firstFixationOrder > 0 ? aoi.firstFixationOrder.ToString(s_Culture) : "NA",
                        aoi.revisitCount.ToString(s_Culture),
                        FormatDouble(aoi.rawSampleDwellMs,      "F2"),
                        aoi.rawSampleCount.ToString(s_Culture)
                    ));
                }

                return SafeAppendAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GazeCsvWriter] Error writing aoi metrics CSV: {ex.Message}");
                return false;
            }
        }

        // ─── fixations.csv ───────────────────────────────────────────────────────

        public static bool WriteFixations(GazeTrialSummary summary, string customRootPath = null)
        {
            if (summary?.fixations == null) return false;
            try
            {
                string dir      = GetSessionDirectory(summary.context.sessionId, summary.isSynthetic, customRootPath);
                string filePath = Path.Combine(dir, "fixations.csv");
                bool   isNew    = !File.Exists(filePath);

                var sb = new StringBuilder();
                if (isNew)
                {
                    sb.AppendLine(string.Join(",",
                        "schema_version",
                        "participant_pseudo_id",
                        "session_id",
                        "trial_id",
                        "scenario_id",
                        "provenance",
                        "is_synthetic",
                        "fixation_id",
                        "start_time_seconds",
                        "end_time_seconds",
                        "duration_ms",
                        "assigned_aoi_type",
                        "assigned_aoi_id",
                        "sample_count",
                        "bridged_gap_ms",
                        "max_dispersion_degrees",
                        "centroid_origin_x",
                        "centroid_origin_y",
                        "centroid_origin_z",
                        "centroid_dir_x",
                        "centroid_dir_y",
                        "centroid_dir_z",
                        "is_valid"
                    ));
                }

                foreach (var fix in summary.fixations)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsvField(summary.schemaVersion),
                        EscapeCsvField(summary.context.participantPseudoId),
                        EscapeCsvField(summary.context.sessionId),
                        EscapeCsvField(summary.context.trialId),
                        EscapeCsvField(summary.context.scenarioId),
                        EscapeCsvField(summary.provenance.ToString()),
                        summary.isSynthetic ? "true" : "false",
                        fix.fixationId.ToString(s_Culture),
                        FormatDouble(fix.startTimeSeconds,       "F4"),
                        FormatDouble(fix.endTimeSeconds,         "F4"),
                        FormatDouble(fix.durationMs,             "F2"),
                        EscapeCsvField(fix.assignedAoiType.ToString()),
                        EscapeCsvField(fix.assignedAoiId),
                        fix.sampleCount.ToString(s_Culture),
                        FormatDouble(fix.bridgedGapMs,           "F2"),
                        FormatFloat (fix.maxDispersionDegrees,   "F2"),
                        FormatFloat (fix.centroidOrigin.x,       "F4"),
                        FormatFloat (fix.centroidOrigin.y,       "F4"),
                        FormatFloat (fix.centroidOrigin.z,       "F4"),
                        FormatFloat (fix.centroidDirection.x,    "F4"),
                        FormatFloat (fix.centroidDirection.y,    "F4"),
                        FormatFloat (fix.centroidDirection.z,    "F4"),
                        fix.isValid ? "true" : "false"
                    ));
                }

                return SafeAppendAllText(filePath, sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GazeCsvWriter] Error writing fixations CSV: {ex.Message}");
                return false;
            }
        }

        // ─── session_metadata.json ───────────────────────────────────────────────

        /// <summary>
        /// Writes session metadata JSON companion file.
        /// Privacy: operatingSystemPlatform is only the RuntimePlatform enum name —
        /// NOT SystemInfo.operatingSystem, deviceName, deviceUniqueIdentifier, deviceModel,
        /// graphicsDeviceName or any user-identifying string.
        /// The file is written only if it does not already exist (never overwritten).
        /// </summary>
        public static bool WriteSessionMetadata(
            TrialContext context,
            GazeProvenance provenance,
            string inputSourceName,
            FixationDetector.Settings fixationSettings,
            GazeMetricsAccumulator.Settings metricsSettings,
            string customRootPath = null)
        {
            try
            {
                bool isSynthetic = context.IsSynthetic ||
                                   provenance == GazeProvenance.Simulated_Interactive ||
                                   provenance == GazeProvenance.Simulated_Scripted   ||
                                   provenance == GazeProvenance.SyntheticTest;

                string dir      = GetSessionDirectory(context.sessionId, isSynthetic, customRootPath);
                string filePath = Path.Combine(dir, "session_metadata.json");

                // Never overwrite an existing metadata file.
                if (File.Exists(filePath)) return true;

                var metadata = new SessionMetadata
                {
                    schemaVersion           = "1.0.0",
                    participantPseudoId     = context.participantPseudoId,
                    sessionId               = context.sessionId,
                    conditionLabel          = context.conditionLabel,
                    provenance              = provenance.ToString(),
                    inputSourceName         = inputSourceName,
                    isSynthetic             = isSynthetic,
                    createdUtcIso8601       = DateTime.UtcNow.ToString("o"),
                    unityVersion            = Application.unityVersion,
                    // RuntimePlatform enum name only — no device serial, model, or username.
                    runtimePlatform         = Application.platform.ToString(),
                    thresholds = new ThresholdMetadata
                    {
                        minFixationDurationMs             = fixationSettings.minFixationDurationMs,
                        maxDispersionDegrees              = fixationSettings.maxDispersionDegrees,
                        saccadeVelocityThresholdDegPerSec = fixationSettings.saccadeVelocityThresholdDegPerSec,
                        maxGapToleranceMs                 = fixationSettings.maxGapToleranceMs,
                        minValidSampleRatioInFixation     = fixationSettings.minValidSampleRatioInFixation,
                        minTrackingValidityRatio          = metricsSettings.minTrackingValidityRatio,
                        includeSecurityBadgeInDiagnostic  = metricsSettings.includeSecurityBadgeInDiagnostic,
                        dwellMode                         = metricsSettings.primaryDwellMode.ToString()
                    }
                };

                string json = JsonUtility.ToJson(metadata, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GazeCsvWriter] Error writing session metadata JSON: {ex.Message}");
                return false;
            }
        }

        // ─── Private serialisation models ────────────────────────────────────────

        [Serializable]
        private class SessionMetadata
        {
            public string schemaVersion;
            public string participantPseudoId;
            public string sessionId;
            public string conditionLabel;
            public string provenance;
            public string inputSourceName;
            public bool   isSynthetic;
            public string createdUtcIso8601;
            public string unityVersion;
            public string runtimePlatform;   // RuntimePlatform enum name — not a device identifier
            public ThresholdMetadata thresholds;
        }

        [Serializable]
        private class ThresholdMetadata
        {
            public double minFixationDurationMs;
            public float  maxDispersionDegrees;
            public float  saccadeVelocityThresholdDegPerSec;
            public double maxGapToleranceMs;
            public float  minValidSampleRatioInFixation;
            public float  minTrackingValidityRatio;
            public bool   includeSecurityBadgeInDiagnostic;
            public string dwellMode;
        }
    }
}
