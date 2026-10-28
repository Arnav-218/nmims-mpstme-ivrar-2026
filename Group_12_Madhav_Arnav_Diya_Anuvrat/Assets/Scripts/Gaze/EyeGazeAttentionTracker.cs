using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Central manager for the Eye-Gaze and Attention Tracking Subsystem.
    ///
    /// Responsibilities:
    ///   - Selects and initialises the appropriate gaze acquisition provider based on TrackerPolicy.
    ///   - Runs Physics.RaycastNonAlloc (pre-allocated buffer) against the GazeAOI layer each frame.
    ///   - Feeds samples through I-DT fixation detection (FixationDetector).
    ///   - Accumulates per-trial gaze metrics (GazeMetricsAccumulator).
    ///   - Manages trial lifecycle (BeginTrial / EndTrial / AbortTrial).
    ///   - Exports RFC 4180 CSV and companion JSON via GazeCsvWriter.
    ///   - Renders a visible on-screen status panel and, in simulator mode, a standalone
    ///     trial-control panel so trials can be started/ended without Person 2's manager.
    ///
    /// Thread-safety: All public API is expected to be called from the main Unity thread.
    /// Application.persistentDataPath is never read in field initialisers or constructors.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class EyeGazeAttentionTracker : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────────

        [Header("Gaze Acquisition Policy")]
        [Tooltip("Operating policy dictating acceptable gaze acquisition providers.")]
        [SerializeField] private TrackerPolicy _policy = TrackerPolicy.PreferRealThenFallback;

        [Header("Hierarchy References")]
        [Tooltip("Transform representing the tracking-space origin (e.g. XR Origin / Camera Offset). " +
                 "If null, the reference camera's parent transform is used instead.")]
        [SerializeField] private Transform _trackingOrigin = null;

        [Tooltip("Main XR Camera rendering the user's viewpoint. Resolved from Camera.main if null.")]
        [SerializeField] private Camera _referenceCamera = null;

        [Header("Raycast & AOI Layer Configuration")]
        [Tooltip("LayerMask for the 'GazeAOI' layer. Auto-populated on Awake if zero.")]
        [SerializeField] private LayerMask _gazeAoiLayerMask;

        [Tooltip("Maximum raycast distance in metres.")]
        [SerializeField] private float _maxRaycastDistance = 15.0f;

        [Tooltip("Whether trigger colliders are tested during raycasting.")]
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Simulation Settings")]
        [Tooltip("Deterministic scripted gaze sequence asset for SimulatorOnly / scripted mode.")]
        [SerializeField] private ScriptedGazeSequence _scriptedSequence = null;

        [Tooltip("Which simulator sub-mode to use: mouse-interactive or deterministic scripted.")]
        [SerializeField] private SimulationSubMode _simulationSubMode = SimulationSubMode.InteractiveMouse;

        [Header("Algorithms & Thresholds")]
        [SerializeField] private FixationDetector.Settings       _fixationSettings = FixationDetector.Settings.Default;
        [SerializeField] private GazeMetricsAccumulator.Settings _metricsSettings  = GazeMetricsAccumulator.Settings.Default;

        [Header("Data Export")]
        [Tooltip("Automatically export CSV / JSON on EndTrial(). Disable to call ExportTrial() manually.")]
        [SerializeField] private bool _autoExportOnTrialEnd = true;

        [Tooltip("Override export root directory (null → Application.persistentDataPath).")]
        [SerializeField] private string _customExportPath = null;

        [Header("Standalone Simulator Panel")]
        [Tooltip("Show OnGUI trial-control panel when no scenario manager is connected. " +
                 "Trials started here use SYNTH- IDs and are written to EyeGazeData_Synthetic only.")]
        [SerializeField] private bool _showSimulatorPanel = true;

        // ─── Public Events ────────────────────────────────────────────────────────

        /// <summary>Raised each time the I-DT detector confirms a spatial fixation.</summary>
        public event Action<FixationRecord>   OnFixationCompleted;

        /// <summary>Raised when EndTrial or AbortTrial finalises and computes trial metrics.</summary>
        public event Action<GazeTrialSummary> OnTrialMetricsReady;

        /// <summary>Raised when the active gaze provider changes (e.g. device reconnect).</summary>
        public event Action<GazeProvenance, string> OnProviderChanged;

        /// <summary>Raised when tracking is lost (valid → invalid transition).</summary>
        public event Action OnTrackingLost;

        /// <summary>Raised when tracking is recovered (invalid → valid transition).</summary>
        public event Action OnTrackingRegained;

        // ─── Private State ────────────────────────────────────────────────────────

        private IGazeSampleProvider      _activeProvider;
        private FixationDetector         _fixationDetector;
        private GazeMetricsAccumulator   _metricsAccumulator;

        private bool             _isTrialActive;
        private TrialContext     _currentContext;
        private GazeTrialSummary _lastTrialSummary;
        private bool             _lastTrackingValidState;

        private GazeSample _latestSample;
        private AOIType    _latestHitAoiType = AOIType.IrrelevantBackground;
        private string     _latestHitAoiId   = string.Empty;

        // Pre-allocated raycast buffer — never reallocated per frame (Rule 7).
        private readonly RaycastHit[] _raycastHits = new RaycastHit[16];

        // Simulator panel state
        private string _panelParticipantId = "SYNTH-01";
        private string _panelSessionId     = "S01";
        private string _panelTrialId       = "T01";
        private string _panelScenarioId    = "SCENARIO_SIM";
        private bool   _sessionLogged;

        // ─── Public Properties ────────────────────────────────────────────────────

        /// <summary>True if a provider is initialised and currently reporting valid gaze.</summary>
        public bool IsTracking => _activeProvider != null && _activeProvider.IsAvailable;

        public bool                IsTrialActive    => _isTrialActive;
        public TrackerPolicy       Policy           => _policy;
        public GazeProvenance      CurrentProvenance => _activeProvider?.Provenance ?? GazeProvenance.HeadDirection_Fallback;
        public IGazeSampleProvider ActiveProvider   => _activeProvider;
        public GazeSample          LatestSample     => _latestSample;
        public AOIType             LatestHitAoiType => _latestHitAoiType;
        public string              LatestHitAoiId   => _latestHitAoiId;
        public GazeTrialSummary    LastTrialSummary => _lastTrialSummary;
        public GazeMetricsAccumulator MetricsAccumulator => _metricsAccumulator;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            ValidateAoiLayerConfiguration();

            if (_referenceCamera == null)
                _referenceCamera = Camera.main;

            _fixationDetector   = new FixationDetector(_fixationSettings);
            _metricsAccumulator = new GazeMetricsAccumulator(_metricsSettings);
        }

        private void Start()
        {
            InitializeGazeProvider();
        }

        private void Update()
        {
            if (_activeProvider == null) return;

            // 1. Acquire sample from active provider.
            bool acquired = _activeProvider.AcquireSample(out _latestSample);

            // 2. Dispatch tracking lost/regained events.
            bool currentlyValid = acquired && _latestSample.isValid;
            if (currentlyValid && !_lastTrackingValidState)
            {
                _lastTrackingValidState = true;
                OnTrackingRegained?.Invoke();
            }
            else if (!currentlyValid && _lastTrackingValidState)
            {
                _lastTrackingValidState = false;
                OnTrackingLost?.Invoke();
            }

            // 3. Raycast against GazeAOI layer.
            PerformAoiRaycast(_latestSample, out _latestHitAoiType, out _latestHitAoiId);

            // 4. Feed through fixation detector.
            if (_fixationDetector.ProcessSample(_latestSample, _latestHitAoiType, _latestHitAoiId, out var completedFixation))
            {
                OnFixationCompleted?.Invoke(completedFixation);
                if (_isTrialActive)
                    _metricsAccumulator.AccumulateFixation(completedFixation);
            }

            // 5. Accumulate raw metrics if trial is active.
            if (_isTrialActive)
                _metricsAccumulator.AccumulateSample(_latestSample, _latestHitAoiType, _latestHitAoiId);
        }

        private void OnDisable()
        {
            if (_isTrialActive)
                EndTrial(TrialOutcomeInfo.Empty);  // flush open trial as aborted
        }

        private void OnDestroy()
        {
            _activeProvider?.Shutdown();
            _activeProvider = null;
        }

        // ─── Layer Validation ─────────────────────────────────────────────────────

        private void ValidateAoiLayerConfiguration()
        {
            int layerId = LayerMask.NameToLayer("GazeAOI");
            if (layerId == -1)
            {
                Debug.LogError(
                    "[EyeGazeAttentionTracker] CRITICAL: Layer 'GazeAOI' is not defined in TagManager. " +
                    "Go to Edit → Project Settings → Tags and Layers and add 'GazeAOI' to any empty User Layer slot. " +
                    "The tracker will NOT silently fall back to the Default layer.");
                throw new InvalidOperationException(
                    "GazeAOI layer not found. Add it in Project Settings → Tags and Layers.");
            }

            // Auto-populate mask if the Inspector field was left at zero.
            if (_gazeAoiLayerMask.value == 0)
                _gazeAoiLayerMask = 1 << layerId;
        }

        public void IncludeAoiLayer(int layerId)
        {
            if (layerId < 0 || layerId > 31)
                throw new ArgumentOutOfRangeException(nameof(layerId), "A valid Unity layer index is required.");

            _gazeAoiLayerMask.value |= 1 << layerId;
        }

        public void ConfigureTrackingReferences(Transform trackingOrigin, Camera referenceCamera)
        {
            if (trackingOrigin == null)
                throw new ArgumentNullException(nameof(trackingOrigin));
            if (referenceCamera == null)
                throw new ArgumentNullException(nameof(referenceCamera));

            _trackingOrigin = trackingOrigin;
            _referenceCamera = referenceCamera;
        }

        // ─── Provider Management ──────────────────────────────────────────────────

        /// <summary>
        /// Sets a new acquisition policy and re-initialises the provider immediately.
        /// Safe to call from code at any point after Awake.
        /// </summary>
        public void SetPolicy(TrackerPolicy policy)
        {
            _policy = policy;
            InitializeGazeProvider();
        }

        /// <summary>
        /// Replaces the scripted gaze sequence used in Scripted sub-mode.
        /// </summary>
        public void SetScriptedSequence(ScriptedGazeSequence sequence)
        {
            _scriptedSequence = sequence;
            if (_activeProvider is SimulatedGazeProvider sim)
                sim.SetSequence(sequence, GazeAOIRegistry.GetTargetWorldPosition);
        }

        /// <summary>
        /// Selects, creates, and initialises the correct provider for the current policy.
        /// Logs the active provider at startup and warns whenever source is not genuine eye tracking.
        /// </summary>
        public void InitializeGazeProvider()
        {
            _activeProvider?.Shutdown();
            _activeProvider = null;

            switch (_policy)
            {
                case TrackerPolicy.RequireRealEyeTracking:
                {
                    var openXr = new OpenXREyeGazeProvider();
                    openXr.Initialize(_trackingOrigin, _referenceCamera);
                    if (openXr.IsAvailable)
                    {
                        _activeProvider = openXr;
                        Debug.Log("[EyeGazeAttentionTracker] Genuine OpenXR eye tracking initialised.");
                    }
                    else
                    {
                        openXr.Shutdown();
                        Debug.LogError(
                            "[EyeGazeAttentionTracker] Policy RequireRealEyeTracking: real eye-tracking hardware " +
                            "or runtime is unavailable. Fallback and simulation are blocked. Export is halted.");
                    }
                    break;
                }

                case TrackerPolicy.PreferRealThenFallback:
                {
                    var openXr = new OpenXREyeGazeProvider();
                    openXr.Initialize(_trackingOrigin, _referenceCamera);
                    if (openXr.IsAvailable)
                    {
                        _activeProvider = openXr;
                        Debug.Log("[EyeGazeAttentionTracker] PreferRealThenFallback: OpenXR eye tracking active.");
                    }
                    else
                    {
                        openXr.Shutdown();
                        var fallback = new HeadDirectionFallbackProvider();
                        fallback.Initialize(_trackingOrigin, _referenceCamera);
                        _activeProvider = fallback;
                        Debug.LogWarning(
                            "[EyeGazeAttentionTracker] Real eye tracking unavailable — using head-direction fallback. " +
                            "PROVENANCE: samples are HeadDirection_Fallback and are NOT genuine eye tracking.");
                    }
                    break;
                }

                case TrackerPolicy.SimulatorOnly:
                {
                    var sim = new SimulatedGazeProvider(
                        _simulationSubMode, _scriptedSequence, GazeAOIRegistry.GetTargetWorldPosition);
                    sim.Initialize(_trackingOrigin, _referenceCamera);
                    _activeProvider = sim;
                    Debug.Log(
                        $"[EyeGazeAttentionTracker] Simulator active (Mode={_simulationSubMode}, " +
                        $"Provenance={sim.Provenance}).");
                    break;
                }

                case TrackerPolicy.HeadDirectionOnly:
                {
                    var fallback = new HeadDirectionFallbackProvider();
                    fallback.Initialize(_trackingOrigin, _referenceCamera);
                    _activeProvider = fallback;
                    Debug.Log("[EyeGazeAttentionTracker] Head Direction Fallback provider active.");
                    break;
                }
            }

            if (_activeProvider != null)
                OnProviderChanged?.Invoke(_activeProvider.Provenance, _activeProvider.ProviderName);
        }

        // ─── AOI Raycast ──────────────────────────────────────────────────────────

        private void PerformAoiRaycast(GazeSample sample, out AOIType hitType, out string hitId)
        {
            hitType = AOIType.IrrelevantBackground;
            hitId   = "background";

            if (!sample.isValid) return;

            var ray = new Ray(sample.originWorld, sample.directionWorld);
            int hitCount = Physics.RaycastNonAlloc(
                ray, _raycastHits, _maxRaycastDistance,
                _gazeAoiLayerMask, _triggerInteraction);

            if (hitCount <= 0) return;

            // One AOI per sample: highest priority wins; tie-break on closest distance.
            GazeAOITarget bestTarget   = null;
            float         bestDistance = float.MaxValue;
            int           highestPri   = int.MinValue;

            for (int i = 0; i < hitCount; i++)
            {
                ref RaycastHit hit = ref _raycastHits[i];
                if (hit.collider == null) continue;

                if (GazeAOIRegistry.TryGetTarget(hit.collider, out var target))
                {
                    if (target.Priority > highestPri ||
                        (target.Priority == highestPri && hit.distance < bestDistance))
                    {
                        highestPri   = target.Priority;
                        bestTarget   = target;
                        bestDistance = hit.distance;
                    }
                }
            }

            if (bestTarget != null)
            {
                hitType = bestTarget.Type;
                hitId   = bestTarget.AoiId;
            }
        }

        // ─── Trial Lifecycle API ──────────────────────────────────────────────────

        /// <summary>
        /// Begins a new trial.  If a trial is already active the previous one is finalised first,
        /// matching the required behaviour for a double-BeginTrial call.
        /// </summary>
        public void BeginTrial(TrialContext context)
        {
            if (_isTrialActive)
            {
                Debug.LogWarning(
                    "[EyeGazeAttentionTracker] BeginTrial called while a trial is already active. " +
                    "Finalising previous trial first.");
                EndTrial(TrialOutcomeInfo.Empty);
            }

            _currentContext = context;
            _fixationDetector.Reset();

            GazeProvenance prov    = _activeProvider?.Provenance ?? GazeProvenance.HeadDirection_Fallback;
            string         srcName = _activeProvider?.ProviderName ?? "None";
            double         now     = Time.unscaledTimeAsDouble;

            _metricsAccumulator.InitializeTrial(_currentContext, prov, srcName, now);
            _isTrialActive = true;

            Debug.Log($"[EyeGazeAttentionTracker] Trial Begun: " +
                      $"Participant={context.participantPseudoId}, " +
                      $"Trial={context.trialId}, Scenario={context.scenarioId}, " +
                      $"Provenance={prov}");

            // Log output folder once per session.
            if (!_sessionLogged && _autoExportOnTrialEnd)
            {
                bool synth = context.IsSynthetic || prov == GazeProvenance.Simulated_Interactive
                             || prov == GazeProvenance.Simulated_Scripted || prov == GazeProvenance.SyntheticTest;
                string folder = GazeCsvWriter.GetSessionDirectory(context.sessionId, synth, _customExportPath);
                Debug.Log($"[EyeGazeAttentionTracker] Session output folder: {folder}");
                _sessionLogged = true;
            }
        }

        /// <summary>
        /// Ends the active trial, finalises open fixations, computes R_inspect, and exports CSV.
        /// Calling EndTrial when no trial is active logs a warning and returns.
        /// </summary>
        public void EndTrial(TrialOutcomeInfo outcomeInfo)
        {
            if (!_isTrialActive)
            {
                Debug.LogWarning("[EyeGazeAttentionTracker] EndTrial called but no trial is active.");
                return;
            }

            double now = Time.unscaledTimeAsDouble;

            // Finalise any fixation window still open at trial end.
            if (_fixationDetector.FinalizeOpenFixation(out var openFixation))
            {
                OnFixationCompleted?.Invoke(openFixation);
                _metricsAccumulator.AccumulateFixation(openFixation);
            }

            _lastTrialSummary = _metricsAccumulator.FinalizeTrial(now, outcomeInfo);
            _isTrialActive    = false;

            OnTrialMetricsReady?.Invoke(_lastTrialSummary);

            Debug.Log($"[EyeGazeAttentionTracker] Trial Ended: " +
                      $"Participant={_lastTrialSummary.context.participantPseudoId}, " +
                      $"R_inspect={FormatRInspect(_lastTrialSummary)} ({_lastTrialSummary.rInspectFlag}), " +
                      $"TotalDwell={_lastTrialSummary.tTotalDwellMs:F1} ms, " +
                      $"Fixations={_lastTrialSummary.totalFixationCount}");

            if (_autoExportOnTrialEnd)
                ExportTrial();
        }

        /// <summary>
        /// Aborts the active trial without requiring a valid outcome. Exported with an abort flag.
        /// Stopping Play mode calls this path via OnDisable so open trials are never silently lost.
        /// </summary>
        public void AbortTrial(string reason)
        {
            if (!_isTrialActive) return;

            double now = Time.unscaledTimeAsDouble;
            _lastTrialSummary = _metricsAccumulator.FinalizeTrial(
                now, TrialOutcomeInfo.Empty, reason ?? "Unspecified");
            _isTrialActive    = false;

            Debug.LogWarning($"[EyeGazeAttentionTracker] Trial Aborted: Reason='{reason}'");

            if (_autoExportOnTrialEnd)
                ExportTrial();
        }

        /// <summary>
        /// Returns the last completed or aborted trial summary.  Persists until ExportTrial or ResetTracker.
        /// </summary>
        public GazeTrialSummary GetTrialSummary() => _lastTrialSummary;

        /// <summary>
        /// Exports the last completed trial summary to CSV files and a companion JSON metadata file.
        /// Export is refused when policy is RequireRealEyeTracking but provenance is not genuine.
        /// </summary>
        public bool ExportTrial()
        {
            if (_lastTrialSummary == null)
            {
                Debug.LogWarning("[EyeGazeAttentionTracker] Cannot export: no completed trial summary available.");
                return false;
            }

            // Policy enforcement: block export if real tracking was required but not obtained.
            if (_policy == TrackerPolicy.RequireRealEyeTracking &&
                _lastTrialSummary.provenance != GazeProvenance.EyeTracking_OpenXR &&
                _lastTrialSummary.provenance != GazeProvenance.EyeTracking_VendorSDK)
            {
                Debug.LogError(
                    "[EyeGazeAttentionTracker] Export refused: policy is RequireRealEyeTracking " +
                    "but trial provenance is not genuine eye tracking.");
                return false;
            }

            bool s1 = GazeCsvWriter.WriteTrialSummary(_lastTrialSummary,  _customExportPath);
            bool s2 = GazeCsvWriter.WriteAoiMetrics  (_lastTrialSummary,  _customExportPath);
            bool s3 = GazeCsvWriter.WriteFixations   (_lastTrialSummary,  _customExportPath);
            bool s4 = GazeCsvWriter.WriteSessionMetadata(
                _lastTrialSummary.context,
                _lastTrialSummary.provenance,
                _lastTrialSummary.inputSourceName,
                _fixationSettings,
                _metricsSettings,
                _customExportPath);

            return s1 && s2 && s3 && s4;
        }

        /// <summary>
        /// Resets internal buffers (fixation window, accumulator).
        /// NEVER erases a completed, unexported trial summary from _lastTrialSummary.
        /// </summary>
        public void ResetTracker()
        {
            _fixationDetector?.Reset();
            _metricsAccumulator?.Reset();
            _isTrialActive = false;
            // _lastTrialSummary intentionally NOT cleared here.
        }

        // ─── OnGUI ────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            // ── Provenance warning banner ────────────────────────────────────────
            if (_activeProvider != null &&
                _activeProvider.Provenance != GazeProvenance.EyeTracking_OpenXR &&
                _activeProvider.Provenance != GazeProvenance.EyeTracking_VendorSDK)
            {
                Color prev = GUI.color;
                GUI.color = new Color(1f, 0.8f, 0.2f, 0.95f);
                GUI.Box(new Rect(10, 10, 380, 48),
                    $"GAZE SOURCE: [{_activeProvider.Provenance}]\n" +
                    "(NOT genuine eye tracking — samples are labelled accordingly)");
                GUI.color = prev;
            }

            // ── Standalone simulator trial-control panel ─────────────────────────
            if (!_showSimulatorPanel ||
                _policy != TrackerPolicy.SimulatorOnly) return;

            float panelX = 10f, panelY = 70f, w = 360f, lineH = 22f, pad = 6f;
            GUI.Box(new Rect(panelX, panelY, w, lineH * 10 + pad * 2), "Simulator Trial Control (Synthetic only)");

            float y = panelY + lineH + pad;

            GUI.Label(new Rect(panelX + pad, y, 120f, lineH), "Participant:");
            _panelParticipantId = GUI.TextField(new Rect(panelX + 130f, y, w - 136f, lineH), _panelParticipantId);
            y += lineH;

            GUI.Label(new Rect(panelX + pad, y, 120f, lineH), "Session ID:");
            _panelSessionId = GUI.TextField(new Rect(panelX + 130f, y, w - 136f, lineH), _panelSessionId);
            y += lineH;

            GUI.Label(new Rect(panelX + pad, y, 120f, lineH), "Trial ID:");
            _panelTrialId = GUI.TextField(new Rect(panelX + 130f, y, w - 136f, lineH), _panelTrialId);
            y += lineH;

            GUI.Label(new Rect(panelX + pad, y, 120f, lineH), "Scenario ID:");
            _panelScenarioId = GUI.TextField(new Rect(panelX + 130f, y, w - 136f, lineH), _panelScenarioId);
            y += lineH;

            // Enforce SYNTH- prefix for simulator panel IDs.
            if (!_panelParticipantId.StartsWith("SYNTH-", StringComparison.OrdinalIgnoreCase))
                _panelParticipantId = "SYNTH-" + _panelParticipantId;

            GUI.enabled = !_isTrialActive;
            if (GUI.Button(new Rect(panelX + pad, y, (w - pad * 3) / 2f, lineH * 1.4f), "▶  Begin Trial"))
            {
                var ctx = new TrialContext(_panelParticipantId, _panelSessionId, _panelTrialId, _panelScenarioId, "SimulatorOnly");
                BeginTrial(ctx);
            }

            GUI.enabled = _isTrialActive;
            if (GUI.Button(new Rect(panelX + pad + (w - pad * 3) / 2f + pad, y, (w - pad * 3) / 2f, lineH * 1.4f), "■  End Trial"))
            {
                EndTrial(new TrialOutcomeInfo(PhishingTrialOutcome.None));
            }
            GUI.enabled = true;
            y += lineH * 1.4f + pad;

            string status = _isTrialActive
                ? $"● RECORDING — {_latestHitAoiType} ({_latestHitAoiId})"
                : (_lastTrialSummary != null
                    ? $"Last: R_inspect={FormatRInspect(_lastTrialSummary)} [{_lastTrialSummary.rInspectFlag}]"
                    : "Ready");
            GUI.Label(new Rect(panelX + pad, y, w - pad, lineH * 2f), status);
        }

        private static string FormatRInspect(GazeTrialSummary s) =>
            float.IsNaN(s.rInspect) ? "NA" : s.rInspect.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
    }
}
