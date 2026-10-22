# 04 - Run and Troubleshoot Guide (Windows & PowerShell)

This guide gives you exact, copy-pasteable commands for Windows PowerShell, shows what output to expect, and explains how to fix common issues.

---

## 1. Environment Verified on This Machine

When we audited your PC (`D:\I080\Project`), we confirmed:
* **Operating System:** Windows (PowerShell)
* **Python Version:** `Python 3.10.11` (in system `PATH`)
* **Installed Python Packages:** `numpy 2.2.6`, `matplotlib 3.10.8` (Note: `scipy` is **not** required because `telemetry/stats_utils.py` implements all statistical functions in pure Python).
* **C# Compiler (Roslyn `csc.exe`):** `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe`
* **Unity Editor Installed:** `Unity 6000.3.3f1` (`C:\Program Files\Unity\Hub\Editor\6000.3.3f1`)
* **Git Executable Installed:** `C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe`

---

## 2. Step-by-Step Commands to Run Your Project

Open **PowerShell** and navigate to your project directory:
```powershell
Set-Location "D:\I080\Project"
```

### Command 1: Run the Complete Test Suite (`test_evaluation_tools.py`)
```powershell
python telemetry/test_evaluation_tools.py
```
**Expected Output:**
* Runs all 3 psychometric calculators (SUS, NASA-TLX, Kennedy SSQ) and prints `[OK]`.
* Runs all **14 automated unit tests** (`Ran 14 tests in ...s — OK`).
* Ends with:
  `[SUCCESS] ALL PSYCHOMETRIC, RETENTION, GAZE & STATISTICAL ENGINES VERIFIED SUCCESSFULLY!`

### Command 2: Run the Full Statistical & 14-Day Retention Report (`human_factors_retention_analysis.py`)
```powershell
python telemetry/human_factors_retention_analysis.py
```
**Optional — Save Machine-Readable JSON Summary:**
```powershell
python telemetry/human_factors_retention_analysis.py --json-out telemetry/retention_analysis_summary.json
```
**Expected Output:**
* Verifies `phishing_simulation_benchmark.csv` (`SHA-256: 67d07bc1c9073b1faccf0d4e30569a390ea1f5dc425b72c9993302bf06ef1925`, `50/50` valid rows).
* Prints descriptive statistics (`Mean +/- SD`, `95% CI`), within-group paired $t$-tests (`Day 0 vs Day 14`), between-group Welch $t$-tests (`VR vs 2D Web`), Difference-in-Differences, Mann-Whitney $U$ p-values, Holm-Bonferroni adjusted p-values, and the documentation reconciliation table.

### Command 3: Render the 300 DPI Publication Figures (`generate_paper_figures.py`)
```powershell
python telemetry/generate_paper_figures.py
```
**Expected Output:**
```text
[OK] Verified read-only input dataset (50 rows): D:\I080\Project\telemetry\phishing_simulation_benchmark.csv
[OK] Input SHA-256 preserved: 67d07bc1c9073b1faccf0d4e30569a390ea1f5dc425b72c9993302bf06ef1925
[OK] Rendered 300 DPI figure (... bytes): D:\I080\Project\docs\figures\figure1_system_architecture.png
[OK] Rendered 300 DPI figure (... bytes): D:\I080\Project\docs\figures\figure2_kinematic_telemetry.png
[OK] Rendered 300 DPI figure (... bytes): D:\I080\Project\docs\figures\figure3_comparative_performance.png
```

### Command 4: Run the Technoeconomic Operational Parity Model (`telecom_latency_debias_eval.py`)
```powershell
python telemetry/telecom_latency_debias_eval.py
```
**Expected Output:**
* Prints **Scenario A** (Prescribed Baseline Parameters: `Web=22.4%, VR=6.8%` $\rightarrow$ `kappa_ratio: 0.24`, `annual_compromise_clicks_avoided: 4680.0`, `annual_escalated_breaches_prevented: 56.2`, `annual_security_remediation_hours_reclaimed: 5686.2`, `payback_period_months: 15.79`).
* Prints **Scenario B** (Derived directly from `phishing_simulation_benchmark.csv`: `Web=26.86%, VR=6.86%` $\rightarrow$ `annual_compromise_clicks_avoided: 6000.0`, `annual_escalated_breaches_prevented: 72.0`, `annual_security_remediation_hours_reclaimed: 7290.0`).

### Command 5: Run Individual Questionnaire Calculators on CSV Files
```powershell
python -c "import sys; sys.path.insert(0, 'telemetry'); import sus_calculator, nasa_tlx_calculator, kennedy_ssq_calculator; sus_calculator.evaluate_cohort('telemetry/phishing_simulation_benchmark.csv'); nasa_tlx_calculator.evaluate_tlx_comparison('telemetry/sample_experiment_data.csv'); kennedy_ssq_calculator.evaluate_ssq_safety('telemetry/sample_experiment_data.csv')"
```

---

## 3. Troubleshooting Common Errors

### Issue 1: `git : The term 'git' is not recognized...` in PowerShell
* **Cause:** Visual Studio installed `git.exe` on your machine, but its folder is not in your Windows `PATH` environment variable by default.
* **Fix:** Add Visual Studio's Git folder to your current PowerShell session with this one-line command:
  ```powershell
  $env:Path += ";C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
  git --version
  ```

### Issue 2: `ModuleNotFoundError: No module named 'csv_utils'`
* **Cause:** Running a script from outside `D:\I080\Project` or importing a module without `telemetry` in `sys.path`.
* **Fix:** Always run `python telemetry/<script_name>.py` from `D:\I080\Project` (Python automatically adds the script's own folder `telemetry/` to `sys.path`).

### Issue 3: `PermissionError: Refusing to write to protected source file: ...phishing_simulation_benchmark.csv`
* **Cause:** You (or a script) tried to pass `telemetry/phishing_simulation_benchmark.csv` as an output path.
* **Fix:** This is our safety guard working as designed! Always write newly generated synthetic data or JSON summaries to a new filename (for example, `--regenerate-synthetic-to telemetry/synthetic_test_copy.csv`).

### Issue 4: Testing Unity C# Scripts Without Opening the Full Unity Editor
* Because `Assets/Scripts/EyeGazeAttentionTracker.cs` uses the `UnityEngine` namespace, standard C# compilers need either Unity or lightweight `UnityEngine` stub classes to compile it outside Unity.
* Full runtime testing of 3D physics raycasting (`Physics.Raycast` against the 3D curved monitor colliders `AOI_SenderDomain`, `AOI_SecurityBadge`, `AOI_SubjectLine`, `AOI_BodyText`, `AOI_Hyperlink`) requires opening the scene in Unity (`2022.3 LTS` or `6000.3.3f1`) with the XR Origin camera rigged by Arnav (`D030`) and Diya (`D065`).
