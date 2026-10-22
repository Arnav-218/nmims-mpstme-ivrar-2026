# 05 - Changes, Scientific Decisions, and Remaining Team Questions

This document records every file created or modified for your role (`I080 - Anuvrat Tripathi`), the exact bugs fixed, the scientific and statistical decisions made, the verification tests executed, and the remaining questions for your teammates (`D021 Madhav`, `D030 Arnav`, `D065 Diya`).

---

## 1. Summary of Files Created and Modified

### Newly Created Files (Within `I080` Scope)
1. **`telemetry/csv_utils.py`** — Shared, dependency-free CSV reader/writer with BOM handling (`utf-8-sig`), safe float parsing (`None` for blank cells instead of `0.0`), `SHA-256` file fingerprinting, and write-protection (`assert_not_protected`) so source datasets can never be overwritten.
2. **`telemetry/stats_utils.py`** — Pure-Python statistical engine implementing descriptive statistics, Student-$t$ CDF/PPF via continued-fraction incomplete beta, Welch's two-sample $t$-test, paired $t$-test, Cohen's $d$, Hedges' $g$, paired Cohen's $d_z$, Mann-Whitney $U$, Wilcoxon signed-rank, and Holm-Bonferroni step-down adjustment (verified against `scipy.stats` to within $10^{-11}$ relative error).
3. **`telemetry/performance_metrics.py`** — Trial-level scoring engine for future Unity experiment logs (`accuracy_pct`, `malicious_click_rate_pct`, `phishing_report_rate_pct`, `false_positive_rate_pct`, `phishing_ignore_rate_pct`) with explicit zero-denominator protection.
4. **`telemetry/gaze_metrics.py`** — Python twin of the C# Threat Inspection Ratio (`TIR`) and Schema `v2.0` gaze CSV validator.
5. **`telemetry/human_factors_retention_analysis.py`** — Complete Day 0 vs. Day 14 longitudinal retention and between-group statistical analysis pipeline with provenance tagging and documentation reconciliation.
6. **`docs/anuvrat/00_START_HERE.md` through `06_VIVA_PREPARATION.md`** — 7-part study, code, statistics, troubleshooting, and viva defense guide.

### Modified Files (Within `I080` Scope)
1. **`telemetry/generate_paper_figures.py`** — Removed automatic overwriting of `phishing_simulation_benchmark.csv`; connected figure rendering directly to `human_factors_retention_analysis.py`; replaced the 131-byte placeholder PNGs (`figure1_system_architecture.png`, `figure2_kinematic_telemetry.png`, `figure3_comparative_performance.png`) with genuine 300 DPI charts annotated with calculated sample statistics and synthetic data provenance.
2. **`telemetry/kennedy_ssq_calculator.py`** — Fixed two scoring bugs in the starter code:
   * Changed `Oculomotor` weight from `3.74` to **`7.58`** per Kennedy et al. (1993, Table 4).
   * Added the missing 16th symptom (`fullness_of_head`) and corrected the 7-item `Disorientation` subscale mapping (`difficulty_focusing`, `nausea`, `fullness_of_head`, `blurred_vision`, `dizziness_eyes_open`, `dizziness_eyes_closed`, `vertigo`).
   * Added range validation (`[0, 3]`) and CSV loading support.
3. **`telemetry/sus_calculator.py`** — Added strict 10-item Likert validation (`[1, 5]`), added `Grade B (74.0 - 80.2)` to close the grading gap between `68.0` and `80.3`, and implemented CSV loading for both raw 10-item CSVs and pre-aggregated score columns (`system_usability_scale`, `sus_score`).
4. **`telemetry/nasa_tlx_calculator.py`** — Added strict 6-subscale validation (`[0, 100]`) and CSV loading support (`raw_tlx_score` or 6 raw subscales).
5. **`telemetry/telecom_latency_debias_eval.py`** — Preserved the exact default class interface and prescribed baseline outputs (`4680.0` clicks avoided, `56.2` breaches prevented, `5686.2` hours reclaimed, $\kappa = 0.24$, `15.79` months payback) while adding `from_benchmark_csv()` and `run_sensitivity_analysis()`.
6. **`telemetry/test_evaluation_tools.py`** — Expanded the Sprint 0 smoke test with 14 `unittest` test cases covering every requirement and edge case.
7. **`Assets/Scripts/EyeGazeAttentionTracker.cs`** — Completed your `TODO [I080]` in `ExportTelemetryCsvRow()`, added `FlushActiveFixation()`, included all 5 email AOIs in the total email dwell denominator, added `CalculateThreatInspectionRatio()` supporting all 3 documented TIR definitions, and added Schema `v2.0` CSV export (`ExportDetailedTelemetryCsvRow()`, `TryAppendDetailedTelemetryToFile()`).

### Teammates' & Source Files Preserved Untouched
* `telemetry/phishing_simulation_benchmark.csv` (`SHA-256: 67d07bc1c9073b1faccf0d4e30569a390ea1f5dc425b72c9993302bf06ef1925`) — **Unmodified.**
* `telemetry/sample_experiment_data.csv` (`SHA-256: 4f4a07f1ce99fcb2d1da4b41c8420d3263e4228d58d185eab9616a3100c17eee`) — **Unmodified.**
* `Assets/Scripts/PhishingThreatSimulationManager.cs` & `madhav/PhishingThreatSimulationManager.cs` — **Unmodified.**
* `Assets/index.html` — **Unmodified.**
* `README.md`, `RESEARCH_AND_IMPLEMENTATION_GUIDE.md`, `docs/RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md`, `docs/LITERATURE_REVIEW_AND_FOUNDATIONAL_PAPERS.md`, `docs/TEAM_ROSTER.json`, `docs/toolchain_spec.md` — **Unmodified.**

---

## 2. Key Bugs Fixed and Why They Mattered

| # | Bug / Issue Found | File(s) Affected | How We Fixed It & Why |
| :--- | :--- | :--- | :--- |
| 1 | `generate_paper_figures.py` overwrote `phishing_simulation_benchmark.csv` every time it ran. | `telemetry/generate_paper_figures.py`, `telemetry/human_factors_retention_analysis.py` | Separated synthetic data generation into an opt-in function (`generate_synthetic_benchmark_dataset`) that refuses to overwrite `phishing_simulation_benchmark.csv`. Figure generation now verifies the input `SHA-256` before and after running. |
| 2 | `kennedy_ssq_calculator.py` used `3.74` instead of `7.58` for the `Oculomotor` multiplier and had the wrong items in `Disorientation` (missing `fullness_of_head`, `nausea`, `blurred_vision`). | `telemetry/kennedy_ssq_calculator.py` | Corrected `WEIGHTS['Oculomotor'] = 7.58` and updated the 3 subscales to match Kennedy et al. (1993, Table 4) exactly. For `typical_profile` (`general_discomfort=1, eyestrain=1`), Oculomotor is now `2 * 7.58 = 15.16` instead of `7.48`. |
| 3 | `EyeGazeAttentionTracker.cs` lost the final ongoing fixation when a trial ended, omitted `SecurityPadlockBadge` and `SubjectLineUrgency` from total email dwell, and returned `0.000` when total dwell was zero. | `Assets/Scripts/EyeGazeAttentionTracker.cs` | Added `FlushActiveFixation()` before computing TIR or exporting CSV; included all 5 email AOIs in `totalEmailDwell`; returned `-1f` (formatted as blank `""` in CSV) when `totalEmailDwell <= 0f`. |
| 4 | Figures in `docs/figures/` on GitHub were 131-byte blank placeholders, and the old Figure 2A plotted hardcoded AOI bars not backed by the CSV. | `telemetry/generate_paper_figures.py`, `docs/figures/*.png` | Generated genuine 300 DPI PNGs (`329 KB`–`383 KB`) plotting the actual validated Day 0 and Day 14 means, 95% CIs, boxplots, Welch $p$-values, and Cohen's $d$ from `phishing_simulation_benchmark.csv`. |
| 5 | Slight numerical differences between the generator parameters cited in `README.md` (`92.8%`, `88.6%`, `d = 2.84`) and the 50-row sample realization in `phishing_simulation_benchmark.csv` (`92.21%`, `88.14%`, `d = 5.42`). | `telemetry/human_factors_retention_analysis.py` | Built `build_documentation_reconciliation()` so both the documented theoretical parameters and the exact CSV sample statistics are reported side by side with a clear mathematical explanation. |

---

## 3. Decisions and Questions to Confirm with Your Teammates

Before final integration or live participant testing, confirm these 4 items with your teammates:

1. **With Diya (`D065` — Eye-Gaze Specialist): Primary TIR Numerator Choice**
   * Both C# (`EyeGazeAttentionTracker.cs`) and Python (`gaze_metrics.py`) support all 3 documented TIR definitions and export all 3 columns (`tir_sender_domain_hyperlink`, `tir_all_security_cues`, `tir_sender_domain_only`).
   * We set `SenderDomainAndHyperlink` as the default primary formula because it matches `RESEARCH_AND_IMPLEMENTATION_GUIDE.md` Eq. 1.1 and the original `I080` code comment. Confirm with Diya whether `AOI_SecurityBadge` (`SecurityPadlockBadge`) is actively tagged on the Unity monitor prefab so `tir_all_security_cues` can also be populated in live trials.
2. **With Madhav (`D021` — Threat Modeling Lead): Merging `madhav/PhishingThreatSimulationManager.cs` and Scoring "Ignore" on Phishing**
   * Madhav's expanded 12-scenario script currently lives in `madhav/PhishingThreatSimulationManager.cs` while `Assets/Scripts/PhishingThreatSimulationManager.cs` still has the 3-scenario starter deck. Ask Madhav and Arnav when `madhav/PhishingThreatSimulationManager.cs` should replace `Assets/Scripts/PhishingThreatSimulationManager.cs`.
   * Also note: in `SubmitUserAction()`, if an email is phishing and the user chooses `didIgnore` (neither clicks nor reports), it is currently scored as `isCorrect = false` (not counted as a malicious click, but counted as an incorrect classification because they failed to report the threat). Confirm with Madhav that this is intentional.
3. **With Arnav (`D030` — XR Systems Architect): Wiring `OnDecisionRecorded` to `EyeGazeAttentionTracker.TryAppendDetailedTelemetryToFile()`**
   * When a user clicks or pulls the report lever in Unity, `PhishingThreatSimulationManager` fires `OnDecisionRecorded`. Arnav can call `eyeGazeTracker.TryAppendDetailedTelemetryToFile(...)` and then `eyeGazeTracker.ResetGazeTracker()` on each scenario transition so every trial is automatically logged to CSV.
4. **With All Teammates: GitHub Username for `docs/TEAM_ROSTER.json`**
   * In `docs/TEAM_ROSTER.json`, `"github_username"` is currently `""` for all 4 group members (`D021`, `D030`, `D065`, `I080`). Before pushing your final branch, fill in your actual GitHub username on line 37 of `docs/TEAM_ROSTER.json`.
