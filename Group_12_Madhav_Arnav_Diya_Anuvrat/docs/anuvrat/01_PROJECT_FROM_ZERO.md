# 01 - Project From Zero: Complete Beginner's Guide to the Concepts

This guide explains every concept in our research project from scratch, using everyday examples first and introducing technical terms only after the basic idea is clear.

---

## 1. What Is Phishing?

### Everyday Example
Imagine someone knocks on your apartment door wearing a fake uniform and holding a clipboard. They say: *"I'm from the building gas company! There's a gas leak on your floor—hand me your house keys right now so I can check your kitchen, or we'll fine you and shut off your utilities!"*

If you hand over your keys without checking their ID badge, they can rob your house. That is a confidence trick.

### In Cybersecurity
**Phishing** is the exact same trick performed through digital messages (usually emails). An attacker sends a fake message pretending to be someone you trust—like your university IT desk, your bank, or your company's CEO—to trick you into:
1. **Clicking a dangerous link** (which takes you to a fake login page that steals your password), or
2. **Opening a dangerous attachment** (which installs malicious software).

### How Legitimate and Phishing Emails Differ
In our Unity simulation (`PhishingThreatSimulationManager.cs`), participants see both **legitimate** emails and **phishing** emails:
* **Spear-Phishing vs. General Phishing:** General phishing (`DeceptivePhish`) is sent in bulk (like a fake "verify your campus account" notice). **Spear-phishing** (`SpearPhishTargeted`) is customized for a specific person or role (like an email pretending to be from the University Director about a "Q3 Audit Discrepancy" before a 2 PM board meeting).
* **Where the Clues Hide (Diagnostic Technical Indicators):**
  1. **Sender Address / Domain:** The display name might say `University IT Security`, but the actual email address is `security-admin@university-portal-verify.com` instead of the real `university.edu`.
  2. **Domain Homograph / Look-Alike Spoofs:** Attackers use URLs that look almost right at a quick glance (such as `university-portal-verify.com` or replacing an `l` with a `1`).
  3. **Raw Hyperlink URL:** The blue text on screen might say `university.edu/login`, but if you inspect the actual web address behind the link, it points to `https://university-portal-verify.com/auth?token=9281a` or ends in `.pdf.exe`.
  4. **Security Padlock / SSL Badge:** Whether the connection and domain security indicator match a trusted institutional certificate.

---

## 2. Human Factors and Dual-Process Thinking (System 1 vs. System 2)

### What "Human Factors" Means
A company can buy the strongest firewalls in the world, but a single human sits at the keyboard with permission to click links. **Human factors** studies how real people—when they are tired, busy, distracted, or stressed—interact with technology and make mistakes.

### System 1 vs. System 2 Thinking (Dual-Process Theory)
Psychologists describe two modes of human thinking (cited in our project via `Vishwanath2011` and `Yao2025`):
* **System 1 (Fast, Automatic, Habitual):** Your brain's autopilot. When you see a red traffic light or read a familiar logo, you react instantly without slow logical reasoning.
* **System 2 (Slow, Analytical, Deliberate):** Your brain's detective mode. When you solve a math problem or carefully read a sender's domain character-by-character (`u-n-i-v-e-r-s-i-t-y`), you are using System 2.

**Why Phishing Works:** Employees receive dozens of emails every day while rushing to finish deadlines. They stay in **System 1** autopilot. Phishing emails are specifically designed to keep the victim in System 1 so they click before their System 2 detective wakes up.

---

## 3. Cognitive Biases and Psychological Manipulation

A **cognitive bias** is a mental shortcut (a "heuristic") that our brain uses to make fast decisions. Attackers weaponize four specific biases in our project (implemented by Madhav in `PhishingThreatSimulationManager.cs`):

1. **Urgency & Scarcity Bias (`UrgencyScarcity`):**
   * *Example:* *"URGENT: Your campus account will be suspended in 2 hours!"*
   * *How it tricks you:* Fear of losing access creates time pressure. When people feel rushed, they stop checking technical details and click immediately to make the problem go away.
2. **Authority Bias (`AuthorityBias`):**
   * *Example:* *"From: Office of the Director — Confidential Q3 Audit Discrepancy."*
   * *How it tricks you:* Humans are trained to obey authority figures (bosses, deans, IT administrators) and feel nervous about questioning or delaying them.
3. **Familiarity & Trust Bias (`FamiliarityTrust`):**
   * *Example:* A routine-looking library overdue notice or software invoice using familiar colors and formatting.
   * *How it tricks you:* Because the email looks like something you have seen 100 times before (`Dhamija2006`), your brain assumes it is safe without checking the URL.
4. **Social Proof / Reward Bias (`SocialProofReward`):**
   * *Example:* *"85% of students have already claimed their Innovation Grant—complete your form now!"*
   * *How it tricks you:* People copy what their peers are doing and lower their guard when promised a reward.

---

## 4. Virtual Reality (VR) vs. 2D Web-Based Training

### Traditional 2D Web Training (Control Group)
In standard corporate/campus training (`2D_Web_Training`), a user sits at a regular flat monitor, clicks "Next" through a slideshow or video, and answers a multiple-choice quiz.
* **The Problem:** It is passive. Your brain treats it like reading a textbook factsheet. You rarely form a lasting personal memory of the experience.

### Immersive VR Training (Experimental Group)
In our project's VR simulation (`Immersive_VR_Training`), the participant wears a VR headset (6-Degrees-of-Freedom / 6-DoF) and sits inside a 3D virtual office with a floating curved monitor and a physical "Report Phish" alarm lever:
* **Why Researchers Test VR (`Zhong2026`, `Sheng2007`):**
  1. **Episodic ("Lived Experience") Memory:** Your brain remembers *events you physically experienced in a 3D space* much longer than text you read on a flat slide.
  2. **Embodied Action:** Physically reaching out to pull a reporting lever or visually scanning a 3D monitor builds procedural habits ("muscle memory" for where to look).
  3. **Safe Consequence Simulation:** If you click a phishing link in VR, the simulation can immediately show you how the attacker compromised your workstation, creating a memorable learning moment without any real-world damage.

---

## 5. Eye Tracking, Areas of Interest (AOIs), and Dwell Time

### How Eye Tracking Works in VR
Modern VR headsets have tiny infrared cameras pointed at the user's eyes. In Unity (`EyeGazeAttentionTracker.cs`), the headset projects an invisible laser beam (**a raycast**) straight out from the user's eyes onto the virtual computer screen to see which UI box (**Collider**) the user is looking at.

### Key Eye-Tracking Terms
* **Area of Interest (AOI):** A specific labeled box on the email screen that researchers care about. Our project defines **5 Email AOIs** (plus background):
  1. `SenderDomainHeader` (Who sent the email and their domain address)
  2. `SecurityPadlockBadge` (The SSL security lock icon)
  3. `SubjectLineUrgency` (The email's subject line)
  4. `BodyTextContent` (The main paragraph text of the email)
  5. `HyperlinkCallToAction` (The clickable link / URL preview)
* **Saccade:** A rapid jump of the eye from one word to another (lasting only 20 to 40 milliseconds). During a jump, the brain is not really reading.
* **Fixation:** When the eye holds steady on one spot for at least **150 milliseconds (`0.15 s`)** so the brain can process the visual detail.
* **Dwell Time:** The total number of seconds a participant spends fixating on a specific AOI while inspecting an email.
* **Threat Inspection Ratio (TIR / $R_{\text{inspect}}$):** The fraction of total email looking time spent inspecting the security-critical indicators (such as the sender domain and hyperlink URL) rather than just reading the emotional story in the body text (`Baltuttis2024`).
  * *Important Caution:* Looking at a warning sign (`TIR`) measures **visual attention**, not **comprehension**. A person can stare right at a fake URL and still click it if they do not realize the domain is fake!

---

## 6. Experimental Research Design and 14-Day Retention

### Why Split Participants into Groups?
To test whether VR training works better than 2D training, we use a **controlled experiment** with two dimensions:
1. **Between-Subjects Factor (Group / Modality):**
   * Half the participants (`n = 25`) are assigned to `2D_Web_Training`.
   * The other half (`n = 25`) are assigned to `Immersive_VR_Training`.
   * Because these are **different people** in each group, comparing VR vs. 2D requires **independent-group** statistical tests.
2. **Within-Subjects Factor (Time / Retention):**
   * Every participant is tested immediately after training (**Day 0**) and tested again two weeks later (**Day 14**).
   * Because the **exact same person** is measured at Day 0 and Day 14, comparing Day 0 vs. Day 14 requires **paired (repeated-measures)** statistical tests.

### What Is Retention and the Forgetting Curve?
**Retention** is how much of a learned skill remains after time passes without practice.
German psychologist Hermann Ebbinghaus showed that human memory decays exponentially over time—fast at first, then leveling off toward a floor ($A_\infty$):
$$A(t) = A_\infty + (A_0 - A_\infty) \cdot e^{-\lambda t}$$
* $A_0$: Accuracy on Day 0.
* $A_\infty$: Long-term floor accuracy.
* $\lambda$ (lambda): Decay rate (a larger $\lambda$ means faster forgetting).

---

## 7. Why We Use Questionnaires (SUS, NASA-TLX, Kennedy SSQ)

Even if a VR simulator teaches phishing detection well, it will fail in the real world if it is confusing to use, mentally exhausting, or makes people dizzy. So human-factors researchers measure three standard questionnaires:
1. **System Usability Scale (SUS):** 10 questions measuring how easy and comfortable the software feels to use (scored `0` to `100`; `68` is average, `80.3+` is Grade A).
2. **NASA Task Load Index (NASA-TLX):** 6 questions measuring how much mental, physical, and time pressure the user felt (scored `0` to `100`; lower means less exhausting).
3. **Kennedy Simulator Sickness Questionnaire (SSQ):** 16 symptoms measuring whether the VR headset caused nausea, eyestrain, or dizziness (`Total Severity <= 15.0` indicates safe ergonomics).

---

## 8. Why Statistical Analysis Is Needed

If Group A averages `88%` and Group B averages `58%`, why not just look at the averages and stop?
Because any small group of people has random variation! Statistical analysis answers three distinct questions:
1. **Descriptive Statistics (Mean, SD, Median, IQR):** What was a typical score, and how spread out were the participants?
2. **Uncertainty & Significance ($p$-value and 95% Confidence Interval):** How likely is it that a difference this large happened just by random luck? (A $p$-value $< 0.05$ or $< 0.001$ means random chance alone is a very unlikely explanation).
3. **Effect Size (Cohen's $d$ / Hedges' $g$):** *How big* is the difference compared to the variability between people? ($d = 0.2$ is small, $d = 0.5$ is medium, $d \ge 0.8$ is large).
4. **Scientific Integrity Rule:** A tiny $p$-value in a **synthetic** dataset only proves that the computer generator was programmed with different averages—it never proves a real-world human effect until real human trials are run!
