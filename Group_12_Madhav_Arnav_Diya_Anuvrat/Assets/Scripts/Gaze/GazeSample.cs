using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Explicit provenance origin for all gaze data samples and trial exports.
    /// In accordance with research integrity standards, simulated or head-direction samples
    /// must never masquerade as genuine eye tracking.
    /// </summary>
    public enum GazeProvenance
    {
        EyeTracking_OpenXR,
        EyeTracking_VendorSDK,
        HeadDirection_Fallback,
        Simulated_Interactive,
        Simulated_Scripted,
        SyntheticTest
    }

    /// <summary>
    /// Operating policy controlling acceptable gaze acquisition sources.
    /// </summary>
    public enum TrackerPolicy
    {
        /// <summary>
        /// Strictly requires genuine eye-tracking hardware/runtime.
        /// If absent, logs an error and halts data export without falling back.
        /// </summary>
        RequireRealEyeTracking,

        /// <summary>
        /// Attempts real eye tracking first; falls back to head direction or simulator if absent.
        /// Provenance is explicitly logged and warned on screen.
        /// </summary>
        PreferRealThenFallback,

        /// <summary>
        /// Runs in simulation mode (Game view mouse ray or deterministic scripted timeline).
        /// </summary>
        SimulatorOnly,

        /// <summary>
        /// Uses camera forward direction only (explicitly labelled HeadDirection_Fallback).
        /// </summary>
        HeadDirectionOnly
    }

    /// <summary>
    /// Method used to credit attention dwell time to AOIs.
    /// </summary>
    public enum DwellMode
    {
        /// <summary>
        /// Primary standard: dwell is credited only when a confirmed spatial fixation rests on the AOI.
        /// </summary>
        FixationBased,

        /// <summary>
        /// Secondary reference: raw gaze sample hit durations accumulated on the AOI.
        /// </summary>
        RawSample
    }

    /// <summary>
    /// Rule used to assign a fixation that straddles multiple AOI boundaries.
    /// </summary>
    public enum StraddleResolution
    {
        /// <summary>
        /// Assign to the AOI that received the majority of sample timestamps during the fixation window.
        /// </summary>
        MajorityVote,

        /// <summary>
        /// Raycast the angular centroid direction of the fixation window.
        /// </summary>
        Centroid
    }

    /// <summary>
    /// Immutable record representing a single gaze measurement sample.
    /// </summary>
    [Serializable]
    public struct GazeSample
    {
        public double timestampSeconds;
        public Vector3 originWorld;
        public Vector3 directionWorld;
        public bool isValid;
        public float confidence;
        public GazeProvenance provenance;
        public string sourceName;

        public GazeSample(
            double timestampSeconds,
            Vector3 originWorld,
            Vector3 directionWorld,
            bool isValid,
            float confidence,
            GazeProvenance provenance,
            string sourceName)
        {
            this.timestampSeconds = timestampSeconds;
            this.originWorld = originWorld;
            this.directionWorld = directionWorld;
            this.isValid = isValid;
            this.confidence = confidence;
            this.provenance = provenance;
            this.sourceName = sourceName ?? "Unknown";
        }

        public static GazeSample Invalid(double timestampSeconds, GazeProvenance provenance, string sourceName)
        {
            return new GazeSample(
                timestampSeconds,
                Vector3.zero,
                Vector3.forward,
                false,
                float.NaN,
                provenance,
                sourceName
            );
        }

        public override string ToString()
        {
            return $"[GazeSample t={timestampSeconds:F4}s valid={isValid} conf={confidence} prov={provenance} src={sourceName}]";
        }
    }

    /// <summary>
    /// Contract for gaze acquisition providers.
    /// Providers solely handle acquisition and world-space conversion;
    /// filtering, raycasting, fixation detection and metrics are decoupled.
    /// </summary>
    public interface IGazeSampleProvider
    {
        string ProviderName { get; }
        GazeProvenance Provenance { get; }
        bool IsAvailable { get; }

        void Initialize(Transform trackingOrigin, Camera referenceCamera);
        bool AcquireSample(out GazeSample sample);
        void Shutdown();
    }
}
