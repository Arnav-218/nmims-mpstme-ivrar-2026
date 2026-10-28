using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Pure C# accumulator that ingests gaze samples and completed fixations,
    /// maintains temporal metrics and per-AOI dwell counters,
    /// and computes the Threat Inspection Ratio (R_inspect) and associated research metrics.
    /// </summary>
    public class GazeMetricsAccumulator
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("Whether to include SecurityBadge in the diagnostic numerator of R_inspect.")]
            public bool includeSecurityBadgeInDiagnostic;

            [Tooltip("Minimum tracking validity ratio required for R_inspect flag to be 'OK' (e.g. 0.5 = 50%).")]
            public float minTrackingValidityRatio;

            [Tooltip("Primary dwell mode to evaluate R_inspect and T_total_dwell.")]
            public DwellMode primaryDwellMode;

            public static Settings Default => new Settings
            {
                includeSecurityBadgeInDiagnostic = false,
                minTrackingValidityRatio = 0.50f,
                primaryDwellMode = DwellMode.FixationBased
            };
        }

        private readonly Settings _settings;
        private readonly Dictionary<string, AOIMetricSummary> _aoiMetrics = new Dictionary<string, AOIMetricSummary>(StringComparer.OrdinalIgnoreCase);
        private readonly List<FixationRecord> _fixations = new List<FixationRecord>();
        private readonly List<string> _scanSequence = new List<string>();

        private TrialContext _context;
        private GazeProvenance _provenance;
        private string _sourceName;
        private DateTime _startUtc;
        private double _startTrialTimestampSeconds = -1.0;
        private double _lastSampleTimestampSeconds = -1.0;

        private int _totalSamples = 0;
        private int _validSamples = 0;
        private double _validTrackingTimeMs = 0.0;
        private double _trackingLossTimeMs = 0.0;
        private double _bridgedGapTimeMs = 0.0;

        private string _lastFixatedAoiId = null;
        private int _firstFixationRankCounter = 0;

        public Settings CurrentSettings => _settings;
        public IReadOnlyList<FixationRecord> Fixations => _fixations;
        public IReadOnlyDictionary<string, AOIMetricSummary> AoiMetrics => _aoiMetrics;

        public GazeMetricsAccumulator(Settings settings)
        {
            _settings = settings;
        }

        public GazeMetricsAccumulator() : this(Settings.Default)
        {
        }

        public void InitializeTrial(TrialContext context, GazeProvenance provenance, string sourceName, double startTimestampSeconds)
        {
            _context = context;
            _provenance = provenance;
            _sourceName = sourceName ?? "Unknown";
            _startUtc = DateTime.UtcNow;
            _startTrialTimestampSeconds = startTimestampSeconds;
            _lastSampleTimestampSeconds = startTimestampSeconds;

            _totalSamples = 0;
            _validSamples = 0;
            _validTrackingTimeMs = 0.0;
            _trackingLossTimeMs = 0.0;
            _bridgedGapTimeMs = 0.0;

            _lastFixatedAoiId = null;
            _firstFixationRankCounter = 0;

            _aoiMetrics.Clear();
            _fixations.Clear();
            _scanSequence.Clear();
        }

        /// <summary>
        /// Registers a raw gaze sample to track frame-level temporal coverage and raw sample dwell.
        /// </summary>
        public void AccumulateSample(GazeSample sample, AOIType hitAoiType, string hitAoiId)
        {
            if (_startTrialTimestampSeconds < 0)
            {
                _startTrialTimestampSeconds = sample.timestampSeconds;
                _lastSampleTimestampSeconds = sample.timestampSeconds;
            }

            double dtSeconds = sample.timestampSeconds - _lastSampleTimestampSeconds;
            if (dtSeconds < 0) dtSeconds = 0; // Guard against timestamp regression
            double dtMs = dtSeconds * 1000.0;

            _totalSamples++;
            _lastSampleTimestampSeconds = sample.timestampSeconds;

            if (sample.isValid)
            {
                _validSamples++;
                _validTrackingTimeMs += dtMs;

                // Accumulate raw sample dwell on AOI
                if (hitAoiType != AOIType.IrrelevantBackground && !string.IsNullOrEmpty(hitAoiId))
                {
                    var summary = GetOrCreateAoiSummary(hitAoiType, hitAoiId);
                    summary.rawSampleDwellMs += dtMs;
                    summary.rawSampleCount++;
                }
            }
            else
            {
                _trackingLossTimeMs += dtMs;
            }
        }

        /// <summary>
        /// Registers a completed fixation from the FixationDetector.
        /// </summary>
        public void AccumulateFixation(FixationRecord fixation)
        {
            _fixations.Add(fixation);
            _bridgedGapTimeMs += fixation.bridgedGapMs;

            if (fixation.assignedAoiType == AOIType.IrrelevantBackground)
            {
                _lastFixatedAoiId = null;
                return;
            }

            var summary = GetOrCreateAoiSummary(fixation.assignedAoiType, fixation.assignedAoiId);

            // Accumulate fixation dwell
            summary.fixationDwellMs += fixation.durationMs;
            summary.fixationCount++;

            if (fixation.durationMs > summary.maxFixationDurationMs)
            {
                summary.maxFixationDurationMs = fixation.durationMs;
            }

            // Time To First Fixation (TTFF)
            if (summary.timeToFirstFixationMs < 0)
            {
                double ttff = (fixation.startTimeSeconds - _startTrialTimestampSeconds) * 1000.0;
                summary.timeToFirstFixationMs = Math.Max(0.0, ttff);
                _firstFixationRankCounter++;
                summary.firstFixationOrder = _firstFixationRankCounter;
            }

            // Revisit count: increment if return to an AOI after fixating a different AOI
            if (_lastFixatedAoiId != null && !string.Equals(_lastFixatedAoiId, fixation.assignedAoiId, StringComparison.OrdinalIgnoreCase))
            {
                if (summary.fixationCount > 1)
                {
                    summary.revisitCount++;
                }
            }
            _lastFixatedAoiId = fixation.assignedAoiId;

            // Update scan sequence
            if (_scanSequence.Count == 0 || !_scanSequence[_scanSequence.Count - 1].Equals(fixation.assignedAoiId, StringComparison.OrdinalIgnoreCase))
            {
                _scanSequence.Add(fixation.assignedAoiId);
            }
        }

        private AOIMetricSummary GetOrCreateAoiSummary(AOIType type, string aoiId)
        {
            string key = string.IsNullOrEmpty(aoiId) ? type.ToString() : aoiId;
            if (!_aoiMetrics.TryGetValue(key, out var summary))
            {
                bool isDiagnostic = type.IsDiagnostic(_settings.includeSecurityBadgeInDiagnostic);
                summary = new AOIMetricSummary(type, key, isDiagnostic);
                _aoiMetrics[key] = summary;
            }
            return summary;
        }

        /// <summary>
        /// Computes and generates the final GazeTrialSummary for export.
        /// </summary>
        public GazeTrialSummary FinalizeTrial(double endTimestampSeconds, TrialOutcomeInfo outcomeInfo, string abortReason = null)
        {
            var summary = new GazeTrialSummary();
            summary.schemaVersion = "1.0.0";
            summary.context = _context;
            summary.provenance = _provenance;
            summary.inputSourceName = _sourceName;
            summary.isSynthetic = _context.IsSynthetic ||
                                  _provenance == GazeProvenance.Simulated_Interactive ||
                                  _provenance == GazeProvenance.Simulated_Scripted ||
                                  _provenance == GazeProvenance.SyntheticTest;

            summary.startUtcIso8601 = _startUtc.ToString("o");
            summary.endUtcIso8601 = DateTime.UtcNow.ToString("o");

            double totalObsSec = endTimestampSeconds - _startTrialTimestampSeconds;
            if (totalObsSec < 0) totalObsSec = 0;
            summary.totalObservationTimeMs = totalObsSec * 1000.0;
            summary.validTrackingTimeMs = _validTrackingTimeMs;
            summary.trackingLossTimeMs = _trackingLossTimeMs;
            summary.bridgedGapTimeMs = _bridgedGapTimeMs;

            summary.trackingValidityRatio = summary.totalObservationTimeMs > 0.0
                ? Mathf.Clamp01((float)(_validTrackingTimeMs / summary.totalObservationTimeMs))
                : 0.0f;

            summary.totalSampleCount = _totalSamples;
            summary.estimatedSamplingRateHz = summary.totalObservationTimeMs > 0.0
                ? (_totalSamples / (summary.totalObservationTimeMs / 1000.0))
                : 0.0;

            summary.totalFixationCount = _fixations.Count;
            summary.dwellMode = _settings.primaryDwellMode;
            summary.includeSecurityBadgeInDiagnostic = _settings.includeSecurityBadgeInDiagnostic;

            // Calculate mean fixation duration
            double totalFixDurationMs = 0;
            for (int i = 0; i < _fixations.Count; i++)
            {
                totalFixDurationMs += _fixations[i].durationMs;
            }
            summary.meanFixationDurationMs = _fixations.Count > 0
                ? totalFixDurationMs / _fixations.Count
                : 0.0;

            // Finalize per-AOI mean durations and assemble metrics
            double tSender = 0.0;
            double tDomain = 0.0;
            double tHyperlink = 0.0;
            double tSecurityBadge = 0.0;
            double tSubject = 0.0;
            double tBody = 0.0;
            double tTotalDwell = 0.0;

            foreach (var kvp in _aoiMetrics)
            {
                var aoi = kvp.Value;
                if (aoi.fixationCount > 0)
                {
                    aoi.meanFixationDurationMs = aoi.fixationDwellMs / aoi.fixationCount;
                }

                double dwell = _settings.primaryDwellMode == DwellMode.FixationBased
                    ? aoi.fixationDwellMs
                    : aoi.rawSampleDwellMs;

                switch (aoi.aoiType)
                {
                    case AOIType.SenderHeader:
                        tSender += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.SenderDomain:
                        tDomain += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.HyperlinkTarget:
                        tHyperlink += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.SecurityBadge:
                        tSecurityBadge += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.SubjectLine:
                        tSubject += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.EmailBody:
                        tBody += dwell;
                        tTotalDwell += dwell;
                        break;
                    case AOIType.IrrelevantBackground:
                        // Excluded from T_total_dwell
                        break;
                }

                summary.aoiSummaries.Add(aoi);
            }

            summary.tSenderHeaderMs = tSender;
            summary.tSenderDomainMs = tDomain;
            summary.tHyperlinkTargetMs = tHyperlink;
            summary.tSecurityBadgeMs = tSecurityBadge;
            summary.tSubjectLineMs = tSubject;
            summary.tEmailBodyMs = tBody;
            summary.tTotalDwellMs = tTotalDwell;

            // Diagnostic dwell numerator
            double tDiagnostic = tSender + tDomain + tHyperlink;
            if (_settings.includeSecurityBadgeInDiagnostic)
            {
                tDiagnostic += tSecurityBadge;
            }
            summary.tDiagnosticDwellMs = tDiagnostic;

            // Threat Inspection Ratio (R_inspect)
            if (tTotalDwell <= 0.0001)
            {
                summary.rInspect = float.NaN;
                summary.rInspectFlag = "NA_NO_DWELL";
            }
            else if (summary.trackingValidityRatio < _settings.minTrackingValidityRatio)
            {
                summary.rInspect = (float)(tDiagnostic / tTotalDwell);
                summary.rInspectFlag = "LOW_VALIDITY";
            }
            else
            {
                summary.rInspect = (float)(tDiagnostic / tTotalDwell);
                summary.rInspectFlag = "OK";
            }

            // Secondary metric: R_inspect_over_T_total
            summary.rInspectOverTTotal = summary.totalObservationTimeMs > 0.0
                ? (float)(tDiagnostic / summary.totalObservationTimeMs)
                : 0.0f;

            // Scan sequence string
            summary.aoiScanSequence = string.Join(" -> ", _scanSequence);

            // Outcome & abort status
            summary.outcome = outcomeInfo.outcome;
            summary.outcomeAvailable = outcomeInfo.outcome != PhishingTrialOutcome.None;
            summary.responseTimeMs = outcomeInfo.responseTimeMs;
            summary.abortReason = abortReason;

            summary.fixations = new List<FixationRecord>(_fixations);

            return summary;
        }

        public void Reset()
        {
            _aoiMetrics.Clear();
            _fixations.Clear();
            _scanSequence.Clear();
            _totalSamples = 0;
            _validSamples = 0;
            _validTrackingTimeMs = 0.0;
            _trackingLossTimeMs = 0.0;
            _bridgedGapTimeMs = 0.0;
            _startTrialTimestampSeconds = -1.0;
            _lastSampleTimestampSeconds = -1.0;
            _lastFixatedAoiId = null;
            _firstFixationRankCounter = 0;
        }
    }
}
