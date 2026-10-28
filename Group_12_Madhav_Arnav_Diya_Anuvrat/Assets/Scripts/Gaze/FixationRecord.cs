using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Represents a completed spatial fixation detected by the I-DT algorithm.
    /// </summary>
    [Serializable]
    public struct FixationRecord
    {
        public int fixationId;
        public double startTimeSeconds;
        public double endTimeSeconds;
        public double durationMs;
        public Vector3 centroidOrigin;
        public Vector3 centroidDirection;
        public AOIType assignedAoiType;
        public string assignedAoiId;
        public int sampleCount;
        public double bridgedGapMs;
        public float maxDispersionDegrees;
        public bool isValid;

        public FixationRecord(
            int fixationId,
            double startTimeSeconds,
            double endTimeSeconds,
            Vector3 centroidOrigin,
            Vector3 centroidDirection,
            AOIType assignedAoiType,
            string assignedAoiId,
            int sampleCount,
            double bridgedGapMs,
            float maxDispersionDegrees,
            bool isValid = true)
        {
            this.fixationId = fixationId;
            this.startTimeSeconds = startTimeSeconds;
            this.endTimeSeconds = endTimeSeconds;
            this.durationMs = Math.Max(0.0, (endTimeSeconds - startTimeSeconds) * 1000.0 - bridgedGapMs);
            this.centroidOrigin = centroidOrigin;
            this.centroidDirection = centroidDirection.normalized;
            this.assignedAoiType = assignedAoiType;
            this.assignedAoiId = assignedAoiId ?? "unassigned";
            this.sampleCount = sampleCount;
            this.bridgedGapMs = bridgedGapMs;
            this.maxDispersionDegrees = maxDispersionDegrees;
            this.isValid = isValid;
        }

        public override string ToString()
        {
            return $"[Fixation #{fixationId} AOI={assignedAoiType}({assignedAoiId}) dur={durationMs:F1}ms t={startTimeSeconds:F3}-{endTimeSeconds:F3}s disp={maxDispersionDegrees:F2}°]";
        }
    }
}
