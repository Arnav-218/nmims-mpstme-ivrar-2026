using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// A single target segment in a deterministic scripted gaze sequence.
    /// </summary>
    [Serializable]
    public struct ScriptedGazeStep
    {
        [Tooltip("AOI identifier to fixate on (e.g. 'sender_header', 'sender_domain', 'hyperlink_target', 'email_body').")]
        public string aoiId;

        [Tooltip("Direct world coordinate to gaze at if AOI lookup is not used.")]
        public Vector3 worldTarget;

        [Tooltip("Whether to use worldTarget directly rather than looking up aoiId in the registry.")]
        public bool useWorldTarget;

        [Tooltip("Duration of this fixation segment in seconds.")]
        public double durationSeconds;

        [Tooltip("Angular jitter/noise amplitude in degrees.")]
        public float noiseDegrees;

        [Tooltip("Simulate a blink or tracking dropout during this step.")]
        public bool simulateDropout;

        [Tooltip("Dropout start relative to this step start, in seconds.")]
        public double dropoutStartSeconds;

        [Tooltip("Dropout duration in seconds.")]
        public double dropoutDurationSeconds;

        public ScriptedGazeStep(
            string aoiId,
            double durationSeconds,
            float noiseDegrees = 0.1f,
            bool simulateDropout = false,
            double dropoutStartSeconds = 0.0,
            double dropoutDurationSeconds = 0.0)
        {
            this.aoiId = aoiId;
            this.worldTarget = Vector3.zero;
            this.useWorldTarget = false;
            this.durationSeconds = durationSeconds;
            this.noiseDegrees = noiseDegrees;
            this.simulateDropout = simulateDropout;
            this.dropoutStartSeconds = dropoutStartSeconds;
            this.dropoutDurationSeconds = dropoutDurationSeconds;
        }

        public ScriptedGazeStep(
            Vector3 worldTarget,
            double durationSeconds,
            float noiseDegrees = 0.1f)
        {
            this.aoiId = string.Empty;
            this.worldTarget = worldTarget;
            this.useWorldTarget = true;
            this.durationSeconds = durationSeconds;
            this.noiseDegrees = noiseDegrees;
            this.simulateDropout = false;
            this.dropoutStartSeconds = 0.0;
            this.dropoutDurationSeconds = 0.0;
        }
    }

    /// <summary>
    /// ScriptableObject asset holding a deterministic scripted gaze timeline.
    /// Can be edited in the Inspector or created dynamically at runtime for tests.
    /// </summary>
    [CreateAssetMenu(fileName = "ScriptedGazeSequence", menuName = "IVRAR/Gaze/Scripted Gaze Sequence")]
    public class ScriptedGazeSequence : ScriptableObject
    {
        [Tooltip("Deterministic random seed for noise generation.")]
        public int randomSeed = 42;

        [Tooltip("List of sequential gaze targets.")]
        public List<ScriptedGazeStep> steps = new List<ScriptedGazeStep>();

        [Tooltip("If true, loops back to the start when reaching the end of the sequence.")]
        public bool loop = false;

        public double TotalDuration
        {
            get
            {
                double sum = 0;
                if (steps != null)
                {
                    foreach (var s in steps) sum += s.durationSeconds;
                }
                return sum;
            }
        }
    }
}
