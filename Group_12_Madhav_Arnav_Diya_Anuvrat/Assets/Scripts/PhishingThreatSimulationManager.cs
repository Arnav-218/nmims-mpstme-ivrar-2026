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
            // S01 - Urgency/Scarcity - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S01",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Routine SSO multi-factor session synchronization notice.",
                displayName = "University Identity & Access Management",
                senderAddress = "iam-sync@univ-sso-services.edu.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://sso.university.edu.iam-session-sync.org/oauth2/v2/authorize",
                subjectLine = "Notice: Academic Term SSO Certificate Renewal & MFA Sync",
                emailBody = "Dear Student,\n\nAs part of the scheduled Fall Semester Identity Provider update, student Single Sign-On (SSO) tokens issued prior to October 1st are being migrated to the updated SAML 2.0 endpoint.\n\nIf you access Canvas, Campus Wi-Fi (eduroam), or Library VPN from off-campus devices, please review your active device bindings on the Identity Management Portal before Friday at 17:00.\n\nIdentity & Access Management\nOffice of Information Technology",
                warningSigns = "Sender domain (univ-sso-services.edu.org) and target host (iam-session-sync.org) are external lookalike domains disguised with a 'sso.university.edu' subdomain prefix.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "The email body uses calm, realistic IT terminology without obvious threats. However, inspecting the Left/Right telemetry reveals the actual host is 'iam-session-sync.org', not 'university.edu'."
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
                context = "Course registration hold & Bursar tuition reconciliation.",
                displayName = "Office of the Bursar & Registration",
                senderAddress = "bursar-billing@university-studentaccounts.com",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://bursar.university-studentaccounts.com/term-clearance/statement",
                subjectLine = "Spring Term Course Pre-Registration Clearance Status",
                emailBody = "Dear Student,\n\nOur student accounting system indicates a pending reconciliation item ($0.00 balance adjustment) on your current semester ledger following the recent financial aid disbursement.\n\nWhile no payment is due, students must acknowledge the updated term statement prior to their assigned registration time-ticket window so course enrollment holds are automatically cleared.\n\nOffice of the Bursar\nStudent Financial Services",
                warningSigns = "Reassuringly claims '$0.00 due' to lower suspicion, but routes through external domain 'university-studentaccounts.com' instead of 'university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Statement_Adjustment_Oct2026.pdf.html",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Sophisticated phishing often avoids asking for money directly (even claiming a $0.00 balance) to make you click the link and enter your SSO password. Check the domain telemetry and double-extension '.pdf.html' attachment."
            });

            // ============================================================
            // S03 - Urgency/Scarcity - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S03",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Genuine urgent campus VPN & Duo MFA security upgrade.",
                displayName = "University IT Security Operations",
                senderAddress = "secops-alerts@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://iam.university.edu/duo-device-management",
                subjectLine = "ACTION REQUIRED: Mandatory Duo MFA Hardware Token Migration by Friday",
                emailBody = "ATTENTION ALL STUDENTS & FACULTY:\n\nFollowing recent advisories regarding SMS-based authentication vulnerabilities, University IT Security is disabling SMS/Voice passcodes on all campus accounts effective Friday at 23:59.\n\nAccounts that have not registered the Duo Mobile push app or a FIDO2 security key by the deadline will experience a temporary login block until verified via the IT Helpdesk.\n\nReview your registered MFA devices on the official Identity Portal.\n\nUniversity IT Security Operations",
                warningSigns = "None. Despite strong urgency and deadline language, the sender (@university.edu), SPF/DKIM alignment, and target host (iam.university.edu) are 100% authentic.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "Duo_Migration_Guide_2026.pdf",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Real IT departments frequently send urgent, deadline-driven security notices! You cannot judge legitimacy by tone alone—inspecting the header telemetry confirms 'secops-alerts@university.edu' and 'iam.university.edu' are completely genuine."
            });

            // ============================================================
            // S04 - Authority Bias - Spear Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S04",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Research ethics IRB protocol renewal notice.",
                displayName = "Institutional Review Board (IRB) Compliance",
                senderAddress = "irb-docket@univ-research-compliance.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://cayuse.univ-research-compliance.org/irb/protocol-review",
                subjectLine = "IRB Protocol #2026-0418: Annual Continuing Review Signature Needed",
                emailBody = "Dear Investigator / Student Researcher,\n\nThe Institutional Review Board has completed the preliminary administrative check for your human-subjects research protocol (#2026-0418).\n\nBefore final committee sign-off can be recorded for the upcoming academic cycle, co-investigators must verify their CITI training completion date on the Cayuse compliance form linked below.\n\nOffice of Research Integrity & Compliance",
                warningSigns = "Highly realistic academic context, but uses external domain 'univ-research-compliance.org' instead of the official 'university.edu' domain.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 210f,
                correctAction = "Report Phish",
                correctiveFeedback = "Spear-phishing targets academic researchers using realistic terminology (IRB, CITI, Cayuse). Always cross-check the sender domain and Return-Path on the forensic monitor."
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
                context = "Academic advising degree-audit graduation check.",
                displayName = "Office of the Dean - Academic Advising",
                senderAddress = "advising-audit@university-academics.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://degreeworks.university-academics.org/audit/worksheet",
                subjectLine = "DegreeWorks Audit: Elective Credit Discrepancy on Transcript",
                emailBody = "Dear Student,\n\nDuring our mid-semester review of degree progression worksheets, the Academic Advising Office noted an unclassified technical elective credit on your current DegreeWorks audit.\n\nTo ensure this credit is properly mapped toward your major requirements prior to next term's enrollment, please review the highlighted course equivalency note on your advising worksheet.\n\nOffice of the Dean\nUndergraduate Academic Advising",
                warningSigns = "Sender domain 'university-academics.org' and link host 'degreeworks.university-academics.org' do not belong to 'university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "DegreeWorks_Credit_Worksheet.pdf.iso",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "This message reads like a helpful, routine note from Academic Advising, but both the domain ('university-academics.org') and the '.pdf.iso' disk-image attachment are malicious indicators."
            });

            // ============================================================
            // S06 - Authority Bias - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S06",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Mandatory academic integrity & conduct policy attestation.",
                displayName = "Office of the Provost & Academic Affairs",
                senderAddress = "provost-compliance@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://provost.university.edu/policies/ai-academic-integrity-2026",
                subjectLine = "MANDATORY: Updated Generative AI & Academic Integrity Policy Attestation",
                emailBody = "Dear Students and Faculty,\n\nEffective this semester, the University Senate has ratified updated guidelines governing the permissible use of Generative AI tools in coursework and laboratory submissions.\n\nAll enrolled students are required to review the updated Honor Code addendum and complete the electronic policy acknowledgment via the Provost Portal prior to midterm examinations.\n\nOffice of the Provost\nUniversity Administration",
                warningSigns = "None. Although it demands mandatory action before midterms, the sender (@university.edu), Return-Path, and link host (provost.university.edu) are completely legitimate.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "Honor_Code_Addendum_2026.pdf",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Mandatory compliance emails from university leadership are common in real life. Because 'provost.university.edu' and SPF/DKIM alignment are verified on the telemetry monitor, reporting this as phishing is a false alarm."
            });

            // ============================================================
            // S07 - Familiarity/Trust - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S07",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Shared OneDrive lab project document collaboration invite.",
                displayName = "Microsoft SharePoint / OneDrive",
                senderAddress = "no-reply@sharepoint-university-files.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://university-my.sharepoint-university-files.com/:w:/g/personal/lab_report",
                subjectLine = "Arnav Jain shared 'IVRAR_Final_Lab_Submission_v3.docx' with you",
                emailBody = "Arnav Jain has invited you to edit a document on Microsoft OneDrive for Business.\n\nDocument: IVRAR_Final_Lab_Submission_v3.docx\nNote from sender: 'Hey, I updated the telemetry section and results table—please check your part before we submit tonight.'\n\nThis link will work only for the direct recipients of this message.\n\nMicrosoft SharePoint Online",
                warningSigns = "Realistic peer-collaboration lure using a classmate's name and SharePoint URL structure, but hosted on 'sharepoint-university-files.com' instead of 'sharepoint.com'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 210f,
                correctAction = "Report Phish",
                correctiveFeedback = "Fake OneDrive/SharePoint collaboration notifications are the #1 real-world corporate and campus phishing template. Inspecting the target host shows 'sharepoint-university-files.com' rather than Microsoft's authentic 'sharepoint.com'."
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
                context = "Subdomain-spoofed campus student portal grade notification.",
                displayName = "University Student Information System",
                senderAddress = "sis-noreply@university.edu.student-portal-auth.com",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://sis.university.edu.student-portal-auth.com/grades/midterm",
                subjectLine = "Grade Roster Update: Midterm Assessment Posted for CS-408",
                emailBody = "Dear Student,\n\nAn instructor has posted or updated a grade item for your enrolled course section (CS-408: Immersive Virtual Reality Systems).\n\nFor FERPA privacy compliance, numerical scores and grader comments are not included in email bodies. You may view your grade breakdown on the Student Information System.\n\nOffice of the Registrar",
                warningSigns = "Cites FERPA privacy rules to look trustworthy, while placing 'university.edu' as a left-hand subdomain prefix of the attacker's root domain 'student-portal-auth.com'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Attackers frequently cite privacy laws like FERPA or GDPR to explain why you must click a link. Looking at the DNS telemetry reveals the actual root domain is 'student-portal-auth.com'."
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
                context = "Genuine Student Portal login alert from new browser session.",
                displayName = "University Identity Security",
                senderAddress = "no-reply-iam@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://iam.university.edu/security/recent-activity",
                subjectLine = "Security Alert: New Sign-In to Your University Account on Windows 11",
                emailBody = "Dear Student,\n\nWe detected a new sign-in to your University Single Sign-On account:\n\nDevice: Windows 11 Desktop (Chrome 130.0)\nLocation: Campus Lab Subnet (10.42.118.14)\nTime: Today at 14:12 IST\n\nIf this was you, you can safely disregard this email. If you do not recognize this sign-in, please review your active sessions and rotate your password immediately via the Identity Portal.\n\nUniversity Identity Security",
                warningSigns = "None. Although 'Security Alert: New Sign-In' looks like a classic phishing subject line, the headers and URL ('iam.university.edu') are 100% genuine.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Legitimate security alerts often look scary! Instead of guessing from the subject line, checking the telemetry monitor confirms that 'no-reply-iam@university.edu' and 'iam.university.edu' are authentic."
            });

            // ============================================================
            // S10 - Social Proof/Reward - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S10",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Departmental research stipend & travel grant reimbursement lure.",
                displayName = "Engineering Research & Travel Grants",
                senderAddress = "grants-disbursement@univ-engineering-funding.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://portal.univ-engineering-funding.org/stipend/direct-deposit",
                subjectLine = "Fall Undergraduate Research & Lab Project Stipend Allocation",
                emailBody = "Dear Student,\n\nFollowing faculty nominations from the Department of Computer Engineering, your project group has been approved for the Fall Lab Equipment & Conference Micro-Grant ($350.00).\n\nMost nominated project teams in your cohort have already confirmed their ACH direct-deposit routing details. Please verify your student banking profile by Friday so funds are included in this month's batch.\n\nSchool of Engineering Finance Office",
                warningSigns = "Realistic academic micro-grant amount ($350) and peer conformity cue, but sent from external domain 'univ-engineering-funding.org'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Grant_Award_Voucher_2026.pdf.lnk",
                timePressureSeconds = 210f,
                correctAction = "Report Phish",
                correctiveFeedback = "Modest, believable grant amounts ($350) are much harder to spot than million-dollar lottery scams. However, the external domain 'univ-engineering-funding.org' and shortcut attachment ('.pdf.lnk') expose the attack."
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
                context = "Campus software license renewal for MATLAB & JetBrains.",
                displayName = "Campus Academic Software Licensing",
                senderAddress = "software-licensing@university-academic-tools.com",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://licenses.university-academic-tools.com/renew-seat",
                subjectLine = "Annual Student Seat Renewal: MATLAB, JetBrains & Unity Pro Licenses",
                emailBody = "Dear Student,\n\nOur annual campus site-license audit shows that your student developer entitlements for MATLAB, JetBrains IDEs, and Unity Education are due for seat re-validation.\n\nOver 90% of engineering students have already renewed their active seats for the 2026–2027 academic year. Confirm your institutional enrollment status below to keep your academic licenses active.\n\nAcademic Software Distribution",
                warningSigns = "Targets engineering students with tools they actually use (MATLAB, JetBrains, Unity), but uses external domain 'university-academic-tools.com'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Tailored lures referencing software you use every day create strong familiarity trust. Always inspect the SMTP From-Domain and Target Host ('university-academic-tools.com' vs 'university.edu')."
            });

            // ============================================================
            // S12 - Social Proof/Reward - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S12",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Genuine AWS / GitHub Student Developer Pack campus credit allocation.",
                displayName = "University Cloud & Developer Program",
                senderAddress = "cloud-education@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://devportal.university.edu/cloud-credits/redeem",
                subjectLine = "Action Needed: Claim Your $100 AWS & Azure Course Lab Cloud Credits",
                emailBody = "Dear Computer Science & Engineering Student,\n\nIn partnership with our academic cloud sponsors, the Department has provisioned $100.00 in AWS and Azure lab credits for students enrolled in upper-division systems and VR courses.\n\nOver 400 students in your department have already activated their sandbox vouchers this week. Sign in via the official University Developer Portal before the add/drop deadline to bind the credit code to your lab account.\n\nDepartment of Computer Science",
                warningSigns = "None. Although it offers '$100 credits' and mentions '400 students have activated', the sender (@university.edu) and host (devportal.university.edu) are verified internal university systems.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Not every email offering credits or mentioning peer participation is a phish! Because the sender ('cloud-education@university.edu'), SPF/DKIM alignment, and host ('devportal.university.edu') are genuine, this is a safe institutional email."
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
                context = "Sponsored research lab invoice sign-off targeting project leads.",
                displayName = "Sponsored Projects & Grant Accounting",
                senderAddress = "sponsored-grants@univ-finance-office.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://procurement.univ-finance-office.org/voucher/approve-po",
                subjectLine = "PO #84920 Reconciliation: Lab Hardware Vendor Invoice Approval",
                emailBody = "Good afternoon,\n\nOur Sponsored Projects accounting desk is closing the monthly ledger for your department's VR laboratory equipment grant. Vendor invoice #INV-9042 (Meta Quest Enterprise Peripherals) requires PI or student project lead verification.\n\nPlease review the line-item packing slip and sign off on the procurement voucher today so vendor payment is not delayed.\n\nSponsored Projects Accounting",
                warningSigns = "Lookalike external domain (univ-finance-office.org); disguised executable attachment (.pdf.exe); realistic lab hardware context.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Vendor_Invoice_PackingSlip_9042.pdf.exe",
                timePressureSeconds = 210f,
                correctAction = "Report Phish",
                correctiveFeedback = "Invoice and procurement lures targeting specific lab equipment you work with are classic spear-phishing attacks. Check the external domain ('univ-finance-office.org') and '.pdf.exe' attachment."
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
                displayName = "Microsoft 365 Identity Protection",
                senderAddress = "security-noreply@rnicrosoft-cloud.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://login.rnicrosoft-cloud.com/common/oauth2/reauth",
                subjectLine = "Conditional Access Notice: Refresh Your Campus Microsoft 365 Session",
                emailBody = "Hello,\n\nYour organization's Conditional Access policy requires periodic re-authentication for devices accessing Exchange Online and Teams from university campus networks.\n\nYour current session token expires today at 18:00. To prevent interruption to your Outlook mail client and OneDrive sync, please refresh your session credentials.\n\nMicrosoft Entra ID Operations",
                warningSigns = "Subtle visual homograph spoofing ('rn' resembles 'm' in 'rnicrosoft-cloud.com'); calm corporate language that mimics routine Entra ID prompts.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Homograph attacks ('rnicrosoft' instead of 'microsoft') combined with calm, non-panicky language are extremely deceptive. Always read the character-by-character host on the forensic telemetry screen."
            });

            // ============================================================
            // S15 - Familiarity/Trust - Spear Phish Targeted - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S15",
                classification = ThreatClassification.SpearPhishTargeted,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Targeted software developer lure impersonating GitHub repository invite.",
                displayName = "GitHub Classroom & Enterprise",
                senderAddress = "noreply@github-classroom-portal.io",
                displayDomain = "github.com",
                rawHyperlinkUrl = "https://github.com.github-classroom-portal.io/orgs/ivrar-2026/invitation",
                subjectLine = "[GitHub] Invitation to join organization @nmims-mpstme-ivrar-2026",
                emailBody = "Hello Developer,\n\nYour course instructor has invited your account to join the @nmims-mpstme-ivrar-2026 organization on GitHub Classroom for final project repository grading.\n\nPlease accept the organization invitation and authorize the grading workflow token within 48 hours so your branch commits are linked to your student ID.\n\nGitHub Education Team",
                warningSigns = "Uses the exact name of your course repository, but the sender and target host belong to 'github-classroom-portal.io' rather than 'github.com'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Spear-phishing often scrapes real project or repository names to build instant trust. Checking the telemetry monitor reveals 'github-classroom-portal.io' is an external impostor domain."
            });

            // ============================================================
            // S16 - Urgency/Scarcity - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S16",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Legitimate urgent SSL VPN certificate update from Campus IT.",
                displayName = "Campus Network & Firewall Operations",
                senderAddress = "netops@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://vpn.university.edu/client-profile/root-ca-2026",
                subjectLine = "URGENT: Palo Alto GlobalProtect VPN Root Certificate Expiring Tonight",
                emailBody = "ATTENTION OFF-CAMPUS & LAB USERS:\n\nThe root TLS certificate for the Campus GlobalProtect VPN gateway expires tonight at 23:59 UTC. Users attempting to connect to internal lab servers or license managers without the updated profile will receive a certificate validation error.\n\nIf you use GlobalProtect VPN, please refresh your client connection profile via the official VPN portal.\n\nCampus Network Operations",
                warningSigns = "None. Despite the 'URGENT' subject line and technical certificate warning, the sender ('netops@university.edu') and host ('vpn.university.edu') are verified internal university endpoints.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Urgent technical alerts about expiring VPN certificates happen in real enterprises. Because the sender domain ('university.edu'), SPF/DKIM, and host ('vpn.university.edu') align cleanly, this is a legitimate IT notice."
            });

            // ============================================================
            // S17 - Social Proof/Reward - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S17",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Campus dining & bookstore flex-dollar refund lure.",
                displayName = "Campus Card & Auxiliaries Office",
                senderAddress = "campuscard@univ-auxiliary-services.net",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://onecard.univ-auxiliary-services.net/rollover-refund",
                subjectLine = "End-of-Term Campus OneCard Flex Balance Rollover / Refund",
                emailBody = "Dear Student,\n\nUnspent Campus OneCard dining and bookstore flex balances ($42.50 remaining on your card) do not automatically carry over to next semester unless a rollover election is submitted.\n\nMost students in your residence hall have already selected direct tuition credit or ACH refund. Submit your rollover preference before the end of the week.\n\nCampus Card Services",
                warningSigns = "Uses a specific, small dollar amount ($42.50) to feel realistic, but routes to external domain 'univ-auxiliary-services.net'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Attackers use specific small numbers like '$42.50' instead of round numbers to make financial lures believable. Always check the domain telemetry ('univ-auxiliary-services.net' != 'university.edu')."
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
                context = "Campus network acceptable-use copyright / DMCA notice.",
                displayName = "Office of General Counsel & IT Policy",
                senderAddress = "policy-enforcement@university-legal-affairs.org",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://compliance.university-legal-affairs.org/ticket/noc-8821",
                subjectLine = "Network Policy Ticket #NOC-8821: Peer-to-Peer Traffic Flagged on MAC Address",
                emailBody = "RE: Automated Acceptable Use Policy Alert (Ticket #NOC-8821)\n\nCampus network sensors logged anomalous outbound P2P / port-scanning traffic originating from a device registered to your student network credentials on eduroam.\n\nTo prevent automatic MAC-address quarantine, please review the packet summary log and confirm whether this device belongs to you within 24 hours.\n\nIT Policy & Legal Compliance",
                warningSigns = "Domain mismatch ('university-legal-affairs.org' vs 'university.edu'); attached compressed archive (.zip); intimidating network quarantine threat.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Packet_Capture_Log_NOC8821.zip",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Fake network violation tickets provoke anxiety so students open malicious '.zip' archives without checking the sender domain ('university-legal-affairs.org')."
            });

            // ============================================================
            // S19 - Familiarity/Trust - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S19",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Authentic work-study & teaching assistant tax form W-2/1098-T notice.",
                displayName = "University Payroll & Tax Compliance",
                senderAddress = "payroll-tax@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://hrportal.university.edu/selfservice/tax-forms",
                subjectLine = "ACTION REQUIRED: Electronic Consent for Tuition 1098-T & Work-Study Tax Statements",
                emailBody = "Dear Student / Student Employee,\n\nTo receive your annual Form 1098-T Tuition Statement and Work-Study earnings summary electronically rather than by postal mail, federal IRS regulations require an active electronic delivery consent on file.\n\nPlease sign in to the official Employee & Student Self-Service portal before the end of the month to verify your tax delivery preference and mailing address.\n\nUniversity Office of Payroll & Tax Compliance",
                warningSigns = "None. Mentions IRS tax forms and 'ACTION REQUIRED', which often makes players suspicious, but the sender ('payroll-tax@university.edu') and host ('hrportal.university.edu') are 100% genuine.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 300f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Tax and 1098-T consent notices look serious, which causes many people to falsely report them as phishing. Checking the telemetry confirms 'payroll-tax@university.edu' and 'hrportal.university.edu' are authentic."
            });

            // ============================================================
            // S20 - Urgency/Scarcity - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S20",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Google Workspace / Campus Drive student storage cap enforcement.",
                displayName = "University Cloud Storage Administration",
                senderAddress = "storage-lifecycle@univ-cloud-quota.com",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://drive.university.edu.univ-cloud-quota.com/retention/archive",
                subjectLine = "Campus Cloud Policy: Student Drive Quota Reduction (15 GB Cap)",
                emailBody = "Dear Student,\n\nUnder the updated education licensing tier, individual student cloud storage allocations are transitioning to a strict 15.0 GB baseline cap next Monday.\n\nYour account currently utilizes 18.4 GB across shared course folders and project backups. Please use the Storage Retention Manager below to mark which project directories should be exempted from automated archiving.\n\nCloud Infrastructure Services",
                warningSigns = "Uses subdomain spoofing ('drive.university.edu.univ-cloud-quota.com') where the actual registered host is 'univ-cloud-quota.com'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Real universities have been reducing cloud storage quotas recently, making this lure very believable. However, the target host ('univ-cloud-quota.com') is an external phishing domain."
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
                subjectLine = "Manuscript #1408 Review Complete: Minor Revisions & Camera-Ready Upload",
                emailBody = "Dear Author,\n\nThe Program Committee has finalized peer review for your submission to the Special Issue on Immersive Cybersecurity & XR Systems. Two reviewers recommended acceptance pending minor formatting revisions.\n\nPlease inspect the attached meta-review PDF and upload your revised LaTeX source package via the author portal by Friday.\n\nProgram Executive Committee",
                warningSigns = "External domain (ieee-tvcg-symposium-2026.org instead of official ieee.org); double-extension executable attachment ('Reviewer_Meta_Reviews.pdf.exe').",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Reviewer_Meta_Reviews.pdf.exe",
                timePressureSeconds = 240f,
                correctAction = "Report Phish",
                correctiveFeedback = "Targeted academic spear-phishing exploits researchers waiting on peer reviews. Both the lookalike domain ('ieee-tvcg-symposium-2026.org') and the '.pdf.exe' attachment reveal the threat."
            });

            // ============================================================
            // S22 - Authority Bias - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S22",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Authentic CISO mandatory laboratory workstation patching directive.",
                displayName = "Office of the Chief Information Security Officer",
                senderAddress = "ciso@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://security.university.edu/advisories/cve-2026-4109",
                subjectLine = "SECURITY DIRECTIVE: Mandatory Patching for CVE-2026-4109 on Lab Machines",
                emailBody = "MEMORANDUM TO ALL LAB ADMINISTRATORS & RESEARCH STUDENTS:\n\nA critical privilege-escalation vulnerability (CVE-2026-4109) affecting Windows and Linux development workstations is being actively exploited.\n\nAll laboratory machines connected to the research VLAN must install the October security rollup by tomorrow at 17:00. Unpatched hosts detected by automated Nessus scans will be temporarily isolated.\n\nReview the remediation instructions on the CISO Advisory Portal.\n\nChief Information Security Officer",
                warningSigns = "None. Uses strong security language ('CVE-2026-4109', 'temporarily isolated'), yet originates from 'ciso@university.edu' and links to 'security.university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "CVE_2026_4109_Remediation_Advisory.pdf",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Real CISO advisories about zero-day vulnerabilities sound urgent and threatening! Verifying the SMTP header telemetry confirms 'ciso@university.edu' and 'security.university.edu' are completely genuine."
            });

            // ============================================================
            // S23 - Familiarity/Trust - Deceptive Phish - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S23",
                classification = ThreatClassification.DeceptivePhish,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Microsoft Teams meeting recording & transcript share lure.",
                displayName = "Microsoft Teams Recording Bot",
                senderAddress = "recordings-noreply@teams-stream-archive.com",
                displayDomain = "microsoft.com",
                rawHyperlinkUrl = "https://stream.teams-stream-archive.com/video/capstone-sync-oct28",
                subjectLine = "Meeting Recording Ready: 'Capstone Final Demo Sync & Code Review'",
                emailBody = "Microsoft Teams Stream\n\nThe cloud recording and AI-generated action items from today's meeting ('Capstone Final Demo Sync & Code Review') are now available for playback.\n\nPer organizational retention settings, meeting recordings expire automatically after 14 days. Open the Stream player below or view the attached transcript summary.\n\nMicrosoft Teams Collaboration",
                warningSigns = "External domain 'teams-stream-archive.com' instead of 'microsoft.com'; attached HTML credential harvester ('Meeting_Transcript_Player.html').",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = true,
                attachmentName = "Meeting_Transcript_Player.html",
                timePressureSeconds = 180f,
                correctAction = "Report Phish",
                correctiveFeedback = "Fake Teams/Zoom meeting recording notifications are among the hardest real-world phishing emails to spot without checking the sender domain ('teams-stream-archive.com') and '.html' attachment."
            });

            // ============================================================
            // S24 - Social Proof/Reward - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S24",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Authentic external sponsor hackathon reimbursement & prize claim.",
                displayName = "University Innovation & Entrepreneurship Center",
                senderAddress = "innovation-grants@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://innovation.university.edu/hackathon/hardware-reimbursement",
                subjectLine = "Action Required: Submit Receipts for 2026 XR Hackathon Hardware Grant",
                emailBody = "Dear Hackathon Finalist,\n\nCongratulations on completing the 2026 Annual AR/VR Innovation Challenge! Over 85% of participating student teams have already uploaded their hardware prototyping receipts for reimbursement (up to $200 per team).\n\nTo include your team's reimbursement in Friday's finance run, please upload your itemized PDF receipts via the official Innovation Center portal.\n\nUniversity Innovation & Research Center",
                warningSigns = "None. Mentions '$200 reimbursement' and '85% of teams', which resembles social-proof lures, but is hosted on the verified 'innovation.university.edu' domain.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "Reimbursement_Claim_Instructions.pdf",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Legitimate university grant programs also have deadlines and mention reimbursement amounts. Cross-checking the telemetry confirms 'innovation-grants@university.edu' and 'innovation.university.edu' are authentic."
            });

            // ============================================================
            // S25 - Urgency/Scarcity - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S25",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "University Library interlibrary loan recall & late-fee hold warning.",
                displayName = "University Library Access Services",
                senderAddress = "circulation@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://library.university.edu/myaccount/recalls",
                subjectLine = "URGENT RECALL: Interlibrary Loan Item Overdue - Transcript Hold Warning",
                emailBody = "Dear Patron,\n\nAn Interlibrary Loan reference volume checked out to your student ID has been recalled by the lending institution for course reserves and is due within 48 hours.\n\nPer university policy, unreturned recalled items accrue $5.00/day replacement fees and place an administrative hold on official transcript requests.\n\nCheck your recall status or request an extension on the University Library portal.\n\nUniversity Library Circulation Desk",
                warningSigns = "None. Despite threatening a '$5.00/day fee' and 'transcript hold', the sender ('circulation@university.edu') and host ('library.university.edu') are 100% genuine.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Library recall notices often sound harsh and mention fees or holds. Checking the telemetry confirms 'circulation@university.edu' and 'library.university.edu' are completely legitimate."
            });

            // ============================================================
            // S26 - Urgency/Scarcity - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S26",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.UrgencyScarcity,
                difficulty = "Hard",
                context = "Campus Health Services mandatory immunization compliance hold.",
                displayName = "Campus Student Health Compliance",
                senderAddress = "healthcenter@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://health.university.edu/patient-portal/immunizations",
                subjectLine = "COMPLIANCE HOLD WARNING: Missing Immunization Record for Spring Enrollment",
                emailBody = "Dear Student,\n\nA routine audit of state-mandated student health records indicates that your immunization verification form or waiver is incomplete for the upcoming academic term.\n\nStudents with incomplete health compliance records by November 15th will have a registration hold placed on their Spring course enrollment.\n\nPlease review your status on the confidential Student Health Patient Portal.\n\nCampus Student Health Services",
                warningSigns = "None. Uses a registration hold deadline, but originates from 'healthcenter@university.edu' and points to 'health.university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Universities enforce real registration holds for health records. Because 'healthcenter@university.edu' and 'health.university.edu' pass all header checks, this is a legitimate administrative email."
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
                context = "Official graduation & degree-candidacy audit deadline from Registrar.",
                displayName = "Office of the University Registrar",
                senderAddress = "registrar@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://registrar.university.edu/graduation/candidacy-form",
                subjectLine = "FINAL DEADLINE: Confirm Diploma Name & Degree Candidacy Application",
                emailBody = "Dear Degree Candidate,\n\nFriday at 17:00 is the final administrative deadline to verify the exact legal spelling of your name for diploma printing and commencement program listing.\n\nCandidates who do not confirm their degree candidacy record on the Registrar Portal by the deadline will be deferred to the subsequent conferral cycle.\n\nOffice of the University Registrar",
                warningSigns = "None. High-stakes deadline from an authority figure, but 'registrar@university.edu' and 'registrar.university.edu' are 100% authentic.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "High-stakes deadlines from the Registrar are common in real life. Inspecting the SMTP headers confirms that 'registrar@university.edu' and 'registrar.university.edu' are authentic."
            });

            // ============================================================
            // S28 - Authority Bias - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S28",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.AuthorityBias,
                difficulty = "Hard",
                context = "Campus Public Safety parking citation appeal / permit verification.",
                displayName = "Department of Campus Public Safety",
                senderAddress = "publicsafety@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://publicsafety.university.edu/parking/vehicle-registry",
                subjectLine = "Notice: Unregistered Vehicle License Plate Warning in Lot 4",
                emailBody = "Notice from Campus Parking Enforcement,\n\nAutomated license-plate recognition (ALPR) scanners recorded a vehicle associated with your student parking permit parked in Lot 4 without an updated decal mapping.\n\nWhile a courtesy warning was issued today in lieu of a $75.00 citation, please verify your license plate number on the Campus Public Safety portal within 72 hours.\n\nDepartment of Campus Public Safety",
                warningSigns = "None. Parking citation warnings look like classic phishing bait, but 'publicsafety@university.edu' and 'publicsafety.university.edu' are completely genuine!",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Parking warnings are a favorite theme for both real campus police and phishers. The only way to tell them apart is checking the telemetry: 'publicsafety@university.edu' is verified and legitimate."
            });

            // ============================================================
            // S29 - Familiarity/Trust - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S29",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Canvas LMS third-party vendor domain ('instructure.com') notification.",
                displayName = "Canvas Learning Management System",
                senderAddress = "notifications@instructure.com",
                displayDomain = "instructure.com",
                rawHyperlinkUrl = "https://university.instructure.com/courses/10482/assignments/89412",
                subjectLine = "Plagiarism Scan Alert: Turnitin Similarity Report Generated for Submission",
                emailBody = "Automated Canvas LMS Notification:\n\nYour recent file upload for 'Project Milestone 2 - Cybersecurity VR' has been processed by the Turnitin Originality service.\n\nStatus: Similarity Index Flagged for Instructor Review.\n\nYou may view the annotated originality report and citation breakdown directly inside your Canvas course submission page.\n\nInstructure Canvas Notification System",
                warningSigns = "None. Uses Instructure's official domain ('instructure.com' / 'university.instructure.com') with full SPF/DKIM alignment.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Canvas LMS is legitimately hosted by Instructure ('instructure.com' / 'university.instructure.com'). Even though 'Turnitin Similarity Flagged' sounds alarming, the SPF/DKIM alignment confirms it is genuine."
            });

            // ============================================================
            // S30 - Familiarity/Trust - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S30",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.FamiliarityTrust,
                difficulty = "Hard",
                context = "Campus Bookstore digital textbook rental auto-charge reminder.",
                displayName = "University Campus Bookstore",
                senderAddress = "orders@campusbookstore.university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://campusbookstore.university.edu/rentals/return-label",
                subjectLine = "Rental Reminder: Return Course Equipment or Card on File Will Be Billed",
                emailBody = "Dear Student,\n\nYour semester rental period for 'XR Development Kit & Course Pack (Order #BK-88421)' concludes at the end of finals week.\n\nPer your rental agreement, unreturned items are automatically converted to a purchase ($145.00) on your student bursar account. Print your free return check-in slip or extend your rental on the Campus Bookstore portal.\n\nUniversity Bookstore Operations",
                warningSigns = "None. Mentions a '$145.00' charge for unreturned rentals, but originates from the verified 'campusbookstore.university.edu' domain.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "Rental_CheckIn_Barcode_BK88421.pdf",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Auto-charge reminders for textbook or lab rentals look suspicious at first glance, but 'campusbookstore.university.edu' is an authentic university subdomain."
            });

            // ============================================================
            // S31 - Social Proof/Reward - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S31",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "Course evaluation survey completion priority grade-view incentive.",
                displayName = "Office of Institutional Effectiveness",
                senderAddress = "course-evals@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://evals.university.edu/student/dashboard",
                subjectLine = "78% of Your Class Has Completed Course Evaluations - Unlock Early Grade View",
                emailBody = "Dear Student,\n\nEnd-of-term faculty and course evaluations are now open. Currently, 78% of students in your enrolled sections have submitted their anonymous feedback.\n\nStudents who complete evaluations for all enrolled courses before Friday at 23:59 unlock early online access to final course grades as soon as instructors submit them.\n\nOffice of Institutional Effectiveness",
                warningSigns = "None. Uses social proof ('78% of your class') and an incentive ('Early Grade View'), yet is sent from 'course-evals@university.edu' and hosted on 'evals.university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Many universities genuinely offer early grade viewing as an incentive to complete course evaluations. Checking the headers confirms 'evals.university.edu' is an official university server."
            });

            // ============================================================
            // S32 - Social Proof/Reward - Legitimate - Hard
            // ============================================================
            masterScenarioPool.Add(new PhishingScenarioData
            {
                scenarioId = "S32",
                classification = ThreatClassification.Legitimate,
                biasType = CognitiveBiasType.SocialProofReward,
                difficulty = "Hard",
                context = "University Career Center on-campus interview slot sign-up.",
                displayName = "University Career Development Center",
                senderAddress = "careercenter@university.edu",
                displayDomain = "university.edu",
                rawHyperlinkUrl = "https://career.university.edu/interviews/schedule-slot",
                subjectLine = "Urgent: 2 Remaining Interview Slots for Cybersecurity Internships",
                emailBody = "Dear Engineering Senior,\n\nBased on your resume profile in the university career system, you matched with technical recruiters attending next week's On-Campus Cybersecurity & XR Recruiting Day.\n\nNearly all interview timeslots have been claimed by fellow seniors—only 2 afternoon slots remain open. Reserve your 20-minute interview slot on the Career Portal today.\n\nUniversity Career Development Center",
                warningSigns = "None. Uses scarcity ('only 2 slots remain') and peer competition, but originates from 'careercenter@university.edu' and links to 'career.university.edu'.",
                hasGrammaticalAnomalies = false,
                hasDomainAnomaly = false,
                attachmentName = "",
                timePressureSeconds = 240f,
                correctAction = "Ignore / Read Without Reporting",
                correctiveFeedback = "Real career center emails often have limited interview slots! Because 'careercenter@university.edu' and 'career.university.edu' are authentic, this is a legitimate email."
            });

            GenerateBalancedDeck(24);
        }

        private static System.Random rngInstance;
        private static System.Random GetFreshRng()
        {
            int seed = Environment.TickCount ^ Guid.NewGuid().GetHashCode();
            UnityEngine.Random.InitState(seed);
            rngInstance = new System.Random(seed);
            return rngInstance;
        }

        public void GenerateBalancedDeck(int targetCount = 24)
        {
            if (masterScenarioPool == null || masterScenarioPool.Count == 0)
                InitializeDefaultScenarios();

            GetFreshRng();

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
            int halfCount = targetCount / 2;

            for (int i = 0; i < halfCount; i++)
                scenarioDeck.Add(phishPool[i % phishPool.Count]);

            for (int i = 0; i < targetCount - halfCount; i++)
                scenarioDeck.Add(legitPool[i % legitPool.Count]);

            // Perform multiple full Fisher-Yates passes on the combined deck for true unpredictable randomization
            ShuffleList(scenarioDeck);
            ShuffleList(scenarioDeck);

            // Prevent extreme streaks (>3 in a row of same type) while preserving genuine randomness
            for (int i = 3; i < scenarioDeck.Count; i++)
            {
                bool p0 = scenarioDeck[i - 3].classification != ThreatClassification.Legitimate;
                bool p1 = scenarioDeck[i - 2].classification != ThreatClassification.Legitimate;
                bool p2 = scenarioDeck[i - 1].classification != ThreatClassification.Legitimate;
                bool p3 = scenarioDeck[i].classification != ThreatClassification.Legitimate;
                if (p0 == p1 && p1 == p2 && p2 == p3)
                {
                    for (int j = i + 1; j < scenarioDeck.Count; j++)
                    {
                        bool pj = scenarioDeck[j].classification != ThreatClassification.Legitimate;
                        if (pj != p3)
                        {
                            var tmp = scenarioDeck[i];
                            scenarioDeck[i] = scenarioDeck[j];
                            scenarioDeck[j] = tmp;
                            break;
                        }
                    }
                }
            }

            Debug.Log($"[StratifiedDeckGenerator] Generated randomized balanced deck of {scenarioDeck.Count} cards (50% Phish / 50% Legit).");
        }

        private static void ShuffleList<T>(List<T> list)
        {
            if (rngInstance == null)
                GetFreshRng();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = rngInstance.Next(0, i + 1);
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