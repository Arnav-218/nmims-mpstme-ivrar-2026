#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using System;
using System.Runtime.InteropServices;
using System.Text;
using IVRAR.CybersecurityVR;
using UnityEngine;

public sealed class PhishingSimulationController : MonoBehaviour
{
    public const int CurrentSceneBuildVersion = 8;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
#endif
    private bool wasWinKeyKDown;

    [Header("Tracking Anchors")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Transform rightControllerAnchor;
    [SerializeField] private Transform leftControllerAnchor;

    [Header("Scene Hierarchy")]
    [SerializeField] private Transform officeContent;
    [SerializeField] private int sceneBuildVersion;
    [SerializeField] private PhishingSimulationIntegration integration;

    [Header("Center Display Text Elements")]
    [SerializeField] private TextMesh headerText;
    [SerializeField] private TextMesh senderText;
    [SerializeField] private TextMesh securityBadgeText;
    [SerializeField] private TextMesh subjectText;
    [SerializeField] private TextMesh bodyText;
    [SerializeField] private TextMesh linkText;
    [SerializeField] private TextMesh feedbackText;

    [Header("Side Display Text Elements")]
    [SerializeField] private TextMesh telemetryHudText;
    [SerializeField] private TextMesh timerHudText;
    [SerializeField] private TextMesh forensicsHudText;
    [SerializeField] private Transform timerProgressBar;

    [Header("Interactive Elements")]
    [SerializeField] private GameObject breachAlertPopup;
    [SerializeField] private TextMesh breachAlertText;
    [SerializeField] private Transform emergencyAlarmLever;
    [SerializeField] private Transform officeChairTarget;

    private PhishingActionButton hoveredButton;
    private bool hoveredHyperlink;
    private bool hoveredAlarmLever;
    private bool hoveredOfficeChair;
    private bool decisionButtonsAvailable;
    private LineRenderer pointerLine;
    private Transform pointerReticle;
    private Transform activeControllerAnchor;

    private const float TextLeftX = -0.66f;
    private const float TextZ = 1.095f;

    public void Configure(Transform head, Transform rightController)
    {
        headAnchor = head;
        rightControllerAnchor = rightController;
    }

    public void SetIntegration(PhishingSimulationIntegration simulationIntegration)
    {
        integration = simulationIntegration;
    }

    private void Start()
    {
        ResolveTrackingReferences();
        InitializeAudioAndBoundaries();
        BuildScene();
        RecenterIfDisplaced(true);

        if (pointerLine == null || pointerLine.sharedMaterial == null)
            CreatePointerLine();

        if (integration == null)
            integration = GetComponent<PhishingSimulationIntegration>();

        if (integration != null)
            integration.StartSimulation();
    }

    private void InitializeAudioAndBoundaries()
    {
        if (GetComponent<PhishingAudioManager>() == null)
            gameObject.AddComponent<PhishingAudioManager>();

        var enforcer = GetComponent<WorkstationBoundaryEnforcer>();
        if (enforcer == null)
            enforcer = gameObject.AddComponent<WorkstationBoundaryEnforcer>();

        enforcer.Configure(headAnchor, headAnchor != null ? headAnchor.parent : null, transform.parent);
    }

    private void ResolveTrackingReferences()
    {
        if (headAnchor == null)
        {
            GameObject head = GameObject.Find("CenterEyeAnchor");
            if (head != null)
                headAnchor = head.transform;
        }

        if (rightControllerAnchor == null)
        {
            GameObject rightHand = GameObject.Find("RightHandAnchor");
            if (rightHand != null)
                rightControllerAnchor = rightHand.transform;
        }

        if (leftControllerAnchor == null)
        {
            GameObject leftHand = GameObject.Find("LeftHandAnchor");
            if (leftHand != null)
                leftControllerAnchor = leftHand.transform;
        }

        activeControllerAnchor = rightControllerAnchor != null ? rightControllerAnchor : leftControllerAnchor;

        EnableCameraPostProcessing();
    }

    private void EnableCameraPostProcessing()
    {
        string[] cameraNames = { "CenterEyeAnchor", "LeftEyeAnchor", "RightEyeAnchor" };
        foreach (string camName in cameraNames)
        {
            GameObject camObj = GameObject.Find(camName);
            if (camObj == null) continue;
            Camera cam = camObj.GetComponent<Camera>();
            if (cam != null)
            {
                // Prevent near clipping grey planes on proximity to screens or walls
                cam.nearClipPlane = 0.01f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.91f, 0.92f, 0.93f);

                var camData = camObj.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.renderPostProcessing = true;
                    camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
                }
            }

            // Remove any leftover physical colliders that could cause clipping/blanking
            SphereCollider sc = camObj.GetComponent<SphereCollider>();
            if (sc != null)
                sc.enabled = false;
        }
    }


    public void RecenterIfDisplaced(bool force = false)
    {
        if (headAnchor == null)
            ResolveTrackingReferences();

        if (headAnchor == null)
            return;

        Transform trackingSpace = headAnchor.parent != null ? headAnchor.parent : headAnchor;
        Vector3 headLocal = transform.InverseTransformPoint(headAnchor.position);

        if (force || Mathf.Abs(headLocal.x) > 0.5f || headLocal.z < -0.3f || headLocal.z > 0.6f || headLocal.y < 0.6f)
        {
            float targetY = 1.18f;
            Vector3 localDelta = new Vector3(0f - headLocal.x, targetY - headLocal.y, 0.22f - headLocal.z);
            Vector3 worldDelta = transform.TransformVector(localDelta);
            trackingSpace.position += worldDelta;
            var enforcer = GetComponent<WorkstationBoundaryEnforcer>();
            if (enforcer != null)
                enforcer.SetAnchorPosition(trackingSpace.position);
            Debug.Log("[PhishingSimulationController] Workstation seated viewpoint recentered.");
        }
    }

    public void BuildScene()
    {
        sceneBuildVersion = CurrentSceneBuildVersion;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name == "Phishing Simulation Pointer" || child.name == "Pointer Reticle" || child.name.EndsWith("AudioSource"))
                continue;

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        officeContent = new GameObject("Office Content").transform;
        officeContent.SetParent(transform, false);

        BuildCorporateOfficeRoom();
        BuildExecutiveDeskAndProps();
        BuildTripleMonitorCockpit();
        BuildInteractiveConsole();
        BuildBreachAlertModal();
        CreatePointerLine();
        SetContinueAvailable(false);
    }

    // =========================================================================
    // 3D Corporate Office Environment (Requirement 5: Professional Office Cabin)
    // =========================================================================
    private void BuildCorporateOfficeRoom()
    {
        Texture2D floorTex = Resources.Load<Texture2D>("Textures/floor_tile");
        Texture2D wallTex = Resources.Load<Texture2D>("Textures/wall_acoustic");
        Texture2D ceilTex = Resources.Load<Texture2D>("Textures/ceiling_acoustic");

        // Professional, bright corporate office cabin walls (clean off-white / light neutral)
        Color officeWallColor = new Color(0.91f, 0.92f, 0.93f);
        Color officeFloorColor = new Color(0.38f, 0.39f, 0.41f);
        Color officeCeilColor = new Color(0.94f, 0.95f, 0.96f);

        // Enclosed office cabin room (7.2m wide, 6.4m deep, 3.4m tall)
        CreateTexturedBox("Office Floor", new Vector3(0f, -0.05f, 1.8f), new Vector3(7.2f, 0.10f, 6.4f), officeFloorColor, floorTex, new Vector2(6f, 6f), officeContent);
        CreateTexturedBox("Office Ceiling", new Vector3(0f, 3.4f, 1.8f), new Vector3(7.2f, 0.10f, 6.4f), officeCeilColor, ceilTex, new Vector2(4f, 4f), officeContent);
        CreateTexturedBox("Office Back Wall", new Vector3(0f, 1.7f, 4.95f), new Vector3(7.2f, 3.4f, 0.12f), officeWallColor, wallTex, new Vector2(5f, 2f), officeContent);
        CreateTexturedBox("Office Front Wall", new Vector3(0f, 1.7f, -1.35f), new Vector3(7.2f, 3.4f, 0.12f), officeWallColor, wallTex, new Vector2(5f, 2f), officeContent);
        CreateTexturedBox("Office Left Wall", new Vector3(-3.55f, 1.7f, 1.8f), new Vector3(0.12f, 3.4f, 6.4f), officeWallColor, wallTex, new Vector2(5f, 2f), officeContent);
        CreateTexturedBox("Office Right Wall", new Vector3(3.55f, 1.7f, 1.8f), new Vector3(0.12f, 3.4f, 6.4f), officeWallColor, wallTex, new Vector2(5f, 2f), officeContent);

        // Clean Modern White/Warm Wood Architectural Baseboards & Trims
        Color trimColor = new Color(0.82f, 0.83f, 0.85f);
        CreateBox("Baseboard Back", new Vector3(0f, 0.05f, 4.88f), new Vector3(7.2f, 0.12f, 0.04f), trimColor, officeContent);
        CreateBox("Baseboard Front", new Vector3(0f, 0.05f, -1.28f), new Vector3(7.2f, 0.12f, 0.04f), trimColor, officeContent);
        CreateBox("Baseboard Left", new Vector3(-3.48f, 0.05f, 1.8f), new Vector3(0.04f, 0.12f, 6.4f), trimColor, officeContent);
        CreateBox("Baseboard Right", new Vector3(3.48f, 0.05f, 1.8f), new Vector3(0.04f, 0.12f, 6.4f), trimColor, officeContent);

        // Ceiling Crown Trims
        CreateBox("Crown Trim Back", new Vector3(0f, 3.34f, 4.88f), new Vector3(7.2f, 0.08f, 0.04f), trimColor, officeContent);
        CreateBox("Crown Trim Front", new Vector3(0f, 3.34f, -1.28f), new Vector3(7.2f, 0.08f, 0.04f), trimColor, officeContent);

        // Architectural Wall Accent Panels (Warm subtle corporate grey / acoustic paneling)
        CreateBox("Accent Panel Center", new Vector3(0f, 1.8f, 4.88f), new Vector3(4.8f, 2.2f, 0.04f), new Color(0.85f, 0.87f, 0.90f), officeContent);
        CreateBox("Accent Panel Left", new Vector3(-3.48f, 1.8f, 1.8f), new Vector3(0.04f, 2.2f, 3.6f), new Color(0.85f, 0.87f, 0.90f), officeContent);
        CreateBox("Accent Panel Right", new Vector3(3.48f, 1.8f, 1.8f), new Vector3(0.04f, 2.2f, 3.6f), new Color(0.85f, 0.87f, 0.90f), officeContent);

    // Office Props: Whiteboard moved safely rightwards to Z=2.85m to ensure zero collision or overlap with bookshelf
    CreateWhiteboard(new Vector3(-3.46f, 1.75f, 2.85f), Quaternion.Euler(0f, 90f, 0f));

        // Office Props: Executive Bookshelf & Storage Cabinet against Back Wall
        CreateBookshelf(new Vector3(-2.2f, 1.15f, 4.65f));

        // Office Props: Potted Plants (Biophilic Corporate Office Design)
        CreatePottedPlant("Corner Plant Left", new Vector3(-3.0f, 0f, 4.3f), 1.2f);
        CreatePottedPlant("Corner Plant Right", new Vector3(2.8f, 0f, 4.3f), 1.0f);
        CreateDeskSucculent("Desk Succulent Plant", new Vector3(0.88f, 0.83f, 1.25f));

        // Office Props: Framed Security Awareness Posters on Back Wall
        // Poster 1 moved right & up so it sits diagonally above-right of the bookshelf (shelf spans X -2.9..-1.5, top Y 2.25)
        CreateFramedPoster("Security Poster 1", new Vector3(-0.55f, 2.45f, 4.85f), "ZERO TRUST PROTOCOL\nVerify explicitly. Limit blast radius.", new Color(0.12f, 0.35f, 0.65f));
        CreateFramedPoster("Security Poster 2", new Vector3(1.05f, 2.05f, 4.85f), "BE AWARE OF PHISHING\nInspect links, senders & urgency cues.", new Color(0.70f, 0.22f, 0.15f));

        // Ceiling recessed LED panel lights with downward Spotlights
        CreateCeilingLightPanel("Ceiling LED 1", new Vector3(-1.6f, 3.32f, 0.8f));
        CreateCeilingLightPanel("Ceiling LED 2", new Vector3(1.6f, 3.32f, 0.8f));
        CreateCeilingLightPanel("Ceiling LED 3", new Vector3(-1.6f, 3.32f, 2.8f));
        CreateCeilingLightPanel("Ceiling LED 4", new Vector3(1.6f, 3.32f, 2.8f));

        // Soft ambient room point light (warm natural office lighting)
        GameObject fillLight = new GameObject("SOC Ambient Light");
        fillLight.transform.SetParent(officeContent, false);
        fillLight.transform.localPosition = new Vector3(0f, 2.7f, 0.9f);
        Light light = fillLight.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.96f, 0.97f, 1.0f);
        light.intensity = 2.0f;
        light.range = 8.5f;

        // Interior ReflectionProbe for metallic workstation reflections
        GameObject probeObj = new GameObject("Workstation Reflection Probe");
        probeObj.transform.SetParent(officeContent, false);
        probeObj.transform.localPosition = new Vector3(0f, 1.5f, 1.5f);
        ReflectionProbe probe = probeObj.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(7.2f, 3.4f, 6.4f);
        probe.boxProjection = true;

        // Server rack against right wall with glowing multi-color status LEDs
        CreateServerRack(new Vector3(3.15f, 1.1f, 2.4f));
    }

    private void CreateWhiteboard(Vector3 pos, Quaternion rotation)
    {
        Transform boardRoot = new GameObject("Office Whiteboard").transform;
        boardRoot.SetParent(officeContent, false);
        boardRoot.localPosition = pos;
        boardRoot.localRotation = rotation;

        // Aluminum frame - Colliders disabled so there is no physical mesh collision
        CreateBox("Whiteboard Frame", Vector3.zero, new Vector3(2.4f, 1.3f, 0.04f), new Color(0.72f, 0.74f, 0.76f), boardRoot, false);
        // Glossy dry-erase surface
        CreateBox("Whiteboard Surface", new Vector3(0f, 0f, -0.015f), new Vector3(2.32f, 1.22f, 0.015f), new Color(0.98f, 0.98f, 0.99f), boardRoot, false);
        // Marker tray
        CreateBox("Marker Tray", new Vector3(0f, -0.66f, -0.04f), new Vector3(1.8f, 0.03f, 0.08f), new Color(0.55f, 0.57f, 0.60f), boardRoot, false);

        // Whiteboard Header & Diagram Notes
        CreateLabel("WB Header", "INCIDENT RESPONSE WORKFLOW & THREAT INTEL", new Vector3(-1.08f, 0.52f, -0.03f), 22, 0.0095f, new Color(0.10f, 0.25f, 0.55f), TextAnchor.MiddleLeft, boardRoot);
        string wbNotes = "1. INCOMING EMAIL EVALUATION:\n   - Check sender domain SPF / DKIM alignment\n   - Detect urgency induction & spoofed subdomains\n   - Cross-verify attachments against sandbox hashes\n\n2. THREAT TRIAGE MATRIX:\n   [Low]    Safe -> Ignore / Archive\n   [Medium] Suspicious -> Quarantine & Flag\n   [High]   Phishing/Ransomware -> IMMEDIATE REPORT";
        CreateLabel("WB Content", wbNotes, new Vector3(-1.08f, 0.35f, -0.03f), 19, 0.0082f, new Color(0.15f, 0.18f, 0.22f), TextAnchor.UpperLeft, boardRoot);
    }

    private void CreateBookshelf(Vector3 pos)
    {
        Transform shelfRoot = new GameObject("Office Bookshelf").transform;
        shelfRoot.SetParent(officeContent, false);
        shelfRoot.localPosition = pos;

        Color woodColor = new Color(0.28f, 0.18f, 0.12f);
        // Outer cabinet frame (1.4m wide, 2.2m tall, 0.42m deep)
        CreateBox("Shelf Left Wall", new Vector3(-0.68f, 0f, 0f), new Vector3(0.04f, 2.2f, 0.42f), woodColor, shelfRoot);
        CreateBox("Shelf Right Wall", new Vector3(0.68f, 0f, 0f), new Vector3(0.04f, 2.2f, 0.42f), woodColor, shelfRoot);
        CreateBox("Shelf Top", new Vector3(0f, 1.08f, 0f), new Vector3(1.40f, 0.04f, 0.42f), woodColor, shelfRoot);
        CreateBox("Shelf Bottom", new Vector3(0f, -1.08f, 0f), new Vector3(1.40f, 0.04f, 0.42f), woodColor, shelfRoot);
        CreateBox("Shelf Back", new Vector3(0f, 0f, 0.19f), new Vector3(1.36f, 2.16f, 0.02f), new Color(0.22f, 0.14f, 0.09f), shelfRoot);

        // 4 Shelves with colored technical books and binders
        Color[] bookColors = {
            new Color(0.18f, 0.35f, 0.65f),
            new Color(0.65f, 0.20f, 0.18f),
            new Color(0.20f, 0.55f, 0.30f),
            new Color(0.85f, 0.65f, 0.15f),
            new Color(0.30f, 0.30f, 0.35f)
        };

        for (int i = 0; i < 4; i++)
        {
            float y = -0.6f + (i * 0.55f);
            CreateBox("Shelf Tier " + i, new Vector3(0f, y, 0f), new Vector3(1.36f, 0.03f, 0.40f), woodColor, shelfRoot);

            // Row of books on shelf
            for (int b = 0; b < 6; b++)
            {
                float bx = -0.52f + (b * 0.16f);
                Color bColor = bookColors[(i + b) % bookColors.Length];
                CreateBox($"Book_{i}_{b}", new Vector3(bx, y + 0.15f, -0.02f), new Vector3(0.09f, 0.28f, 0.24f), bColor, shelfRoot);
            }
        }
    }

    private void CreatePottedPlant(string name, Vector3 pos, float heightScale)
    {
        Transform plantRoot = new GameObject(name).transform;
        plantRoot.SetParent(officeContent, false);
        plantRoot.localPosition = pos;

        // Ceramic white planter pot
        CreateBox("Planter Pot", new Vector3(0f, 0.35f * heightScale, 0f), new Vector3(0.48f, 0.70f * heightScale, 0.48f), new Color(0.92f, 0.93f, 0.95f), plantRoot);
        // Soil
        CreateBox("Planter Soil", new Vector3(0f, 0.68f * heightScale, 0f), new Vector3(0.44f, 0.06f, 0.44f), new Color(0.18f, 0.12f, 0.08f), plantRoot);

        // Biophilic Green Leaves (Lush layered foliage cubes rotated dynamically)
        Color leafDark = new Color(0.12f, 0.45f, 0.18f);
        Color leafLight = new Color(0.22f, 0.62f, 0.25f);
        Color leafMid = new Color(0.16f, 0.52f, 0.22f);

        CreateBox("Plant Foliage Base", new Vector3(0f, 0.95f * heightScale, 0f), new Vector3(0.55f, 0.50f * heightScale, 0.55f), leafDark, plantRoot);

        GameObject foliageMid = CreateBox("Plant Foliage Mid", new Vector3(0.05f, 1.25f * heightScale, -0.05f), new Vector3(0.48f, 0.45f * heightScale, 0.48f), leafMid, plantRoot);
        foliageMid.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

        GameObject foliageTop = CreateBox("Plant Foliage Top", new Vector3(-0.04f, 1.55f * heightScale, 0.04f), new Vector3(0.38f, 0.38f * heightScale, 0.38f), leafLight, plantRoot);
        foliageTop.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
    }

    private void CreateDeskSucculent(string name, Vector3 pos)
    {
        Transform succulentRoot = new GameObject(name).transform;
        succulentRoot.SetParent(officeContent, false);
        succulentRoot.localPosition = pos;

        // Small modern white ceramic geometric pot
        CreateBox("Pot", new Vector3(0f, 0.04f, 0f), new Vector3(0.10f, 0.08f, 0.10f), new Color(0.95f, 0.95f, 0.96f), succulentRoot);
        // Soil
        CreateBox("Soil", new Vector3(0f, 0.075f, 0f), new Vector3(0.085f, 0.015f, 0.085f), new Color(0.2f, 0.15f, 0.1f), succulentRoot);
        // Succulent leaves
        CreateBox("Succulent Leaf Core", new Vector3(0f, 0.11f, 0f), new Vector3(0.075f, 0.06f, 0.075f), new Color(0.25f, 0.68f, 0.35f), succulentRoot);
        GameObject leafRing = CreateBox("Succulent Leaf Outer", new Vector3(0f, 0.10f, 0f), new Vector3(0.11f, 0.04f, 0.11f), new Color(0.18f, 0.55f, 0.28f), succulentRoot);
        leafRing.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
    }

    private void CreateFramedPoster(string name, Vector3 pos, string text, Color accentColor)
    {
        Transform posterRoot = new GameObject(name).transform;
        posterRoot.SetParent(officeContent, false);
        posterRoot.localPosition = pos;

        // Black picture frame - Colliders disabled to eliminate camera collision
        CreateBox("Frame", Vector3.zero, new Vector3(1.10f, 0.85f, 0.03f), new Color(0.12f, 0.13f, 0.15f), posterRoot, false);
        // Poster backing
        CreateBox("Print", new Vector3(0f, 0f, -0.018f), new Vector3(1.02f, 0.77f, 0.01f), new Color(0.96f, 0.97f, 0.98f), posterRoot, false);
        // Accent color stripe
        CreateBox("Color Stripe", new Vector3(0f, 0.30f, -0.022f), new Vector3(1.02f, 0.08f, 0.005f), accentColor, posterRoot, false);

        CreateLabel("Poster Text", text, new Vector3(0f, -0.05f, -0.025f), 22, 0.0095f, new Color(0.15f, 0.17f, 0.20f), TextAnchor.MiddleCenter, posterRoot);
    }

    private void CreateCeilingLightPanel(string name, Vector3 pos)
    {
        GameObject panel = CreateBox(name, pos, new Vector3(1.1f, 0.04f, 0.7f), new Color(0.92f, 0.96f, 1.0f), officeContent);
        Renderer panelRend = panel.GetComponent<Renderer>();
        Material mat = Application.isPlaying ? panelRend.material : panelRend.sharedMaterial;
        if (mat != null && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.75f, 0.88f, 1.0f) * 1.5f);
        }

        // Attach downward spotlight casting realistic desk illumination
        GameObject spotObj = new GameObject(name + " Spotlight");
        spotObj.transform.SetParent(panel.transform, false);
        spotObj.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        spotObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Light spot = spotObj.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = new Color(0.90f, 0.95f, 1.0f);
        spot.intensity = 1.2f;
        spot.range = 4.0f;
        spot.spotAngle = 75f;
    }

    private void CreateServerRack(Vector3 pos)
    {
        CreateBox("Server Rack Body", pos, new Vector3(0.65f, 2.1f, 0.75f), new Color(0.11f, 0.12f, 0.14f), officeContent);
        for (int i = 0; i < 5; i++)
        {
            float y = pos.y + 0.75f - (i * 0.32f);
            Color ledColor = (i % 2 == 0) ? new Color(0f, 0.9f, 0.95f) : (i == 3 ? new Color(1f, 0.7f, 0f) : new Color(0.1f, 0.95f, 0.3f));
            GameObject led = CreateBox("Server LED " + i, new Vector3(pos.x - 0.33f, y, pos.z - 0.2f + (i * 0.08f)), new Vector3(0.03f, 0.03f, 0.03f), ledColor, officeContent);
            Renderer ledRend = led.GetComponent<Renderer>();
            Material ledMat = Application.isPlaying ? ledRend.material : ledRend.sharedMaterial;
            if (ledMat != null && ledMat.HasProperty("_EmissionColor"))
            {
                ledMat.EnableKeyword("_EMISSION");
                ledMat.SetColor("_EmissionColor", ledColor * 1.8f);
            }
        }
    }

    // =========================================================================
    // Executive Workstation Desk & Props (Requirement 5)
    // =========================================================================
    private void BuildExecutiveDeskAndProps()
    {
        Texture2D deskTex = Resources.Load<Texture2D>("Textures/desk_wood");
        Texture2D kbTex = Resources.Load<Texture2D>("Textures/keyboard_map");

        // Ergonomic Executive Desk at Z=1.05m
        CreateTexturedBox("Desk Top", new Vector3(0f, 0.74f, 1.05f), new Vector3(2.10f, 0.06f, 0.92f), new Color(0.36f, 0.22f, 0.15f), deskTex, new Vector2(2f, 1f), officeContent);
        CreateBox("Desk Bevel Trim Front", new Vector3(0f, 0.74f, 0.585f), new Vector3(2.12f, 0.065f, 0.015f), new Color(0.14f, 0.15f, 0.17f), officeContent);
        CreateBox("Desk Monitor Riser", new Vector3(0f, 0.81f, 1.22f), new Vector3(1.95f, 0.08f, 0.38f), new Color(0.24f, 0.15f, 0.10f), officeContent);
        CreateBox("Desk Left Leg", new Vector3(-0.95f, 0.36f, 1.05f), new Vector3(0.08f, 0.70f, 0.78f), new Color(0.18f, 0.19f, 0.21f), officeContent);
        CreateBox("Desk Right Leg", new Vector3(0.95f, 0.36f, 1.05f), new Vector3(0.08f, 0.70f, 0.78f), new Color(0.18f, 0.19f, 0.21f), officeContent);
        CreateBox("Desk Modesty Panel", new Vector3(0f, 0.42f, 1.40f), new Vector3(1.82f, 0.54f, 0.04f), new Color(0.18f, 0.19f, 0.21f), officeContent);

        // Desktop Accessories: Keyboard with keycaps and cable
        CreateTexturedBox("Keyboard", new Vector3(0f, 0.775f, 0.70f), new Vector3(0.44f, 0.015f, 0.15f), new Color(0.16f, 0.17f, 0.20f), kbTex, Vector2.one, officeContent);
        CreateBox("Keyboard Cable", new Vector3(0f, 0.772f, 0.78f), new Vector3(0.008f, 0.005f, 0.05f), new Color(0.1f, 0.1f, 0.1f), officeContent);

        // Mouse Pad & Contoured Mouse with scroll wheel
        CreateBox("Mouse Pad", new Vector3(0.36f, 0.773f, 0.70f), new Vector3(0.24f, 0.005f, 0.22f), new Color(0.08f, 0.09f, 0.11f), officeContent);
        CreateBox("Mouse Body", new Vector3(0.36f, 0.785f, 0.70f), new Vector3(0.068f, 0.026f, 0.115f), new Color(0.18f, 0.20f, 0.23f), officeContent);
        CreateBox("Mouse Scroll Wheel", new Vector3(0.36f, 0.798f, 0.685f), new Vector3(0.010f, 0.010f, 0.024f), new Color(0.3f, 0.85f, 1f), officeContent);

        // Coffee Mug (Cylindrical mug with handle)
        CreateCoffeeMug(new Vector3(-0.52f, 0.77f, 0.75f));

        // Note: The random unused desk lamp on the left side of the table has been removed per user instruction.

        // Security incident report dossiers / document folders
        CreateIncidentDossierStack(new Vector3(-0.72f, 0.775f, 0.84f));

        // Cable management trunk drop
        CreateBox("Cable Trunk Desk Drop", new Vector3(0f, 0.40f, 1.25f), new Vector3(0.08f, 0.72f, 0.06f), new Color(0.12f, 0.13f, 0.15f), officeContent);

        // Physical Emergency Phishing Report Alarm on right corner of desk
        GameObject alarmBase = CreateBox("Emergency Alarm Base", new Vector3(0.70f, 0.78f, 0.82f), new Vector3(0.16f, 0.035f, 0.16f), new Color(0.22f, 0.23f, 0.25f), officeContent);
        GameObject alarmButton = CreateBox("Emergency Report Alarm", new Vector3(0.70f, 0.815f, 0.82f), new Vector3(0.11f, 0.045f, 0.11f), new Color(0.88f, 0.14f, 0.14f), officeContent);
        emergencyAlarmLever = alarmButton.transform;
        BoxCollider alarmCol = alarmButton.GetComponent<BoxCollider>();
        if (alarmCol != null) alarmCol.isTrigger = false;
        CreateLabel("Alarm Label", "EMERGENCY\nREPORT", new Vector3(0.70f, 0.840f, 0.82f), 20, 0.008f, Color.white, TextAnchor.MiddleCenter, officeContent, Quaternion.Euler(90f, 0f, 0f), true);

        // Chair placed so the seat sits under the seated player (eyes at Z=0.18) with the backrest just behind them
        CreateOfficeChair(new Vector3(0f, 0f, 0.30f));
        BuildOfficePolish();
    }

    // Extra production-polish dressing: rug, window with skyline, wall clock, side cabinet, cable-free floor accents.
    private void BuildOfficePolish()
    {
        // Area rug under the workstation
        CreateBox("Workstation Rug", new Vector3(0f, 0.005f, 0.75f), new Vector3(3.0f, 0.01f, 2.4f), new Color(0.20f, 0.27f, 0.36f), officeContent, false);
        CreateBox("Workstation Rug Border", new Vector3(0f, 0.004f, 0.75f), new Vector3(3.15f, 0.008f, 2.55f), new Color(0.78f, 0.74f, 0.66f), officeContent, false);

        // Window on right wall with city skyline backdrop
        Transform win = new GameObject("Office Window").transform;
        win.SetParent(officeContent, false);
        win.localPosition = new Vector3(3.47f, 1.75f, 0.6f);
        win.localRotation = Quaternion.Euler(0f, -90f, 0f);
        CreateBox("Window Frame", Vector3.zero, new Vector3(1.8f, 1.2f, 0.06f), new Color(0.85f, 0.86f, 0.88f), win, false);
        GameObject sky = CreateBox("Window Sky", new Vector3(0f, 0f, -0.035f), new Vector3(1.68f, 1.08f, 0.01f), new Color(0.55f, 0.75f, 0.95f), win, false);
        Material skyMat = Application.isPlaying ? sky.GetComponent<Renderer>().material : sky.GetComponent<Renderer>().sharedMaterial;
        if (skyMat != null && skyMat.HasProperty("_EmissionColor")) { skyMat.EnableKeyword("_EMISSION"); skyMat.SetColor("_EmissionColor", new Color(0.45f, 0.62f, 0.85f)); }
        float[] heights = { 0.35f, 0.55f, 0.28f, 0.7f, 0.45f, 0.6f, 0.32f };
        for (int i = 0; i < heights.Length; i++)
            CreateBox("Skyline " + i, new Vector3(-0.72f + i * 0.24f, -0.54f + heights[i] * 0.5f, -0.04f), new Vector3(0.2f, heights[i], 0.005f), new Color(0.22f, 0.28f, 0.38f), win, false);
        CreateBox("Window Mullion V", new Vector3(0f, 0f, -0.045f), new Vector3(0.04f, 1.08f, 0.02f), new Color(0.85f, 0.86f, 0.88f), win, false);
        CreateBox("Window Mullion H", new Vector3(0f, 0f, -0.045f), new Vector3(1.68f, 0.04f, 0.02f), new Color(0.85f, 0.86f, 0.88f), win, false);
        CreateBox("Window Sill", new Vector3(0f, -0.63f, -0.08f), new Vector3(1.9f, 0.04f, 0.16f), new Color(0.9f, 0.9f, 0.92f), win, false);

        // Wall clock above whiteboard area (left wall)
        Transform clock = new GameObject("Wall Clock").transform;
        clock.SetParent(officeContent, false);
        clock.localPosition = new Vector3(-3.47f, 2.75f, 1.2f);
        clock.localRotation = Quaternion.Euler(0f, 90f, 0f);
        CreateBox("Clock Rim", Vector3.zero, new Vector3(0.36f, 0.36f, 0.04f), new Color(0.12f, 0.12f, 0.14f), clock, false);
        CreateBox("Clock Face", new Vector3(0f, 0f, -0.022f), new Vector3(0.32f, 0.32f, 0.005f), new Color(0.97f, 0.97f, 0.96f), clock, false);
        GameObject hourHand = CreateBox("Clock Hour Hand", new Vector3(0.03f, 0.03f, -0.027f), new Vector3(0.012f, 0.09f, 0.003f), Color.black, clock, false);
        hourHand.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
        CreateBox("Clock Minute Hand", new Vector3(0f, 0.055f, -0.028f), new Vector3(0.008f, 0.12f, 0.003f), Color.black, clock, false);

        // Low side credenza on right wall with printer & files
        CreateBox("Credenza Body", new Vector3(2.9f, 0.38f, 3.5f), new Vector3(0.5f, 0.76f, 1.1f), new Color(0.30f, 0.20f, 0.14f), officeContent);
        CreateBox("Credenza Top", new Vector3(2.9f, 0.775f, 3.5f), new Vector3(0.54f, 0.03f, 1.14f), new Color(0.20f, 0.21f, 0.23f), officeContent);
        CreateBox("Printer", new Vector3(2.9f, 0.88f, 3.7f), new Vector3(0.42f, 0.18f, 0.38f), new Color(0.88f, 0.89f, 0.90f), officeContent, false);
        CreateBox("Printer Tray", new Vector3(2.9f, 0.90f, 3.7f - 0.22f), new Vector3(0.3f, 0.01f, 0.1f), new Color(0.3f, 0.3f, 0.32f), officeContent, false);
        CreateBox("File Box", new Vector3(2.9f, 0.86f, 3.25f), new Vector3(0.3f, 0.14f, 0.36f), new Color(0.18f, 0.35f, 0.6f), officeContent, false);
    }

    private void CreateOfficeChair(Vector3 pos)
    {
        Transform chairRoot = new GameObject("Executive Office Chair").transform;
        chairRoot.SetParent(officeContent, false);
        chairRoot.localPosition = pos;
        officeChairTarget = chairRoot;

        Color darkMesh = new Color(0.12f, 0.13f, 0.15f);
        Color chrome = new Color(0.75f, 0.77f, 0.80f);
        Color cushionDark = new Color(0.16f, 0.17f, 0.20f);

        // 1. 5-Star Caster Wheel Base (on floor) - non-colliding visual prop
        CreateBox("Chair Base Hub", new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.06f, 0.12f), chrome, chairRoot, false);
        CreateBox("Chair Gas Lift Cylinder", new Vector3(0f, 0.26f, 0f), new Vector3(0.065f, 0.30f, 0.065f), chrome, chairRoot, false);
        
        // Caster Legs (Cross base)
        CreateBox("Chair Leg X", new Vector3(0f, 0.06f, 0f), new Vector3(0.56f, 0.035f, 0.08f), chrome, chairRoot, false);
        CreateBox("Chair Leg Z", new Vector3(0f, 0.06f, 0f), new Vector3(0.08f, 0.035f, 0.56f), chrome, chairRoot, false);

        // 2. Contoured Seat Cushion (at standard ergonomic seat height 0.46m) - Trigger collider so player can click chair to sit without physical collision!
        GameObject seatObj = CreateBox("Chair Seat Cushion", new Vector3(0f, 0.46f, 0f), new Vector3(0.52f, 0.08f, 0.50f), cushionDark, chairRoot, true);
        Collider seatCol = seatObj.GetComponent<Collider>();
        if (seatCol != null) seatCol.isTrigger = true;

        // 3. Ergonomic High-Back Mesh Rest (angled back slightly at Z = -0.22m, Y = 0.85m)
        GameObject backrest = CreateBox("Chair High Backrest", new Vector3(0f, 0.84f, -0.22f), new Vector3(0.48f, 0.70f, 0.05f), darkMesh, chairRoot, true);
        Collider backCol = backrest.GetComponent<Collider>();
        if (backCol != null) backCol.isTrigger = true;
        backrest.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

        // Lumbar Support & Adjustable Headrest
        CreateBox("Chair Lumbar Pad", new Vector3(0f, 0.65f, -0.20f), new Vector3(0.40f, 0.12f, 0.04f), cushionDark, chairRoot, false);
        CreateBox("Chair Headrest", new Vector3(0f, 1.22f, -0.26f), new Vector3(0.28f, 0.14f, 0.06f), cushionDark, chairRoot, false);

        // 4. Ergonomic Armrests (Left & Right)
        CreateBox("Armrest Left Post", new Vector3(-0.27f, 0.56f, 0f), new Vector3(0.035f, 0.18f, 0.05f), chrome, chairRoot, false);
        CreateBox("Armrest Left Pad", new Vector3(-0.27f, 0.66f, 0.04f), new Vector3(0.08f, 0.035f, 0.26f), cushionDark, chairRoot, false);

        CreateBox("Armrest Right Post", new Vector3(0.27f, 0.56f, 0f), new Vector3(0.035f, 0.18f, 0.05f), chrome, chairRoot, false);
        CreateBox("Armrest Right Pad", new Vector3(0.27f, 0.66f, 0.04f), new Vector3(0.08f, 0.035f, 0.26f), cushionDark, chairRoot, false);
    }

    private void CreateCoffeeMug(Vector3 pos)
    {
        Transform mugRoot = new GameObject("Coffee Mug").transform;
        mugRoot.SetParent(officeContent, false);
        mugRoot.localPosition = pos;

        // Mug outer body
        CreateBox("Mug Body", new Vector3(0f, 0.05f, 0f), new Vector3(0.085f, 0.10f, 0.085f), new Color(0.88f, 0.90f, 0.92f), mugRoot);
        // Mug coffee surface
        CreateBox("Coffee Surface", new Vector3(0f, 0.095f, 0f), new Vector3(0.075f, 0.005f, 0.075f), new Color(0.25f, 0.15f, 0.10f), mugRoot);
        // Mug handle
        CreateBox("Mug Handle Top", new Vector3(0.055f, 0.08f, 0f), new Vector3(0.03f, 0.015f, 0.02f), new Color(0.88f, 0.90f, 0.92f), mugRoot);
        CreateBox("Mug Handle Outer", new Vector3(0.07f, 0.05f, 0f), new Vector3(0.015f, 0.06f, 0.02f), new Color(0.88f, 0.90f, 0.92f), mugRoot);
        CreateBox("Mug Handle Bottom", new Vector3(0.055f, 0.02f, 0f), new Vector3(0.03f, 0.015f, 0.02f), new Color(0.88f, 0.90f, 0.92f), mugRoot);
    }

    private void CreateIncidentDossierStack(Vector3 pos)
    {
        Transform stackRoot = new GameObject("Incident Dossiers").transform;
        stackRoot.SetParent(officeContent, false);
        stackRoot.localPosition = pos;

        // Stack of 3 security filing folders
        CreateBox("Dossier Folder 1", new Vector3(0f, 0.010f, 0f), new Vector3(0.24f, 0.015f, 0.32f), new Color(0.58f, 0.50f, 0.35f), stackRoot);
        CreateBox("Dossier Tab 1", new Vector3(-0.08f, 0.010f, 0.17f), new Vector3(0.07f, 0.012f, 0.025f), new Color(0.75f, 0.25f, 0.20f), stackRoot);

        CreateBox("Dossier Folder 2", new Vector3(0.015f, 0.026f, -0.01f), new Vector3(0.24f, 0.015f, 0.32f), new Color(0.22f, 0.38f, 0.52f), stackRoot);
        CreateBox("Dossier Tab 2", new Vector3(0.02f, 0.026f, 0.16f), new Vector3(0.07f, 0.012f, 0.025f), new Color(0.20f, 0.65f, 0.85f), stackRoot);

        CreateBox("Dossier Folder 3", new Vector3(-0.01f, 0.042f, 0.01f), new Vector3(0.24f, 0.015f, 0.32f), new Color(0.28f, 0.32f, 0.36f), stackRoot);
        CreateBox("Dossier Tab 3", new Vector3(0.08f, 0.042f, 0.18f), new Vector3(0.07f, 0.012f, 0.025f), new Color(0.85f, 0.70f, 0.20f), stackRoot);
    }

    // =========================================================================
    // Triple-Monitor Cockpit UI (Professional Matte Slate UI - No Neon Lights)
    // =========================================================================
    private void BuildTripleMonitorCockpit()
    {
        Color bezelColor = new Color(0.10f, 0.11f, 0.13f);
        Color screenBgColor = new Color(0.11f, 0.13f, 0.16f);
        Color headerBarColor = new Color(0.16f, 0.19f, 0.24f);
        Color cardSurfaceColor = new Color(0.14f, 0.16f, 0.20f);
        Color cardSubtleColor = new Color(0.12f, 0.14f, 0.18f);
        Color dividerColor = new Color(0.24f, 0.28f, 0.34f);
        Color headerTextColor = new Color(0.90f, 0.93f, 0.96f);
        Color subHeaderColor = new Color(0.70f, 0.76f, 0.84f);

        // 1. CENTER ULTRAWIDE MONITOR (Main Mail Client)
        Vector3 centerPos = new Vector3(0f, 1.34f, 1.15f);
        GameObject centerMonObj = new GameObject("Center Main Monitor");
        centerMonObj.transform.SetParent(officeContent, false);
        centerMonObj.transform.localPosition = centerPos;

        // Front bezel & matte screen canvas
        CreateBox("Center Monitor Bezel", Vector3.zero, new Vector3(1.54f, 0.94f, 0.05f), bezelColor, centerMonObj.transform);
        CreateBox("Center Monitor Screen", new Vector3(0f, 0f, -0.026f), new Vector3(1.50f, 0.90f, 0.005f), screenBgColor, centerMonObj.transform);

        // Solid Opaque Backplate Casing (Eliminates reverse-side text visibility)
        CreateBox("Center Monitor Back Casing", new Vector3(0f, 0f, 0.045f), new Vector3(1.56f, 0.96f, 0.08f), new Color(0.07f, 0.08f, 0.09f), centerMonObj.transform);
        CreateBox("Center VESA Plate", new Vector3(0f, 0f, 0.090f), new Vector3(0.24f, 0.24f, 0.025f), new Color(0.14f, 0.15f, 0.18f), centerMonObj.transform);

        // Center Monitor Heavy Dual-Column Stand resting on riser
        CreateBox("Center Monitor Stand Left", new Vector3(-0.15f, -0.46f, 0.05f), new Vector3(0.06f, 0.26f, 0.10f), new Color(0.18f, 0.19f, 0.22f), centerMonObj.transform);
        CreateBox("Center Monitor Stand Right", new Vector3(0.15f, -0.46f, 0.05f), new Vector3(0.06f, 0.26f, 0.10f), new Color(0.18f, 0.19f, 0.22f), centerMonObj.transform);
        CreateBox("Center Monitor Stand Base", new Vector3(0f, -0.52f, 0.05f), new Vector3(0.42f, 0.025f, 0.22f), new Color(0.15f, 0.16f, 0.18f), centerMonObj.transform);

        // Clean Corporate Mail Client UI Layout (Matte cards & subtle neutral dividers, zero neon glow)
        CreateBox("Titlebar Bar", new Vector3(0f, 0.415f, -0.028f), new Vector3(1.48f, 0.055f, 0.004f), headerBarColor, centerMonObj.transform, false);
        CreateBox("Titlebar Divider", new Vector3(0f, 0.386f, -0.029f), new Vector3(1.48f, 0.003f, 0.004f), dividerColor, centerMonObj.transform, false);
        CreateBox("Mail Header Card", new Vector3(0f, 0.235f, -0.027f), new Vector3(1.46f, 0.27f, 0.003f), cardSurfaceColor, centerMonObj.transform, false);
        CreateBox("Mail Body Card", new Vector3(0f, -0.065f, -0.027f), new Vector3(1.46f, 0.29f, 0.003f), cardSubtleColor, centerMonObj.transform, false);
        CreateBox("Mail Link Card", new Vector3(0f, -0.26f, -0.027f), new Vector3(1.46f, 0.065f, 0.003f), cardSurfaceColor, centerMonObj.transform, false);
        CreateBox("Mail Footer Bar", new Vector3(0f, -0.37f, -0.027f), new Vector3(1.46f, 0.11f, 0.003f), headerBarColor, centerMonObj.transform, false);
        headerText = CreateLabel("Titlebar Text", "ENTERPRISE MAIL CLIENT  //  INBOX MESSAGE VIEWER   [PRESS 'K' OR CLICK CHAIR TO SIT]", centerPos + new Vector3(-0.70f, 0.418f, -0.032f), 21, 0.0094f, headerTextColor, TextAnchor.MiddleLeft);

        // Center Monitor 5 Eye-Gaze AOIs (Clean neutral typography - no color-coded phishing giveaways)
        senderText = CreateAoiLabel("AOI_SenderHeader", "", new Vector3(TextLeftX, 1.68f, TextZ), 25, 0.0110f, new Color(0.95f, 0.96f, 0.98f), AOIType.SenderHeader, "sender_header", 20);
        securityBadgeText = CreateAoiLabel("AOI_SecurityBadge", "", new Vector3(TextLeftX, 1.58f, TextZ), 22, 0.0100f, subHeaderColor, AOIType.SecurityBadge, "security_badge", 25);
        subjectText = CreateAoiLabel("AOI_SubjectLine", "", new Vector3(TextLeftX, 1.48f, TextZ), 25, 0.0112f, new Color(0.98f, 0.98f, 1.0f), AOIType.SubjectLine, "subject_line", 10);
        bodyText = CreateAoiLabel("AOI_EmailBody", "", new Vector3(TextLeftX, 1.38f, TextZ), 22, 0.0100f, new Color(0.86f, 0.89f, 0.93f), AOIType.EmailBody, "email_body", 1);
        linkText = CreateAoiLabel("AOI_Hyperlink", "", new Vector3(TextLeftX, 1.08f, TextZ), 23, 0.0105f, new Color(0.58f, 0.78f, 0.96f), AOIType.HyperlinkTarget, "hyperlink_target", 30);
        feedbackText = CreateLabel("Scenario Feedback", "", new Vector3(TextLeftX, 0.98f, TextZ), 21, 0.0096f, subHeaderColor, TextAnchor.UpperLeft);

        // 2. LEFT TELEMETRY MONITOR (Angled -28° inward toward player, offset to X=-1.12m)
        GameObject leftMonObj = new GameObject("Left Telemetry Monitor");
        leftMonObj.transform.SetParent(officeContent, false);
        leftMonObj.transform.localPosition = new Vector3(-1.12f, 1.34f, 1.06f);
        leftMonObj.transform.localRotation = Quaternion.Euler(0f, -28f, 0f);

        CreateBox("Left Monitor Bezel", Vector3.zero, new Vector3(0.74f, 0.84f, 0.05f), bezelColor, leftMonObj.transform);
        CreateBox("Left Monitor Screen", new Vector3(0f, 0f, -0.026f), new Vector3(0.70f, 0.80f, 0.005f), screenBgColor, leftMonObj.transform);
        CreateBox("Left Monitor Back Casing", new Vector3(0f, 0f, 0.045f), new Vector3(0.76f, 0.86f, 0.08f), new Color(0.07f, 0.08f, 0.09f), leftMonObj.transform);
        CreateBox("Left VESA Plate", new Vector3(0f, 0f, 0.090f), new Vector3(0.18f, 0.18f, 0.025f), new Color(0.14f, 0.15f, 0.18f), leftMonObj.transform);

        CreateBox("Left Mount Arm Upper", new Vector3(0.12f, -0.38f, 0.08f), new Vector3(0.05f, 0.16f, 0.08f), new Color(0.18f, 0.20f, 0.23f), leftMonObj.transform);
        CreateBox("Left Mount Arm Lower", new Vector3(0.18f, -0.48f, 0.12f), new Vector3(0.06f, 0.18f, 0.08f), new Color(0.18f, 0.20f, 0.23f), leftMonObj.transform);

        CreateBox("Left Monitor Titlebar", new Vector3(0f, 0.365f, -0.028f), new Vector3(0.68f, 0.05f, 0.004f), headerBarColor, leftMonObj.transform, false);
        CreateBox("Left Titlebar Divider", new Vector3(0f, 0.338f, -0.029f), new Vector3(0.68f, 0.003f, 0.004f), dividerColor, leftMonObj.transform, false);
        CreateBox("Left Telemetry Card", new Vector3(0f, -0.02f, -0.027f), new Vector3(0.66f, 0.68f, 0.003f), cardSurfaceColor, leftMonObj.transform, false);
        CreateLabel("Left Title Text", "SESSION METRICS & GAZE TELEMETRY", new Vector3(-0.31f, 0.368f, -0.032f), 20, 0.0090f, headerTextColor, TextAnchor.MiddleLeft, leftMonObj.transform);
        telemetryHudText = CreateLabel("Telemetry HUD Text", "INITIALIZING...", new Vector3(-0.31f, 0.30f, -0.032f), 20, 0.0092f, new Color(0.84f, 0.88f, 0.92f), TextAnchor.UpperLeft, leftMonObj.transform);

        // 3. RIGHT FORENSICS & HEADER INSPECTOR MONITOR (X=+1.12m)
        GameObject rightMonObj = new GameObject("Right Forensics Monitor");
        rightMonObj.transform.SetParent(officeContent, false);
        rightMonObj.transform.localPosition = new Vector3(1.12f, 1.34f, 1.06f);
        rightMonObj.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);

        CreateBox("Right Monitor Bezel", Vector3.zero, new Vector3(0.74f, 0.84f, 0.05f), bezelColor, rightMonObj.transform);
        CreateBox("Right Monitor Screen", new Vector3(0f, 0f, -0.026f), new Vector3(0.70f, 0.80f, 0.005f), screenBgColor, rightMonObj.transform);
        CreateBox("Right Monitor Back Casing", new Vector3(0f, 0f, 0.045f), new Vector3(0.76f, 0.86f, 0.08f), new Color(0.07f, 0.08f, 0.09f), rightMonObj.transform);
        CreateBox("Right VESA Plate", new Vector3(0f, 0f, 0.090f), new Vector3(0.18f, 0.18f, 0.025f), new Color(0.14f, 0.15f, 0.18f), rightMonObj.transform);

        CreateBox("Right Mount Arm Upper", new Vector3(-0.12f, -0.38f, 0.08f), new Vector3(0.05f, 0.16f, 0.08f), new Color(0.18f, 0.20f, 0.23f), rightMonObj.transform);
        CreateBox("Right Mount Arm Lower", new Vector3(-0.18f, -0.48f, 0.12f), new Vector3(0.06f, 0.18f, 0.08f), new Color(0.18f, 0.20f, 0.23f), rightMonObj.transform);

        CreateBox("Right Monitor Titlebar", new Vector3(0f, 0.365f, -0.028f), new Vector3(0.68f, 0.05f, 0.004f), headerBarColor, rightMonObj.transform, false);
        CreateBox("Right Titlebar Divider", new Vector3(0f, 0.338f, -0.029f), new Vector3(0.68f, 0.003f, 0.004f), dividerColor, rightMonObj.transform, false);
        CreateBox("Right Forensics Card", new Vector3(0f, -0.07f, -0.027f), new Vector3(0.66f, 0.56f, 0.003f), cardSurfaceColor, rightMonObj.transform, false);
        CreateLabel("Right Title Text", "HEADER & DNS FORENSIC INSPECTOR", new Vector3(-0.31f, 0.368f, -0.032f), 20, 0.0090f, headerTextColor, TextAnchor.MiddleLeft, rightMonObj.transform);

        // Timer background & progress bar (strictly contained inside Timer BG at X=0, width=0.62m, calm matte slate blue)
        CreateBox("Timer BG", new Vector3(0f, 0.29f, -0.028f), new Vector3(0.64f, 0.030f, 0.004f), new Color(0.09f, 0.11f, 0.14f), rightMonObj.transform, false);
        GameObject timerBarObj = CreateBox("Timer Bar", new Vector3(0f, 0.29f, -0.031f), new Vector3(0.62f, 0.020f, 0.004f), new Color(0.38f, 0.62f, 0.85f), rightMonObj.transform, false);
        timerProgressBar = timerBarObj.transform;

        timerHudText = CreateLabel("Timer Text", "RESPONSE WINDOW: 05:00", new Vector3(-0.31f, 0.245f, -0.032f), 19, 0.0088f, subHeaderColor, TextAnchor.MiddleLeft, rightMonObj.transform);
        forensicsHudText = CreateLabel("Forensics Text", "INSPECTING HEADERS...", new Vector3(-0.31f, 0.195f, -0.032f), 18, 0.0084f, new Color(0.84f, 0.88f, 0.92f), TextAnchor.UpperLeft, rightMonObj.transform);
    }

    // =========================================================================
    // Interactive Console Buttons & Ergonomics (Front-and-Center, Elevated, High Contrast)
    // =========================================================================
    private void BuildInteractiveConsole()
    {
        // Dedicated unscaled root container for console ergonomics (Z=0.86m, Y=0.795m, angled -22° so the face tilts TOWARDS the seated player!)
        // Critical: Must remain localScale = Vector3.one so child button geometry is never distorted or flattened!
        GameObject consoleRoot = new GameObject("Tactile Console");
        consoleRoot.transform.SetParent(officeContent, false);
        consoleRoot.transform.localPosition = new Vector3(0f, 0.795f, 0.86f);
        consoleRoot.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
        consoleRoot.transform.localScale = Vector3.one;

        // Unified Sleek Housing Base (fits comfortably on desk between keyboard and screens, width=0.96m, depth=0.28m, height=0.024m)
        CreateBox("Console Base Housing", Vector3.zero, new Vector3(0.96f, 0.024f, 0.28f), new Color(0.12f, 0.13f, 0.16f), consoleRoot.transform);

        // Opaque back shield at the rear of the console so buttons cannot be seen from behind the PC desk
        CreateBox("Console Rear Shield", new Vector3(0f, 0.035f, 0.138f), new Vector3(0.96f, 0.09f, 0.012f), new Color(0.10f, 0.11f, 0.14f), consoleRoot.transform);

        // 1. Primary Decision Tier (Row 1, closest to player at Z=-0.055m, elevated keycaps)
        CreateBezeledButton("Click Link", PhishingSimulationAction.ClickLink, new Vector3(-0.31f, 0.025f, -0.055f), new Vector3(0.27f, 0.045f, 0.085f), new Color(0.14f, 0.50f, 0.95f), consoleRoot.transform);
        CreateBezeledButton("Report Phishing", PhishingSimulationAction.ReportPhishing, new Vector3(0f, 0.027f, -0.055f), new Vector3(0.29f, 0.048f, 0.090f), new Color(0.92f, 0.18f, 0.16f), consoleRoot.transform);
        CreateBezeledButton("Ignore / Safe", PhishingSimulationAction.Ignore, new Vector3(0.31f, 0.025f, -0.055f), new Vector3(0.27f, 0.045f, 0.085f), new Color(0.16f, 0.68f, 0.38f), consoleRoot.transform);

        // 2. Secondary Administrative Tier (Row 2, upper row at Z=+0.065m)
        CreateBezeledButton("Continue", PhishingSimulationAction.Continue, new Vector3(-0.29f, 0.022f, 0.065f), new Vector3(0.25f, 0.038f, 0.065f), new Color(0.18f, 0.65f, 0.38f), consoleRoot.transform);
        CreateBezeledButton("Shuffle Deck", PhishingSimulationAction.Shuffle, new Vector3(0f, 0.022f, 0.065f), new Vector3(0.25f, 0.038f, 0.065f), new Color(0.85f, 0.58f, 0.16f), consoleRoot.transform);
        CreateBezeledButton("Restart", PhishingSimulationAction.Restart, new Vector3(0.29f, 0.022f, 0.065f), new Vector3(0.25f, 0.038f, 0.065f), new Color(0.42f, 0.46f, 0.55f), consoleRoot.transform);
    }

    private void CreateBezeledButton(string label, PhishingSimulationAction action, Vector3 localPosition, Vector3 scale, Color color, Transform parent)
    {
        // 1. Recessed Bezel Frame Well (anchored directly to console root)
        GameObject bezel = CreateBox(label + " Bezel", localPosition + new Vector3(0f, -scale.y * 0.35f, 0f), new Vector3(scale.x + 0.016f, scale.y * 0.45f, scale.z + 0.016f), new Color(0.08f, 0.09f, 0.11f), parent);

        // 2. Button Plunger Keycap (sits prominently above bezel and housing)
        GameObject button = CreateBox(label + " Button", localPosition, scale, color, parent);
        PhishingActionButton actionButton = button.AddComponent<PhishingActionButton>();
        actionButton.Initialize(action, color);

        // 3. Button Label (Rotated 90° on X, positioned cleanly on top surface of plunger; cullBack=true + ZTest LEqual so it is invisible from behind the PC!)
        // Parented to the UNSCALED console root (not the scaled keycap) so the text is never squashed flat.
        // Dark backing plate under the text keeps every label readable on any key colour.
        CreateBox(label + " Label Plate", localPosition + new Vector3(0f, scale.y * 0.5f + 0.001f, 0f), new Vector3(scale.x * 0.92f, 0.002f, scale.z * 0.62f), new Color(0.05f, 0.06f, 0.08f), parent, false);
        CreateLabel(label + " Label", label.ToUpperInvariant(), localPosition + new Vector3(0f, scale.y * 0.5f + 0.004f, 0f), 48, 0.0055f, Color.white, TextAnchor.MiddleCenter, parent, Quaternion.Euler(90f, 0f, 0f), true);
    }

    // =========================================================================
    // 3D Ransomware / Security Breach Modal Popup
    // =========================================================================
    private void BuildBreachAlertModal()
    {
        breachAlertPopup = new GameObject("Breach Alert Popup");
        breachAlertPopup.transform.SetParent(officeContent, false);
        breachAlertPopup.transform.localPosition = new Vector3(0f, 1.34f, 0.95f);

        CreateBox("Breach Frame", Vector3.zero, new Vector3(1.28f, 0.68f, 0.04f), new Color(0.55f, 0.08f, 0.08f), breachAlertPopup.transform);
        CreateBox("Breach Background", new Vector3(0f, 0f, -0.022f), new Vector3(1.22f, 0.62f, 0.005f), new Color(0.12f, 0.02f, 0.02f), breachAlertPopup.transform);
        CreateLabel("Breach Title", "⚠ SECURITY INCIDENT: MALICIOUS PAYLOAD EXECUTED", new Vector3(0f, 0.22f, -0.028f), 26, 0.012f, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleCenter, breachAlertPopup.transform);
        breachAlertText = CreateLabel("Breach Details", "", new Vector3(0f, 0.02f, -0.028f), 22, 0.010f, Color.white, TextAnchor.MiddleCenter, breachAlertPopup.transform);
        CreateLabel("Breach Instruction", "Squeeze trigger on 'Continue' to review threat debrief.", new Vector3(0f, -0.22f, -0.028f), 20, 0.0095f, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, breachAlertPopup.transform);

        breachAlertPopup.SetActive(false);
    }

    // =========================================================================
    // Update Loop: Multi-Modal Controls, Raycast, & Timers
    // =========================================================================
    private void Update()
    {
        HandleKeyboardShortcuts();
        UpdateUrgencyTimerDisplay();
        UpdateLaserPointerRay();
    }

    private void HandleKeyboardShortcuts()
    {
        bool k1 = false, k2 = false, k3 = false, kCont = false, kShuf = false, kRest = false, kRec = false, kSit = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            k1 = Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame;
            k2 = Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame;
            k3 = Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame;
            kCont = Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
            kShuf = Keyboard.current.sKey.wasPressedThisFrame;
            kRest = Keyboard.current.backspaceKey.wasPressedThisFrame;
            kRec = Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.cKey.wasPressedThisFrame;
            kSit = Keyboard.current.kKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        k1 = k1 || Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
        k2 = k2 || Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
        k3 = k3 || Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
        kCont = kCont || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
        kShuf = kShuf || Input.GetKeyDown(KeyCode.S);
        kRest = kRest || Input.GetKeyDown(KeyCode.Backspace);
        kRec = kRec || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.C);
        kSit = kSit || Input.GetKeyDown(KeyCode.K);
#endif
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        bool isWinKDown = (GetAsyncKeyState(0x4B) & 0x8000) != 0;
        if (isWinKDown && !wasWinKeyKDown)
            kSit = true;
        wasWinKeyKDown = isWinKDown;
#endif
        if (kSit || OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch) || OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.RTouch))
        {
            ToggleSitInChair();
        }
        else if (k1)
            Activate(PhishingSimulationAction.ClickLink);
        else if (k2)
            Activate(PhishingSimulationAction.ReportPhishing);
        else if (k3)
            Activate(PhishingSimulationAction.Ignore);
        else if (kCont)
            Activate(PhishingSimulationAction.Continue);
        else if (kShuf)
            Activate(PhishingSimulationAction.Shuffle);
        else if (kRest)
            Activate(PhishingSimulationAction.Restart);
        else if (kRec || OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) || OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch))
            RecenterIfDisplaced(true);
    }

    public void ToggleSitInChair()
    {
        if (headAnchor == null)
            ResolveTrackingReferences();

        var enforcer = GetComponent<WorkstationBoundaryEnforcer>();
        if (enforcer == null)
        {
            enforcer = gameObject.AddComponent<WorkstationBoundaryEnforcer>();
        }
        enforcer.Configure(headAnchor, headAnchor != null ? headAnchor.parent : null, transform.parent);

        bool nextSeated = !enforcer.IsSeatedLocked;
        Vector3 seatedEyeTarget = new Vector3(0f, 1.18f, 0.22f);

        // Align TrackingSpace forward facing directly towards the center monitor when sitting down (without rotating the room!)
        if (nextSeated && headAnchor != null && headAnchor.parent != null)
        {
            Transform trackingSpace = headAnchor.parent;
            float headLocalYaw = headAnchor.localEulerAngles.y;
            float roomYaw = transform.eulerAngles.y;
            trackingSpace.rotation = Quaternion.Euler(0f, roomYaw - headLocalYaw, 0f);
        }

        enforcer.SetSeatedLocked(nextSeated, seatedEyeTarget);

        if (PhishingAudioManager.Instance != null)
            PhishingAudioManager.Instance.PlayButtonClick();

        if (headerText != null)
        {
            headerText.text = nextSeated
                ? "CYBER-DEFENSE WORKSTATION  //  [SEATED IN CHAIR - PRESS 'K' TO STAND]"
                : "CYBER-DEFENSE WORKSTATION  //  SECURE MAIL CLIENT v4.2  [PRESS 'K' TO SIT]";
        }
    }

    private void UpdateUrgencyTimerDisplay()
    {
        if (integration == null || integration.ScenarioManager == null || !integration.ScenarioManager.IsSessionActive)
            return;

        var mgr = integration.ScenarioManager;
        float remaining = mgr.CurrentScenarioRemainingTime;
        float total = mgr.CurrentScenarioTimePressure;

        if (total > 0f)
        {
            int mins = Mathf.FloorToInt(remaining / 60f);
            int secs = Mathf.FloorToInt(remaining % 60f);
            timerHudText.text = $"TIME PRESSURE: {mins:D2}:{secs:D2} / {Mathf.FloorToInt(total / 60f):D2}:{Mathf.FloorToInt(total % 60f):D2}";

            float ratio = Mathf.Clamp01(remaining / total);
            if (timerProgressBar != null)
            {
                float fullWidth = 0.62f;
                float currentWidth = Mathf.Max(0.001f, fullWidth * ratio);
                timerProgressBar.localScale = new Vector3(currentWidth, 0.024f, 0.004f);
                timerProgressBar.localPosition = new Vector3(-fullWidth * 0.5f + currentWidth * 0.5f, 0.29f, -0.031f);
                Color barColor = ratio > 0.4f
                    ? Color.Lerp(new Color(0.82f, 0.64f, 0.32f), new Color(0.38f, 0.62f, 0.85f), (ratio - 0.4f) / 0.6f)
                    : Color.Lerp(new Color(0.82f, 0.35f, 0.35f), new Color(0.82f, 0.64f, 0.32f), ratio / 0.4f);
                SetRendererColor(timerProgressBar.GetComponent<Renderer>(), barColor);
            }
        }
    }

    private void UpdateLaserPointerRay()
    {
        Ray ray;
        bool hasVrRay = false;

        if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.LTouch))
            activeControllerAnchor = leftControllerAnchor != null ? leftControllerAnchor : rightControllerAnchor;
        else if (OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) || OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            activeControllerAnchor = rightControllerAnchor != null ? rightControllerAnchor : leftControllerAnchor;

        if (activeControllerAnchor != null)
        {
            ray = new Ray(activeControllerAnchor.position, activeControllerAnchor.forward);
            hasVrRay = true;
        }
        else if (Camera.main != null)
        {
            Vector2 mPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) mPos = Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            mPos = Input.mousePosition;
#endif
            ray = Camera.main.ScreenPointToRay(mPos);
        }
        else
            return;

        RaycastHit[] hits = Physics.RaycastAll(ray, 6f);
        PhishingActionButton nextHovered = null;
        bool nextHyperlinkHover = false;
        bool nextAlarmHover = false;
        bool nextChairHover = false;
        Vector3 hitPoint = ray.GetPoint(3f);

        RaycastHit closestHit = default;
        float closestDist = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider.isTrigger && hits[i].collider.gameObject.layer == LayerMask.NameToLayer("GazeAOI"))
                continue;
            if (headAnchor != null && hits[i].collider.transform.IsChildOf(headAnchor))
                continue;

            if (hits[i].distance < closestDist)
            {
                closestDist = hits[i].distance;
                closestHit = hits[i];
                foundHit = true;
            }
        }

        if (foundHit)
        {
            hitPoint = closestHit.point;
            PhishingActionButton candidate = closestHit.collider.GetComponent<PhishingActionButton>();
            if (candidate != null && candidate.IsInteractable)
            {
                nextHovered = candidate;
            }
            else if (linkText != null && (closestHit.collider.transform == linkText.transform || closestHit.collider.transform.IsChildOf(linkText.transform)))
            {
                if (decisionButtonsAvailable)
                    nextHyperlinkHover = true;
            }
            else if (emergencyAlarmLever != null && (closestHit.transform == emergencyAlarmLever || closestHit.transform.IsChildOf(emergencyAlarmLever)))
            {
                if (decisionButtonsAvailable)
                    nextAlarmHover = true;
            }
            else if (officeChairTarget != null && (closestHit.transform == officeChairTarget || closestHit.transform.IsChildOf(officeChairTarget)))
            {
                nextChairHover = true;
            }
        }

        // Hover state updates
        if (hoveredButton != nextHovered)
        {
            if (hoveredButton != null)
                hoveredButton.SetHovered(false);
            hoveredButton = nextHovered;
            if (hoveredButton != null)
            {
                hoveredButton.SetHovered(true);
                TriggerHapticPulse(0.2f, 0.05f);
            }
        }

        hoveredHyperlink = nextHyperlinkHover;
        hoveredAlarmLever = nextAlarmHover;
        hoveredOfficeChair = nextChairHover;

        bool isAnyInteractive = hoveredButton != null || hoveredHyperlink || hoveredAlarmLever || hoveredOfficeChair;
        Color pointerColor = isAnyInteractive ? new Color(0.35f, 0.82f, 0.55f) : new Color(0.55f, 0.72f, 0.88f);

        if (pointerLine != null)
        {
            pointerLine.startColor = pointerColor;
            pointerLine.endColor = pointerColor;
            pointerLine.SetPosition(0, ray.origin);
            pointerLine.SetPosition(1, hitPoint);
        }

        if (pointerReticle != null)
        {
            pointerReticle.position = hitPoint;
            pointerReticle.gameObject.SetActive(isAnyInteractive);
        }

        // Trigger input handling
        bool mouseClicked = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null) mouseClicked = Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        mouseClicked = Input.GetMouseButtonDown(0);
#endif
        bool triggerPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch) ||
                              OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                              OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                              OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch) ||
                              mouseClicked;

        if (triggerPressed)
        {
            if (hoveredButton != null)
            {
                TriggerHapticPulse(0.7f, 0.12f);
                hoveredButton.TriggerPressFeedback();
                Activate(hoveredButton.Action);
            }
            else if (hoveredHyperlink)
            {
                TriggerHapticPulse(0.8f, 0.15f);
                if (PhishingAudioManager.Instance != null)
                    PhishingAudioManager.Instance.PlayButtonClick();
                Activate(PhishingSimulationAction.ClickLink);
            }
            else if (hoveredAlarmLever)
            {
                TriggerHapticPulse(0.9f, 0.20f);
                if (PhishingAudioManager.Instance != null)
                    PhishingAudioManager.Instance.PlayThreatAlarm();
                Activate(PhishingSimulationAction.ReportPhishing);
            }
            else if (hoveredOfficeChair)
            {
                TriggerHapticPulse(0.5f, 0.10f);
                ToggleSitInChair();
            }
        }
    }

    private void TriggerHapticPulse(float amplitude, float duration)
    {
        OVRInput.SetControllerVibration(amplitude, amplitude, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(amplitude, amplitude, OVRInput.Controller.LTouch);
    }

    private void Activate(PhishingSimulationAction action)
    {
        if (integration != null)
            integration.Activate(action);
    }

    // =========================================================================
    // Scenario Presentation & Forensic Feedback
    // =========================================================================
    public void ShowScenario(PhishingThreatSimulationManager.PhishingScenarioData scenario, int index, int total)
    {
        if (breachAlertPopup != null)
            breachAlertPopup.SetActive(false);

        senderText.text = WrapText($"From: {scenario.displayName} <{scenario.senderAddress}>", 78, false);

        string attachmentTag = string.IsNullOrEmpty(scenario.attachmentName) ? "None" : scenario.attachmentName;
        securityBadgeText.text = $"To: student@university.edu   |   TLS 1.3 Encrypted   |   Attachment: {attachmentTag}";
        securityBadgeText.color = new Color(0.68f, 0.74f, 0.82f);

        subjectText.text = WrapText($"Subject: {scenario.subjectLine}", 78, false);
        bodyText.text = WrapText(scenario.emailBody, 82, true);
        linkText.text = WrapText($"[ LINK ]: {scenario.rawHyperlinkUrl}", 78, false);
        feedbackText.text = "Inspect the Right Monitor's SMTP Header & URL Telemetry to verify sender authenticity before acting.";
        feedbackText.color = new Color(0.66f, 0.72f, 0.80f);

        LayoutEmailElements();

        ResizeAoiCollider(senderText);
        ResizeAoiCollider(securityBadgeText);
        ResizeAoiCollider(subjectText);
        ResizeAoiCollider(bodyText);
        ResizeAoiCollider(linkText);

        UpdateTelemetryDisplay(index, total);

        string senderDomain = ExtractDomainFromAddress(scenario.senderAddress);
        string linkHost = ExtractHostFromUrl(scenario.rawHyperlinkUrl);
        string returnPath = scenario.hasDomainAnomaly ? $"bounce-relay@{senderDomain}" : scenario.senderAddress;
        string spfStatus = string.Equals(senderDomain, scenario.displayDomain, StringComparison.OrdinalIgnoreCase)
            ? "PASS (aligned)"
            : $"SOFTFAIL (sender {senderDomain} != {scenario.displayDomain})";

        forensicsHudText.text =
            $"SMTP HEADER TELEMETRY:\n" +
            $"Claimed Org:  {scenario.displayDomain}\n" +
            $"From Domain:  {senderDomain}\n" +
            $"Return-Path:  <{returnPath}>\n" +
            $"SPF / DKIM:   {spfStatus}\n" +
            $"--------------------------------\n" +
            $"HYPERLINK INSPECTION:\n" +
            $"Target Host:  {linkHost}\n" +
            $"Full URI:     {WrapText(scenario.rawHyperlinkUrl, 34, false)}\n" +
            $"Attachment:   {attachmentTag}";

        SetDecisionButtonsAvailable(true);
        SetContinueAvailable(false);
    }

    private static string ExtractDomainFromAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
            return "unknown";
        int atIndex = address.IndexOf('@');
        return atIndex >= 0 && atIndex < address.Length - 1 ? address.Substring(atIndex + 1).Trim() : address;
    }

    private static string ExtractHostFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "none";
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri parsed))
            return parsed.Host;
        return url;
    }

    public void ShowDecisionOutcome(
        string action,
        bool correct,
        string classification,
        string correctAction,
        string feedback)
    {
        bool isMaliciousClick = action == "Click Link" && classification != "Legitimate";
        if (isMaliciousClick && breachAlertPopup != null)
        {
            breachAlertPopup.SetActive(true);
            breachAlertText.text = $"Malicious hyperlink executed: credentials compromised!\nExpected Defense: {correctAction}\nReview the forensic intelligence panel.";
            if (PhishingAudioManager.Instance != null)
                PhishingAudioManager.Instance.PlayThreatAlarm();
        }
        else
        {
            if (PhishingAudioManager.Instance != null)
            {
                if (correct)
                    PhishingAudioManager.Instance.PlaySafeChime();
                else
                    PhishingAudioManager.Instance.PlayThreatAlarm();
            }
        }

        string outcomeHeader = correct ? "[ ✓ DECISION ACCURATE ]" : "[ ✗ THREAT ASSESSMENT ERROR ]";
        feedbackText.text = WrapText($"{outcomeHeader} Selected: {action}  |  Classification: {classification}\nRecommended Action: {correctAction}\n{feedback}", 84, true);
        feedbackText.color = correct ? new Color(0.3f, 0.95f, 0.5f) : new Color(1f, 0.35f, 0.35f);

        LayoutEmailElements();

        if (integration != null && integration.ScenarioManager != null)
        {
            var mgr = integration.ScenarioManager;
            UpdateTelemetryDisplay(mgr.CurrentScenarioIndex + 1, mgr.ScenarioCount);
            forensicsHudText.text = $"FORENSIC ANALYSIS:\nOutcome: {(correct ? "CORRECT" : "COMPROMISED")}\nClassification: {classification}\n\nWARNING SIGNS:\n{WrapText(mgr.CurrentScenario.warningSigns, 42, false)}\n\nFEEDBACK:\n{WrapText(feedback, 42, false)}";
        }

        SetDecisionButtonsAvailable(false);
        SetContinueAvailable(true);
    }

    public void ShowSimulationComplete()
    {
        senderText.text = "PHISHING SIMULATION PROTOCOL COMPLETE";
        securityBadgeText.text = "[ 🔒 ALL SCENARIOS EVALUATED ]";
        subjectText.text = "Session Telemetry Successfully Exported";
        bodyText.text = "Comprehensive eye-gaze fixations, threat inspection ratios (TIR), and cognitive bias susceptibility metrics have been committed to telemetry.\n\nSelect 'Shuffle Deck' to replay balanced randomized trials or 'Restart' for sequential order.";
        linkText.text = "";
        feedbackText.text = "Session complete. Thank you for participating in the IVRAR VR Cybersecurity study.";
        LayoutEmailElements();

        if (integration != null && integration.ScenarioManager != null)
        {
            var mgr = integration.ScenarioManager;
            UpdateTelemetryDisplay(mgr.ScenarioCount, mgr.ScenarioCount);
            float accuracy = mgr.completedDecisionsCount > 0 ? (float)mgr.correctClassifications / mgr.completedDecisionsCount * 100f : 0f;
            forensicsHudText.text = $"FINAL SESSION SUMMARY:\nFinal Accuracy: {accuracy:F1}%\nThreats Reported: {mgr.reportingActionsCount}\nMalicious Clicks: {mgr.maliciousClicksCount}\n\nStatus: Telemetry Committed.";
        }

        SetDecisionButtonsAvailable(false);
        SetContinueAvailable(false);
    }

    private void UpdateTelemetryDisplay(int current, int total)
    {
        if (telemetryHudText == null || integration == null || integration.ScenarioManager == null)
            return;

        var mgr = integration.ScenarioManager;
        int completed = mgr.completedDecisionsCount;
        float accuracy = completed > 0 ? (float)mgr.correctClassifications / completed * 100f : 100.0f;
        string accuracyStr = completed > 0 ? $"{accuracy:F1}%" : "100.0% (INITIAL)";

        telemetryHudText.text =
            $"PROGRESS: Scenario {current} of {total}\n" +
            $"--------------------------------\n" +
            $"DETECTION ACCURACY: {accuracyStr}\n" +
            $"THREATS REPORTED:   {mgr.reportingActionsCount}\n" +
            $"MALICIOUS CLICKS:   {mgr.maliciousClicksCount}\n" +
            $"CORRECT RESPONSES:  {mgr.correctClassifications}\n" +
            $"DECISIONS LOGGED:   {completed}\n" +
            $"SESSION TIMER:      {mgr.CurrentScenarioElapsedTime:F1}s\n" +
            $"--------------------------------\n" +
            $"EYE-GAZE TRACKER:   ACTIVE (I-DT)\n" +
            $"AOI REGISTRY:       5 ACTIVE ZONES";
    }

    private void LayoutEmailElements()
    {
        float currentY = 1.68f;
        senderText.transform.localPosition = new Vector3(TextLeftX, currentY, TextZ);
        currentY -= GetTextBlockHeight(senderText) + 0.018f;

        securityBadgeText.transform.localPosition = new Vector3(TextLeftX, currentY, TextZ);
        currentY -= GetTextBlockHeight(securityBadgeText) + 0.020f;

        subjectText.transform.localPosition = new Vector3(TextLeftX, currentY, TextZ);
        currentY -= GetTextBlockHeight(subjectText) + 0.022f;

        bodyText.transform.localPosition = new Vector3(TextLeftX, currentY, TextZ);
        currentY -= GetTextBlockHeight(bodyText) + 0.024f;

        float linkY = Mathf.Min(currentY, 1.16f);
        linkY = Mathf.Max(linkY, 1.04f);
        linkText.transform.localPosition = new Vector3(TextLeftX, linkY, TextZ);

        float feedbackY = linkY - GetTextBlockHeight(linkText) - 0.018f;
        feedbackY = Mathf.Max(feedbackY, 0.94f);
        feedbackText.transform.localPosition = new Vector3(TextLeftX, feedbackY, TextZ);
    }

    private static float GetTextBlockHeight(TextMesh text)
    {
        if (text == null || string.IsNullOrEmpty(text.text))
            return 0f;

        int lines = text.text.Split('\n').Length;
        float lineHeight = text.fontSize * text.characterSize * 0.105f;
        return lines * lineHeight;
    }

    private static string WrapText(string input, int maxCharsPerLine, bool compactBlankLines)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        string normalized = input.Replace("\r\n", "\n").Replace('\r', '\n');
        if (compactBlankLines)
        {
            while (normalized.Contains("\n\n"))
                normalized = normalized.Replace("\n\n", "\n");
        }

        string[] paragraphs = normalized.Split('\n');
        StringBuilder sb = new StringBuilder();

        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (p > 0)
                sb.Append('\n');

            string paragraph = paragraphs[p].Trim();
            if (paragraph.Length <= maxCharsPerLine)
            {
                sb.Append(paragraph);
                continue;
            }

            string[] words = paragraph.Split(' ');
            int currentLineLength = 0;

            for (int w = 0; w < words.Length; w++)
            {
                string word = words[w];
                if (string.IsNullOrEmpty(word))
                    continue;

                if (currentLineLength == 0)
                {
                    sb.Append(word);
                    currentLineLength = word.Length;
                }
                else if (currentLineLength + 1 + word.Length <= maxCharsPerLine)
                {
                    sb.Append(' ').Append(word);
                    currentLineLength += 1 + word.Length;
                }
                else
                {
                    sb.Append('\n').Append(word);
                    currentLineLength = word.Length;
                }
            }
        }

        return sb.ToString();
    }

    private void SetDecisionButtonsAvailable(bool available)
    {
        decisionButtonsAvailable = available;
        PhishingActionButton[] buttons = officeContent.GetComponentsInChildren<PhishingActionButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            PhishingSimulationAction action = buttons[i].Action;
            if (action == PhishingSimulationAction.ClickLink ||
                action == PhishingSimulationAction.ReportPhishing ||
                action == PhishingSimulationAction.Ignore)
            {
                buttons[i].SetInteractable(available);
            }
            else if (action == PhishingSimulationAction.Shuffle || action == PhishingSimulationAction.Restart)
            {
                // Administrative buttons remain accessible so participant can reset or replay
                buttons[i].SetInteractable(true);
            }
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

    // =========================================================================
    // Factory Helpers with Clean URP Materials & Textures
    // =========================================================================
    private GameObject CreateBox(string objectName, Vector3 localPosition, Vector3 localScale, Color color, Transform parent, bool enableCollider = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = localScale;

        if (!enableCollider)
        {
            Collider col = box.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);

        Renderer rend = box.GetComponent<Renderer>();
        if (rend != null)
        {
            if (Application.isPlaying)
                rend.material = mat;
            else
                rend.sharedMaterial = mat;
            SetRendererColor(rend, color);
        }

        return box;
    }

    private GameObject CreateTexturedBox(string objectName, Vector3 localPosition, Vector3 localScale, Color color, Texture2D texture, Vector2 tiling, Transform parent)
    {
        GameObject box = CreateBox(objectName, localPosition, localScale, color, parent);
        Renderer rend = box.GetComponent<Renderer>();
        if (rend != null && texture != null)
        {
            Material mat = Application.isPlaying ? rend.material : rend.sharedMaterial;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", texture);
                    mat.SetTextureScale("_BaseMap", tiling);
                }
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", texture);
                    mat.SetTextureScale("_MainTex", tiling);
                }
            }
        }
        return box;
    }

    private TextMesh CreateLabel(
        string objectName,
        string value,
        Vector3 localPosition,
        int fontSize,
        float characterSize,
        Color color,
        TextAnchor anchor,
        Transform parent = null,
        Quaternion? localRotation = null,
        bool cullBack = true)
    {
        GameObject label = new GameObject(objectName);
        label.transform.SetParent(parent != null ? parent : officeContent, false);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = localRotation.HasValue ? localRotation.Value : Quaternion.identity;
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = value;
        text.fontSize = fontSize;
        text.characterSize = characterSize;
        text.anchor = anchor;
        text.alignment = anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
        text.color = color;
        text.richText = false;

        // Requirement 3: Depth-tested & Backface-culled 3D Text
        // ALWAYS apply GUI/3D Text Shader Cull Back so ZTest LEqual prevents text from bleeding through solid monitors/walls!
        Shader cullShader = Shader.Find("GUI/3D Text Shader Cull Back");
        MeshRenderer rend = label.GetComponent<MeshRenderer>();
        if (cullShader != null && rend != null && text.font != null && text.font.material != null)
        {
            Material textMat = new Material(cullShader);
            textMat.mainTexture = text.font.material.mainTexture;
            textMat.color = color;
            if (Application.isPlaying)
                rend.material = textMat;
            else
                rend.sharedMaterial = textMat;
        }

        return text;
    }

    private TextMesh CreateAoiLabel(
        string objectName,
        string value,
        Vector3 localPosition,
        int fontSize,
        float characterSize,
        Color color,
        AOIType aoiType,
        string aoiId,
        int priority)
    {
        TextMesh text = CreateLabel(objectName, value, localPosition, fontSize, characterSize, color, TextAnchor.UpperLeft);
        int aoiLayer = LayerMask.NameToLayer("GazeAOI");
        if (aoiLayer < 0)
        {
            Debug.LogError("Cannot create gaze AOI target '" + aoiId + "': the GazeAOI layer is not configured.");
            return text;
        }

        GameObject targetObject = new GameObject(objectName + " AOI");
        targetObject.layer = aoiLayer;
        targetObject.transform.SetParent(text.transform, false);
        BoxCollider collider = targetObject.AddComponent<BoxCollider>();
        GazeAOITarget target = targetObject.AddComponent<GazeAOITarget>();
        target.Configure(aoiType, aoiId, priority, "active_email");
        ResizeAoiCollider(text);
        return text;
    }

    private static void ResizeAoiCollider(TextMesh text)
    {
        Transform targetTransform = text.transform.Find(text.gameObject.name + " AOI");
        BoxCollider collider = targetTransform != null ? targetTransform.GetComponent<BoxCollider>() : null;
        if (collider == null)
            return;

        string[] lines = text.text.Split('\n');
        int maxLineChars = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > maxLineChars)
                maxLineChars = lines[i].Length;
        }

        float width = Mathf.Clamp(maxLineChars * text.characterSize * text.fontSize * 0.036f, 0.25f, 1.42f);
        float height = Mathf.Max(text.characterSize * text.fontSize * 0.105f * lines.Length, 0.035f);

        collider.size = new Vector3(width, height, 0.025f);
        collider.center = new Vector3(width * 0.5f, -height * 0.5f, 0f);
    }

    private void CreatePointerLine()
    {
        Transform existingLine = transform.Find("Phishing Simulation Pointer");
        GameObject lineObject = existingLine != null ? existingLine.gameObject : new GameObject("Phishing Simulation Pointer");
        if (existingLine == null)
            lineObject.transform.SetParent(transform, false);

        pointerLine = lineObject.GetComponent<LineRenderer>();
        if (pointerLine == null)
            pointerLine = lineObject.AddComponent<LineRenderer>();

        pointerLine.useWorldSpace = true;
        pointerLine.positionCount = 2;
        pointerLine.startWidth = 0.005f;
        pointerLine.endWidth = 0.002f;
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

        Transform existingReticle = transform.Find("Pointer Reticle");
        GameObject reticleObj = existingReticle != null ? existingReticle.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        reticleObj.name = "Pointer Reticle";
        if (existingReticle == null)
            reticleObj.transform.SetParent(transform, false);

        pointerReticle = reticleObj.transform;
        pointerReticle.localScale = new Vector3(0.016f, 0.016f, 0.016f);
        Collider retCol = reticleObj.GetComponent<Collider>();
        if (retCol != null)
            retCol.enabled = false;

        Material reticleMat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        reticleMat.color = new Color(0.2f, 0.95f, 0.45f);
        reticleObj.GetComponent<Renderer>().material = reticleMat;
        reticleObj.SetActive(false);
    }

    private static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }
}
