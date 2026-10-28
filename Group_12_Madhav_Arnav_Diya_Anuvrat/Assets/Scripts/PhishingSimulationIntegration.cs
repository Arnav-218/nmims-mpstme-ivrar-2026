using System.Collections;
using System.Collections.Generic;
using System;
using IVRAR.CybersecurityVR;
using UnityEngine;

public sealed class PhishingSimulationIntegration : MonoBehaviour
{
    [SerializeField] private PhishingThreatSimulationManager scenarioManager;
    [SerializeField] private PhishingSimulationController presentationController;
    [SerializeField] private EyeGazeScenarioBridge gazeBridge;

    private readonly HashSet<string> recordedTrialIds = new HashSet<string>();
    private bool gazeTrialActive;
    private bool beginRequested;
    private bool sessionInitialized;
    private string currentTrialId;
    private string sessionId;

    public PhishingThreatSimulationManager ScenarioManager => scenarioManager;
    public EyeGazeScenarioBridge GazeBridge => gazeBridge;

    public void Configure(
        PhishingThreatSimulationManager manager,
        PhishingSimulationController controller,
        EyeGazeScenarioBridge bridge)
    {
        scenarioManager = manager;
        presentationController = controller;
        gazeBridge = bridge;
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (scenarioManager == null)
            return;

        scenarioManager.OnScenarioPresented -= HandleScenarioPresented;
        scenarioManager.OnDecisionAccepted -= HandleDecisionAccepted;
        scenarioManager.OnSimulationRestarting -= HandleSimulationRestarting;
        scenarioManager.OnScenarioPresented += HandleScenarioPresented;
        scenarioManager.OnDecisionAccepted += HandleDecisionAccepted;
        scenarioManager.OnSimulationRestarting += HandleSimulationRestarting;
    }

    private void OnDisable()
    {
        if (scenarioManager == null)
            return;

        scenarioManager.OnScenarioPresented -= HandleScenarioPresented;
        scenarioManager.OnDecisionAccepted -= HandleDecisionAccepted;
        scenarioManager.OnSimulationRestarting -= HandleSimulationRestarting;
    }

    private void ResolveReferences()
    {
        if (scenarioManager == null)
            scenarioManager = FindAnyObjectByType<PhishingThreatSimulationManager>();
        if (presentationController == null)
            presentationController = GetComponent<PhishingSimulationController>();
        if (gazeBridge == null)
            gazeBridge = GetComponent<EyeGazeScenarioBridge>();
        if (gazeBridge != null)
            gazeBridge.ResolveTracker();
    }

    public void StartSimulation()
    {
        ResolveReferences();
        if (scenarioManager == null)
        {
            Debug.LogError("[PhishingSimulationIntegration] PhishingThreatSimulationManager is missing.");
            return;
        }

        if (!sessionInitialized)
        {
            sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" +
                        Guid.NewGuid().ToString("N").Substring(0, 8);
            if (gazeBridge != null)
                gazeBridge.SetParticipant(gazeBridge.ParticipantPseudoId, sessionId, gazeBridge.ConditionLabel);
            sessionInitialized = true;
        }

        if (presentationController == null)
        {
            Debug.LogError("[PhishingSimulationIntegration] PhishingSimulationController is missing.");
            return;
        }

        if (!beginRequested && !scenarioManager.IsSessionActive)
        {
            beginRequested = true;
            StartCoroutine(BeginAfterSceneInitialization());
        }
    }

    private IEnumerator BeginAfterSceneInitialization()
    {
        yield return null;
        beginRequested = false;
        if (isActiveAndEnabled && scenarioManager != null && !scenarioManager.IsSessionActive)
            scenarioManager.BeginSimulation();
    }

    public void Activate(PhishingSimulationAction action)
    {
        if (scenarioManager == null)
            return;

        switch (action)
        {
            case PhishingSimulationAction.ClickLink:
                scenarioManager.SubmitUserAction("Click Link");
                break;
            case PhishingSimulationAction.ReportPhishing:
                scenarioManager.SubmitUserAction("Report Phishing");
                break;
            case PhishingSimulationAction.Ignore:
                scenarioManager.SubmitUserAction("Ignore");
                break;
            case PhishingSimulationAction.Continue:
                if (scenarioManager.AdvanceToNextScenario())
                    return;
                if (!scenarioManager.IsSessionActive)
                    presentationController.ShowSimulationComplete();
                break;
            case PhishingSimulationAction.Restart:
                scenarioManager.RestartSimulation(false);
                break;
            case PhishingSimulationAction.Shuffle:
                scenarioManager.RestartSimulation(true);
                break;
        }
    }

    private void HandleScenarioPresented(
        PhishingThreatSimulationManager.PhishingScenarioData scenario,
        string trialId,
        int index,
        int total)
    {
        currentTrialId = trialId;
        presentationController.ShowScenario(scenario, index, total);

        if (gazeBridge == null || gazeBridge.Tracker == null)
        {
            if (!gazeUnavailableWarningShown)
            {
                Debug.LogWarning("[PhishingSimulationIntegration] Gaze trial logging is unavailable: add the gaze tracker and configure the GazeAOI layer in the project before running.");
                gazeUnavailableWarningShown = true;
            }
            return;
        }

        gazeBridge.OnScenarioTrialStarted(trialId, scenario.scenarioId);
        gazeTrialActive = gazeBridge.Tracker.IsTrialActive;
    }

    private bool gazeUnavailableWarningShown;

    private void HandleDecisionAccepted(
        PhishingThreatSimulationManager.PhishingScenarioData scenario,
        string selectedAction,
        bool isCorrect,
        float responseTimeSeconds,
        string trialId)
    {
        if (recordedTrialIds.Contains(trialId))
        {
            Debug.LogError("[PhishingSimulationIntegration] Duplicate decision event ignored for trial " + trialId);
            return;
        }
        recordedTrialIds.Add(trialId);

        PhishingTrialOutcome outcome = scenario.classification == PhishingThreatSimulationManager.ThreatClassification.Legitimate
            ? (isCorrect ? PhishingTrialOutcome.CorrectDismiss : PhishingTrialOutcome.FalseAlarm)
            : (isCorrect ? PhishingTrialOutcome.CorrectReport : PhishingTrialOutcome.MissedThreat);
        GazeProvenance provenance = gazeBridge != null && gazeBridge.Tracker != null
            ? gazeBridge.Tracker.CurrentProvenance
            : GazeProvenance.HeadDirection_Fallback;

        if (gazeTrialActive && gazeBridge != null)
        {
            gazeBridge.OnScenarioTrialEnded(
                outcome,
                responseTimeSeconds * 1000.0,
                selectedAction,
                "correct=" + isCorrect);
            gazeTrialActive = false;
        }

        string participantId = gazeBridge != null ? gazeBridge.ParticipantPseudoId : "P01";
        string decisionSessionId = gazeBridge != null ? gazeBridge.SessionId : sessionId;
        string condition = gazeBridge != null ? gazeBridge.ConditionLabel : "VR_Immersive";
        var context = new TrialContext(participantId, decisionSessionId, trialId, scenario.scenarioId, condition);
        GazeCsvWriter.WritePhishingDecision(
            context,
            provenance,
            selectedAction,
            isCorrect,
            responseTimeSeconds * 1000.0);

        string classification = scenario.classification == PhishingThreatSimulationManager.ThreatClassification.Legitimate
            ? "Legitimate"
            : "Phishing";
        string correctAction = GetDisplayAction(scenario.correctAction);
        presentationController.ShowDecisionOutcome(
            selectedAction,
            isCorrect,
            classification,
            correctAction,
            scenario.correctiveFeedback);
    }

    private static string GetDisplayAction(string correctAction)
    {
        if (string.IsNullOrWhiteSpace(correctAction))
        {
            Debug.LogError("[PhishingSimulationIntegration] Scenario data has no expected action.");
            return "Unavailable";
        }

        if (correctAction.IndexOf("Report", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Report Phishing";
        if (correctAction.IndexOf("Ignore", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Ignore";
        if (correctAction.IndexOf("Click", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Click Link";

        Debug.LogError("[PhishingSimulationIntegration] Unknown expected action in scenario data: " + correctAction);
        return correctAction;
    }

    private void HandleSimulationRestarting()
    {
        if (!gazeTrialActive || gazeBridge == null)
            return;

        gazeBridge.OnScenarioTrialAborted("Simulation restarted before a decision.");
        gazeTrialActive = false;
    }
}
