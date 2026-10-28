// OpenXREyeGazeProvider.cs
// Genuine OpenXR Eye-Gaze acquisition provider.
//
// Compile-safety guarantee:
//   The entire body that references UnityEngine.InputSystem.XR types is enclosed in
//   #if UNITY_INPUT_SYSTEM_ENABLE_XR so the file compiles cleanly whether or not the
//   XR module / OpenXR plugin is active.
//
//   UNITY_INPUT_SYSTEM_ENABLE_XR is set automatically by Unity when
//   com.unity.xr.management is installed and XR is enabled in Project Settings.
//   It is NOT a manual scripting define; never add it by hand.
//
// API verification:
//   PoseState  — UnityEngine.InputSystem.XR (PoseControl.cs line 23,
//                Library/PackageCache/com.unity.inputsystem@21a28c3a6c83)
//   PoseState.position (Vector3), PoseState.rotation (Quaternion),
//   PoseState.trackingState (TrackingState/int), PoseState.isTracked (bool)
//   — PoseControl.cs lines 79-125.
//   InputAction.ReadValue<PoseState>() — valid because PoseControl : InputControl<PoseState>.
//   InputSystem.onDeviceChange — verified in InputSystem.cs.
//   InputDevice.layout, .name — verified in InputDevice.cs.

using System;
using UnityEngine;
using UnityEngine.InputSystem;

// XR types are only available when UNITY_INPUT_SYSTEM_ENABLE_XR is defined.
// This symbol is injected by Unity when com.unity.xr.management is installed and enabled.
// Without it, the XR namespace does not compile, so every XR-dependent block is guarded.
#if UNITY_INPUT_SYSTEM_ENABLE_XR
using UnityEngine.InputSystem.XR;
#endif

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Genuine OpenXR Eye-Gaze acquisition provider.
    /// Binds to Input System layout controls exposed by the OpenXR Eye Gaze Interaction profile
    /// (&lt;EyeGaze&gt;/pose) and converts tracking-space coordinates to world space.
    ///
    /// When <c>UNITY_INPUT_SYSTEM_ENABLE_XR</c> is NOT defined (i.e. XR Management or OpenXR is
    /// not installed), <see cref="IsAvailable"/> returns <c>false</c> immediately and all
    /// samples are marked invalid — the provider never silently fabricates eye-tracking data.
    ///
    /// To enable real eye tracking:
    ///   1. Install: com.unity.xr.management (XR Plug-in Management), com.unity.xr.openxr.
    ///   2. Project Settings → XR Plug-in Management → enable OpenXR for the Standalone platform.
    ///   3. OpenXR → Interaction Profiles → add "Eye Gaze Interaction".
    ///   4. On the headset, grant eye-tracking permission.
    ///   5. Attach EyeGazeAttentionTracker to the XR Origin and assign the Camera and Tracking
    ///      Origin (CameraOffset) fields.
    /// </summary>
    public class OpenXREyeGazeProvider : IGazeSampleProvider
    {
        public string ProviderName => "OpenXR Eye Gaze Provider";
        public GazeProvenance Provenance => GazeProvenance.EyeTracking_OpenXR;

        private Transform _trackingOrigin;
        private Camera    _referenceCamera;

#if UNITY_INPUT_SYSTEM_ENABLE_XR
        private InputAction _gazePoseAction;
        private InputAction _trackingStateAction;
        private InputAction _eyesFixationAction;
        private bool        _isInitialized;
        private bool        _deviceDetected;
#endif

        /// <summary>
        /// True only when genuine OpenXR eye-tracking hardware and runtime are active.
        /// Returns false in any environment where eye-tracking is absent or XR support
        /// is not compiled in. Never returns true without real hardware.
        /// </summary>
        public bool IsAvailable =>
#if UNITY_INPUT_SYSTEM_ENABLE_XR
            _isInitialized && _deviceDetected;
#else
            false;
#endif

        public void Initialize(Transform trackingOrigin, Camera referenceCamera)
        {
            _trackingOrigin  = trackingOrigin;
            _referenceCamera = referenceCamera;

#if UNITY_INPUT_SYSTEM_ENABLE_XR
            // Bind to the standard OpenXR Eye Gaze Interaction profile controls.
            // Binding paths verified against OpenXR Input System support documentation.
            _gazePoseAction     = new InputAction("OpenXR_GazePose",
                                      binding: "<EyeGaze>/pose");
            _trackingStateAction = new InputAction("OpenXR_GazeTrackingState",
                                      binding: "<EyeGaze>/trackingState");
            _eyesFixationAction  = new InputAction("OpenXR_EyesFixation",
                                      binding: "<Eyes>/fixationPoint");

            try
            {
                _gazePoseAction.Enable();
                _trackingStateAction.Enable();
                _eyesFixationAction.Enable();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[OpenXREyeGazeProvider] Could not enable OpenXR input bindings: {ex.Message}. " +
                    "Ensure OpenXR is installed and the Eye Gaze Interaction profile is added.");
            }

            InputSystem.onDeviceChange += OnDeviceChange;
            _isInitialized = true;
            CheckDeviceAvailability();
#else
            // XR support not compiled. Log once and remain unavailable.
            Debug.LogWarning(
                "[OpenXREyeGazeProvider] UNITY_INPUT_SYSTEM_ENABLE_XR is not defined. " +
                "OpenXR eye-tracking is unavailable. See provider XML docs for installation steps.");
#endif
        }

#if UNITY_INPUT_SYSTEM_ENABLE_XR
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added      ||
                change == InputDeviceChange.Removed    ||
                change == InputDeviceChange.Reconnected||
                change == InputDeviceChange.Disconnected)
            {
                CheckDeviceAvailability();
            }
        }

        /// <summary>
        /// Scans connected devices for an eye-tracking device layout.
        /// Also reports available if the pose action already has a bound control (runtime already set up).
        /// </summary>
        public void CheckDeviceAvailability()
        {
            _deviceDetected = false;

            foreach (var device in InputSystem.devices)
            {
                if (device == null) continue;
                string layout = device.layout ?? string.Empty;
                string name   = device.name   ?? string.Empty;

                if (layout.IndexOf("EyeGaze", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    layout.IndexOf("Eyes",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name  .IndexOf("EyeGaze", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name  .IndexOf("Eyes",    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _deviceDetected = true;
                    Debug.Log($"[OpenXREyeGazeProvider] Eye-tracking device detected: " +
                              $"{device.displayName} (layout={layout})");
                    break;
                }
            }

            // Also treat as available if the pose action already has a bound control.
            if (!_deviceDetected &&
                _gazePoseAction != null &&
                _gazePoseAction.controls.Count > 0)
            {
                _deviceDetected = true;
            }
        }
#endif // UNITY_INPUT_SYSTEM_ENABLE_XR

        public bool AcquireSample(out GazeSample sample)
        {
            double now = Time.unscaledTimeAsDouble;

#if UNITY_INPUT_SYSTEM_ENABLE_XR
            if (!IsAvailable)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            // ── Tracking State ──────────────────────────────────────────────────────
            // PoseState.trackingState is a TrackingState (= UnityEngine.XR.InputTrackingState) flags enum.
            // Bit 0 (value 1) = Position tracked; Bit 1 (value 2) = Rotation tracked.
            // Eye-gaze requires rotation to be valid.
            int trackingStateBits = 0;
            if (_trackingStateAction != null && _trackingStateAction.controls.Count > 0)
            {
                try { trackingStateBits = _trackingStateAction.ReadValue<int>(); }
                catch { trackingStateBits = 0; }
            }
            else if (_gazePoseAction != null && _gazePoseAction.controls.Count > 0)
            {
                // trackingState not separately bound — read it from the PoseState struct.
                try
                {
                    var ps = _gazePoseAction.ReadValue<PoseState>();
                    trackingStateBits = (int)ps.trackingState;
                    if (ps.isTracked) trackingStateBits |= 3; // both bits set
                }
                catch { trackingStateBits = 0; }
            }

            // Rotation bit (bit 1, decimal 2) must be set for a valid gaze direction.
            bool rotationTracked = (trackingStateBits & 2) != 0;
            if (!rotationTracked)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            // ── Pose Reading ────────────────────────────────────────────────────────
            // PoseControl is InputControl<PoseState>; ReadValue<PoseState>() is the correct API.
            // Verified: PoseState has .position (Vector3) and .rotation (Quaternion).
            // Source: PoseControl.cs lines 79-125, com.unity.inputsystem@21a28c3a6c83.
            PoseState localPose;
            bool readSuccess = false;

            if (_gazePoseAction != null && _gazePoseAction.controls.Count > 0)
            {
                try
                {
                    localPose    = _gazePoseAction.ReadValue<PoseState>();
                    readSuccess  = true;
                }
                catch
                {
                    localPose    = default;
                    readSuccess  = false;
                }
            }
            else
            {
                localPose   = default;
                readSuccess = false;
            }

            // Reject default/unset/NaN poses (zero position AND identity rotation is the default
            // value of an unread PoseState — not a valid tracking pose).
            if (!readSuccess ||
                float.IsNaN(localPose.position.x)  ||
                float.IsNaN(localPose.rotation.x)  ||
                (localPose.position == Vector3.zero && localPose.rotation == Quaternion.identity))
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            // ── Tracking-Space → World-Space Conversion ─────────────────────────────
            // XR Origin or Camera Offset transform converts from tracking-space to world-space.
            // If neither is set, positions are already in world-space (fallback, not recommended).
            Transform originTrans = _trackingOrigin != null ? _trackingOrigin :
                (_referenceCamera != null && _referenceCamera.transform.parent != null
                    ? _referenceCamera.transform.parent
                    : _referenceCamera?.transform);

            Vector3 worldOrigin;
            Vector3 worldDirection;

            if (originTrans != null)
            {
                worldOrigin    = originTrans.TransformPoint    (localPose.position);
                worldDirection = originTrans.TransformDirection(localPose.rotation * Vector3.forward).normalized;
            }
            else
            {
                worldOrigin    = localPose.position;
                worldDirection = (localPose.rotation * Vector3.forward).normalized;
            }

            sample = new GazeSample(
                now,
                worldOrigin,
                worldDirection,
                true,
                1.0f,
                Provenance,
                ProviderName
            );
            return true;

#else
            // XR not compiled. Always invalid.
            sample = GazeSample.Invalid(now, Provenance, ProviderName);
            return false;
#endif
        }

        public void Shutdown()
        {
#if UNITY_INPUT_SYSTEM_ENABLE_XR
            if (_isInitialized)
            {
                InputSystem.onDeviceChange -= OnDeviceChange;
            }

            _gazePoseAction?.Disable();
            _gazePoseAction?.Dispose();
            _gazePoseAction = null;

            _trackingStateAction?.Disable();
            _trackingStateAction?.Dispose();
            _trackingStateAction = null;

            _eyesFixationAction?.Disable();
            _eyesFixationAction?.Dispose();
            _eyesFixationAction = null;

            _isInitialized  = false;
            _deviceDetected = false;
#endif
        }
    }
}
