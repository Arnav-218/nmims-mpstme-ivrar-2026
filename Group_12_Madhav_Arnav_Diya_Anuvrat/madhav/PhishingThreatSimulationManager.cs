using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Core state machine and scenario controller for the Immersive VR Phishing Simulation.
    /// Manages randomized email and spear-phishing attack vectors, cognitive bias cues,
    /// and participant interaction telemetry.
    /// 
    /// Student Roles:
    /// - D021 Madhav Gaonkar (Phishing Threat Modeling Lead): Heuristic bias cue formulation and threat matrix
    /// - D030 Arnav Jain (XR Systems Architect): Virtual office interaction and immersive spatial UI rendering
    /// </summary>
    public class PhishingThreatSimulationManager : MonoBehaviour
    {
        public enum CognitiveBiasType
        {
            AuthorityBias,      // Impersonating CEO, IT Director, or Provost
            UrgencyScarcity,    // Immediate account suspension countdown
            FamiliarityTrust,   // Known vendor invoice or campus portal clone
            SocialProofReward   // Department-wide bonus or mandatory compliance award
        }

        public enum ThreatClassification
        {
            Legitimate,
            DeceptivePhish,
            SpearPhishTargeted
        }

        [System.Serializable]
public struct PhishingScenarioData
{
    public string scenarioId;

    public ThreatClassification classification;
    public CognitiveBiasType biasType;

    public string difficulty;
    public string context;

    public string displayName;
    public string senderAddress;
    public string displayDomain;
    public string rawHyperlinkUrl;

    public string subjectLine;

    [TextArea(3, 6)]
    public string emailBody;

    [TextArea(3, 6)]
    public string warningSigns;

    public bool hasGrammaticalAnomalies;
    public bool hasDomainAnomaly;

    public string attachmentName;

    public float timePressureSeconds;

    public string correctAction;

    [TextArea(3, 6)]
    public string correctiveFeedback;
}

        [Header("Simulation State Configuration")]
        [SerializeField] private List<PhishingScenarioData> scenarioDeck = new List<PhishingScenarioData>();
        [SerializeField] private int currentScenarioIndex = 0;
        [SerializeField] private float sessionTimer = 0f;
        [SerializeField] private float scenarioTimer = 0f;
        [SerializeField] private bool isSessionActive = false;

        [Header("Telemetry Counters")]
        public int totalScenariosPresented = 0;
        public int correctClassifications = 0;
        public int maliciousClicksCount = 0;
        public int reportingActionsCount = 0;

        public event Action<string, bool, float> OnDecisionRecorded;
        public event Action<string, string> OnCorrectiveFeedback;

        private void Start()
        {
            InitializeDefaultScenarios();
        }

        private void Update()
{
    if (isSessionActive)
    {
        sessionTimer += Time.deltaTime;
        scenarioTimer += Time.deltaTime;
    }
}

       private void InitializeDefaultScenarios()
{
    scenarioDeck.Clear();

    // ============================================================
    // S01 - Urgency/Scarcity - Deceptive Phish - Easy
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S01",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.UrgencyScarcity,
        difficulty = "Easy",
        context = "Student receives an urgent university account verification email.",
        displayName = "University IT Security",
        senderAddress = "security-admin@university-portal-verify.com",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-portal-verify.com/auth",
        subjectLine = "URGENT: Mandatory Account Verification Required Within 2 Hours",
        emailBody = "Your university account has been flagged due to unusual login activity. To prevent suspension of your campus services, you must verify your account immediately.\n\nFailure to complete verification within 2 hours may result in temporary loss of access to university email, learning resources, and other campus services.\n\nPlease use the verification link below to confirm your account.",
        warningSigns = "Strong time pressure; sender domain mismatch; deceptive displayed domain; immediate verification request; threat of losing access.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 120f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message uses urgency and fear of account suspension to pressure the recipient. The sender domain and actual link do not belong to the legitimate university domain."
    });

    // ============================================================
    // S02 - Urgency/Scarcity - Deceptive Phish - Hard
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S02",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.UrgencyScarcity,
        difficulty = "Hard",
        context = "Student receives a registration verification request near a deadline.",
        displayName = "Student Services",
        senderAddress = "studentservices@university-registrar.com",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-registrar.com/verify-registration",
        subjectLine = "Action Required: Complete Registration Verification",
        emailBody = "Dear Student,\n\nAs part of the current registration process, an additional verification step is required for selected student accounts.\n\nOur records indicate that your registration requires verification before the current registration window closes. Please review your account and complete the verification process using the link below.\n\nIf verification is not completed before the registration window closes, your registration may require additional processing.\n\nStudent Services\nUniversity Administration",
        warningSigns = "Sender domain differs from legitimate university domain; deadline pressure; unfamiliar verification link; plausible administrative context; displayed domain does not match destination.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 300f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message appears credible but the sender and destination domains do not match the official university domain. Administrative requests should be independently verified."
    });

    // ============================================================
    // S03 - Urgency/Scarcity - Legitimate - Medium
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S03",
        classification = ThreatClassification.Legitimate,
        biasType = CognitiveBiasType.UrgencyScarcity,
        difficulty = "Medium",
        context = "Genuine university IT maintenance notification.",
        displayName = "University IT Service Desk",
        senderAddress = "servicedesk@university.edu",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://servicedesk.university.edu/maintenance",
        subjectLine = "Scheduled Campus Network Maintenance - Sunday 02:00 AM",
        emailBody = "Dear Student,\n\nThe University IT Service Desk will perform scheduled network maintenance on Sunday between 02:00 AM and 04:00 AM.\n\nDuring this period, some university online services may be temporarily unavailable.\n\nNo action is required from students. You do not need to provide your password or account credentials.\n\nFor further information, please visit the IT Service Desk maintenance page through the university's official service portal.\n\nUniversity IT Service Desk",
        warningSigns = "None.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = false,
        attachmentName = "",
        timePressureSeconds = 300f,
        correctAction = "Ignore / Read Without Reporting",
        correctiveFeedback = "This was a legitimate university notification. The sender and destination use the official university domain and no credentials are requested."
    });

    // ============================================================
    // S04 - Authority Bias - Spear Phish - Medium
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S04",
        classification = ThreatClassification.SpearPhishTargeted,
        biasType = CognitiveBiasType.AuthorityBias,
        difficulty = "Medium",
        context = "Email impersonates a senior university IT administrator.",
        displayName = "Director of Information Technology",
        senderAddress = "it-director@university-itservices.com",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-itservices.com/security-review",
        subjectLine = "Security Review: Student Account Confirmation Required",
        emailBody = "Dear Student,\n\nAs part of a routine university security review, selected student accounts have been identified for confirmation.\n\nPlease complete the account confirmation process using the secure link below. This request has been issued by the Office of Information Technology.\n\nPlease complete the review at your earliest convenience.\n\nRegards,\nDirector of Information Technology\nUniversity Information Technology Services",
        warningSigns = "Sender domain mismatch; authority-based display name; unfamiliar account-confirmation link; reliance on senior role.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 180f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message attempts to exploit authority bias. The sender and destination domains should be independently verified rather than trusted because of the claimed position."
    });

    // ============================================================
    // S05 - Authority Bias - Spear Phish - Hard
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S05",
        classification = ThreatClassification.SpearPhishTargeted,
        biasType = CognitiveBiasType.AuthorityBias,
        difficulty = "Hard",
        context = "Senior university administrator impersonation involving a confidential academic review.",
        displayName = "Office of the Dean",
        senderAddress = "dean-office@university-academics.org",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-academics.org/review/document",
        subjectLine = "Confidential: Academic Review Document",
        emailBody = "Dear Student,\n\nThe Office of the Dean is currently conducting a confidential review of selected academic records.\n\nYou have been included in the review process. Please access the confidential document using the secure link below and review the information before the academic review meeting.\n\nPlease do not forward this message as the contents are intended only for the recipient.\n\nOffice of the Dean\nUniversity Administration",
        warningSigns = "Sender domain mismatch; authority cue; confidentiality discourages verification; unfamiliar destination; independent confirmation is required.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 600f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message uses authority and confidentiality to reduce verification. A request from a senior official should still be independently verified, especially when sensitive information is involved."
    });

    // ============================================================
    // S06 - Authority Bias - Legitimate - Medium
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S06",
        classification = ThreatClassification.Legitimate,
        biasType = CognitiveBiasType.AuthorityBias,
        difficulty = "Medium",
        context = "Genuine academic announcement from the Office of the Dean.",
        displayName = "Office of the Dean",
        senderAddress = "dean.office@university.edu",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university.edu/academics/review-meeting",
        subjectLine = "Academic Review Meeting - Student Information",
        emailBody = "Dear Student,\n\nThe Office of the Dean will be conducting an academic review meeting next week.\n\nStudents are requested to review the meeting information available on the official university academic portal.\n\nNo password or personal account information is required.\n\nIf you have questions regarding the meeting, please contact the Academic Office through the contact details listed on the official university website.\n\nOffice of the Dean\nUniversity",
        warningSigns = "None.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = false,
        attachmentName = "",
        timePressureSeconds = 300f,
        correctAction = "Ignore / Read Without Reporting",
        correctiveFeedback = "This was a legitimate communication from the university. The sender and destination use the official university domain and no sensitive information is requested."
    });

    // ============================================================
    // S07 - Familiarity/Trust - Deceptive Phish - Easy
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S07",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.FamiliarityTrust,
        difficulty = "Easy",
        context = "Email impersonates a familiar technology vendor.",
        displayName = "Microsoft 365 Support",
        senderAddxress = "support@microsoft-365-security-help.com",
        displayDomain = "microsoft.com",
        rawHyperlinkUrl = "https://microsoft-365-security-help.com/verify",
        subjectLine = "Microsoft 365 Account Security Notification",
        emailBody = "Dear User,\n\nWe detected unusual activity associated with your Microsoft 365 account.\n\nPlease review your account security information using the link below to ensure that your account remains active.\n\nIf you do not recognize the recent activity, we recommend completing the security verification as soon as possible.\n\nMicrosoft 365 Support",
        warningSigns = "Sender domain does not match legitimate Microsoft domain; familiar branding; unfamiliar verification link.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 300f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message exploits familiarity with a commonly used technology service. Familiarity should not replace checking the actual sender and destination domains."
    });

    // ============================================================
    // S08 - Familiarity/Trust - Deceptive Phish - Hard
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S08",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.FamiliarityTrust,
        difficulty = "Hard",
        context = "Email impersonates the university student portal using deceptive domain structure.",
        displayName = "University Student Portal",
        senderAddress = "studentportal@university.edu.accounts-review.com",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university.edu.accounts-review.com/student/login",
        subjectLine = "New Notification Available in Your Student Portal",
        emailBody = "Dear Student,\n\nA new notification has been added to your student portal regarding your account.\n\nPlease sign in to your student portal using the link below to review the notification.\n\nIf you have recently accessed your account, no further action may be required.\n\nUniversity Student Portal",
        warningSigns = "University name appears inside a deceptive domain; destination is not the official university domain; familiar branding; sign-in request.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 600f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message exploits familiarity with the university portal. The important distinction is between a domain containing the university name and the university's actual official domain."
    });

    // ============================================================
    // S09 - Familiarity/Trust - Legitimate - Hard
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S09",
        classification = ThreatClassification.Legitimate,
        biasType = CognitiveBiasType.FamiliarityTrust,
        difficulty = "Hard",
        context = "Genuine student portal notification.",
        displayName = "University Student Portal",
        senderAddress = "studentportal@university.edu",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university.edu/student-portal/timetable",
        subjectLine = "Your Academic Timetable Has Been Updated",
        emailBody = "Dear Student,\n\nYour academic timetable has been updated for the upcoming academic period.\n\nPlease sign in through the official University Student Portal to view your latest timetable.\n\nFor security, the University will never ask you to provide your password by email.\n\nUniversity Student Portal",
        warningSigns = "None.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = false,
        attachmentName = "",
        timePressureSeconds = 600f,
        correctAction = "Ignore / Read Without Reporting",
        correctiveFeedback = "This was a legitimate university notification. The sender uses the official university domain and the destination is hosted on the same official domain."
    });

    // ============================================================
    // S10 - Social Proof/Reward - Deceptive Phish - Medium
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S10",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.SocialProofReward,
        difficulty = "Medium",
        context = "Email claims the student's department has been selected for a recognition program.",
        displayName = "University Student Engagement Team",
        senderAddress = "student-engagement@university-rewards.com",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-rewards.com/student-verification",
        subjectLine = "Your Department Has Been Selected - Student Recognition Program",
        emailBody = "Dear Student,\n\nYour department has been selected to participate in this year's University Student Recognition Program.\n\nStudents in participating departments are being asked to complete a short verification form to confirm their eligibility.\n\nSeveral students in your department have already completed the process.\n\nPlease complete your verification using the link below so that your participation can be recorded.\n\nStudent Engagement Team\nUniversity",
        warningSigns = "Sender domain mismatch; social proof claim; potential reward; unfamiliar verification link.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 300f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message uses social proof and a potential reward to create trust. These cues should not replace verification of the sender and destination domain."
    });

    // ============================================================
    // S11 - Social Proof/Reward - Deceptive Phish - Hard
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S11",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.SocialProofReward,
        difficulty = "Hard",
        context = "Email claims most students have completed a mandatory cybersecurity compliance check.",
        displayName = "University Compliance Office",
        senderAddress = "compliance@university-compliance.org",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university-compliance.org/verification",
        subjectLine = "Student Compliance Check - Action Required",
        emailBody = "Dear Student,\n\nAs part of the current university cybersecurity compliance process, students in your department are being asked to complete a short account verification check.\n\nThe majority of students in your department have already completed the verification.\n\nPlease complete the remaining verification step using the link below so that your compliance status can be updated.\n\nUniversity Compliance Office",
        warningSigns = "Sender domain mismatch; majority-completed claim; compliance pressure; unfamiliar destination; conformity pressure.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = true,
        attachmentName = "",
        timePressureSeconds = 600f,
        correctAction = "Report Phish",
        correctiveFeedback = "The message uses social proof and compliance pressure. The claimed participation of others does not establish that the message is genuine."
    });

    // ============================================================
    // S12 - Social Proof/Reward - Legitimate - Medium
    // ============================================================
    scenarioDeck.Add(new PhishingScenarioData
    {
        scenarioId = "S12",
        classification = ThreatClassification.Legitimate,
        biasType = CognitiveBiasType.SocialProofReward,
        difficulty = "Medium",
        context = "Genuine university student recognition announcement.",
        displayName = "University Student Engagement Office",
        senderAddress = "studentengagement@university.edu",
        displayDomain = "university.edu",
        rawHyperlinkUrl = "https://university.edu/student-engagement/recognition",
        subjectLine = "Student Recognition Program - Participation Information",
        emailBody = "Dear Student,\n\nThe University Student Engagement Office is pleased to announce this semester's Student Recognition Program.\n\nStudents may review the program information and participation requirements through the official university student portal.\n\nParticipation is optional. No password or sensitive account information is requested by email.\n\nFor full details, please visit the Student Engagement section of the official university website.\n\nUniversity Student Engagement Office",
        warningSigns = "None.",
        hasGrammaticalAnomalies = false,
        hasDomainAnomaly = false,
        attachmentName = "",
        timePressureSeconds = 600f,
        correctAction = "Ignore / Read Without Reporting",
        correctiveFeedback = "This was a legitimate university announcement. The official university domain can be independently verified and no sensitive information is requested."
    });
}
        public void BeginSimulation()
        {
            currentScenarioIndex = 0;
            totalScenariosPresented = 0;
            correctClassifications = 0;
            maliciousClicksCount = 0;
            reportingActionsCount = 0;
            sessionTimer = 0f;
            isSessionActive = true;
            PresentScenario(currentScenarioIndex);
        }

        private void PresentScenario(int index)
        {
            if (index < 0 || index >= scenarioDeck.Count)
            {
                EndSimulation();
                return;
            }

            PhishingScenarioData current = scenarioDeck[index];
            scenarioTimer = 0f;
            totalScenariosPresented++;

            // =========================================================================
            // TODO [D030 - Arnav Jain - XR Systems Architect]:
            // 1. Render current.subjectLine and current.emailBody onto the floating 3D virtual office workstation canvas.
            // 2. Bind current.rawHyperlinkUrl to the interactive clickable element with hover raycast detection.
            // 3. Initiate spatial countdown visual timer if current.timePressureSeconds is constrained.
            // =========================================================================

            Debug.Log($"[PhishingSimulation] Scenario {current.scenarioId} presented. Bias: {current.biasType}");
        }

        public void SubmitUserAction(bool didClickLink, bool didReportPhish, bool didIgnore)
        {
            if (!isSessionActive || currentScenarioIndex >= scenarioDeck.Count) return;

            PhishingScenarioData current = scenarioDeck[currentScenarioIndex];
            bool isCorrect = false;

            if (current.classification == ThreatClassification.Legitimate)
            {
                if (!didReportPhish && (didClickLink || didIgnore))
                {
                    isCorrect = true;
                    correctClassifications++;
                }
            }
            else
            {
                if (didClickLink)
                {
                    maliciousClicksCount++;
                    isCorrect = false;
                }
                else if (didReportPhish)
                {
                    correctClassifications++;
                    reportingActionsCount++;
                    isCorrect = true;
                }
            }

            // =========================================================================
// D021 - Madhav Gaonkar - Phishing Threat Modeling Lead
// Threat-model telemetry and susceptibility scoring
// =========================================================================

float reactionLatency = scenarioTimer;

float cognitiveSusceptibility = 0f;

if (current.classification != ThreatClassification.Legitimate)
{
    if (didClickLink)
    {
        cognitiveSusceptibility = 1f;
    }
    else if (!didReportPhish)
    {
        cognitiveSusceptibility = 0.5f;
    }
}

if (isCorrect && didReportPhish)
{
    cognitiveSusceptibility = 0f;
}

float domainAnomalyScore = 0f;

// At this stage, actual URL inspection is not yet available.
// The XR interaction system will provide this telemetry later.
if (!current.hasDomainAnomaly)
{
    domainAnomalyScore = 1f;
}

Debug.Log(
    $"[ThreatModel] " +
    $"Scenario={current.scenarioId} | " +
    $"Bias={current.biasType} | " +
    $"Difficulty={current.difficulty} | " +
    $"Correct={isCorrect} | " +
    $"Clicked={didClickLink} | " +
    $"Reported={didReportPhish} | " +
    $"Ignored={didIgnore} | " +
    $"ReactionTime={reactionLatency:F2}s | " +
    $"Susceptibility={cognitiveSusceptibility:F2} | " +
    $"DomainAnomalyScore={domainAnomalyScore:F2}"
);

if (!isCorrect && current.classification != ThreatClassification.Legitimate)
{
    Debug.Log(
        $"[CorrectiveFeedback] {current.scenarioId}: " +
        $"{current.correctiveFeedback}"
    );

    // The XR/UI layer can later subscribe to this event
    // and display the corrective feedback to the participant.
    OnCorrectiveFeedback?.Invoke(
        current.scenarioId,
        current.correctiveFeedback
    );
}
            OnDecisionRecorded?.Invoke(current.scenarioId, isCorrect, sessionTimer);

            currentScenarioIndex++;
            PresentScenario(currentScenarioIndex);
        }

        private void EndSimulation()
        {
            isSessionActive = false;
            float accuracy = totalScenariosPresented > 0 ? (float)correctClassifications / totalScenariosPresented * 100f : 0f;
            Debug.Log($"[PhishingSimulation] Completed. Accuracy: {accuracy:F1}% | Malicious Clicks: {maliciousClicksCount} | Reports: {reportingActionsCount}");
        }
    }
}