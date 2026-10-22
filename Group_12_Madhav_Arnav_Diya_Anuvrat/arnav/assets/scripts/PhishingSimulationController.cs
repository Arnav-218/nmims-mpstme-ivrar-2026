using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

public sealed class PhishingSimulationController : MonoBehaviour
{
    public const int CurrentSceneBuildVersion = 1;

    [Serializable]
    private sealed class EmailScenario
    {
        public string id;
        public string type;
        public string sender;
        public string subject;
        public string body;
        public string link;
        public bool isPhishing;
    }

    [SerializeField] private Transform headAnchor;
    [SerializeField] private Transform rightControllerAnchor;
    [SerializeField] private Transform officeContent;
    [SerializeField] private TextMesh senderText;
    [SerializeField] private TextMesh subjectText;
    [SerializeField] private TextMesh bodyText;
    [SerializeField] private TextMesh linkText;
    [SerializeField] private TextMesh feedbackText;
    [SerializeField] private int sceneBuildVersion;

    private readonly EmailScenario[] scenarios =
    {
        new EmailScenario
        {
            id = "LEGIT-001",
            type = "Legitimate",
            sender = "Facilities Team <facilities@northstar.example>",
            subject = "Office maintenance window",
            body = "The east meeting rooms will be serviced Friday from 6-7 PM.\nNo action is required. Contact Facilities if you have questions.",
            link = "https://intranet.northstar.example/facilities",
            isPhishing = false
        },
        new EmailScenario
        {
            id = "PHISH-001",
            type = "Phishing",
            sender = "Northstar IT <security-alert@northstar-help.example>",
            subject = "Urgent: mailbox access expires today",
            body = "Your mailbox will be disabled in 30 minutes.\nVerify your password immediately using the link below.",
            link = "https://northstar-help.example/verify",
            isPhishing = true
        }
    };

    private readonly int[] scenarioOrder = { 0, 1 };
    private PhishingActionButton hoveredButton;
    private LineRenderer pointerLine;
    private int scenarioIndex;
    private float scenarioStartTime;
    private bool acceptingDecision;
    private bool showingOutcome;
    private string csvPath;

    public void Configure(Transform head, Transform rightController)
    {
        headAnchor = head;
        rightControllerAnchor = rightController;
    }

    public void BuildScene()
    {
        sceneBuildVersion = CurrentSceneBuildVersion;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        officeContent = new GameObject("Office Content").transform;
        officeContent.SetParent(transform, false);

        CreateBox("Office Floor", new Vector3(0f, -0.09f, 2f), new Vector3(8f, 0.18f, 8f), new Color(0.34f, 0.36f, 0.38f), officeContent);
        CreateBox("Office Back Wall", new Vector3(0f, 2.1f, 5.5f), new Vector3(8f, 4.2f, 0.12f), new Color(0.72f, 0.76f, 0.79f), officeContent);
        CreateBox("Office Left Wall", new Vector3(-4f, 2.1f, 2f), new Vector3(0.12f, 4.2f, 7f), new Color(0.64f, 0.69f, 0.72f), officeContent);
        CreateBox("Office Right Wall", new Vector3(4f, 2.1f, 2f), new Vector3(0.12f, 4.2f, 7f), new Color(0.64f, 0.69f, 0.72f), officeContent);
        CreateBox("Desk Top", new Vector3(0f, 0.76f, 1.25f), new Vector3(1.8f, 0.09f, 0.85f), new Color(0.38f, 0.23f, 0.14f), officeContent);
        CreateBox("Desk Left Leg", new Vector3(-0.76f, 0.36f, 1.35f), new Vector3(0.09f, 0.72f, 0.09f), new Color(0.25f, 0.17f, 0.12f), officeContent);
        CreateBox("Desk Right Leg", new Vector3(0.76f, 0.36f, 1.35f), new Vector3(0.09f, 0.72f, 0.09f), new Color(0.25f, 0.17f, 0.12f), officeContent);
        CreateBox("Monitor", new Vector3(0f, 1.63f, 1.48f), new Vector3(1.8f, 1.28f, 0.08f), new Color(0.035f, 0.055f, 0.075f), officeContent);
        CreateBox("Monitor Stand", new Vector3(0f, 0.91f, 1.51f), new Vector3(0.16f, 0.28f, 0.13f), new Color(0.12f, 0.14f, 0.16f), officeContent);
        CreateBox("Monitor Base", new Vector3(0f, 0.79f, 1.48f), new Vector3(0.48f, 0.035f, 0.25f), new Color(0.12f, 0.14f, 0.16f), officeContent);

        GameObject lightObject = new GameObject("Office Fill Light");
        lightObject.transform.SetParent(officeContent, false);
        lightObject.transform.localPosition = new Vector3(0f, 3.2f, 1f);
        Light officeLight = lightObject.AddComponent<Light>();
        officeLight.type = LightType.Point;
        officeLight.color = new Color(0.78f, 0.86f, 1f);
        officeLight.intensity = 3f;
        officeLight.range = 8f;

        CreateLabel("Email Header", "INBOX  /  MESSAGE REVIEW", new Vector3(-0.78f, 2.18f, 1.425f), 42, 0.02f, new Color(0.35f, 0.82f, 0.96f), TextAnchor.UpperLeft);
        senderText = CreateLabel("Email Sender", "", new Vector3(-0.78f, 2.02f, 1.425f), 40, 0.018f, Color.white, TextAnchor.UpperLeft);
        subjectText = CreateLabel("Email Subject", "", new Vector3(-0.78f, 1.84f, 1.425f), 40, 0.018f, new Color(1f, 0.83f, 0.42f), TextAnchor.UpperLeft);
        bodyText = CreateLabel("Email Body", "", new Vector3(-0.78f, 1.63f, 1.425f), 36, 0.018f, new Color(0.91f, 0.93f, 0.95f), TextAnchor.UpperLeft);
        linkText = CreateLabel("Email Hyperlink", "", new Vector3(-0.78f, 1.24f, 1.425f), 36, 0.018f, new Color(0.35f, 0.72f, 1f), TextAnchor.UpperLeft);
        feedbackText = CreateLabel("Scenario Feedback", "", new Vector3(-0.78f, 1.08f, 1.425f), 32, 0.016f, new Color(0.82f, 0.88f, 0.91f), TextAnchor.UpperLeft);

        CreateButton("Click Link", PhishingSimulationAction.ClickLink, new Vector3(-0.53f, 0.98f, 1.39f), new Color(0.16f, 0.42f, 0.67f));
        CreateButton("Report Phishing", PhishingSimulationAction.ReportPhishing, new Vector3(0f, 0.98f, 1.39f), new Color(0.65f, 0.24f, 0.2f));
        CreateButton("Ignore", PhishingSimulationAction.Ignore, new Vector3(0.53f, 0.98f, 1.39f), new Color(0.3f, 0.36f, 0.4f));
        CreateButton("Continue", PhishingSimulationAction.Continue, new Vector3(-0.28f, 0.88f, 1.39f), new Color(0.2f, 0.48f, 0.3f));
        CreateButton("Restart", PhishingSimulationAction.Restart, new Vector3(0.28f, 0.88f, 1.39f), new Color(0.34f, 0.38f, 0.42f));
        CreatePointerLine();
        SetContinueAvailable(false);
    }

    private void Start()
    {
        if (headAnchor == null)
        {
            GameObject head = GameObject.Find("CenterEyeAnchor");
            if (head != null)
                headAnchor = head.transform;
        }

        if (rightControllerAnchor == null)
        {
            GameObject controller = GameObject.Find("RightHandAnchor");
            if (controller != null)
                rightControllerAnchor = controller.transform;
        }

        if (rightControllerAnchor == null)
        {
            Debug.LogError("Phishing simulation needs the existing Meta XR RightHandAnchor. Open the scene containing the existing XR Rig and rebuild.");
            enabled = false;
            return;
        }

        if (officeContent == null)
            BuildScene();

        csvPath = CreateSessionCsvPath();
        ShuffleScenarioOrder();
        DisplayScenario();
        Debug.Log("Phishing simulation results will be written to " + csvPath);
    }

    private void Update()
    {
        if (rightControllerAnchor == null)
            return;

        Ray ray = new Ray(rightControllerAnchor.position, rightControllerAnchor.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 5f);
        PhishingActionButton nextHovered = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            PhishingActionButton candidate = hits[i].collider.GetComponent<PhishingActionButton>();
            if (candidate != null && candidate.IsInteractable && hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                nextHovered = candidate;
            }
        }

        if (hoveredButton != nextHovered)
        {
            if (hoveredButton != null)
                hoveredButton.SetHovered(false);
            hoveredButton = nextHovered;
            if (hoveredButton != null)
                hoveredButton.SetHovered(true);
        }

        if (pointerLine != null)
        {
            pointerLine.SetPosition(0, ray.origin);
            pointerLine.SetPosition(1, ray.GetPoint(nextHovered != null ? closestDistance : 3f));
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) && hoveredButton != null)
            Activate(hoveredButton.Action);
    }

    private void Activate(PhishingSimulationAction action)
    {
        switch (action)
        {
            case PhishingSimulationAction.ClickLink:
            case PhishingSimulationAction.ReportPhishing:
            case PhishingSimulationAction.Ignore:
                SubmitDecision(action);
                break;
            case PhishingSimulationAction.Continue:
                if (showingOutcome)
                    ContinueSimulation();
                break;
            case PhishingSimulationAction.Restart:
                RestartSimulation();
                break;
        }
    }

    private void DisplayScenario()
    {
        if (scenarioIndex >= scenarioOrder.Length)
        {
            acceptingDecision = false;
            showingOutcome = true;
            senderText.text = "Simulation complete";
            subjectText.text = "";
            bodyText.text = "You reviewed both email scenarios.";
            linkText.text = "";
            feedbackText.text = "Select Restart to play again.";
            SetDecisionButtonsAvailable(false);
            SetContinueAvailable(false);
            return;
        }

        EmailScenario scenario = scenarios[scenarioOrder[scenarioIndex]];
        senderText.text = "Email " + (scenarioIndex + 1) + " of " + scenarioOrder.Length + "\nFrom: " + scenario.sender;
        subjectText.text = "Subject: " + scenario.subject;
        bodyText.text = scenario.body;
        linkText.text = "Link: " + scenario.link;
        feedbackText.text = "Aim with the right controller. Squeeze the trigger to select.";
        scenarioStartTime = Time.unscaledTime;
        acceptingDecision = true;
        showingOutcome = false;
        SetDecisionButtonsAvailable(true);
        SetContinueAvailable(false);
    }

    private void SubmitDecision(PhishingSimulationAction action)
    {
        if (!acceptingDecision || scenarioIndex >= scenarioOrder.Length)
            return;

        acceptingDecision = false;
        EmailScenario scenario = scenarios[scenarioOrder[scenarioIndex]];
        bool correct = (action == PhishingSimulationAction.ClickLink && !scenario.isPhishing) ||
                       (action == PhishingSimulationAction.ReportPhishing && scenario.isPhishing);
        float responseSeconds = Mathf.Max(0f, Time.unscaledTime - scenarioStartTime);
        bool logged = WriteResult(scenario, ActionLabel(action), correct, responseSeconds);

        feedbackText.text = (correct ? "Correct!" : "Incorrect.") + " You selected: " + ActionLabel(action) +
                            (logged ? "\nDecision saved." : "\nWarning: this decision could not be saved.");
        showingOutcome = true;
        scenarioIndex++;
        SetDecisionButtonsAvailable(false);
        SetContinueAvailable(true);
    }

    private void ContinueSimulation()
    {
        if (!showingOutcome)
            return;

        DisplayScenario();
    }

    private void RestartSimulation()
    {
        scenarioIndex = 0;
        ShuffleScenarioOrder();
        DisplayScenario();
    }

    private void ShuffleScenarioOrder()
    {
        for (int i = scenarioOrder.Length - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            int value = scenarioOrder[i];
            scenarioOrder[i] = scenarioOrder[swapIndex];
            scenarioOrder[swapIndex] = value;
        }
    }

    private bool WriteResult(EmailScenario scenario, string action, bool correct, float responseSeconds)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(csvPath, true))
            {
                if (!File.Exists(csvPath) || new FileInfo(csvPath).Length == 0)
                    writer.WriteLine("timestamp_utc,scenario_id,scenario_type,selected_action,correct,response_time_seconds");

                writer.WriteLine(string.Join(",",
                    Csv(DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture)),
                    Csv(scenario.id),
                    Csv(scenario.type),
                    Csv(action),
                    correct ? "true" : "false",
                    responseSeconds.ToString("F3", CultureInfo.InvariantCulture)));
            }
            return true;
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not append phishing simulation results to " + csvPath + ": " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Could not append phishing simulation results to " + csvPath + ": " + exception.Message);
        }

        return false;
    }

    private static string CreateSessionCsvPath()
    {
        string sessionId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) +
                           "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        string path = Path.Combine(Application.persistentDataPath, "PhishingSimulation_" + sessionId + ".csv");
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            using (StreamWriter writer = new StreamWriter(path, false))
                writer.WriteLine("timestamp_utc,scenario_id,scenario_type,selected_action,correct,response_time_seconds");
        }
        catch (IOException exception)
        {
            Debug.LogError("Could not create phishing simulation results CSV at " + path + ": " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Debug.LogError("Could not create phishing simulation results CSV at " + path + ": " + exception.Message);
        }

        return path;
    }

    private static string Csv(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string ActionLabel(PhishingSimulationAction action)
    {
        switch (action)
        {
            case PhishingSimulationAction.ClickLink:
                return "Click Link";
            case PhishingSimulationAction.ReportPhishing:
                return "Report Phishing";
            default:
                return "Ignore";
        }
    }

    private void SetDecisionButtonsAvailable(bool available)
    {
        PhishingActionButton[] buttons = officeContent.GetComponentsInChildren<PhishingActionButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            PhishingSimulationAction action = buttons[i].Action;
            if (action == PhishingSimulationAction.ClickLink ||
                action == PhishingSimulationAction.ReportPhishing ||
                action == PhishingSimulationAction.Ignore)
                buttons[i].SetInteractable(available);
        }
    }

    private void SetContinueAvailable(bool available)
    {
        PhishingActionButton[] buttons = officeContent.GetComponentsInChildren<PhishingActionButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].Action == PhishingSimulationAction.Continue)
                buttons[i].SetInteractable(available);
        }
    }

    private GameObject CreateBox(string objectName, Vector3 localPosition, Vector3 localScale, Color color, Transform parent)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;
        SetRendererColor(box.GetComponent<Renderer>(), color);
        return box;
    }

    private TextMesh CreateLabel(string objectName, string value, Vector3 localPosition, int fontSize, float characterSize, Color color, TextAnchor anchor)
    {
        GameObject label = new GameObject(objectName);
        label.transform.SetParent(officeContent, false);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = Quaternion.identity;
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = fontSize;
        text.characterSize = characterSize;
        text.anchor = anchor;
        text.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
        text.color = color;
        text.richText = false;
        return text;
    }

    private void CreateButton(string label, PhishingSimulationAction action, Vector3 localPosition, Color color)
    {
        GameObject button = CreateBox(label + " Button", localPosition, new Vector3(0.48f, 0.14f, 0.045f), color, officeContent);
        PhishingActionButton actionButton = button.AddComponent<PhishingActionButton>();
        actionButton.Initialize(action, color);
        CreateLabel(label + " Label", label, localPosition + new Vector3(-0.21f, 0.035f, -0.026f), 32, 0.019f, Color.white, TextAnchor.MiddleCenter);
    }

    private void CreatePointerLine()
    {
        GameObject lineObject = new GameObject("Phishing Simulation Pointer");
        lineObject.transform.SetParent(transform, false);
        pointerLine = lineObject.AddComponent<LineRenderer>();
        pointerLine.useWorldSpace = true;
        pointerLine.positionCount = 2;
        pointerLine.startWidth = 0.006f;
        pointerLine.endWidth = 0.003f;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader != null)
        {
            Material pointerMaterial = new Material(shader);
            pointerMaterial.color = new Color(0.25f, 0.85f, 1f);
            pointerLine.sharedMaterial = pointerMaterial;
            SetRendererColor(pointerLine, new Color(0.25f, 0.85f, 1f));
        }
        else
            pointerLine.enabled = false;
    }

    private static void SetRendererColor(Renderer renderer, Color color)
    {
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }
}
