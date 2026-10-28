using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IVRAR.CybersecurityVR
{
    public enum SimulationSubMode
    {
        InteractiveMouse,
        DeterministicScripted
    }

    /// <summary>
    /// Self-contained gaze simulation provider requiring NO external packages or Meta XR Simulator.
    /// Supports:
    /// (a) Interactive mode: Gaze ray driven by Game-view mouse position via ScreenPointToRay,
    ///     with keyboard shortcuts for blink (Space/B) and tracking loss (L).
    /// (b) Scripted mode: Deterministic replay of a ScriptedGazeSequence timeline with pseudo-random noise
    ///     driven by a fixed random seed for automated and repeatable research testing.
    /// </summary>
    public class SimulatedGazeProvider : IGazeSampleProvider
    {
        public string ProviderName => _subMode == SimulationSubMode.InteractiveMouse
            ? "Simulated Interactive Provider (Mouse)"
            : "Simulated Scripted Provider (Deterministic)";

        public GazeProvenance Provenance => _subMode == SimulationSubMode.InteractiveMouse
            ? GazeProvenance.Simulated_Interactive
            : GazeProvenance.Simulated_Scripted;

        public bool IsAvailable => _isInitialized;

        private SimulationSubMode _subMode;
        private Camera _referenceCamera;
        private Transform _trackingOrigin;
        private ScriptedGazeSequence _sequence;
        private bool _isInitialized;

        // Interactive simulation state
        private bool _isBlinking;
        private bool _isTrackingLossToggled;

        // Scripted simulation state
        private double _scriptedStartTime;
        private System.Random _pseudoRandom;
        private Func<string, Vector3?> _aoiPositionResolver;

        // Manual timestamp injection for unit testing
        private double _injectedTimestamp = -1;

        public SimulatedGazeProvider(
            SimulationSubMode subMode = SimulationSubMode.InteractiveMouse,
            ScriptedGazeSequence sequence = null,
            Func<string, Vector3?> aoiPositionResolver = null)
        {
            _subMode = subMode;
            _sequence = sequence;
            _aoiPositionResolver = aoiPositionResolver;
        }

        public void SetMode(SimulationSubMode subMode)
        {
            _subMode = subMode;
        }

        public void SetSequence(ScriptedGazeSequence sequence, Func<string, Vector3?> aoiPositionResolver = null)
        {
            _sequence = sequence;
            if (aoiPositionResolver != null) _aoiPositionResolver = aoiPositionResolver;
            ResetScriptedPlayback();
        }

        public void ResetScriptedPlayback(double startTime = 0.0)
        {
            _scriptedStartTime = startTime > 0 ? startTime : Time.unscaledTimeAsDouble;
            int seed = _sequence != null ? _sequence.randomSeed : 42;
            _pseudoRandom = new System.Random(seed);
        }

        public void SetInjectedTimestamp(double timestamp)
        {
            _injectedTimestamp = timestamp;
        }

        public void Initialize(Transform trackingOrigin, Camera referenceCamera)
        {
            _trackingOrigin = trackingOrigin;
            _referenceCamera = referenceCamera;
            ResetScriptedPlayback();
            _isInitialized = true;
        }

        public bool AcquireSample(out GazeSample sample)
        {
            double now = _injectedTimestamp >= 0 ? _injectedTimestamp : Time.unscaledTimeAsDouble;

            if (!IsAvailable)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            if (_subMode == SimulationSubMode.InteractiveMouse)
            {
                return AcquireInteractiveSample(now, out sample);
            }
            else
            {
                return AcquireScriptedSample(now, out sample);
            }
        }

        private bool AcquireInteractiveSample(double now, out GazeSample sample)
        {
            // Poll keyboard shortcuts to simulate human eye events
            UpdateInteractiveKeyStates();

            if (_isBlinking || _isTrackingLossToggled)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            Camera cam = _referenceCamera != null ? _referenceCamera : Camera.main;
            if (cam == null)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            Vector2 mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            mousePos = Input.mousePosition;
#endif

            Ray ray = cam.ScreenPointToRay(mousePos);
            sample = new GazeSample(
                now,
                ray.origin,
                ray.direction.normalized,
                true,
                1.0f,
                Provenance,
                ProviderName
            );
            return true;
        }

        private void UpdateInteractiveKeyStates()
        {
            bool blinkKeyPressed = false;
            bool lossTogglePressed = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                blinkKeyPressed = Keyboard.current.spaceKey.isPressed || Keyboard.current.bKey.isPressed;
                if (Keyboard.current.lKey.wasPressedThisFrame)
                {
                    lossTogglePressed = true;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            blinkKeyPressed = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.B);
            if (Input.GetKeyDown(KeyCode.L))
            {
                lossTogglePressed = true;
            }
#endif

            _isBlinking = blinkKeyPressed;
            if (lossTogglePressed)
            {
                _isTrackingLossToggled = !_isTrackingLossToggled;
                Debug.Log($"[SimulatedGazeProvider] Simulated tracking loss toggled: {_isTrackingLossToggled}");
            }
        }

        private bool AcquireScriptedSample(double now, out GazeSample sample)
        {
            if (_sequence == null || _sequence.steps == null || _sequence.steps.Count == 0)
            {
                // Fallback forward gaze if no steps configured
                Vector3 origin = _referenceCamera != null ? _referenceCamera.transform.position : Vector3.zero;
                Vector3 forward = _referenceCamera != null ? _referenceCamera.transform.forward : Vector3.forward;
                sample = new GazeSample(now, origin, forward, true, 1.0f, Provenance, ProviderName);
                return true;
            }

            double elapsedTotal = now - _scriptedStartTime;
            if (elapsedTotal < 0) elapsedTotal = 0;

            // Find current step based on cumulative duration
            double accumulated = 0;
            int stepIdx = -1;
            double stepStart = 0;

            for (int i = 0; i < _sequence.steps.Count; i++)
            {
                double stepDur = _sequence.steps[i].durationSeconds;
                if (elapsedTotal >= accumulated && elapsedTotal < accumulated + stepDur)
                {
                    stepIdx = i;
                    stepStart = accumulated;
                    break;
                }
                accumulated += stepDur;
            }

            if (stepIdx == -1)
            {
                if (_sequence.loop && accumulated > 0)
                {
                    double loopedElapsed = elapsedTotal % accumulated;
                    accumulated = 0;
                    for (int i = 0; i < _sequence.steps.Count; i++)
                    {
                        double stepDur = _sequence.steps[i].durationSeconds;
                        if (loopedElapsed >= accumulated && loopedElapsed < accumulated + stepDur)
                        {
                            stepIdx = i;
                            stepStart = accumulated;
                            break;
                        }
                        accumulated += stepDur;
                    }
                }
                else
                {
                    // Sequence ended: stay on last step target
                    stepIdx = _sequence.steps.Count - 1;
                    stepStart = accumulated - _sequence.steps[stepIdx].durationSeconds;
                }
            }

            var step = _sequence.steps[stepIdx];
            double stepLocalTime = elapsedTotal - stepStart;

            // Check if step simulates a dropout / blink window
            if (step.simulateDropout &&
                stepLocalTime >= step.dropoutStartSeconds &&
                stepLocalTime < (step.dropoutStartSeconds + step.dropoutDurationSeconds))
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            // Determine world target position
            Vector3 targetPosition = Vector3.forward * 2.0f;
            if (step.useWorldTarget)
            {
                targetPosition = step.worldTarget;
            }
            else if (!string.IsNullOrEmpty(step.aoiId) && _aoiPositionResolver != null)
            {
                Vector3? resolved = _aoiPositionResolver.Invoke(step.aoiId);
                if (resolved.HasValue)
                {
                    targetPosition = resolved.Value;
                }
            }

            Vector3 originPos = _referenceCamera != null ? _referenceCamera.transform.position : Vector3.zero;
            Vector3 baseDir = (targetPosition - originPos).normalized;
            if (baseDir == Vector3.zero) baseDir = Vector3.forward;

            // Apply deterministic angular jitter if noiseDegrees > 0
            Vector3 finalDir = baseDir;
            if (step.noiseDegrees > 0 && _pseudoRandom != null)
            {
                double u1 = 1.0 - _pseudoRandom.NextDouble();
                double u2 = 1.0 - _pseudoRandom.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                float jitterAngle = (float)(randStdNormal * step.noiseDegrees);

                float pitch = (float)((_pseudoRandom.NextDouble() * 2.0 - 1.0) * jitterAngle);
                float yaw = (float)((_pseudoRandom.NextDouble() * 2.0 - 1.0) * jitterAngle);

                Quaternion jitterRot = Quaternion.Euler(pitch, yaw, 0);
                finalDir = (jitterRot * baseDir).normalized;
            }

            sample = new GazeSample(
                now,
                originPos,
                finalDir,
                true,
                1.0f,
                Provenance,
                ProviderName
            );
            return true;
        }

        public void Shutdown()
        {
            _referenceCamera = null;
            _trackingOrigin = null;
            _sequence = null;
            _aoiPositionResolver = null;
            _isInitialized = false;
        }
    }
}
