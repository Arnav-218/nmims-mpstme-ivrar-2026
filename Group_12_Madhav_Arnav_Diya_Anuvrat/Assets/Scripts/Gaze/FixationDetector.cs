using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Pure C# implementation of the Identification by Dispersion-Threshold (I-DT)
    /// fixation detection algorithm in 3D angular visual space.
    /// Operates completely independently of Unity scene hierarchy for headless testability.
    /// </summary>
    public class FixationDetector
    {
        [Serializable]
        public struct Settings
        {
            [Tooltip("Minimum temporal window duration to qualify as a fixation in milliseconds.")]
            public double minFixationDurationMs;

            [Tooltip("Maximum spatial dispersion threshold in degrees of visual angle.")]
            public float maxDispersionDegrees;

            [Tooltip("Saccade angular velocity gate threshold in degrees per second.")]
            public float saccadeVelocityThresholdDegPerSec;

            [Tooltip("Maximum tracking gap duration to bridge in milliseconds. Bridged gaps are NOT counted as gaze time.")]
            public double maxGapToleranceMs;

            [Tooltip("Maximum interval between samples before declaring a gap in milliseconds.")]
            public double maxSampleIntervalMs;

            [Tooltip("Minimum ratio of valid samples required within the fixation window (0.0 - 1.0).")]
            public float minValidSampleRatioInFixation;

            [Tooltip("Resolution strategy when a fixation straddles multiple AOI boundaries.")]
            public StraddleResolution straddleResolution;

            public static Settings Default => new Settings
            {
                minFixationDurationMs = 150.0,
                maxDispersionDegrees = 1.0f,
                saccadeVelocityThresholdDegPerSec = 300.0f,
                maxGapToleranceMs = 75.0,
                maxSampleIntervalMs = 100.0,
                minValidSampleRatioInFixation = 0.80f,
                straddleResolution = StraddleResolution.MajorityVote
            };
        }

        private struct WindowSample
        {
            public double timestamp;
            public Vector3 origin;
            public Vector3 direction;
            public AOIType aoiType;
            public string aoiId;
            public bool isValid;

            public WindowSample(double timestamp, Vector3 origin, Vector3 direction, AOIType aoiType, string aoiId, bool isValid)
            {
                this.timestamp = timestamp;
                this.origin = origin;
                this.direction = direction;
                this.aoiType = aoiType;
                this.aoiId = aoiId;
                this.isValid = isValid;
            }
        }

        /// <summary>
        /// Fixation duration epsilon in milliseconds.
        /// A window whose net gaze duration d satisfies d >= (minFixationDurationMs - DURATION_EPSILON_MS)
        /// is accepted as a confirmed fixation. This ensures that exactly 150.000 ms passes while
        /// 149.999 ms and below is rejected, absorbing sub-millisecond floating-point rounding.
        /// </summary>
        private const double DURATION_EPSILON_MS = 0.5;

        private readonly Settings _settings;
        private readonly List<WindowSample> _window = new List<WindowSample>(128);

        private int _fixationCounter = 0;
        private double _lastSampleTimestamp = -1.0;
        private Vector3 _lastSampleDirection = Vector3.forward;
        private bool _hasLastSample = false;
        private double _activeGapDurationMs = 0.0;
        private double _totalBridgedGapMsInWindow = 0.0;

        public Settings CurrentSettings => _settings;
        public int FixationCount => _fixationCounter;
        public bool HasOpenWindow => _window.Count > 0;

        public FixationDetector(Settings settings)
        {
            _settings = settings;
        }

        public FixationDetector() : this(Settings.Default)
        {
        }

        /// <summary>
        /// Ingests a new gaze sample and its associated AOI hit.
        /// Returns true if a confirmed fixation was finalized and emitted.
        /// </summary>
        public bool ProcessSample(
            GazeSample sample,
            AOIType hitAoiType,
            string hitAoiId,
            out FixationRecord completedFixation)
        {
            completedFixation = default;

            // Pre-filtering: reject timestamp regression
            if (_hasLastSample && sample.timestampSeconds <= _lastSampleTimestamp)
            {
                return false;
            }

            double dt = _hasLastSample ? (sample.timestampSeconds - _lastSampleTimestamp) : 0.0;
            double dtMs = dt * 1000.0;

            // Pre-filtering: validate direction and validity
            bool sampleIsValid = sample.isValid &&
                                 !float.IsNaN(sample.directionWorld.x) &&
                                 !float.IsInfinity(sample.directionWorld.x) &&
                                 sample.directionWorld.sqrMagnitude > 1e-6f;

            Vector3 normalizedDir = sampleIsValid ? sample.directionWorld.normalized : Vector3.forward;

            // Handle invalid samples or long tracking gaps
            if (!sampleIsValid)
            {
                if (_hasLastSample)
                {
                    _activeGapDurationMs += dtMs;
                }

                _lastSampleTimestamp = sample.timestampSeconds;

                // If gap exceeds bridge tolerance, evaluate and close open window
                if (_activeGapDurationMs > _settings.maxGapToleranceMs)
                {
                    bool finalized = TryFinalizeWindow(out completedFixation);
                    _window.Clear();
                    _totalBridgedGapMsInWindow = 0.0;
                    _activeGapDurationMs = 0.0;
                    _hasLastSample = false;
                    return finalized;
                }
                return false;
            }

            // Sample is valid. Check if recovering from a gap.
            if (_activeGapDurationMs > 0.0)
            {
                if (_activeGapDurationMs <= _settings.maxGapToleranceMs)
                {
                    // Bridge the minor gap: record bridged gap time
                    _totalBridgedGapMsInWindow += _activeGapDurationMs;
                    _activeGapDurationMs = 0.0;
                }
                else
                {
                    // Gap was too long: finalize previous window
                    bool finalized = TryFinalizeWindow(out completedFixation);
                    _window.Clear();
                    _totalBridgedGapMsInWindow = 0.0;
                    _activeGapDurationMs = 0.0;
                    _hasLastSample = false;
                    // Start new window below
                    if (finalized)
                    {
                        StartNewWindow(sample.timestampSeconds, sample.originWorld, normalizedDir, hitAoiType, hitAoiId);
                        _lastSampleTimestamp = sample.timestampSeconds;
                        _lastSampleDirection = normalizedDir;
                        _hasLastSample = true;
                        return true;
                    }
                }
            }

            // Saccade velocity check
            if (_hasLastSample && dt > 0.0001)
            {
                float angularSpeedDegPerSec = Vector3.Angle(_lastSampleDirection, normalizedDir) / (float)dt;
                if (angularSpeedDegPerSec > _settings.saccadeVelocityThresholdDegPerSec)
                {
                    // Saccade movement detected: finalize current fixation before saccade
                    bool finalized = TryFinalizeWindow(out completedFixation);
                    _window.Clear();
                    _totalBridgedGapMsInWindow = 0.0;
                    _activeGapDurationMs = 0.0;
                    _lastSampleTimestamp = sample.timestampSeconds;
                    _lastSampleDirection = normalizedDir;
                    _hasLastSample = true;
                    return finalized;
                }
            }

            _lastSampleTimestamp = sample.timestampSeconds;
            _lastSampleDirection = normalizedDir;
            _hasLastSample = true;

            // If window is empty, start fresh window
            if (_window.Count == 0)
            {
                StartNewWindow(sample.timestampSeconds, sample.originWorld, normalizedDir, hitAoiType, hitAoiId);
                return false;
            }

            // Check if adding this sample maintains dispersion threshold
            float testDispersion = CalculatePotentialDispersion(normalizedDir);
            if (testDispersion <= _settings.maxDispersionDegrees)
            {
                // Accept sample into current expanding fixation window
                _window.Add(new WindowSample(sample.timestampSeconds, sample.originWorld, normalizedDir, hitAoiType, hitAoiId, true));
                return false;
            }
            else
            {
                // Sample exceeds dispersion threshold!
                // Check if current window meets minimum fixation duration
                bool finalized = TryFinalizeWindow(out completedFixation);

                // Start new window beginning with this sample
                _window.Clear();
                _totalBridgedGapMsInWindow = 0.0;
                _activeGapDurationMs = 0.0;
                StartNewWindow(sample.timestampSeconds, sample.originWorld, normalizedDir, hitAoiType, hitAoiId);

                return finalized;
            }
        }

        private void StartNewWindow(double timestamp, Vector3 origin, Vector3 direction, AOIType aoiType, string aoiId)
        {
            _window.Add(new WindowSample(timestamp, origin, direction, aoiType, aoiId, true));
        }

        private float CalculatePotentialDispersion(Vector3 candidateDir)
        {
            float maxAngle = 0f;
            for (int i = 0; i < _window.Count; i++)
            {
                float angle = Vector3.Angle(_window[i].direction, candidateDir);
                if (angle > maxAngle) maxAngle = angle;

                // Also check pairwise within existing window
                for (int j = i + 1; j < _window.Count; j++)
                {
                    float pairAngle = Vector3.Angle(_window[i].direction, _window[j].direction);
                    if (pairAngle > maxAngle) maxAngle = pairAngle;
                }
            }
            return maxAngle;
        }

        private float CalculateCurrentWindowDispersion()
        {
            if (_window.Count < 2) return 0f;
            float maxAngle = 0f;
            for (int i = 0; i < _window.Count; i++)
            {
                for (int j = i + 1; j < _window.Count; j++)
                {
                    float angle = Vector3.Angle(_window[i].direction, _window[j].direction);
                    if (angle > maxAngle) maxAngle = angle;
                }
            }
            return maxAngle;
        }

        private bool TryFinalizeWindow(out FixationRecord fixation)
        {
            fixation = default;
            if (_window.Count < 2) return false;

            double startTime = _window[0].timestamp;
            double endTime = _window[_window.Count - 1].timestamp;
            double rawDurationMs = (endTime - startTime) * 1000.0;
            double netGazeDurationMs = rawDurationMs - _totalBridgedGapMsInWindow;

            // Duration must be >= threshold minus epsilon so that exactly threshold ms is accepted.
            if (netGazeDurationMs < _settings.minFixationDurationMs - DURATION_EPSILON_MS)
            {
                return false;
            }

            // Check validity ratio: valid sample count vs. expected count given max interval.
            double intervalMs = _settings.maxSampleIntervalMs > 0.0 ? _settings.maxSampleIntervalMs : 16.67;
            double expectedSamples = rawDurationMs / intervalMs;
            if (expectedSamples > 1.0)
            {
                float validRatio = (float)(_window.Count / expectedSamples);
                if (validRatio < _settings.minValidSampleRatioInFixation)
                {
                    // Insufficient sample density within window — reject.
                    return false;
                }
            }

            // Calculate centroid origin and direction
            Vector3 sumOrigin = Vector3.zero;
            Vector3 sumDir = Vector3.zero;
            for (int i = 0; i < _window.Count; i++)
            {
                sumOrigin += _window[i].origin;
                sumDir += _window[i].direction;
            }
            Vector3 centroidOrigin = sumOrigin / _window.Count;
            Vector3 centroidDirection = sumDir.normalized;

            // Resolve AOI straddling
            AOIType assignedAoiType;
            string assignedAoiId;
            ResolveAoiAssignment(out assignedAoiType, out assignedAoiId);

            _fixationCounter++;
            fixation = new FixationRecord(
                _fixationCounter,
                startTime,
                endTime,
                centroidOrigin,
                centroidDirection,
                assignedAoiType,
                assignedAoiId,
                _window.Count,
                _totalBridgedGapMsInWindow,
                CalculateCurrentWindowDispersion(),
                true
            );

            return true;
        }

        private void ResolveAoiAssignment(out AOIType resolvedType, out string resolvedId)
        {
            if (_settings.straddleResolution == StraddleResolution.MajorityVote || _window.Count <= 2)
            {
                // Count sample occurrences per AOI
                var counts = new Dictionary<AOIType, int>();
                var idMap = new Dictionary<AOIType, string>();

                for (int i = 0; i < _window.Count; i++)
                {
                    var type = _window[i].aoiType;
                    counts[type] = counts.ContainsKey(type) ? counts[type] + 1 : 1;
                    if (!idMap.ContainsKey(type)) idMap[type] = _window[i].aoiId;
                }

                AOIType bestType = _window[0].aoiType;
                int maxCount = 0;
                foreach (var kvp in counts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        bestType = kvp.Key;
                    }
                }

                resolvedType = bestType;
                resolvedId = idMap.TryGetValue(bestType, out var id) ? id : "aoi";
            }
            else
            {
                // Centroid mode: pick the sample closest to the centroid direction
                Vector3 sumDir = Vector3.zero;
                for (int i = 0; i < _window.Count; i++) sumDir += _window[i].direction;
                Vector3 centroid = sumDir.normalized;

                int bestIdx = 0;
                float minAngle = float.MaxValue;
                for (int i = 0; i < _window.Count; i++)
                {
                    float ang = Vector3.Angle(_window[i].direction, centroid);
                    if (ang < minAngle)
                    {
                        minAngle = ang;
                        bestIdx = i;
                    }
                }

                resolvedType = _window[bestIdx].aoiType;
                resolvedId = _window[bestIdx].aoiId;
            }
        }

        /// <summary>
        /// Finalizes an open fixation window at the end of a trial if it meets the minimum duration threshold.
        /// </summary>
        public bool FinalizeOpenFixation(out FixationRecord completedFixation)
        {
            bool finalized = TryFinalizeWindow(out completedFixation);
            _window.Clear();
            _totalBridgedGapMsInWindow = 0.0;
            _activeGapDurationMs = 0.0;
            return finalized;
        }

        public void Reset()
        {
            _window.Clear();
            _fixationCounter = 0;
            _lastSampleTimestamp = -1.0;
            _lastSampleDirection = Vector3.forward;
            _hasLastSample = false;
            _activeGapDurationMs = 0.0;
            _totalBridgedGapMsInWindow = 0.0;
        }
    }
}
