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
        [SerializeField] private List<PhishingScenarioData> masterScenarioPool = new List<PhishingScenarioData>();
        [SerializeField] private List<PhishingScenarioData> scenarioDeck = new List<PhishingScenarioData>();
        [SerializeField] private int currentScenarioIndex = 0;
        [SerializeField] private float sessionTimer = 0f;
        [SerializeField] private float scenarioTimer = 0f;
        [SerializeField] private bool isSessionActive = false;
        private bool decisionSubmitted;
        private int presentationSequence;
        private string currentTrialId = string.Empty;

        [Header("Telemetry Counters")]
        public int totalScenariosPresented = 0;
        public int completedDecisionsCount = 0;
        public int correctClassifications = 0;
        public int maliciousClicksCount = 0;
        public int reportingActionsCount = 0;

        public event Action<string, bool, float> OnDecisionRecorded;
        public event Action<string, string> OnCorrectiveFeedback;
        public event Action<PhishingScenarioData, string, int, int> OnScenarioPresented;
        public event Action<PhishingScenarioData, string, bool, float, string> OnDecisionAccepted;
        public event Action OnSimulationRestarting;

        public bool IsSessionActive => isSessionActive;
        public int CurrentScenarioIndex => currentScenarioIndex;
        public int ScenarioCount => scenarioDeck.Count;
        public string CurrentTrialId => currentTrialId;
        public PhishingScenarioData CurrentScenario =>
            currentScenarioIndex >= 0 && currentScenarioIndex < scenarioDeck.Count
                ? scenarioDeck[currentScenarioIndex]
                : default;

        private void Awake()
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
    masterScenarioPool.Clear();

    // ============================================================
    // S01 - Urgency/Scarcity - Deceptive Phish - Easy
    // ============================================================
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
    {
        scenarioId = "S07",
        classification = ThreatClassification.DeceptivePhish,
        biasType = CognitiveBiasType.FamiliarityTrust,
        difficulty = "Easy",
        context = "Email impersonates a familiar technology vendor.",
        displayName = "Microsoft 365 Support",
        senderAddress = "support@microsoft-365-security-help.com",
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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
    masterScenarioPool.Add(new PhishingScenarioData
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

            // ============================================================
            // S13 - Authority Bias - Spear Phish Targeted - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S13",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Executive CFO urgent wire authorization override targeting accounting personnel.",
                displayName = "Victoria Sterling - Chief Financial Officer",
                senderAddress = "v.sterling@corp-finance-executives.com",
                displayDomain = "corp-global.com",
                rawHyperlinkUrl = "https://corp-finance-executives.com/portal/wire-release",
                subjectLine = "STRICTLY CONFIDENTIAL: Immediate Acquisition Wire Authorization",
                emailBody = "Good afternoon,\n\nUnder NDA, we are closing project Apex ahead of schedule today. The escrow deposit requires your immediate sign-off on the wire release form.\n\nDue to audit restrictions, do not process this through standard ticketing. Review and authorize the schedule via our executive escrow portal below within 45 minutes.\n\nVictoria Sterling\nChief Financial Officer",
                warningSigns = "Lookalike external executive domain (corp-finance-executives.com); bypass of formal ticketing policy; extreme authority pressure and secrecy.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Wire_Schedule_Apex_Signed.pdf.exe",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Executive impersonations exploit authority and artificial secrecy to bypass standard financial validation controls. Always independently verify wire instructions via out-of-band communication."
            });

            // ============================================================
            // S14 - Urgency/Scarcity - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S14",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Homograph domain attack impersonating Microsoft 365 identity verification.",
                displayName = "Microsoft 365 Security Operations",
                senderAddress = "security-noreply@rnicrosoft-cloud.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://rnicrosoft-cloud.com/auth/mfa-restore",
                subjectLine = "Critical Security Alert: Suspicious MFA Token Revocation Detected",
                emailBody = "Dear Subscriber,\n\nMultiple anomalous login attempts from foreign IP ranges were blocked on your Microsoft 365 tenant. Your Multi-Factor Authentication session token is scheduled for permanent revocation in 30 minutes.\n\nTo prevent workforce disruption and maintain access to email and SharePoint, re-authenticate your cryptographic identity profile immediately.\n\nMicrosoft Security Engineering",
                warningSigns = "Subtle visual homograph spoofing ('rn' resembles 'm' in standard sans-serif typography); non-Microsoft domain (rnicrosoft-cloud.com); acute 30-minute deadline.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Homograph and typo-squatting attacks rely on hasty scanning of the sender domain. Carefully inspect letterforms like 'rn' vs 'm' and verify certificate root authorities."
            });

            // ============================================================
            // S15 - Familiarity/Trust - Spear Phish Targeted - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S15",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Medium",
                context = "Targeted software developer lure impersonating GitHub Enterprise SSH audit.",
                displayName = "GitHub Enterprise Security",
                senderAddress = "audit-bot@github-enterprise-compliance.io",
                displayDomain = "github.com",
                rawHyperlinkUrl = "https://github-enterprise-compliance.io/keys/renew",
                subjectLine = "Action Required: Deprecated SSH Key Audit for Internal Organization",
                emailBody = "Hello Developer,\n\nAs part of our Q4 Cryptographic Key Hygiene audit, older ED25519 and RSA keys must be re-certified against our organization's updated security baseline.\n\nYour active SSH signing key has been flagged for deprecation. Please review and rotate your public credentials through the compliance portal to avoid repository push restrictions.\n\nGitHub Enterprise Operations",
                warningSigns = "Domain github-enterprise-compliance.io is an external registration; GitHub uses internal organizational settings for SSH key lifecycle management.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Report Phish",
                correctiveFeedback = "Developers are frequently targeted with supply-chain and credential audit lures. Genuine repository platforms notify users in-app or via authenticated organizational dashboards."
            });

            // ============================================================
            // S16 - Urgency/Scarcity - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S16",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Medium",
                context = "Legitimate campus IT infrastructure maintenance notification.",
                displayName = "Campus Information Technology Services",
                senderAddress = "it-announcements@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://it.university.edu/outage-calendar/q4-maintenance",
                subjectLine = "NOTICE: Emergency Core Switch Maintenance - Saturday 02:00 UTC",
                emailBody = "Dear University Community,\n\nNetwork Operations will perform urgent firmware updates on the campus gateway routers this Saturday from 02:00 to 04:00 UTC. Campus WiFi, VPN, and on-premises portal services will be momentarily disrupted.\n\nNo action is required on your computer or account. Cloud-hosted email and LMS will remain accessible.\n\nFor real-time service status, bookmark our central status page.\n\nCampus IT Infrastructure Operations",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate maintenance announcement. The message originates from the valid university domain, makes no credential or software download requests, and provides transparent advisory information."
            });

            // ============================================================
            // S17 - Social Proof/Reward - Deceptive Phish - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S17",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Easy",
                context = "Corporate wellness spot bonus lure offering Amazon gift cards.",
                displayName = "Corporate Employee Perks Program",
                senderAddress = "wellness-rewards@employee-appreciation-portal.net",
                displayDomain = "company.com",
                rawHyperlinkUrl = "https://employee-appreciation-portal.net/claim?bonus=250",
                subjectLine = "Congratulations! Claim Your $250 Quarterly Wellness Allowance",
                emailBody = "Dear Team Member,\n\nIn recognition of outstanding organizational performance this quarter, Executive Leadership has authorized an exclusive $250 Wellness Allowance for all full-time staff.\n\nOver 84% of your departmental colleagues have already redeemed their vouchers. Select your digital gift card preference before the registration allotment is exhausted.\n\nPeople & Culture Rewards",
                warningSigns = "Sender domain is not affiliated with the corporation; excessive social proof manipulation ('84% redeemed'); scarcity pressure ('allotment exhausted').",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Social proof and unprompted financial bonuses are prominent phishing lures. Legitimate organizational bonuses are delivered through verified payroll systems, never via external claim portals."
            });

            // ============================================================
            // S18 - Authority Bias - Spear Phish Targeted - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S18",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Impersonation of corporate legal counsel with malicious archive attachment.",
                displayName = "Baker & Vance LLP - External Legal Counsel",
                senderAddress = "litigation-service@bakervance-counsel.org",
                displayDomain = "bakervance.com",
                rawHyperlinkUrl = "https://bakervance-counsel.org/secure-docs/subpoena-docket",
                subjectLine = "LEGAL NOTICE: Formal Subpoena for Deposition & Document Preservation",
                emailBody = "RE: United States District Court Case #2026-CV-08914\n\nPlease review the enclosed formal subpoena requiring document preservation concerning current vendor procurement disputes. As a material witness, you are required to acknowledge receipt within 72 hours.\n\nAccess the court transcript docket via the link below or inspect the attached encrypted legal package.\n\nBaker & Vance LLP\nLitigation Practice Group",
                warningSigns = "Domain mismatch (bakervance-counsel.org vs official legal firm domain); suspicious compressed attachment (.zip); legal intimidation designed to provoke impulsive compliance.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Court_Docket_Subpoena_Notice_2026.zip",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Legal threats and court summons provoke strong anxiety, driving recipients to open malicious attachments. External legal inquiries must always be routed through your organization's internal General Counsel."
            });

            // ============================================================
            // S19 - Familiarity/Trust - Legitimate - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S19",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Easy",
                context = "Authentic monthly electronic paystub notification from university payroll.",
                displayName = "University Payroll Office",
                senderAddress = "payroll-notifications@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://portal.university.edu/selfservice/paystubs",
                subjectLine = "Monthly Pay Advice Statement Available - October 2026",
                emailBody = "Dear Employee,\n\nYour electronic earnings statement for the October 2026 payroll cycle is now available for review in the Employee Self-Service portal.\n\nLog in directly to your verified university portal using your standard institutional credentials to review withholding and direct deposit distributions.\n\nUniversity Office of Payroll & Benefits",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate payroll notification. The sender and link point to the authentic institutional domain and advise logging in through the official portal."
            });

            // ============================================================
            // S20 - Urgency/Scarcity - Deceptive Phish - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S20",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Medium",
                context = "Cloud file storage quota overflow threat with immediate deletion notice.",
                displayName = "Enterprise Cloud Storage Team",
                senderAddress = "storage-admin@onedrive-cloud-management.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://onedrive-cloud-management.com/storage/upgrade",
                subjectLine = "Storage Warning: Account at 99.8% Capacity - Files Scheduled for Purge",
                emailBody = "ATTENTION: Your cloud storage pool has exceeded its allocated limit (49.9 GB of 50.0 GB utilized).\n\nIn accordance with tenant retention protocols, incoming emails and shared files will bounce, and pending uploads will be permanently purged within 48 hours.\n\nExpand your enterprise quota or clear archived data using the administrator console below.\n\nCloud Storage Operations",
                warningSigns = "Domain onedrive-cloud-management.com is a third-party clone; threat of permanent data loss creates artificial urgency.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Storage limit warnings frequently induce panic about lost work. Always check storage quotas directly inside your cloud app settings rather than through email links."
            });

            // ============================================================
            // S21 - Social Proof/Reward - Spear Phish Targeted - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S21",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Academic paper acceptance and presentation invoice scam targeting researchers.",
                displayName = "IEEE TVCG Conference Organizing Committee",
                senderAddress = "registration-desk@ieee-tvcg-symposium-2026.org",
                displayDomain = "ieee.org",
                rawHyperlinkUrl = "https://ieee-tvcg-symposium-2026.org/authors/camera-ready",
                subjectLine = "Paper Acceptance Notification: Camera-Ready Submission & Early Registration",
                emailBody = "Dear Author,\n\nWe are delighted to inform you that your manuscript has been accepted for oral presentation at the upcoming IEEE TVCG Virtual Reality Symposium.\n\nTo ensure inclusion in the IEEE Xplore proceedings, the primary author must upload camera-ready sources and pay the mandatory author registration fee via our secure merchant portal by Friday.\n\nProgram Executive Committee",
                warningSigns = "External domain (ieee-tvcg-symposium-2026.org instead of official ieee.org); urgent wire/credit card payment link for publication inclusion.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Reviewer_Meta_Reviews.pdf.exe",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Predatory and spoofed academic conference scams exploit author pride and tight deadlines. Genuine IEEE notifications use official @ieee.org domains and reputable conference systems like HotCRP or EasyChair."
            });

            // ============================================================
            // S22 - Authority Bias - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S22",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Medium",
                context = "Authentic CISO cybersecurity awareness bulletin and quarterly drill report.",
                displayName = "Chief Information Security Officer",
                senderAddress = "ciso@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://security.university.edu/bulletins/2026-q4-results",
                subjectLine = "CISO Cybersecurity Briefing: Q4 Threat Landscape & Phishing Drill Summary",
                emailBody = "Dear Faculty, Staff, and Students,\n\nEach quarter, our security operations team assesses institutional resilience against social engineering. Last month's simulated phishing exercise demonstrated a 24% reduction in click-through rates across campus.\n\nWe encourage everyone to review the comprehensive briefing report published on our official cybersecurity portal.\n\nNo credentials or authentication details are required to read this report.\n\nOffice of the Chief Information Security Officer\nUniversity System",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate security bulletin from the institutional CISO. The sender domain matches university.edu, no sensitive information is requested, and the link leads to the authentic public security page."
            });

            // ============================================================
            // S23 - Familiarity/Trust - Deceptive Phish - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S23",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Medium",
                context = "Microsoft Teams missed audio voicemail notification with malicious HTML payload.",
                displayName = "Microsoft Teams Voice Service",
                senderAddress = "noreply-voicemail@teams-notification-audio.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://teams-notification-audio.com/listen?id=vm-9801",
                subjectLine = "Missed Call: You have a new 42-second voicemail from +1 (555) 019-2841",
                emailBody = "Microsoft Teams Audio Messaging\n\nYou missed an audio call on your enterprise extension from +1 (555) 019-2841 at 14:22.\n\nA digital transcript and audio recording have been generated. Listen to your voicemail via the secure Teams audio player link or inspect the attached playback HTML envelope.\n\nMicrosoft Unified Communications",
                warningSigns = "External domain teams-notification-audio.com; attached HTML file designed to harvest credentials via fake login overlay.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Voicemail_Audio_Playback_42s.html",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Voicemail notification lures exploit natural curiosity. Legitimate Teams voicemails are stored and played directly inside the Microsoft Teams desktop/mobile application, never through HTML email attachments."
            });

            // ============================================================
            // S24 - Social Proof/Reward - Legitimate - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S24",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Easy",
                context = "Authentic campus innovation challenge and hackathon awards announcement.",
                displayName = "University Innovation & Technology Center",
                senderAddress = "innovation-events@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://innovation.university.edu/hackathon/2026-winners",
                subjectLine = "Celebration: Announcing the 2026 Annual AR/VR Hackathon Winners!",
                emailBody = "Dear Students and Faculty,\n\nWe extend our congratulations to all 48 student teams who competed in this weekend's Annual IVRAR Innovation Challenge! Over 200 participants demonstrated breakthrough spatial computing and immersive training prototypes.\n\nVisit our public showcase gallery to view project demonstrations, awards ceremonies, and photo albums.\n\nUniversity Innovation & Research Center",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate celebratory announcement from the university innovation center. The sender and link point to the valid university.edu domain and no personal information is requested."
            });

            // ============================================================
            // S25 - Urgency/Scarcity - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S25",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Medium",
                context = "University Library official automated loan return reminder.",
                displayName = "University Library Circulation",
                senderAddress = "circulation@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://library.university.edu/myaccount/renewals",
                subjectLine = "Notice: Library Loan Due Date Approaching in 48 Hours",
                emailBody = "Dear Student,\n\nOur circulation records indicate that one or more items checked out to your student account are due within the next 48 hours.\n\nYou may renew eligible titles directly through the official University Library portal or return them to any campus book drop.\n\nNo passwords or confidential credentials are requested by email.\n\nUniversity Library Circulation Desk",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate loan reminder from the university library. The sender and link point to the authentic institutional domain and advise managing renewals via the library portal."
            });

            // ============================================================
            // S26 - Urgency/Scarcity - Legitimate - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S26",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Easy",
                context = "Campus Health Services annual vaccination clinic scheduling advisory.",
                displayName = "Campus Student Health Center",
                senderAddress = "healthcenter@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://health.university.edu/clinics/immunization-schedule",
                subjectLine = "Advisory: Annual Campus Wellness & Flu Clinic Appointments Available",
                emailBody = "Dear Campus Community,\n\nStudent Health Services is hosting the annual campus wellness and immunization clinic this week at the Student Center.\n\nAppointments can be booked through the student health portal. Walk-ins are also welcome between 09:00 and 16:00.\n\nNo insurance payments or banking details are collected via email.\n\nStudent Health Services\nDivision of Student Life",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is an authentic health advisory from the campus wellness center. All appointments are handled via the official university health system."
            });

            // ============================================================
            // S27 - Authority Bias - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S27",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Official academic circular from the University Office of the Registrar.",
                displayName = "Office of the University Registrar",
                senderAddress = "registrar@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://registrar.university.edu/calendar/academic-deadlines",
                subjectLine = "Official Academic Notice: Midterm Grade Publishing & Course Audit Deadline",
                emailBody = "Dear Student,\n\nThe University Registrar announces that midterm grade evaluations have been posted to official student records.\n\nStudents wishing to audit courses or review academic standing must submit elective petitions via the verified student portal before the published deadline.\n\nOfficial transcripts and academic records should only be accessed through the university's authenticated student portal.\n\nOffice of the University Registrar",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate academic circular from the Office of the Registrar. The domain matches the verified university institution and directs students to official portals."
            });

            // ============================================================
            // S28 - Authority Bias - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S28",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Medium",
                context = "Campus Public Safety and Police facility parking advisory.",
                displayName = "Department of Campus Public Safety",
                senderAddress = "publicsafety@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://publicsafety.university.edu/alerts/parking-protocol",
                subjectLine = "Campus Safety Advisory: Facility Maintenance & Parking Reallocation",
                emailBody = "Notice to All Campus Residents,\n\nDue to scheduled facility maintenance and parking structure resurfacing, parking permits in Lot 4 will be temporarily relocated to the North Deck.\n\nReview the updated campus transit routes and shuttle schedules on our public safety website.\n\nCampus police officers will not issue citations in overflow zones during this transition.\n\nDepartment of Campus Public Safety",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is an authentic public safety notice with verified domains and transparent logistical information."
            });

            // ============================================================
            // S29 - Familiarity/Trust - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S29",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Medium",
                context = "Canvas Learning Management System automated submission receipt.",
                displayName = "Canvas Learning Management System",
                senderAddress = "notifications@instructure.com",
                displayDomain = "instructure.com",
                rawHyperlinkUrl = "https://university.instructure.com/courses/10482/submissions",
                subjectLine = "Submission Receipt: Project Milestone 2 - Cybersecurity VR",
                emailBody = "Dear Student,\n\nYour digital submission for 'Project Milestone 2' was successfully recorded in Canvas on October 27 at 14:15 UTC.\n\nSubmission details:\nFile: Team_Report_Final.pdf\nSize: 2.4 MB\n\nYou can review your uploaded submission and instructor feedback through your course dashboard.\n\nInstructure Canvas Notification System",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate automated LMS submission confirmation. The link uses the university's authentic Canvas tenant subdomain."
            });

            // ============================================================
            // S30 - Familiarity/Trust - Legitimate - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S30",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Easy",
                context = "Campus Bookstore digital order fulfillment confirmation.",
                displayName = "University Campus Bookstore",
                senderAddress = "orders@campusbookstore.university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://campusbookstore.university.edu/orders/lookup",
                subjectLine = "Order Confirmation: Course Materials Ready for Pickup",
                emailBody = "Dear Customer,\n\nYour course textbook order #BK-88421 is now packaged and ready for pickup at the Campus Bookstore service counter.\n\nPlease bring your physical student ID card when picking up your materials. Pickup hours are Monday through Friday, 08:30 to 17:00.\n\nUniversity Bookstore Operations",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is an authentic bookstore pickup confirmation. The sender and link point to the valid university.edu domain."
            });

            // ============================================================
            // S31 - Social Proof/Reward - Legitimate - Medium
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S31",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Medium",
                context = "Student Government Association annual campus election bulletin.",
                displayName = "Student Government Association",
                senderAddress = "elections@sga.university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://sga.university.edu/elections/candidate-platforms",
                subjectLine = "Campus Voting Open: 2026 Student Government Executive Elections",
                emailBody = "Dear Fellow Students,\n\nVoting is now open for this year's Student Government Association executive officers. Over 3,200 students cast their ballots on Day 1!\n\nReview certified candidate statements, debate transcripts, and policy platforms on the official SGA portal.\n\nBalloting is open through Thursday at 18:00.\n\nSGA Election Commission",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is a legitimate student election announcement. Social participation metrics are factual and no personal credentials are required."
            });

            // ============================================================
            // S32 - Social Proof/Reward - Legitimate - Easy
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S32",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Easy",
                context = "University Career Development Center employer showcase directory.",
                displayName = "Career Development Center",
                senderAddress = "careercenter@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://career.university.edu/fairs/fall-stem-directory",
                subjectLine = "Fall STEM Career Fair: Employer Guide & Workshop Schedule",
                emailBody = "Dear Students,\n\nThe Career Development Center invites you to attend the Fall STEM Career Showcase next Wednesday in the Grand Ballroom.\n\nMore than 60 engineering, cybersecurity, and software employers will be conducting on-campus interviews and technical workshops.\n\nDownload the official employer directory and prepare your resume via the career portal.\n\nUniversity Career Development Center",
                warningSigns = "None.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "This is an authentic career fair bulletin hosted on the valid university domain."
            });

            GenerateBalancedDeck(24);
        }

        public void GenerateBalancedDeck(int targetCount = 24)
        {
            if (masterScenarioPool == null || masterScenarioPool.Count == 0)
                InitializeDefaultScenarios();

            List<PhishingScenarioData> phishPool = new List<PhishingScenarioData>();
            List<PhishingScenarioData> legitPool = new List<PhishingScenarioData>();

            for (int i = 0; i < masterScenarioPool.Count; i++)
            {
                if (masterScenarioPool[i].classification == ThreatClassification.Legitimate)
                    legitPool.Add(masterScenarioPool[i]);
                else
                    phishPool.Add(masterScenarioPool[i]);
            }

            ShuffleList(phishPool);
            ShuffleList(legitPool);

            scenarioDeck.Clear();
            int phishIdx = 0;
            int legitIdx = 0;

            int halfCount = targetCount / 2;
            int pRem = halfCount;
            int lRem = targetCount - halfCount;

            int consecutivePhish = 0;
            int consecutiveLegit = 0;

            for (int i = 0; i < targetCount; i++)
            {
                bool canPickPhish = (pRem > 0) && (consecutivePhish < 2);
                bool canPickLegit = (lRem > 0) && (consecutiveLegit < 2);

                if (canPickPhish)
                {
                    int nextP = pRem - 1;
                    int nextL = lRem;
                    if (nextP > 2 * (nextL + 1) || nextL > 2 * (nextP + 1))
                        canPickPhish = false;
                }

                if (canPickLegit)
                {
                    int nextL = lRem - 1;
                    int nextP = pRem;
                    if (nextL > 2 * (nextP + 1) || nextP > 2 * (nextL + 1))
                        canPickLegit = false;
                }

                bool pickPhish;
                if (canPickPhish && canPickLegit)
                {
                    float pWeight = (float)pRem / (pRem + lRem);
                    pickPhish = UnityEngine.Random.value < pWeight;
                }
                else if (canPickPhish)
                {
                    pickPhish = true;
                }
                else if (canPickLegit)
                {
                    pickPhish = false;
                }
                else
                {
                    // Fallback prioritizing whichever pool still has items remaining
                    pickPhish = pRem > lRem;
                }

                if (pickPhish)
                {
                    scenarioDeck.Add(phishPool[phishIdx % phishPool.Count]);
                    phishIdx++;
                    pRem--;
                    consecutivePhish++;
                    consecutiveLegit = 0;
                }
                else
                {
                    scenarioDeck.Add(legitPool[legitIdx % legitPool.Count]);
                    legitIdx++;
                    lRem--;
                    consecutiveLegit++;
                    consecutivePhish = 0;
                }
            }

            Debug.Log($"[StratifiedDeckGenerator] Generated balanced deck of {scenarioDeck.Count} cards (50% Phish / 50% Legit, MaxRun <= 2).");
        }

        private static void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }

        public float CurrentScenarioRemainingTime =>
            CurrentScenario.timePressureSeconds > 0f
                ? Mathf.Max(0f, CurrentScenario.timePressureSeconds - scenarioTimer)
                : 0f;

        public float CurrentScenarioElapsedTime => scenarioTimer;
        public float CurrentScenarioTimePressure => CurrentScenario.timePressureSeconds;

        public void RestartSimulation(bool shuffle = true)
        {
            if (isSessionActive)
                OnSimulationRestarting?.Invoke();

            currentScenarioIndex = 0;
            totalScenariosPresented = 0;
            completedDecisionsCount = 0;
            correctClassifications = 0;
            maliciousClicksCount = 0;
            reportingActionsCount = 0;
            sessionTimer = 0f;
            scenarioTimer = 0f;
            if (shuffle)
                ShuffleScenarioDeck();
            isSessionActive = true;
            PresentScenario(currentScenarioIndex);
        }
        public void BeginSimulation()
        {
            if (isSessionActive)
                OnSimulationRestarting?.Invoke();

            currentScenarioIndex = 0;
            totalScenariosPresented = 0;
            completedDecisionsCount = 0;
            correctClassifications = 0;
            maliciousClicksCount = 0;
            reportingActionsCount = 0;
            sessionTimer = 0f;
            ShuffleScenarioDeck();
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
            decisionSubmitted = false;
            currentTrialId = current.scenarioId + "-" + (++presentationSequence).ToString("D4");

            Debug.Log($"[PhishingSimulation] Scenario {current.scenarioId} presented. Bias: {current.biasType}");
            OnScenarioPresented?.Invoke(current, currentTrialId, currentScenarioIndex + 1, scenarioDeck.Count);
        }

        public void SubmitUserAction(bool didClickLink, bool didReportPhish, bool didIgnore)
        {
            int selectedCount = (didClickLink ? 1 : 0) + (didReportPhish ? 1 : 0) + (didIgnore ? 1 : 0);
            if (selectedCount != 1)
            {
                Debug.LogWarning("[PhishingSimulation] SubmitUserAction requires exactly one selected action.");
                return;
            }

            string selectedAction = didClickLink ? "Click Link" : didReportPhish ? "Report Phishing" : "Ignore";
            SubmitUserAction(selectedAction);
        }

        public bool SubmitUserAction(string selectedAction)
        {
            if (!isSessionActive || decisionSubmitted || currentScenarioIndex >= scenarioDeck.Count)
                return false;
            if (selectedAction != "Click Link" && selectedAction != "Report Phishing" && selectedAction != "Ignore")
            {
                Debug.LogError("[PhishingSimulation] Unknown action: " + selectedAction);
                return false;
            }

            PhishingScenarioData current = scenarioDeck[currentScenarioIndex];
            bool didClickLink = selectedAction == "Click Link";
            bool didReportPhish = selectedAction == "Report Phishing";
            bool didIgnore = selectedAction == "Ignore";
            bool isCorrect = IsCorrectAction(current.classification, current.correctAction, selectedAction);
            float responseTimeSeconds = Mathf.Max(0f, scenarioTimer);
            decisionSubmitted = true;
            completedDecisionsCount++;

            if (isCorrect)
                correctClassifications++;
            if (didClickLink && current.classification != ThreatClassification.Legitimate)
                maliciousClicksCount++;
            if (didReportPhish)
                reportingActionsCount++;

            // =========================================================================
// D021 - Madhav Gaonkar - Phishing Threat Modeling Lead
// Threat-model telemetry and susceptibility scoring
// =========================================================================

float reactionLatency = responseTimeSeconds;

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
            OnDecisionAccepted?.Invoke(current, selectedAction, isCorrect, responseTimeSeconds, currentTrialId);
            OnDecisionRecorded?.Invoke(current.scenarioId, isCorrect, responseTimeSeconds);
            return true;
        }

        public bool AdvanceToNextScenario()
        {
            if (!isSessionActive || !decisionSubmitted)
                return false;

            currentScenarioIndex++;
            if (currentScenarioIndex >= scenarioDeck.Count)
            {
                PresentScenario(currentScenarioIndex);
                return false;
            }

            PresentScenario(currentScenarioIndex);
            return true;
        }

        private static bool IsCorrectAction(ThreatClassification classification, string expectedAction, string selectedAction)
        {
            if (classification == ThreatClassification.Legitimate)
            {
                // Legitimate emails: participant is safe if they ignore or click authentic link.
                // Reporting legitimate email as phishing is a false positive (incorrect).
                return selectedAction != "Report Phishing";
            }

            // Phishing emails: reporting phishing is the only correct defensive response.
            return selectedAction == "Report Phishing";
        }

        private void ShuffleScenarioDeck()
        {
            GenerateBalancedDeck(24);
        }

        private void EndSimulation()
        {
            isSessionActive = false;
            float accuracy = completedDecisionsCount > 0 ? (float)correctClassifications / completedDecisionsCount * 100f : 0f;
            Debug.Log($"[PhishingSimulation] Completed. Accuracy: {accuracy:F1}% | Malicious Clicks: {maliciousClicksCount} | Reports: {reportingActionsCount}");
        }
    }
}