# 02 - Code Walkthrough: File-by-File and Function-by-Function Guide

This document explains every file created or modified for your role (`I080 - Anuvrat Tripathi`), how the functions work line by line, how the files connect, and how to troubleshoot common errors.

---

## 1. Architecture Map: How Your Files Connect

```
telemetry/phishing_simulation_benchmark.csv   (Protected 50-row input dataset)
        │
        ▼
telemetry/csv_utils.py                        (Safe CSV reading, float parsing, SHA-256, overwrite protection)
        │
        ├──► telemetry/stats_utils.py         (Pure-Python t-tests, Welch, paired t, Cohen's d, Mann-Whitney, Wilcoxon, Holm)
        │           │
        │           ▼
        ├──► telemetry/human_factors_retention_analysis.py   (Full Day 0 vs Day 14 validation & statistical report)
        │           │
        │           ├──► telemetry/generate_paper_figures.py (Renders 300 DPI Figures 1, 2, and 3 into docs/figures/)
        │           └──► telemetry/telecom_latency_debias_eval.py (Technoeconomic labor & cost parity ratio kappa)
        │
        ├──► telemetry/performance_metrics.py (Scores raw trial-level click/report/ignore logs)
        ├──► telemetry/gaze_metrics.py        (Python twin of C# Threat Inspection Ratio & gaze CSV validator)
        └──► telemetry/sus_calculator.py, nasa_tlx_calculator.py, kennedy_ssq_calculator.py (Psychometric engines)
                    │
                    ▼
        telemetry/test_evaluation_tools.py    (14 automated unit tests + Sprint 0 verification suite)
```

And on the Unity C# side:
* `Assets/Scripts/EyeGazeAttentionTracker.cs` tracks real-time gaze fixations across the 5 email Areas of Interest (AOIs), flushes the active fixation when a trial ends, computes the Threat Inspection Ratio (TIR), and exports CSV rows matching `telemetry/gaze_metrics.py`.

---

## 2. `telemetry/csv_utils.py` — Safe File I/O and Validation Helpers

### Purpose
Provides a single, reliable way for every Python script to read CSV files, check file fingerprints (`SHA-256`), parse numbers without turning blank cells into `0.0`, and prevent any script from accidentally overwriting `phishing_simulation_benchmark.csv`.

### Key Functions
* **`read_csv_rows(path)`**:
  * **Input:** Path to a CSV file.
  * **Output:** A tuple `(header, rows)` where `header` is a list of column names and `rows` is a list of dictionaries (`{column_name: string_value}`).
  * **Why it matters:** Uses `encoding="utf-8-sig"` so if someone opens and saves a CSV in Microsoft Excel, the invisible Byte Order Mark (`BOM`) character at the start of the file is stripped automatically.
* **`parse_optional_float(text)`**:
  * **Input:** Raw string from a CSV cell (e.g., `"83.89"`, `""`, or `"abc"`).
  * **Output:** `(value, problem_message)`.
  * **Walkthrough:**
    ```python
    if text is None or str(text).strip() == "":
        return None, None  # Blank cell -> None (missing), NOT 0.0!
    value = float(str(text).strip())
    if not math.isfinite(value):
        return None, f"not a finite number: {text!r}"  # Rejects NaN and Inf
    return value, None
    ```
* **`file_sha256(path)`**:
  * Computes a 64-character cryptographic fingerprint of a file. If even a single character in `phishing_simulation_benchmark.csv` changes, the `SHA-256` hash changes.
* **`assert_not_protected(output_path, protected_paths)`**:
  * Raises a `PermissionError` if any function tries to write to `phishing_simulation_benchmark.csv`.

---

## 3. `telemetry/stats_utils.py` — Pure-Python Statistical Engine

### Purpose
Lab machines often do not have `scipy` installed. This module implements exact, textbook statistical functions using only Python's standard `math` module, cross-checked against `scipy.stats` to within $10^{-11}$ relative precision.

### Key Functions
* **`describe(values)`**:
  * Computes count (`n`), missing count (`n_missing`), `mean`, sample standard deviation (`sd`, dividing by $n - 1$), `median`, quartiles (`q1`, `q3`), `min`, `max`, `skewness`, and the exact $t$-distribution 95% confidence interval (`ci95_low`, `ci95_high`).
* **`welch_t_test(group_a, group_b, confidence=0.95)`**:
  * **When used:** Comparing **two independent groups** (e.g., the 25 VR participants vs. the 25 2D Web participants on Day 14).
  * **Formula:**
    $$t = \frac{\bar{X}_A - \bar{X}_B}{\sqrt{\frac{s_A^2}{n_A} + \frac{s_B^2}{n_B}}}$$
  * Uses the Welch-Satterthwaite degrees of freedom so it remains accurate even when the two groups have different standard deviations.
* **`paired_t_test(before, after, confidence=0.95)`**:
  * **When used:** Comparing **repeated measurements on the same participants** (e.g., Day 0 vs. Day 14 within the VR group).
  * **Walkthrough:**
    ```python
    diffs = [a - b for b, a in pairs]      # Step 1: Change per person (Day 14 - Day 0)
    mean_diff, sd_diff = mean(diffs), sample_sd(diffs)
    se = sd_diff / math.sqrt(n)            # Step 2: Standard error of paired changes
    t_stat = mean_diff / se                # Step 3: Paired t-statistic with df = n - 1
    ```
* **`hedges_g(group_a, group_b)` & `cohens_dz(before, after)`**:
  * Computes standardized effect sizes:
    * Between-group **Cohen's $d$** divides the group mean difference by the pooled standard deviation $s_{\text{pooled}}$.
    * **Hedges' $g$** multiplies Cohen's $d$ by the small-sample correction factor $J = 1 - \frac{3}{4(n_1 + n_2) - 9}$.
    * Paired **Cohen's $d_z$** divides the mean paired change by the standard deviation of the paired changes.
* **`mann_whitney_u(group_a, group_b)` & `wilcoxon_signed_rank(before, after)`**:
  * Non-parametric rank-based tests that do not assume a bell-shaped normal distribution. Used to verify that our conclusions do not depend on normality assumptions.
* **`holm_adjust(p_values)`**:
  * Adjusts $p$-values across multiple comparisons using the Holm-Bonferroni step-down procedure so we do not get false positives from running multiple tests.

---

## 4. `telemetry/human_factors_retention_analysis.py` — Core Analysis Pipeline

### Purpose
Loads and validates `telemetry/phishing_simulation_benchmark.csv`, computes all Day 0 and Day 14 retention metrics, executes all paired and between-group statistical tests, reconciles the CSV sample statistics with the documented generator parameters, and prints a structured console or JSON report.

### Key Functions
* **`validate_and_load_benchmark_csv(csv_path)`**:
  * Checks that all 11 required columns exist, checks that `participant_id` values (`P001`..`P050`) are unique, verifies that `training_modality` is either `Immersive_VR_Training` or `2D_Web_Training`, and verifies every numeric value against `COLUMN_BOUNDS`.
  * Computes per-participant retention columns:
    * `delta_<metric>` = $\text{Day 14} - \text{Day 0}$
    * `rel_change_pct_<metric>` = $\frac{\text{Day 14} - \text{Day 0}}{\text{Day 0}} \times 100$
    * `retention_ratio_pct_<metric>` = $\frac{\text{Day 14}}{\text{Day 0}} \times 100$
* **`run_full_retention_analysis(csv_path)`**:
  * Runs the complete statistical suite for all 4 longitudinal metrics (`detection_accuracy_pct`, `malicious_click_rate_pct`, `security_aoi_dwell_sec`, `cognitive_bias_score`) plus `system_usability_scale`, applies Holm-Bonferroni correction across the 9 primary tests, and builds the documentation reconciliation table.
* **`generate_synthetic_benchmark_dataset(output_path, seed=42, allow_overwrite=False)`**:
  * Explicit opt-in generator. If someone tries to pass `phishing_simulation_benchmark.csv` without `allow_overwrite=True`, `assert_not_protected()` blocks it with a `PermissionError`.

---

## 5. `telemetry/generate_paper_figures.py` — Non-Destructive 300 DPI Figure Generator

### What Was Broken Originally
Originally, running `python telemetry/generate_paper_figures.py` immediately overwrote `telemetry/phishing_simulation_benchmark.csv` and plotted hardcoded numbers in Figure 2A without computing any statistical tests or confidence intervals from the CSV. Additionally, the 3 figure files on GitHub were 131-byte blank placeholders.

### How It Works Now
* **`generate_all_figures(csv_path, figures_dir)`**:
  1. Records the `SHA-256` hash of `phishing_simulation_benchmark.csv` before running.
  2. Calls `run_full_retention_analysis(csv_path)` to load, validate, and compute exact statistics from the dataset.
  3. Calls `render_figure1_architecture()`, `render_figure2_telemetry_and_retention()`, and `render_figure3_comparative_performance()` to save 300 DPI PNGs into `docs/figures/`.
  4. Verifies that the `SHA-256` hash of `phishing_simulation_benchmark.csv` after running is identical to before.

---

## 6. Questionnaire Calculators (`sus_calculator.py`, `nasa_tlx_calculator.py`, `kennedy_ssq_calculator.py`)

### A. `telemetry/sus_calculator.py`
* **`calculate_sus_score(responses)`**:
  * Validates that `responses` has exactly 10 numeric ratings between `1` and `5`.
  * Odd items (`index 0, 2, 4, 6, 8`): `score - 1`.
  * Even items (`index 1, 3, 5, 7, 9`): `5 - score`.
  * Multiplies the sum by `2.5` to produce a score in `[0.0, 100.0]`.
* **`grade_sus(score)`**:
  * Maps `>= 80.3` to Grade A, `>= 74.0` to Grade B, `>= 68.0` to Grade C, `>= 51.0` to Grade D, and `< 51.0` to Grade F.
* **`load_sus_from_csv(csv_file)`**:
  * Reads either 10 raw Likert columns (`q1`..`q10`) or pre-aggregated `sus_score` / `system_usability_scale` columns, computing overall and per-group statistics.

### B. `telemetry/nasa_tlx_calculator.py`
* **`calculate_tlx(subscales)`**:
  * Validates that all 6 subscales (`mental`, `physical`, `temporal`, `performance`, `effort`, `frustration`) are present and within `[0.0, 100.0]`, then returns their unweighted average (Raw TLX).
* **`load_tlx_from_csv(csv_file)`**:
  * Reads either the 6 subscales or `raw_tlx_score` from `sample_experiment_data.csv` (and raises a clear error if given a CSV without TLX data).

### C. `telemetry/kennedy_ssq_calculator.py`
* **Bugs Fixed:**
  1. Corrected `WEIGHTS["Oculomotor"]` from `3.74` to **`7.58`** (Kennedy et al., 1993, Table 4).
  2. Added the missing 16th symptom `fullness_of_head` to `ALL_16_SYMPTOMS`.
  3. Fixed `DISORIENTATION_SYMPTOMS` to the exact 7 items defined by Kennedy et al. (1993): `difficulty_focusing`, `nausea`, `fullness_of_head`, `blurred_vision`, `dizziness_eyes_open`, `dizziness_eyes_closed`, and `vertigo`.
* **`calculate_ssq(symptoms)`**:
  * Validates all 16 symptoms in `[0, 3]`, sums the 3 subscale raw scores, multiplies by `9.54` (Nausea), `7.58` (Oculomotor), `13.92` (Disorientation), and multiplies `(raw_N + raw_O + raw_D)` by `3.74` for `Total_Severity`.

---

## 7. Unity C# Eye-Gaze Telemetry (`Assets/Scripts/EyeGazeAttentionTracker.cs`) & Python Twin (`telemetry/gaze_metrics.py`)

### Key Methods Added / Completed in `EyeGazeAttentionTracker.cs`
1. **`FlushActiveFixation()` & `CommitPendingFixationIfEligible()`**:
   * **Why this was needed:** In the original code, `UpdateAOIDwellTimes()` only added `currentAOIDwellTimer` to `fixationDatabase` when `detectedAOI != activeAOI` (when the user looked away). If a participant stared at the hyperlink for 2 seconds and clicked it while still looking at it, those 2 seconds were never saved! Calling `FlushActiveFixation()` at the end of a trial commits any ongoing fixation that lasted at least `0.15 s` (`150 ms`).
2. **`CalculateThreatInspectionRatio(...)`**:
   * Computes TIR using all 5 email AOIs in the denominator:
     ```csharp
     float totalEmailDwell =
         senderDomainDwellSec +
         securityBadgeDwellSec +
         subjectLineDwellSec +
         bodyTextDwellSec +
         hyperlinkDwellSec;
     if (totalEmailDwell <= 0f || float.IsInfinity(totalEmailDwell))
         return -1f; // Undefined ratio when total email dwell is zero
     ```
   * Supports all three formula variants via `ThreatInspectionFormula`:
     * `SenderDomainAndHyperlink` (default primary)
     * `AllSecurityCuesIncludePadlock`
     * `SenderDomainOnly`
3. **`ExportTelemetryCsvRow(participantId, scenarioId, dayInterval)`**:
   * Preserves the original 3-parameter method signature while flushing the active fixation, using `CultureInfo.InvariantCulture` (so decimal points are always `.` and never `,` on European locales), and writing `""` instead of `0.000` if TIR is undefined (`-1f`).
4. **`ExportDetailedTelemetryCsvRow(...)` & `TryAppendDetailedTelemetryToFile(...)`**:
   * Writes the full 22-column Schema `v2.0` CSV file (all 5 AOI dwell times, all 5 fixation counts, valid/invalid gaze seconds, and all 3 TIR variants) consumed directly by `telemetry/gaze_metrics.py`.

---

## 8. `telemetry/telecom_latency_debias_eval.py` — Technoeconomic Evaluation

### Purpose
Models the operational impact of VR vs. 2D phishing training across a 2,500-employee enterprise exposed to 12 phishing campaigns per year, strictly using dimensionless ratios, labor hours, and payback months (zero currency symbols).

### Key Methods
* **`compute_telecom_and_operational_parity()`**:
  * Computes `kappa_ratio` ($\kappa = 0.24$), `annual_compromise_clicks_avoided` (`4680.0`), `annual_escalated_breaches_prevented` (`56.2`), `annual_security_remediation_hours_reclaimed` (`5686.2`), and `payback_period_months` (`15.79`).
* **`TelecomLatencyDebiasEval.from_benchmark_csv(csv_path)`**:
  * Feeds the actual Day 14 mean click rates from `phishing_simulation_benchmark.csv` (`26.86%` Web vs. `6.86%` VR) into the model, yielding `6000.0` compromise clicks avoided, `72.0` escalated breaches prevented, and `7290.0` remediation hours reclaimed.
* **`TelecomLatencyDebiasEval.run_sensitivity_analysis()`**:
  * Evaluates a 12-scenario sensitivity grid across workforce sizes (`500`, `1000`, `2500`, `5000`) and breach escalation probabilities (`0.6%`, `1.2%`, `2.0%`).
