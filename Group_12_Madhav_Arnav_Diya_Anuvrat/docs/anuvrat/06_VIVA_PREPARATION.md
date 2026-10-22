# 06 - Individual Viva Defense Preparation Guide (Anuvrat Tripathi - I080)

In Milestone 4, 15 marks are allocated to your **Individual Oral Viva Defense**, where examiners question each student directly on their declared specialty (**Human Factors & Retention Analyst**).

This guide gives you **15 likely faculty questions** and **concise, honest, scientifically accurate answers** in plain English.

---

## Part 1: Questions About Your Role and Data Integrity

### Q1: "Anuvrat, what was your exact technical contribution to Group 12?"
**Your Answer:**
> "I served as the Human Factors and Retention Analyst (`I080`). My work covered four connected areas:
> 1. **In Unity C# (`EyeGazeAttentionTracker.cs`):** I implemented the telemetry export and Threat Inspection Ratio (`TIR`) calculation, including flushing active gaze fixations when a trial ends so ongoing eye dwell is not lost, and exporting a 22-column CSV schema.
> 2. **Statistical & 14-Day Retention Pipeline (`human_factors_retention_analysis.py` & `stats_utils.py`):** I built a read-only validation and statistical engine that checks CSV integrity via `SHA-256`, calculates Day 0 to Day 14 retention changes, runs paired $t$-tests and Wilcoxon tests within groups, and runs Welch's two-sample $t$-tests, Mann-Whitney $U$ tests, Cohen's $d$/Hedges' $g$, and Holm-Bonferroni corrections between the VR and 2D Web cohorts.
> 3. **Psychometric & Technoeconomic Validation (`sus_calculator.py`, `nasa_tlx_calculator.py`, `kennedy_ssq_calculator.py`, `telecom_latency_debias_eval.py`):** I validated the usability, workload, and cybersickness calculators—fixing a weight and symptom-mapping bug in the Kennedy SSQ calculator—and connected the technoeconomic parity model ($\kappa = 0.24$) to our dataset.
> 4. **Publication Figures & Unit Tests (`generate_paper_figures.py`, `test_evaluation_tools.py`):** I made the 300 DPI figure pipeline non-destructive and wrote 14 automated unit tests verifying every calculation."

---

### Q2: "Is the 50-participant dataset in `phishing_simulation_benchmark.csv` from real human subjects?"
**Your Answer (Be 100% honest—examiners respect integrity and penalize fabrication):**
> "No. `phishing_simulation_benchmark.csv` is a **synthetic benchmark dataset** of 50 simulated participant records (25 in `Immersive_VR_Training` and 25 in `2D_Web_Training`) generated with a fixed random seed (`seed = 42`). In my analysis code, reports, and figures, I explicitly label it as a synthetic benchmark dataset. It validates that our telemetry, statistical pipeline, and figure generation work end-to-end, while clearly distinguishing synthetic benchmark results from real human experimental evidence."

---

### Q3: "Why did you modify `generate_paper_figures.py` so it no longer generates the CSV automatically?"
**Your Answer:**
> "In the original starter code, running `generate_paper_figures.py` overwrote `phishing_simulation_benchmark.csv` every time and plotted hardcoded numbers without running statistical tests on the file. In a reproducible research pipeline, an analysis or plotting script must treat input data as **read-only**. I separated synthetic data generation into an opt-in function with overwrite protection and added a `SHA-256` checksum check so figure generation never alters the source CSV."

---

### Q4: "Why do the numbers in `README.md` (like VR Day 14 accuracy `88.6%` vs Web `57.6%`, `d = 2.84`) differ slightly from the averages your script calculates from `phishing_simulation_benchmark.csv` (`88.14%` vs `57.51%`, `d = 5.42`)?"
**Your Answer:**
> "That happens because `README.md` listed the **population parameters** ($\mu$ and $\sigma$) that were typed into `random.gauss(mu, sigma)` when generating the synthetic dataset. When you draw a finite random sample of $n = 25$ rows per group with `seed = 42`, the realized sample mean and sample standard deviation fluctuate slightly around the theoretical generator parameters (for example, `88.14%` instead of `88.60%`). Furthermore, in the generator, Day 14 was computed by subtracting a random decay from Day 0, which changes the sample variance and therefore changes the empirical Cohen's $d$. My analysis script includes a reconciliation table that shows both the theoretical generator parameters and the exact sample statistics computed from the 50 CSV rows."

---

## Part 2: Questions About Phishing Metrics and Retention

### Q5: "How do you define Accuracy vs. Malicious Click Rate, and why do their denominators differ?"
**Your Answer:**
> "**Accuracy** is the percentage of *all* email scenarios (both phishing and legitimate) where the participant made the correct decision: reporting phishing emails and not falsely reporting legitimate emails. Its denominator is total trials presented (12 in Madhav's deck).
> **Malicious Click Rate**, on the other hand, measures how often a participant clicked a link inside a *phishing* email. Its denominator includes **only eligible phishing trials** (8 in Madhav's deck), because clicking a safe link inside a legitimate campus email is not a malicious compromise."

---

### Q6: "In your retention analysis, VR accuracy dropped from `92.21%` on Day 0 to `88.14%` on Day 14. What is the difference between the percentage-point change, the relative percentage change, and the retention ratio?"
**Your Answer:**
> "All three compare Day 14 (`88.14%`) to Day 0 (`92.21%`), but with different denominators:
> 1. **Raw / Percentage-Point Change** is simple subtraction: $88.14 - 92.21 = \mathbf{-4.08\text{ percentage points}}$ (compare that to **$-15.27\text{ percentage points}$** in 2D Web).
> 2. **Relative Percentage Change** divides that change by the starting Day 0 score: $\frac{-4.08}{92.21} \times 100 = \mathbf{-4.42\%}$ (compared to **$-20.98\%$** in 2D Web).
> 3. **Retention Ratio** is the share of Day 0 performance kept at Day 14: $\frac{88.14}{92.21} \times 100 = \mathbf{95.6\%}$ retained in VR, versus **`79.0%`** retained in 2D Web."

---

## Part 3: Questions About Statistical Methods

### Q7: "Why did you use a Paired $t$-test for Day 0 vs. Day 14, but Welch's two-sample $t$-test for VR vs. 2D Web?"
**Your Answer:**
> "Because the experimental structure is different:
> * **Day 0 vs. Day 14 within a cohort** measures the **exact same 25 participants** at two time points. Their Day 0 and Day 14 scores are paired, so we first calculate each person's individual change ($\Delta_i = \text{Day 14}_i - \text{Day 0}_i$) and run a **paired $t$-test** (along with a non-parametric Wilcoxon signed-rank check).
> * **Immersive VR vs. 2D Web** compares **two separate, independent groups** of 25 people each. Because the two groups also have different spreads (for example, Day 14 accuracy SD is `6.82%` in Web vs. `4.16%` in VR), I used **Welch's two-sample $t$-test**, which does not assume equal variances between groups."

---

### Q8: "How do you test whether VR actually improved *retention over time* rather than just having a higher starting score on Day 0?"
**Your Answer:**
> "We use a **Difference-in-Differences (DiD)** comparison on the 14-day change scores: we compare each VR participant's 14-day change ($\Delta_{\text{VR}}$, mean `-4.08 pp`) against each 2D Web participant's 14-day change ($\Delta_{\text{Web}}$, mean `-15.27 pp`) using Welch's $t$-test. In the benchmark dataset, the difference in decay is $+11.19\text{ percentage points}$ in favor of VR ($t(36.0) = 14.99, p < 0.001$, Cohen's $d = 4.24$). That directly tests whether the slope of forgetting differs between the two training modalities."

---

### Q9: "Did every single Difference-in-Differences test in the dataset come out statistically significant?"
**Your Answer:**
> "No! One very instructive finding is on **Diagnostic AOI Dwell Time**. Even though VR participants looked at security cues much longer than 2D Web participants at both Day 0 (`2.58 s` vs. `0.86 s`, $p < 0.001$) and Day 14 (`2.26 s` vs. `0.55 s`, $p < 0.001$), the **absolute 14-day drop in seconds** was almost identical (`-0.32 s` in VR vs. `-0.31 s` in Web), giving a Difference-in-Differences $p$-value of `0.709` (not significant). However, in **relative terms**, losing `0.31 s` from a `0.86 s` baseline meant 2D Web participants lost `35.7%` of their inspection dwell time, whereas VR participants lost only `12.5%`."

---

### Q10: "Why did you include Mann-Whitney $U$, Wilcoxon signed-rank, and Holm-Bonferroni adjustments?"
**Your Answer:**
> "* **Mann-Whitney $U$ and Wilcoxon signed-rank** are non-parametric rank tests. They check that our results remain valid even if a variable is skewed or clamped at a boundary (like the 1-to-10 cognitive bias score, where several Day 14 Web participants hit the ceiling of `10.0`).
> * **Holm-Bonferroni** adjusts $p$-values when we run multiple hypothesis tests across several metrics so we control the family-wise false-positive rate."

---

## Part 4: Questions About Eye Tracking, Questionnaires, and Technoeconomics

### Q11: "What is the Threat Inspection Ratio (TIR), and what bug did you fix in `EyeGazeAttentionTracker.cs`?"
**Your Answer:**
> "The **Threat Inspection Ratio (`TIR`)** is the fraction of a participant's total email gaze dwell time spent fixating on diagnostic security indicators (such as the sender domain header and hyperlink URL) rather than distracting narrative body text.
> I fixed three issues in `EyeGazeAttentionTracker.cs`:
> 1. **Active Fixation Flushing:** Originally, dwell time was only saved when the user looked *away* from an AOI. If a user was still looking at a link when they clicked it to end the trial, that final fixation was lost. I added `FlushActiveFixation()` so the final fixation is committed before computing TIR.
> 2. **Complete Denominator:** I included all 5 email AOIs (`SenderDomainHeader`, `SecurityPadlockBadge`, `SubjectLineUrgency`, `BodyTextContent`, `HyperlinkCallToAction`) in total email dwell time.
> 3. **Zero-Dwell Safety & Formula Reconciliation:** If total email dwell is zero, the code returns `-1f` (and outputs a blank CSV cell) instead of dividing by zero or reporting a fake `0.000`, and exports all three TIR definitions documented across our project guides."

---

### Q12: "Does a high Threat Inspection Ratio (TIR) prove that the user understood the threat?"
**Your Answer:**
> "No. Eye tracking measures **visual attention allocation** (where the eyes fixated for at least 150 milliseconds), not **cognitive comprehension**. A participant could stare at a spoofed domain like `university-portal-verify.com` for 3 seconds because they are confused, and still click the link. That is why we pair eye-gaze telemetry with behavioral decision outcomes (accuracy and malicious click rate)."

---

### Q13: "How does System Usability Scale (SUS) scoring work?"
**Your Answer:**
> "SUS has 10 questions rated from 1 (Strongly Disagree) to 5 (Strongly Agree). Odd-numbered questions (1, 3, 5, 7, 9) are positive statements, so their contribution is `score - 1` (0 to 4). Even-numbered questions (2, 4, 6, 8, 10) are negative statements, so their contribution is `5 - score` (0 to 4). We sum all 10 contributions (giving a number from 0 to 40) and multiply by `2.5` to scale it from `0` to `100`. A score of `68` is the industry average (Grade C), and `80.3` or above is Grade A."

---

### Q14: "What bug did you find and fix in `kennedy_ssq_calculator.py`?"
**Your Answer:**
> "In the original `kennedy_ssq_calculator.py`, the **Oculomotor subscale weight** was erroneously set to `3.74` (which is the Total Severity multiplier) instead of the canonical **Kennedy et al. (1993) weight of `7.58`**. In addition, the 16th symptom (`fullness_of_head`) was missing, and `stomach_awareness` and `burping` were placed inside the `Disorientation` list instead of `fullness_of_head`, `nausea`, and `blurred_vision`. I restored all 16 standard symptoms and verified the exact Kennedy (1993) weights: `Nausea = 9.54`, `Oculomotor = 7.58`, `Disorientation = 13.92`, and `Total = 3.74`."

---

### Q15: "Explain the dimensionless cost parity ratio $\kappa = 0.24$ and payback period (`15.79 months`) in `telecom_latency_debias_eval.py`. Why are there no currency symbols?"
**Your Answer:**
> "Under our course evaluation policy, all technoeconomic models must be **dimensionless** (zero currency symbols) so that engineering comparisons remain valid across countries and inflation regimes.
> * We normalize traditional 2D web operational expenditure ($\text{OpEx}_{\text{Web}}$) to `1.00` (`0.25` LMS licensing + `0.52` SOC investigation overhead + `0.23` employee downtime tickets).
> * Under VR training, normalized operational expenditure ($\text{OpEx}_{\text{VR}}$) is `0.24` (`0.10` headset MDM/hygiene + `0.09` scenario updates + `0.05` residual incident containment).
> * Their ratio is the dimensionless cost parity ratio $\kappa = \frac{0.24}{1.00} = 0.24$, representing a `76.0%` reduction in annual operational burden.
> * With initial capital hardware expenditure normalized to $K_{\text{capex}} = 1.0$, the capital payback period is $\frac{12 \times 1.0}{1.00 - 0.24} = \frac{12}{0.76} = 15.79\text{ operating months}$."
