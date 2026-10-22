# 03 - Metrics and Statistics: Complete Reference Guide

This guide explains every formula, numerator, denominator, statistical test, effect size, and chart in your part of the project (`I080 - Anuvrat Tripathi`).

---

## 1. Phishing Recognition Performance Metrics

When a participant completes a session of email scenarios (for example, Madhav's 12-scenario deck containing 8 phishing emails and 4 legitimate emails), their decisions are scored as follows (`telemetry/performance_metrics.py`):

### A. Threat Detection Accuracy (%)
* **What it measures:** The percentage of all emails (both phishing and legitimate) where the participant took the correct action.
* **Formula:**
  $$\text{Accuracy (\%)} = \left(\frac{\text{Number of Correct Decisions}}{\text{Total Emails Presented}}\right) \times 100$$
* **Numerator:**
  * For a **phishing** email (`DeceptivePhish` or `SpearPhishTargeted`): participant reported the email (`didReportPhish == true`).
  * For a **legitimate** email (`Legitimate`): participant did **not** falsely report it (`!didReportPhish && (didClickLink || didIgnore)`).
* **Denominator:** Total number of email trials presented in that session.
* **Worked Example:** If a participant sees 12 emails (8 phishing, 4 legitimate), reports 7 of the 8 phishing emails, and ignores all 4 legitimate emails, they made $7 + 4 = 11$ correct decisions out of 12:
  $$\text{Accuracy} = \frac{11}{12} \times 100 = 91.67\%$$
* **What can make it misleading:** If a test deck had 95 legitimate emails and only 5 phishing emails, a participant who blindly ignored every single email would get `95%` accuracy while missing `100%` of the phishing attacks! That is why we must also measure **Malicious Click Rate**, **Phishing Report Rate**, and **False Positive Rate**.

### B. Malicious Link Click Rate (%)
* **What it measures:** How often a participant falls for a phishing trap by clicking its malicious link.
* **Formula:**
  $$\text{Malicious Click Rate (\%)} = \left(\frac{\text{Number of Phishing Emails Clicked}}{\text{Number of Eligible Phishing Emails Presented}}\right) \times 100$$
* **Numerator:** Number of phishing trials where `didClickLink == true`.
* **Denominator:** Number of **phishing** trials presented (legitimate emails are **excluded** from the denominator because clicking a safe link in a legitimate campus announcement is not a malicious click).
* **Worked Example:** Out of 8 phishing emails, if the participant clicks 1 malicious link:
  $$\text{Malicious Click Rate} = \frac{1}{8} \times 100 = 12.50\%$$

### C. Phishing Report Rate (Sensitivity / True Positive Rate) & Legitimate False-Positive Rate
* **Phishing Report Rate (%):** $\frac{\text{Phishing Emails Reported}}{\text{Total Phishing Emails}} \times 100$
* **False-Positive Rate (%):** $\frac{\text{Legitimate Emails Falsely Reported as Phish}}{\text{Total Legitimate Emails}} \times 100$
  * *Why False-Positive Rate matters:* If training makes employees paranoid so they report every single legitimate email from their boss as "phishing," productivity collapses. Good training keeps Phishing Report Rate high and False-Positive Rate low.

---

## 2. Eye-Gaze Metrics and Threat Inspection Ratio (TIR)

### A. Fixation and Dwell Time
* **Fixation Threshold:** A gaze raycast intersection on an Area of Interest (AOI) collider that remains continuously inside that AOI for $\Delta t \ge 0.15\text{ s}$ (`150 ms`). Rapid eye jumps shorter than `150 ms` (saccades) are ignored.
* **AOI Dwell Time ($T_{\text{AOI}}$):** The cumulative duration (in seconds) of all valid fixations on that AOI during an email trial, including any ongoing fixation flushed when the trial ends (`FlushActiveFixation()`).

### B. Threat Inspection Ratio (TIR / $R_{\text{inspect}}$)
* **Denominator (Total Email Dwell Time):** Sum of fixation dwell times across all **5 email AOIs** (excluding `IrrelevantBackground` and excluding missing/invalid gaze samples):
  $$T_{\text{Email\_Total}} = T_{\text{SenderDomain}} + T_{\text{SecurityBadge}} + T_{\text{SubjectLine}} + T_{\text{BodyText}} + T_{\text{Hyperlink}}$$
* **Three Documented Formula Variants (Reconciled in `gaze_metrics.py` & `EyeGazeAttentionTracker.cs`):**
  1. **Primary Formula (`sender_domain_hyperlink` / `ThreatInspectionFormula.SenderDomainAndHyperlink`):**
     $$\text{TIR}_{\text{primary}} = \frac{T_{\text{SenderDomain}} + T_{\text{Hyperlink}}}{T_{\text{Email\_Total}}}$$
     *(Matches `RESEARCH_AND_IMPLEMENTATION_GUIDE.md` Eq. 1.1 and the original `I080` C# specification; note that Sender and Domain share the `AOI_SenderDomain` collider in the Unity scene).*
  2. **All Security Cues Variant (`all_security_cues` / `ThreatInspectionFormula.AllSecurityCuesIncludePadlock`):**
     $$\text{TIR}_{\text{all\_cues}} = \frac{T_{\text{SenderDomain}} + T_{\text{SecurityBadge}} + T_{\text{Hyperlink}}}{T_{\text{Email\_Total}}}$$
     *(Includes the SSL security padlock badge collider).*
  3. **Sender Domain Only Variant (`sender_domain_only` / `ThreatInspectionFormula.SenderDomainOnly`):**
     $$\text{TIR}_{\text{domain}} = \frac{T_{\text{SenderDomain}}}{T_{\text{Email\_Total}}}$$
     *(Matches `RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md` Section 3.4).*
* **Zero-Denominator Rule:** If $T_{\text{Email\_Total}} \le 0$, TIR is **undefined** (`None` in Python, `-1f` / blank CSV cell in C#), never `0.0`.

---

## 3. Longitudinal Retention Metrics (Day 0 vs. Day 14)

When comparing a measurement at **Day 0** ($Y_0$) and **Day 14** ($Y_{14}$), there are three different ways to describe the change. Knowing the difference is a classic viva question!

###Worked Example Using VR vs. 2D Web Detection Accuracy (`phishing_simulation_benchmark.csv`)

| Quantity | Formula | Immersive VR (`n = 25`) | 2D Web (`n = 25`) | Plain-English Meaning |
| :--- | :--- | :--- | :--- | :--- |
| **Day 0 Mean ($Y_0$)** | $\bar{Y}_0$ | `92.21%` | `72.78%` | Immediate post-training accuracy. |
| **Day 14 Mean ($Y_{14}$)** | $\bar{Y}_{14}$ | `88.14%` | `57.51%` | Accuracy 14 days later without refresher. |
| **1. Raw / Percentage-Point Change ($\Delta$)** | $Y_{14} - Y_0$ | **`-4.08 pp`** | **`-15.27 pp`** | Absolute subtraction on the percentage scale. |
| **2. Relative Percentage Change (%)** | $\frac{Y_{14} - Y_0}{Y_0} \times 100$ | **`-4.42%`** | **`-20.98%`** | Change expressed as a fraction of the starting Day 0 score. |
| **3. Retention Ratio (%)** | $\frac{Y_{14}}{Y_0} \times 100$ | **`95.58%`** | **`79.02%`** | Percentage of Day 0 performance kept at Day 14. |

**Why the Distinction Matters:**
If someone's click rate rises from `5.0%` on Day 0 to `7.5%` on Day 14:
* The **percentage-point change** is $+2.5\text{ percentage points}$ (`7.5 - 5.0`).
* The **relative percentage change** is $+50.0\%$ (`(7.5 - 5.0) / 5.0 * 100`).
Never say "click rate increased by 2.5%" without specifying whether you mean **percentage points** or **relative percent**!

---

## 4. Statistical Tests: Which Test We Use and Why

Our study has a **2 (Group: VR vs. 2D Web) $\times$ 2 (Time: Day 0 vs. Day 14) mixed design**:

| Research Question | Data Structure | Parametric Test Used | Effect Size | Non-Parametric Cross-Check | Why This Method Is Correct |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Did VR outperform 2D Web at Day 0 or Day 14?** | **Two Independent Groups** (25 VR people vs. 25 different Web people) | **Welch's two-sample $t$-test** (`su.welch_t_test`) | **Cohen's $d$ & Hedges' $g$** (`su.hedges_g`) | **Mann-Whitney $U$ test** (`su.mann_whitney_u`) | Welch's $t$-test compares two independent groups without assuming they have identical variances (important because the 2D Web group has a larger spread than the VR group). |
| **2. Did accuracy change from Day 0 to Day 14 within the VR (or Web) cohort?** | **Paired / Repeated Measures** (the exact same 25 people tested twice) | **Paired $t$-test** on $D_i = Y_{i,14} - Y_{i,0}$ (`su.paired_t_test`) | **Paired Cohen's $d_z$** (`su.cohens_dz`) | **Wilcoxon signed-rank test** (`su.wilcoxon_signed_rank`) | An independent $t$-test between Day 0 and Day 14 would be **wrong** because Day 0 and Day 14 scores belong to the same person and are strongly correlated. A paired $t$-test analyzes each person's individual change $D_i$. |
| **3. Did the 2D Web group forget significantly more over 14 days than the VR group?** | **Difference-in-Differences (Interaction)** comparing $\Delta_{\text{VR}}$ vs. $\Delta_{\text{Web}}$ | **Welch's two-sample $t$-test** on the individual change scores $\Delta_i = Y_{i,14} - Y_{i,0}$ | **Cohen's $d$ & Hedges' $g$ on $\Delta$** | **Mann-Whitney $U$ on $\Delta$** | Directly tests the interaction between Training Modality and Time: whether the 14-day drop in VR (`-4.08 pp`) is smaller than the 14-day drop in 2D Web (`-15.27 pp`). Difference = `+11.19 pp`, $t(36.0) = 14.99, p < 0.001$. |
| **4. Do multiple tests inflate false positives?** | 9 primary hypothesis tests across metrics | **Holm-Bonferroni step-down adjustment** (`su.holm_adjust`) | N/A | N/A | Controls the family-wise error rate across all primary comparisons. |

---

## 5. Complete Summary Table of Computed Results (`phishing_simulation_benchmark.csv`, $N = 50$)

> **PROVENANCE NOTICE:** All numbers below are computed directly from the 50-row synthetic benchmark dataset (`phishing_simulation_benchmark.csv`, `SHA-256: 67d07bc1...`, generated with `seed = 42`).

| Metric | 2D Web Day 0 ($n=25$) | 2D Web Day 14 ($n=25$) | Web 14-Day Change ($\Delta$) | Immersive VR Day 0 ($n=25$) | Immersive VR Day 14 ($n=25$) | VR 14-Day Change ($\Delta$) | Day 14 Between-Group Welch $t$, $p$ (Holm-$p$), Cohen's $d$ | DiD ($\Delta_{\text{VR}} - \Delta_{\text{Web}}$) Welch $t$, $p$, Cohen's $d$ |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Detection Accuracy (%)** | $72.78 \pm 5.36$ | $57.51 \pm 6.82$ | $-15.27\text{ pp}$ ($p < 0.001$) | $92.21 \pm 3.83$ | $88.14 \pm 4.16$ | $-4.08\text{ pp}$ ($p < 0.001$) | $t(39.7) = 19.17$, $p < 0.001$ ($p_{\text{Holm}} < 0.001$), $d = 5.42$ | $+11.19\text{ pp}$, $t(36.0) = 14.99$, $p < 0.001$, $d = 4.24$ |
| **Malicious Click Rate (%)** | $18.75 \pm 4.98$ | $26.86 \pm 6.87$ | $+8.10\text{ pp}$ ($p < 0.001$) | $4.44 \pm 1.68$ | $6.86 \pm 2.02$ | $+2.43\text{ pp}$ ($p < 0.001$) | $t(28.1) = -13.95$, $p < 0.001$ ($p_{\text{Holm}} < 0.001$), $d = -3.95$ | $-5.68\text{ pp}$, $t(29.9) = -8.18$, $p < 0.001$, $d = -2.31$ |
| **Diagnostic AOI Dwell (s)** | $0.86 \pm 0.17$ | $0.55 \pm 0.22$ | $-0.31\text{ s}$ ($p < 0.001$) | $2.58 \pm 0.39$ | $2.26 \pm 0.45$ | $-0.32\text{ s}$ ($p < 0.001$) | $t(34.7) = 16.91$, $p < 0.001$ ($p_{\text{Holm}} < 0.001$), $d = 4.78$ | $-0.02\text{ s}$, $t(40.1) = -0.38$, $p = 0.709$ (NS), $d = -0.11$ |
| **Cognitive Bias Score (1-10)** | $6.80 \pm 1.33$ | $8.53 \pm 1.15$ | $+1.73\text{ pts}$ ($p < 0.001$) | $2.83 \pm 0.70$ | $3.27 \pm 0.77$ | $+0.44\text{ pts}$ ($p < 0.001$) | $t(41.9) = -19.03$, $p < 0.001$ ($p_{\text{Holm}} < 0.001$), $d = -5.38$ | $-1.29\text{ pts}$, $t(47.3) = -10.81$, $p < 0.001$, $d = -3.06$ |
| **System Usability Scale (0-100)** | $63.13 \pm 7.97$ | N/A | N/A | $87.27 \pm 4.28$ | N/A | N/A | $t(36.7) = 13.34$, $p < 0.001$ ($p_{\text{Holm}} < 0.001$), $d = 3.77$ | N/A |

### Notice That Interesting Result on Diagnostic AOI Dwell Time DiD ($p = 0.709$)!
Look closely at the third row (**Diagnostic AOI Dwell (s)**):
* At Day 0, VR dwell is much higher than Web dwell (`2.58 s` vs. `0.86 s`, $p < 0.001$).
* At Day 14, VR dwell is still much higher than Web dwell (`2.26 s` vs. `0.55 s`, $p < 0.001$).
* However, the **absolute drop** from Day 0 to Day 14 is `-0.32 s` in VR and `-0.31 s` in Web ($p = 0.709$, not statistically significant)! Why? Because the synthetic generator subtracted `random.gauss(0.28, 0.15)` from VR and `random.gauss(0.32, 0.12)` from Web—nearly the same absolute number of seconds! Yet in **relative terms**, `-0.31 s` out of `0.86 s` is a **`35.7%` loss** for Web, whereas `-0.32 s` out of `2.58 s` is only a **`12.5%` loss** for VR. Pointing this out in your viva will show the examiners that you genuinely analyzed the data rather than just repeating summary claims!

---

## 6. How to Interpret Each Generated Figure (`docs/figures/`)

1. **`figure1_system_architecture.png`:**
   * Shows the 4 software layers (Madhav's Threat Scenario Engine, Arnav's VR Office Core, Diya's Eye-Gaze Engine, and your Human Factors & 14-Day Retention Analytics layer) and how data flows from top to bottom.
2. **`figure2_kinematic_telemetry.png`:**
   * **Panel (A):** Bar chart with 95% confidence interval error bars comparing Mean Diagnostic Security Cue Dwell Time (in seconds) at Day 0 and Day 14 between 2D Web (`0.86 s` $\rightarrow$ `0.55 s`) and Immersive VR (`2.58 s` $\rightarrow$ `2.26 s`).
   * **Panel (B):** 14-day retention curve showing observed cohort means $\pm$ 95% CI at Day 0 and Day 14 alongside the exponential forgetting trajectory ($A(t) = A_\infty + (A_0 - A_\infty)e^{-\lambda t}$), illustrating the `+30.63 pp` Day 14 retention gap.
3. **`figure3_comparative_performance.png`:**
   * 4-panel boxplot + group-mean diamond overlay comparing 2D Web vs. Immersive VR on (A) Day 14 Detection Accuracy, (B) Day 14 Malicious Click Rate, (C) Day 14 Cognitive Bias Susceptibility Score, and (D) System Usability Scale (SUS).
   * In each boxplot: the colored box spans the 25th to 75th percentile (middle 50% of participants), the horizontal line inside the box is the **median**, the black diamond (`D`) is the **group mean**, and the whiskers show the data spread.
