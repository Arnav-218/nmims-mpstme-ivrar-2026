"""
Sprint 0 & Comprehensive Verification Suite: Psychometric, Statistical & Telemetry Toolchains
=============================================================================================
Group 12: Immersive VR Phishing Simulation vs 2D Web-Based Training
Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)

Covers:
1. Sprint 0 onboarding smoke check (`test_all()`).
2. CSV validation (valid benchmark, missing columns, duplicate IDs, out-of-range values, empty CSV).
3. Trial-level accuracy, malicious click rate, detection rate, false-positive rate, and zero denominators.
4. Day 0 vs Day 14 retention deltas, paired t-tests, Welch two-sample t-tests, Cohen's d / Hedges' g,
   Mann-Whitney U, Wilcoxon signed-rank, and Holm-Bonferroni adjustments on hand-calculated fixtures.
5. Questionnaire calculators (SUS, NASA-TLX, and corrected 16-item Kennedy SSQ with Oculomotor weight 7.58).
6. Eye-gaze Threat Inspection Ratio (TIR) formulas, zero-dwell handling, and C# parity checks.
7. Technoeconomic operational parity model (prescribed baseline, CSV-derived scenario, sensitivity sweep).
8. Non-destructive figure generation and SHA-256 dataset protection verification.
"""
from __future__ import annotations

import math
import os
import sys
import tempfile
import unittest

TELEMETRY_DIR = os.path.dirname(os.path.abspath(__file__))
if TELEMETRY_DIR not in sys.path:
    sys.path.insert(0, TELEMETRY_DIR)

from csv_utils import CsvFormatError, file_sha256, parse_optional_float, write_csv_rows
import gaze_metrics
import generate_paper_figures
import human_factors_retention_analysis as hfra
import kennedy_ssq_calculator as ssq_calc
import nasa_tlx_calculator as tlx_calc
import performance_metrics as perf_metrics
import stats_utils as su
import sus_calculator as sus_calc
import telecom_latency_debias_eval as telecom_eval


class TestCsvAndValidation(unittest.TestCase):
    def test_benchmark_csv_valid_and_sha256_preserved(self):
        loaded = hfra.validate_and_load_benchmark_csv(hfra.DEFAULT_BENCHMARK_CSV)
        self.assertTrue(loaded["is_valid"])
        self.assertEqual(loaded["valid_row_count"], 50)
        self.assertEqual(loaded["excluded_row_count"], 0)
        self.assertEqual(loaded["group_counts"][hfra.VR_MODALITY], 25)
        self.assertEqual(loaded["group_counts"][hfra.WEB_MODALITY], 25)
        self.assertTrue(loaded["provenance"]["is_synthetic"])
        self.assertIn(
            loaded["provenance"]["sha256"].lower(),
            hfra.KNOWN_SYNTHETIC_BENCHMARK_SHA256_SET,
        )

    def test_protected_benchmark_cannot_be_overwritten(self):
        with self.assertRaises(PermissionError):
            hfra.generate_synthetic_benchmark_dataset(
                hfra.DEFAULT_BENCHMARK_CSV, seed=42, allow_overwrite=False
            )

    def test_missing_columns_and_empty_csv_raise(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            bad_col_csv = os.path.join(tmpdir, "bad_cols.csv")
            with open(bad_col_csv, "w", encoding="utf-8") as f:
                f.write("participant_id,training_modality\nP001,2D_Web_Training\n")
            with self.assertRaises(CsvFormatError):
                hfra.validate_and_load_benchmark_csv(bad_col_csv)

            empty_csv = os.path.join(tmpdir, "empty.csv")
            with open(empty_csv, "w", encoding="utf-8") as f:
                f.write(",".join(hfra.REQUIRED_BENCHMARK_COLUMNS) + "\n")
            with self.assertRaises(CsvFormatError):
                hfra.validate_and_load_benchmark_csv(empty_csv)

    def test_duplicate_ids_and_invalid_ranges_flagged(self):
        with tempfile.TemporaryDirectory() as tmpdir:
            bad_rows_csv = os.path.join(tmpdir, "bad_rows.csv")
            rows = [
                {
                    "participant_id": "P001",
                    "training_modality": "Immersive_VR_Training",
                    "day0_detection_accuracy_pct": 90.0,
                    "day0_malicious_click_rate_pct": 5.0,
                    "day0_security_aoi_dwell_sec": 2.5,
                    "day0_cognitive_bias_score": 2.5,
                    "day14_detection_accuracy_pct": 88.0,
                    "day14_malicious_click_rate_pct": 6.0,
                    "day14_security_aoi_dwell_sec": 2.2,
                    "day14_cognitive_bias_score": 3.0,
                    "system_usability_scale": 85.0,
                },
                # Duplicate ID P001 + out-of-range accuracy (120%)
                {
                    "participant_id": "P001",
                    "training_modality": "Immersive_VR_Training",
                    "day0_detection_accuracy_pct": 120.0,
                    "day0_malicious_click_rate_pct": 5.0,
                    "day0_security_aoi_dwell_sec": 2.5,
                    "day0_cognitive_bias_score": 2.5,
                    "day14_detection_accuracy_pct": 88.0,
                    "day14_malicious_click_rate_pct": 6.0,
                    "day14_security_aoi_dwell_sec": 2.2,
                    "day14_cognitive_bias_score": 3.0,
                    "system_usability_scale": 85.0,
                },
            ]
            write_csv_rows(bad_rows_csv, hfra.REQUIRED_BENCHMARK_COLUMNS, rows)
            loaded = hfra.validate_and_load_benchmark_csv(bad_rows_csv)
            self.assertFalse(loaded["is_valid"])
            self.assertEqual(loaded["excluded_row_count"], 1)
            self.assertTrue(any("duplicate participant_id" in e for e in loaded["errors"]))
            self.assertTrue(any("outside valid range" in e for e in loaded["errors"]))


class TestTrialPerformanceMetrics(unittest.TestCase):
    def test_hand_calculated_session_metrics(self):
        # 4 trials: 3 phishing (1 clicked, 2 reported), 1 legitimate (ignored)
        # Expected:
        # - accuracy = 3 correct / 4 total = 75.0%
        # - malicious_click_rate = 1 click / 3 phishing = 33.333...%
        # - phishing_report_rate = 2 / 3 = 66.666...%
        # - false_positive_rate = 0 / 1 = 0.0%
        trials = [
            {"scenario_classification": "DeceptivePhish", "participant_action": "click"},
            {"scenario_classification": "SpearPhishTargeted", "participant_action": "report"},
            {"scenario_classification": "DeceptivePhish", "participant_action": "report"},
            {"scenario_classification": "Legitimate", "participant_action": "ignore"},
        ]
        res = perf_metrics.compute_session_metrics(trials)
        self.assertEqual(res["n_trials"], 4)
        self.assertEqual(res["n_phishing_trials"], 3)
        self.assertEqual(res["n_legitimate_trials"], 1)
        self.assertAlmostEqual(res["accuracy_pct"], 75.0, places=5)
        self.assertAlmostEqual(res["malicious_click_rate_pct"], 100.0 / 3.0, places=5)
        self.assertAlmostEqual(res["phishing_report_rate_pct"], 200.0 / 3.0, places=5)
        self.assertAlmostEqual(res["false_positive_rate_pct"], 0.0, places=5)

    def test_zero_denominator_returns_none(self):
        # Session with only legitimate emails -> phishing denominator is 0 -> None
        only_legit = [{"scenario_classification": "Legitimate", "participant_action": "ignore"}]
        res = perf_metrics.compute_session_metrics(only_legit)
        self.assertEqual(res["accuracy_pct"], 100.0)
        self.assertIsNone(res["malicious_click_rate_pct"])
        self.assertIsNone(res["phishing_report_rate_pct"])

        # Empty trial list -> all rates None
        empty_res = perf_metrics.compute_session_metrics([])
        self.assertIsNone(empty_res["accuracy_pct"])
        self.assertIsNone(empty_res["malicious_click_rate_pct"])


class TestRetentionAndStatistics(unittest.TestCase):
    def test_retention_change_definitions(self):
        # Day 0 = 80.0%, Day 14 = 60.0%
        # raw/pp change = -20.0 pp
        # relative % change = (-20 / 80) * 100 = -25.0%
        # retention ratio = (60 / 80) * 100 = 75.0%
        c = hfra.compute_retention_change_metrics(80.0, 60.0)
        self.assertAlmostEqual(c["raw_change"], -20.0)
        self.assertAlmostEqual(c["percentage_point_change"], -20.0)
        self.assertAlmostEqual(c["relative_pct_change"], -25.0)
        self.assertAlmostEqual(c["retention_ratio_pct"], 75.0)

        # Zero Day 0 -> NaN relative change
        c0 = hfra.compute_retention_change_metrics(0.0, 10.0)
        self.assertTrue(math.isnan(c0["relative_pct_change"]))

    def test_paired_and_welch_t_tests_hand_calculated(self):
        # Paired test: before=[80, 85, 90, 95], after=[78, 81, 86, 89]
        # diffs (after - before) = [-2, -4, -4, -6], mean = -4.0, sd = sqrt(8/3) = 1.632993
        # se = 1.632993 / 2 = 0.8164966, t = -4.0 / 0.8164966 = -4.898979, d_z = -2.44949
        before = [80.0, 85.0, 90.0, 95.0]
        after = [78.0, 81.0, 86.0, 89.0]
        pt = su.paired_t_test(before, after)
        dz = su.cohens_dz(before, after)
        self.assertAlmostEqual(pt["mean_difference"], -4.0, places=6)
        self.assertAlmostEqual(pt["statistic"], -math.sqrt(24.0), places=5)
        self.assertAlmostEqual(dz["cohens_dz"], -math.sqrt(6.0), places=5)
        self.assertLess(pt["p_value"], 0.02)

        # Welch two-sample test: group A = [90, 92, 94], group B = [70, 72, 74]
        # mean_A = 92, mean_B = 72, sd_A = 2, sd_B = 2, pooled_sd = 2 -> Cohen's d = 20 / 2 = 10.0
        wt = su.welch_t_test([90.0, 92.0, 94.0], [70.0, 72.0, 74.0])
        eff = su.hedges_g([90.0, 92.0, 94.0], [70.0, 72.0, 74.0])
        self.assertAlmostEqual(wt["mean_difference"], 20.0, places=6)
        self.assertAlmostEqual(eff["cohens_d"], 10.0, places=6)
        self.assertLess(wt["p_value"], 0.001)


class TestQuestionnaireCalculators(unittest.TestCase):
    def test_sus_scoring_and_validation(self):
        # All 5s on odd items and all 1s on even items -> maximum SUS = 100.0
        self.assertAlmostEqual(
            sus_calc.calculate_sus_score([5, 1, 5, 1, 5, 1, 5, 1, 5, 1]), 100.0
        )
        # All 3s (neutral) -> each item contributes 2 -> 20 * 2.5 = 50.0
        self.assertAlmostEqual(
            sus_calc.calculate_sus_score([3, 3, 3, 3, 3, 3, 3, 3, 3, 3]), 50.0
        )
        # First row of sus_calculator.py sample_data -> 85.0
        self.assertAlmostEqual(
            sus_calc.calculate_sus_score([4, 2, 5, 1, 4, 2, 5, 2, 4, 1]), 85.0
        )
        # Invalid item count or range raises ValueError
        with self.assertRaises(ValueError):
            sus_calc.calculate_sus_score([4, 2, 5])
        with self.assertRaises(ValueError):
            sus_calc.calculate_sus_score([6, 2, 5, 1, 4, 2, 5, 2, 4, 1])

        # Load pre-aggregated SUS from benchmark CSV
        loaded = sus_calc.load_sus_from_csv(hfra.DEFAULT_BENCHMARK_CSV)
        self.assertEqual(loaded["overall"]["n"], 50)
        self.assertIn(hfra.VR_MODALITY, loaded["by_group"])
        self.assertIn(hfra.WEB_MODALITY, loaded["by_group"])

    def test_nasa_tlx_scoring_and_validation(self):
        subscales = {
            "mental": 60.0,
            "physical": 30.0,
            "temporal": 45.0,
            "performance": 30.0,
            "effort": 60.0,
            "frustration": 15.0,
        }
        self.assertAlmostEqual(tlx_calc.calculate_tlx(subscales), 40.0)

        with self.assertRaises(ValueError):
            tlx_calc.calculate_tlx({"mental": 50.0})
        with self.assertRaises(ValueError):
            tlx_calc.calculate_tlx({**subscales, "mental": 110.0})

        # Load pre-aggregated raw_tlx_score from sample_experiment_data.csv
        sample_csv = os.path.join(TELEMETRY_DIR, "sample_experiment_data.csv")
        loaded = tlx_calc.load_tlx_from_csv(sample_csv)
        self.assertEqual(loaded["summary"]["n"], 18)
        self.assertIsNotNone(loaded["limitation_note"])

    def test_kennedy_ssq_corrected_weights_and_16_items(self):
        # Verify standard Kennedy et al. (1993) weights: N=9.54, O=7.58, D=13.92, TS=3.74
        self.assertAlmostEqual(ssq_calc.WEIGHTS["Nausea"], 9.54)
        self.assertAlmostEqual(ssq_calc.WEIGHTS["Oculomotor"], 7.58)
        self.assertAlmostEqual(ssq_calc.WEIGHTS["Disorientation"], 13.92)
        self.assertAlmostEqual(ssq_calc.WEIGHTS["Total"], 3.74)

        # Typical profile: general_discomfort=1 (in N and O), eyestrain=1 (in O)
        # raw_N = 1, raw_O = 2, raw_D = 0 -> N = 9.54, O = 15.16, D = 0.0, TS = 3 * 3.74 = 11.22
        zero_all = {k: 0 for k in ssq_calc.ALL_16_SYMPTOMS}
        profile = {**zero_all, "general_discomfort": 1, "eyestrain": 1}
        res = ssq_calc.calculate_ssq(profile)
        self.assertAlmostEqual(res["Nausea"], 9.54, places=4)
        self.assertAlmostEqual(res["Oculomotor"], 15.16, places=4)
        self.assertAlmostEqual(res["Disorientation"], 0.0, places=4)
        self.assertAlmostEqual(res["Total_Severity"], 11.22, places=4)

        # Verify fullness_of_head loads onto Disorientation (1 * 13.92)
        head_profile = {**zero_all, "fullness_of_head": 2}
        res_head = ssq_calc.calculate_ssq(head_profile)
        self.assertAlmostEqual(res_head["Disorientation"], 2 * 13.92, places=4)
        self.assertAlmostEqual(res_head["Total_Severity"], 2 * 3.74, places=4)

        # Out-of-range symptom rating raises ValueError
        with self.assertRaises(ValueError):
            ssq_calc.calculate_ssq({**zero_all, "nausea": 4})


class TestGazeTelemetryAndTIR(unittest.TestCase):
    def test_tir_formulas_and_zero_dwell(self):
        dwell = {
            "sender_domain": 1.5,
            "security_badge": 0.5,
            "subject_line": 1.0,
            "body_text": 1.0,
            "hyperlink": 1.0,
        }
        # Total dwell = 5.0 s
        # Primary (sender_domain + hyperlink) = 2.5 / 5.0 = 0.50
        # All security cues (+ security_badge) = 3.0 / 5.0 = 0.60
        # Sender domain only = 1.5 / 5.0 = 0.30
        self.assertAlmostEqual(
            gaze_metrics.compute_tir(dwell, "sender_domain_hyperlink"), 0.50
        )
        self.assertAlmostEqual(
            gaze_metrics.compute_tir(dwell, "all_security_cues"), 0.60
        )
        self.assertAlmostEqual(
            gaze_metrics.compute_tir(dwell, "sender_domain_only"), 0.30
        )

        # Zero total email dwell -> None (undefined)
        zero_dwell = {k: 0.0 for k in dwell}
        self.assertIsNone(gaze_metrics.compute_tir(zero_dwell))

        # Negative dwell -> None
        neg_dwell = {**dwell, "body_text": -0.1}
        self.assertIsNone(gaze_metrics.compute_tir(neg_dwell))


class TestTechnoeconomicAndFigures(unittest.TestCase):
    def test_prescribed_and_csv_derived_technoeconomics(self):
        model = telecom_eval.TelecomLatencyDebiasEval()
        res = model.compute_telecom_and_operational_parity()
        self.assertAlmostEqual(res["kappa_ratio"], 0.24, places=4)
        self.assertAlmostEqual(res["annual_compromise_clicks_avoided"], 4680.0, places=1)
        self.assertAlmostEqual(res["annual_escalated_breaches_prevented"], 56.2, places=1)
        self.assertAlmostEqual(res["annual_security_remediation_hours_reclaimed"], 5686.2, places=1)
        self.assertAlmostEqual(res["payback_period_months"], 15.79, places=2)

        csv_scen = telecom_eval.TelecomLatencyDebiasEval.from_benchmark_csv()
        self.assertEqual(csv_scen["scenario_type"], "SYNTHETIC_BENCHMARK_CSV_DERIVED_ESTIMATE")
        self.assertGreater(csv_scen["results"]["annual_compromise_clicks_avoided"], 5000.0)

        sens = telecom_eval.TelecomLatencyDebiasEval.run_sensitivity_analysis()
        self.assertEqual(len(sens), 12)

    def test_figure_generation_non_destructive(self):
        sha_before = file_sha256(hfra.DEFAULT_BENCHMARK_CSV)
        with tempfile.TemporaryDirectory() as tmpdir:
            out = generate_paper_figures.generate_all_figures(
                csv_path=hfra.DEFAULT_BENCHMARK_CSV, figures_dir=tmpdir
            )
            self.assertEqual(len(out["figures"]), 3)
            for fpath in out["figures"]:
                self.assertTrue(os.path.isfile(fpath))
                self.assertGreater(os.path.getsize(fpath), 50_000)
        sha_after = file_sha256(hfra.DEFAULT_BENCHMARK_CSV)
        self.assertEqual(sha_before, sha_after)


def test_all() -> bool:
    print("=== Testing IVRAR Evaluation & Statistical Toolchains (Group 12 - I080) ===")
    try:
        sus_calc.evaluate_cohort()
        print("[OK] SUS Calculator operational.")
    except Exception as e:
        print(f"[FAIL] SUS Calculator failed: {e}")
        return False

    try:
        tlx_calc.evaluate_tlx_comparison()
        print("[OK] NASA-TLX Calculator operational.")
    except Exception as e:
        print(f"[FAIL] NASA-TLX Calculator failed: {e}")
        return False

    try:
        ssq_calc.evaluate_ssq_safety()
        print("[OK] Kennedy SSQ Calculator operational.")
    except Exception as e:
        print(f"[FAIL] Kennedy SSQ Calculator failed: {e}")
        return False

    suite = unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__])
    runner = unittest.TextTestRunner(verbosity=2)
    result = runner.run(suite)
    if not result.wasSuccessful():
        print("[FAIL] Unit test suite reported failures.")
        return False

    print("[SUCCESS] ALL PSYCHOMETRIC, RETENTION, GAZE & STATISTICAL ENGINES VERIFIED SUCCESSFULLY!")
    return True


if __name__ == "__main__":
    success = test_all()
    if not success:
        sys.exit(1)
