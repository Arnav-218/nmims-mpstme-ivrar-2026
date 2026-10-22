"""
Human Factors and 14-Day Retention Analysis Engine (Group 12 - I080)
===================================================================
Course: IVRAR (Immersive Virtual, Real & Augmented Reality - 702COI002)
Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)

Purpose
-------
This module implements the complete, non-destructive statistical analysis pipeline
for evaluating Immersive VR Phishing Simulation versus 2D Web-Based Training across
a 14-day retention interval.

Key Guarantees
--------------
1. NEVER overwrites or modifies source CSV datasets during analysis.
2. Explicitly distinguishes SYNTHETIC benchmark data from real human experimental
   evidence in all reports, return dictionaries, and outputs.
3. Validates schema, missing values, duplicate participant IDs, numeric ranges,
   group labels, and paired Day 0 / Day 14 completeness.
4. Computes within-subject retention changes (Day 14 minus Day 0: raw change,
   percentage-point change, relative percentage change, retention ratio, and
   exponential forgetting decay rate lambda) using paired tests (paired t-test,
   Cohen's d_z, Wilcoxon signed-rank test).
5. Computes between-group comparisons (Immersive VR vs 2D Web at Day 0, Day 14,
   and Difference-in-Differences on the 14-day change) using Welch's two-sample
   t-test, Hedges' g, Cohen's d, 95% confidence intervals, Mann-Whitney U test,
   and Holm-Bonferroni multiple-comparison adjustment.
6. Contains zero currency symbols and zero emojis in compliance with course rules.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import os
import random
from typing import Dict, List, Optional, Sequence, Tuple

from csv_utils import (
    CsvFormatError,
    assert_not_protected,
    file_sha256,
    parse_optional_float,
    read_csv_rows,
)
import stats_utils as su


# ---------------------------------------------------------------------------
# 1. Schema and Constants
# ---------------------------------------------------------------------------
DEFAULT_BENCHMARK_CSV = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "phishing_simulation_benchmark.csv",
)

# Known SHA-256 of the seed-42 synthetic benchmark dataset shipped in the repo.
# Supports both canonical LF (Unix/Git) and CRLF (Windows git checkout).
KNOWN_SYNTHETIC_BENCHMARK_SHA256 = (
    "67d07bc1c9073b1faccf0d4e30569a390ea1f5dc425b72c9993302bf06ef1925"
)
KNOWN_SYNTHETIC_BENCHMARK_SHA256_CRLF = (
    "bea83b3e85c2a5a93445cc5f8bd5df517d061c14ad46076e7ca79e471d578875"
)
KNOWN_SYNTHETIC_BENCHMARK_SHA256_SET = {
    KNOWN_SYNTHETIC_BENCHMARK_SHA256,
    KNOWN_SYNTHETIC_BENCHMARK_SHA256_CRLF,
}

VR_MODALITY = "Immersive_VR_Training"
WEB_MODALITY = "2D_Web_Training"
VALID_MODALITIES = {VR_MODALITY, WEB_MODALITY}

REQUIRED_BENCHMARK_COLUMNS: List[str] = [
    "participant_id",
    "training_modality",
    "day0_detection_accuracy_pct",
    "day0_malicious_click_rate_pct",
    "day0_security_aoi_dwell_sec",
    "day0_cognitive_bias_score",
    "day14_detection_accuracy_pct",
    "day14_malicious_click_rate_pct",
    "day14_security_aoi_dwell_sec",
    "day14_cognitive_bias_score",
    "system_usability_scale",
]

# Validated numeric bounds for each column: (min_value, max_value, units)
COLUMN_BOUNDS: Dict[str, Tuple[float, float, str]] = {
    "day0_detection_accuracy_pct": (0.0, 100.0, "%"),
    "day0_malicious_click_rate_pct": (0.0, 100.0, "%"),
    "day0_security_aoi_dwell_sec": (0.0, 3600.0, "s"),
    "day0_cognitive_bias_score": (1.0, 10.0, "score (1-10)"),
    "day14_detection_accuracy_pct": (0.0, 100.0, "%"),
    "day14_malicious_click_rate_pct": (0.0, 100.0, "%"),
    "day14_security_aoi_dwell_sec": (0.0, 3600.0, "s"),
    "day14_cognitive_bias_score": (1.0, 10.0, "score (1-10)"),
    "system_usability_scale": (0.0, 100.0, "score (0-100)"),
}

# Paired longitudinal metrics tracked at both Day 0 and Day 14
LONGITUDINAL_METRICS: List[Dict[str, str]] = [
    {
        "key": "detection_accuracy_pct",
        "label": "Threat Detection Accuracy (%)",
        "day0_col": "day0_detection_accuracy_pct",
        "day14_col": "day14_detection_accuracy_pct",
        "units": "percentage points",
        "favorable_direction": "higher",
    },
    {
        "key": "malicious_click_rate_pct",
        "label": "Malicious Link Click Rate (%)",
        "day0_col": "day0_malicious_click_rate_pct",
        "day14_col": "day14_malicious_click_rate_pct",
        "units": "percentage points",
        "favorable_direction": "lower",
    },
    {
        "key": "security_aoi_dwell_sec",
        "label": "Diagnostic Security AOI Dwell Time (s)",
        "day0_col": "day0_security_aoi_dwell_sec",
        "day14_col": "day14_security_aoi_dwell_sec",
        "units": "seconds",
        "favorable_direction": "higher",
    },
    {
        "key": "cognitive_bias_score",
        "label": "Cognitive Bias Susceptibility Score (1-10)",
        "day0_col": "day0_cognitive_bias_score",
        "day14_col": "day14_cognitive_bias_score",
        "units": "scale points (1-10)",
        "favorable_direction": "lower",
    },
]


# ---------------------------------------------------------------------------
# 2. Data Loading, Provenance Identification, and Validation
# ---------------------------------------------------------------------------
def identify_data_provenance(csv_path: str, rows: Sequence[Dict[str, object]]) -> Dict[str, object]:
    """Identify whether a dataset is the known seed-42 synthetic benchmark or custom data.

    Never claims a dataset is real human evidence unless explicitly verified.
    """
    sha = file_sha256(csv_path) if os.path.isfile(csv_path) else ""
    basename = os.path.basename(csv_path).lower()
    is_known_synthetic = sha.lower() in KNOWN_SYNTHETIC_BENCHMARK_SHA256_SET
    is_named_synthetic = any(
        token in basename for token in ("benchmark", "sample", "synthetic", "mock", "sim")
    )
    if is_known_synthetic:
        status = "SYNTHETIC_BENCHMARK_SEED42"
        label = "Synthetic Benchmark Dataset (N=50, random.seed=42)"
        is_synthetic = True
    elif is_named_synthetic:
        status = "SYNTHETIC_OR_SAMPLE_DATASET"
        label = f"Synthetic / Sample Dataset ({os.path.basename(csv_path)})"
        is_synthetic = True
    else:
        status = "UNVERIFIED_EXTERNAL_DATASET"
        label = f"External Dataset ({os.path.basename(csv_path)} - provenance requires verification)"
        is_synthetic = False

    limitations = [
        "Aggregated percentages in phishing_simulation_benchmark.csv do not include raw trial counts or per-AOI denominator breakdowns.",
        "The column day*_security_aoi_dwell_sec records diagnostic AOI dwell time in seconds, not the dimensionless Threat Inspection Ratio (TIR) which requires total email dwell time across all 5 AOIs.",
        "Only System Usability Scale (SUS) is present in phishing_simulation_benchmark.csv; NASA-TLX and Kennedy SSQ appear only as pre-aggregated columns in sample_experiment_data.csv.",
    ]
    if is_synthetic:
        limitations.insert(
            0,
            "CRITICAL: This dataset is computer-generated synthetic data (Gaussian RNG). "
            "Statistical significance in synthetic data reflects the generator's programmed parameters "
            "and does NOT constitute empirical proof that VR training works in real human participants.",
        )

    return {
        "file_path": os.path.abspath(csv_path),
        "file_name": os.path.basename(csv_path),
        "sha256": sha,
        "status": status,
        "label": label,
        "is_synthetic": is_synthetic,
        "row_count": len(rows),
        "limitations": limitations,
    }


def validate_and_load_benchmark_csv(csv_path: str = DEFAULT_BENCHMARK_CSV) -> Dict[str, object]:
    """Load and validate the benchmark CSV without modifying it.

    Returns a dictionary with:
    * ``is_valid``: True if required columns exist and at least 2 valid records per group exist
    * ``errors``: list of critical schema/row errors
    * ``warnings``: list of non-fatal data-quality warnings
    * ``records``: list of cleaned participant dictionaries (valid rows only)
    * ``excluded_rows``: list of rows excluded due to missing/invalid values
    * ``provenance``: provenance metadata dictionary
    * ``group_counts``: participant counts per training_modality
    """
    header, raw_rows = read_csv_rows(csv_path)
    errors: List[str] = []
    warnings: List[str] = []

    missing_cols = [col for col in REQUIRED_BENCHMARK_COLUMNS if col not in header]
    if missing_cols:
        raise CsvFormatError(
            f"Benchmark CSV is missing required columns: {missing_cols}. Found: {header}"
        )

    if not raw_rows:
        raise CsvFormatError(f"Benchmark CSV contains header but zero data rows: {csv_path}")

    seen_ids = set()
    valid_records: List[Dict[str, object]] = []
    excluded_rows: List[Dict[str, object]] = []
    group_counts: Dict[str, int] = {VR_MODALITY: 0, WEB_MODALITY: 0}

    for line_idx, raw in enumerate(raw_rows, start=2):
        row_problems: List[str] = []
        pid = (raw.get("participant_id") or "").strip()
        modality = (raw.get("training_modality") or "").strip()

        if not pid:
            row_problems.append(f"line {line_idx}: missing participant_id")
        elif pid in seen_ids:
            row_problems.append(f"line {line_idx}: duplicate participant_id '{pid}'")
        else:
            seen_ids.add(pid)

        if modality not in VALID_MODALITIES:
            row_problems.append(
                f"line {line_idx} ({pid or 'unknown'}): invalid training_modality '{modality}' "
                f"(expected one of {sorted(VALID_MODALITIES)})"
            )

        parsed_numbers: Dict[str, float] = {}
        for col_name, (low, high, _units) in COLUMN_BOUNDS.items():
            val, prob = parse_optional_float(raw.get(col_name))
            if prob is not None:
                row_problems.append(f"line {line_idx} ({pid}): {col_name} {prob}")
            elif val is None:
                row_problems.append(f"line {line_idx} ({pid}): {col_name} is missing/blank")
            elif not (low <= val <= high):
                row_problems.append(
                    f"line {line_idx} ({pid}): {col_name}={val} outside valid range [{low}, {high}]"
                )
            else:
                parsed_numbers[col_name] = val

        if row_problems:
            errors.extend(row_problems)
            excluded_rows.append({"line": line_idx, "raw": raw, "problems": row_problems})
            continue

        record: Dict[str, object] = {
            "participant_id": pid,
            "training_modality": modality,
        }
        record.update(parsed_numbers)

        # Derived per-participant longitudinal retention metrics
        for m in LONGITUDINAL_METRICS:
            k = m["key"]
            d0 = parsed_numbers[m["day0_col"]]
            d14 = parsed_numbers[m["day14_col"]]
            delta = d14 - d0
            rel_pct = ((d14 - d0) / d0 * 100.0) if d0 != 0.0 else float("nan")
            ret_ratio = (d14 / d0 * 100.0) if d0 != 0.0 else float("nan")
            record[f"delta_{k}"] = delta
            record[f"rel_change_pct_{k}"] = rel_pct
            record[f"retention_ratio_pct_{k}"] = ret_ratio

        valid_records.append(record)
        group_counts[modality] = group_counts.get(modality, 0) + 1

    if group_counts[VR_MODALITY] != group_counts[WEB_MODALITY]:
        warnings.append(
            f"Unbalanced group sizes: {VR_MODALITY}={group_counts[VR_MODALITY]}, "
            f"{WEB_MODALITY}={group_counts[WEB_MODALITY]}"
        )

    provenance = identify_data_provenance(csv_path, valid_records)
    is_valid = (
        len(errors) == 0
        and group_counts[VR_MODALITY] >= 2
        and group_counts[WEB_MODALITY] >= 2
    )

    return {
        "is_valid": is_valid,
        "errors": errors,
        "warnings": warnings,
        "total_rows_read": len(raw_rows),
        "valid_row_count": len(valid_records),
        "excluded_row_count": len(excluded_rows),
        "excluded_rows": excluded_rows,
        "group_counts": group_counts,
        "provenance": provenance,
        "records": valid_records,
    }


# ---------------------------------------------------------------------------
# 3. Retention Change and Forgetting Curve Calculations
# ---------------------------------------------------------------------------
def compute_retention_change_metrics(day0: float, day14: float) -> Dict[str, float]:
    """Calculate the three distinct ways to express change between Day 0 and Day 14.

    1. ``raw_change`` (also equals ``percentage_point_change`` when the input is a
       percentage): ``Day 14 - Day 0``.
    2. ``relative_pct_change``: ``((Day 14 - Day 0) / Day 0) * 100``.
    3. ``retention_ratio_pct``: ``(Day 14 / Day 0) * 100`` (fraction of initial
       level retained at Day 14).
    """
    raw_delta = day14 - day0
    if day0 == 0.0:
        rel_change = float("nan")
        retained_pct = float("nan")
    else:
        rel_change = (raw_delta / day0) * 100.0
        retained_pct = (day14 / day0) * 100.0
    return {
        "day0": day0,
        "day14": day14,
        "raw_change": raw_delta,
        "percentage_point_change": raw_delta,
        "relative_pct_change": rel_change,
        "retention_ratio_pct": retained_pct,
    }


def evaluate_exponential_forgetting_curve(
    days: Sequence[float],
    a0: float,
    a_inf: float,
    decay_lambda: float,
) -> List[float]:
    """Evaluate the asymptotic exponential forgetting model from Guide Eq. 1.2:

        A(t) = A_inf + (A_0 - A_inf) * exp(-lambda * t)
    """
    return [a_inf + (a0 - a_inf) * math.exp(-decay_lambda * float(t)) for t in days]


# ---------------------------------------------------------------------------
# 4. Complete Statistical Analysis Pipeline
# ---------------------------------------------------------------------------
def run_full_retention_analysis(csv_path: str = DEFAULT_BENCHMARK_CSV) -> Dict[str, object]:
    """Run validation, descriptive statistics, paired Day 0 vs Day 14 tests,
    between-group VR vs 2D tests, Difference-in-Differences, and Holm-Bonferroni
    corrections on the benchmark dataset.
    """
    loaded = validate_and_load_benchmark_csv(csv_path)
    records = loaded["records"]

    vr_rows = [r for r in records if r["training_modality"] == VR_MODALITY]
    web_rows = [r for r in records if r["training_modality"] == WEB_MODALITY]

    if len(vr_rows) < 2 or len(web_rows) < 2:
        raise ValueError(
            f"Insufficient valid rows per group for statistical analysis: "
            f"VR={len(vr_rows)}, Web={len(web_rows)}"
        )

    metrics_analysis: Dict[str, Dict[str, object]] = {}
    primary_p_values: List[float] = []
    primary_keys: List[Tuple[str, str]] = []

    for m in LONGITUDINAL_METRICS:
        key = m["key"]
        d0_col = m["day0_col"]
        d14_col = m["day14_col"]
        delta_col = f"delta_{key}"

        vr_d0 = [float(r[d0_col]) for r in vr_rows]
        vr_d14 = [float(r[d14_col]) for r in vr_rows]
        vr_delta = [float(r[delta_col]) for r in vr_rows]

        web_d0 = [float(r[d0_col]) for r in web_rows]
        web_d14 = [float(r[d14_col]) for r in web_rows]
        web_delta = [float(r[delta_col]) for r in web_rows]

        # 1. Descriptive stats
        desc_vr_d0 = su.describe(vr_d0)
        desc_vr_d14 = su.describe(vr_d14)
        desc_vr_delta = su.describe(vr_delta)

        desc_web_d0 = su.describe(web_d0)
        desc_web_d14 = su.describe(web_d14)
        desc_web_delta = su.describe(web_delta)

        # Group-mean retention summary (raw/pp change, relative %, retention ratio)
        vr_retention_summary = compute_retention_change_metrics(
            desc_vr_d0["mean"], desc_vr_d14["mean"]
        )
        web_retention_summary = compute_retention_change_metrics(
            desc_web_d0["mean"], desc_web_d14["mean"]
        )

        # 2. Within-participant paired tests (Day 14 minus Day 0 inside each group)
        vr_paired_t = su.paired_t_test(vr_d0, vr_d14)
        vr_dz = su.cohens_dz(vr_d0, vr_d14)
        vr_wilcoxon = su.wilcoxon_signed_rank(vr_d0, vr_d14)

        web_paired_t = su.paired_t_test(web_d0, web_d14)
        web_dz = su.cohens_dz(web_d0, web_d14)
        web_wilcoxon = su.wilcoxon_signed_rank(web_d0, web_d14)

        # 3. Between-group independent tests (VR minus 2D Web)
        between_d0_t = su.welch_t_test(vr_d0, web_d0)
        between_d0_eff = su.hedges_g(vr_d0, web_d0)
        between_d0_mwu = su.mann_whitney_u(vr_d0, web_d0)

        between_d14_t = su.welch_t_test(vr_d14, web_d14)
        between_d14_eff = su.hedges_g(vr_d14, web_d14)
        between_d14_mwu = su.mann_whitney_u(vr_d14, web_d14)

        # 4. Difference-in-Differences (VR 14-day change minus Web 14-day change)
        did_t = su.welch_t_test(vr_delta, web_delta)
        did_eff = su.hedges_g(vr_delta, web_delta)
        did_mwu = su.mann_whitney_u(vr_delta, web_delta)

        primary_p_values.append(float(between_d14_t["p_value"]))
        primary_keys.append((key, "between_day14"))
        primary_p_values.append(float(did_t["p_value"]))
        primary_keys.append((key, "difference_in_differences"))

        metrics_analysis[key] = {
            "meta": m,
            "vr": {
                "day0": desc_vr_d0,
                "day14": desc_vr_d14,
                "delta": desc_vr_delta,
                "retention_summary": vr_retention_summary,
                "paired_t": vr_paired_t,
                "effect_size_dz": vr_dz,
                "wilcoxon": vr_wilcoxon,
            },
            "web": {
                "day0": desc_web_d0,
                "day14": desc_web_d14,
                "delta": desc_web_delta,
                "retention_summary": web_retention_summary,
                "paired_t": web_paired_t,
                "effect_size_dz": web_dz,
                "wilcoxon": web_wilcoxon,
            },
            "between_day0": {
                "welch_t": between_d0_t,
                "effect_size": between_d0_eff,
                "mann_whitney_u": between_d0_mwu,
                "relative_difference_pct": (
                    (desc_vr_d0["mean"] - desc_web_d0["mean"]) / desc_web_d0["mean"] * 100.0
                    if desc_web_d0["mean"] != 0
                    else float("nan")
                ),
            },
            "between_day14": {
                "welch_t": between_d14_t,
                "effect_size": between_d14_eff,
                "mann_whitney_u": between_d14_mwu,
                "relative_difference_pct": (
                    (desc_vr_d14["mean"] - desc_web_d14["mean"]) / desc_web_d14["mean"] * 100.0
                    if desc_web_d14["mean"] != 0
                    else float("nan")
                ),
            },
            "difference_in_differences": {
                "welch_t": did_t,
                "effect_size": did_eff,
                "mann_whitney_u": did_mwu,
            },
        }

    # Single-timepoint metric: System Usability Scale (SUS)
    vr_sus = [float(r["system_usability_scale"]) for r in vr_rows]
    web_sus = [float(r["system_usability_scale"]) for r in web_rows]
    sus_t = su.welch_t_test(vr_sus, web_sus)
    sus_eff = su.hedges_g(vr_sus, web_sus)
    sus_mwu = su.mann_whitney_u(vr_sus, web_sus)
    primary_p_values.append(float(sus_t["p_value"]))
    primary_keys.append(("system_usability_scale", "between"))

    sus_analysis = {
        "label": "System Usability Scale (SUS, 0-100)",
        "vr": su.describe(vr_sus),
        "web": su.describe(web_sus),
        "between": {
            "welch_t": sus_t,
            "effect_size": sus_eff,
            "mann_whitney_u": sus_mwu,
            "relative_difference_pct": (
                (su.mean(vr_sus) - su.mean(web_sus)) / su.mean(web_sus) * 100.0
                if su.mean(web_sus) != 0
                else float("nan")
            ),
        },
    }

    # Apply Holm-Bonferroni adjustment across all 9 primary hypothesis tests
    adjusted_ps = su.holm_adjust(primary_p_values)
    for (metric_key, comparison_key), adj_p in zip(primary_keys, adjusted_ps):
        if metric_key == "system_usability_scale":
            sus_analysis["between"]["welch_t"]["p_value_holm"] = adj_p
        else:
            metrics_analysis[metric_key][comparison_key]["welch_t"]["p_value_holm"] = adj_p

    # Reconcile documented theoretical parameters vs actual CSV sample statistics
    reconciliation = build_documentation_reconciliation(metrics_analysis, sus_analysis)

    return {
        "validation": {
            "is_valid": loaded["is_valid"],
            "errors": loaded["errors"],
            "warnings": loaded["warnings"],
            "total_rows_read": loaded["total_rows_read"],
            "valid_row_count": loaded["valid_row_count"],
            "excluded_row_count": loaded["excluded_row_count"],
            "group_counts": loaded["group_counts"],
        },
        "provenance": loaded["provenance"],
        "metrics": metrics_analysis,
        "system_usability_scale": sus_analysis,
        "reconciliation_table": reconciliation,
        "records": records,
    }


def build_documentation_reconciliation(
    metrics_analysis: Dict[str, Dict[str, object]],
    sus_analysis: Dict[str, object],
) -> List[Dict[str, object]]:
    """Compare the README/Guide theoretical generator parameters against the
    actual sample statistics computed from the 50 rows in phishing_simulation_benchmark.csv.

    Why do they differ slightly?
    In generate_paper_figures.py, the 50 rows were drawn from random.gauss(mu, sigma)
    with random.seed(42). A finite sample of n=25 draws has a sample mean and sample SD
    that fluctuate slightly around the population parameters (mu, sigma) passed to
    random.gauss(). Reporting both transparently prevents any confusion during viva.
    """
    acc = metrics_analysis["detection_accuracy_pct"]
    clk = metrics_analysis["malicious_click_rate_pct"]
    dwl = metrics_analysis["security_aoi_dwell_sec"]
    bias = metrics_analysis["cognitive_bias_score"]

    return [
        {
            "metric": "Day 0 Detection Accuracy (%)",
            "documented_web": "72.4 +/- 6.2%",
            "computed_web": f"{acc['web']['day0']['mean']:.2f} +/- {acc['web']['day0']['sd']:.2f}%",
            "documented_vr": "92.8 +/- 3.4%",
            "computed_vr": f"{acc['vr']['day0']['mean']:.2f} +/- {acc['vr']['day0']['sd']:.2f}%",
            "reason": "Finite sample realization (n=25 per group, seed=42) vs generator population mu/sigma",
        },
        {
            "metric": "Day 14 Detection Accuracy (%)",
            "documented_web": "57.6 +/- 5.8%",
            "computed_web": f"{acc['web']['day14']['mean']:.2f} +/- {acc['web']['day14']['sd']:.2f}%",
            "documented_vr": "88.6 +/- 3.1%",
            "computed_vr": f"{acc['vr']['day14']['mean']:.2f} +/- {acc['vr']['day14']['sd']:.2f}%",
            "reason": "Day 14 = Day 0 - Gaussian decay; variance adds in independent sequential draws",
        },
        {
            "metric": "Day 0 Malicious Click Rate (%)",
            "documented_web": "18.2 +/- 4.5%",
            "computed_web": f"{clk['web']['day0']['mean']:.2f} +/- {clk['web']['day0']['sd']:.2f}%",
            "documented_vr": "5.2 +/- 1.8%",
            "computed_vr": f"{clk['vr']['day0']['mean']:.2f} +/- {clk['vr']['day0']['sd']:.2f}%",
            "reason": "Finite sample realization (n=25 per group, seed=42)",
        },
        {
            "metric": "Day 14 Malicious Click Rate (%)",
            "documented_web": "26.8 +/- 4.9% (22.4% in telecom script)",
            "computed_web": f"{clk['web']['day14']['mean']:.2f} +/- {clk['web']['day14']['sd']:.2f}%",
            "documented_vr": "7.3 +/- 1.9% (6.8% in telecom script)",
            "computed_vr": f"{clk['vr']['day14']['mean']:.2f} +/- {clk['vr']['day14']['sd']:.2f}%",
            "reason": "Sample mean in CSV (6.83% VR, 26.86% Web) vs generator parameter (7.3% VR, 26.8% Web)",
        },
        {
            "metric": "Day 0 Diagnostic AOI Dwell (s)",
            "documented_web": "0.85 +/- 0.22 s (0.74 s in README abstract)",
            "computed_web": f"{dwl['web']['day0']['mean']:.2f} +/- {dwl['web']['day0']['sd']:.2f} s",
            "documented_vr": "2.45 +/- 0.38 s",
            "computed_vr": f"{dwl['vr']['day0']['mean']:.2f} +/- {dwl['vr']['day0']['sd']:.2f} s",
            "reason": "README abstract cites 0.74 s; Blueprint Table 4.2 cites Day 0 (0.85 s) and Day 14 (0.53 s)",
        },
        {
            "metric": "Day 14 Diagnostic AOI Dwell (s)",
            "documented_web": "0.53 +/- 0.18 s",
            "computed_web": f"{dwl['web']['day14']['mean']:.2f} +/- {dwl['web']['day14']['sd']:.2f} s",
            "documented_vr": "2.17 +/- 0.32 s",
            "computed_vr": f"{dwl['vr']['day14']['mean']:.2f} +/- {dwl['vr']['day14']['sd']:.2f} s",
            "reason": "Finite sample realization (n=25 per group, seed=42)",
        },
        {
            "metric": "Day 14 Cognitive Bias Score (1-10)",
            "documented_web": "8.2 +/- 0.9",
            "computed_web": f"{bias['web']['day14']['mean']:.2f} +/- {bias['web']['day14']['sd']:.2f}",
            "documented_vr": "3.3 +/- 0.6",
            "computed_vr": f"{bias['vr']['day14']['mean']:.2f} +/- {bias['vr']['day14']['sd']:.2f}",
            "reason": "Upper bound clamping at 10.0 in Web group slightly compresses Web SD",
        },
        {
            "metric": "System Usability Scale (0-100)",
            "documented_web": "62.5 +/- 7.8 (Grade C)",
            "computed_web": f"{sus_analysis['web']['mean']:.2f} +/- {sus_analysis['web']['sd']:.2f}",
            "documented_vr": "86.8 +/- 4.9 (Grade A)",
            "computed_vr": f"{sus_analysis['vr']['mean']:.2f} +/- {sus_analysis['vr']['sd']:.2f}",
            "reason": "Finite sample realization (n=25 per group, seed=42)",
        },
    ]


# ---------------------------------------------------------------------------
# 5. Explicit, Opt-In Synthetic Data Generator (Never Overwrites by Default)
# ---------------------------------------------------------------------------
def generate_synthetic_benchmark_dataset(
    output_path: str,
    seed: int = 42,
    allow_overwrite: bool = False,
) -> List[Dict[str, object]]:
    """Generate the 50-row synthetic benchmark dataset using a fixed random seed.

    SAFETY RULE: By default ``allow_overwrite=False``, and writing directly to
    ``DEFAULT_BENCHMARK_CSV`` is blocked unless ``allow_overwrite=True`` is
    explicitly passed. Ordinary analysis NEVER calls this function.
    """
    if not allow_overwrite:
        assert_not_protected(output_path, [DEFAULT_BENCHMARK_CSV])
        if os.path.exists(output_path):
            raise FileExistsError(
                f"Refusing to overwrite existing file '{output_path}' without allow_overwrite=True."
            )

    rng = random.Random(seed)
    participants = [f"P{i + 1:03d}" for i in range(50)]
    training_conditions = [WEB_MODALITY] * 25 + [VR_MODALITY] * 25
    rng.shuffle(training_conditions)

    records: List[Dict[str, object]] = []
    for pid, cond in zip(participants, training_conditions):
        if cond == WEB_MODALITY:
            day0_acc = rng.gauss(72.4, 6.2)
            day0_click = rng.gauss(18.2, 4.5)
            day0_aoi_dwell = rng.gauss(0.85, 0.22)
            day0_bias_score = rng.gauss(6.4, 1.1)

            day14_acc = day0_acc - rng.gauss(14.8, 3.5)
            day14_click = day0_click + rng.gauss(8.6, 2.8)
            day14_aoi_dwell = day0_aoi_dwell - rng.gauss(0.32, 0.12)
            day14_bias_score = day0_bias_score + rng.gauss(1.8, 0.6)
            sus_score = rng.gauss(62.5, 7.8)
        else:
            day0_acc = rng.gauss(92.8, 3.4)
            day0_click = rng.gauss(5.2, 1.8)
            day0_aoi_dwell = rng.gauss(2.45, 0.38)
            day0_bias_score = rng.gauss(2.8, 0.7)

            day14_acc = day0_acc - rng.gauss(4.2, 1.5)
            day14_click = day0_click + rng.gauss(2.1, 1.0)
            day14_aoi_dwell = day0_aoi_dwell - rng.gauss(0.28, 0.15)
            day14_bias_score = day0_bias_score + rng.gauss(0.5, 0.4)
            sus_score = rng.gauss(86.8, 4.9)

        records.append(
            {
                "participant_id": pid,
                "training_modality": cond,
                "day0_detection_accuracy_pct": round(max(30.0, min(100.0, day0_acc)), 2),
                "day0_malicious_click_rate_pct": round(max(0.0, min(60.0, day0_click)), 2),
                "day0_security_aoi_dwell_sec": round(max(0.1, day0_aoi_dwell), 3),
                "day0_cognitive_bias_score": round(max(1.0, min(10.0, day0_bias_score)), 2),
                "day14_detection_accuracy_pct": round(max(25.0, min(100.0, day14_acc)), 2),
                "day14_malicious_click_rate_pct": round(max(0.0, min(60.0, day14_click)), 2),
                "day14_security_aoi_dwell_sec": round(max(0.1, day14_aoi_dwell), 3),
                "day14_cognitive_bias_score": round(max(1.0, min(10.0, day14_bias_score)), 2),
                "system_usability_scale": round(max(40.0, min(100.0, sus_score)), 1),
            }
        )

    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    with open(output_path, "w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=REQUIRED_BENCHMARK_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(records)

    return records


# ---------------------------------------------------------------------------
# 6. Human-Readable Report Formatting
# ---------------------------------------------------------------------------
def format_analysis_report(results: Dict[str, object]) -> str:
    """Create a plain-English, console-friendly statistical report."""
    val = results["validation"]
    prov = results["provenance"]
    lines: List[str] = []

    lines.append("=" * 82)
    lines.append("IVRAR GROUP 12 - HUMAN FACTORS & 14-DAY RETENTION STATISTICAL REPORT (I080)")
    lines.append("=" * 82)
    lines.append(f"Dataset File      : {prov['file_name']}")
    lines.append(f"SHA-256 Checksum  : {prov['sha256']}")
    lines.append(f"Data Provenance   : {prov['label']}")
    lines.append(
        f"Validation Status : {'PASSED' if val['is_valid'] else 'FAILED'} "
        f"(Valid Rows: {val['valid_row_count']}/{val['total_rows_read']}, "
        f"Excluded: {val['excluded_row_count']})"
    )
    lines.append(
        f"Cohort Sizes      : {VR_MODALITY} n={val['group_counts'][VR_MODALITY]}, "
        f"{WEB_MODALITY} n={val['group_counts'][WEB_MODALITY]}"
    )
    lines.append("-" * 82)
    lines.append("DATA INTEGRITY & LIMITATION NOTES:")
    for idx, note in enumerate(prov["limitations"], start=1):
        lines.append(f"  {idx}. {note}")

    lines.append("-" * 82)
    lines.append("1. LONGITUDINAL METRICS (DAY 0 VS DAY 14 WITHIN-GROUP & BETWEEN-GROUP)")
    lines.append("-" * 82)

    for key, item in results["metrics"].items():
        meta = item["meta"]
        vr = item["vr"]
        web = item["web"]
        b0 = item["between_day0"]
        b14 = item["between_day14"]
        did = item["difference_in_differences"]

        lines.append(f"\n[{meta['label']}]")
        lines.append(
            f"  Immersive VR (n={vr['day0']['n']}): "
            f"Day 0 = {vr['day0']['mean']:.2f} +/- {vr['day0']['sd']:.2f} "
            f"(95% CI [{vr['day0']['ci95_low']:.2f}, {vr['day0']['ci95_high']:.2f}]) | "
            f"Day 14 = {vr['day14']['mean']:.2f} +/- {vr['day14']['sd']:.2f} "
            f"(95% CI [{vr['day14']['ci95_low']:.2f}, {vr['day14']['ci95_high']:.2f}])"
        )
        lines.append(
            f"    -> VR 14-Day Change: raw/pp = {vr['retention_summary']['raw_change']:+.2f}, "
            f"relative = {vr['retention_summary']['relative_pct_change']:+.2f}%, "
            f"retained = {vr['retention_summary']['retention_ratio_pct']:.1f}% | "
            f"Paired t({vr['paired_t']['df']:.0f}) = {vr['paired_t']['statistic']:.2f}, "
            f"p = {su.format_p(vr['paired_t']['p_value'])}, d_z = {vr['effect_size_dz']['cohens_dz']:.2f}"
        )
        lines.append(
            f"  2D Web (n={web['day0']['n']}):       "
            f"Day 0 = {web['day0']['mean']:.2f} +/- {web['day0']['sd']:.2f} "
            f"(95% CI [{web['day0']['ci95_low']:.2f}, {web['day0']['ci95_high']:.2f}]) | "
            f"Day 14 = {web['day14']['mean']:.2f} +/- {web['day14']['sd']:.2f} "
            f"(95% CI [{web['day14']['ci95_low']:.2f}, {web['day14']['ci95_high']:.2f}])"
        )
        lines.append(
            f"    -> Web 14-Day Change: raw/pp = {web['retention_summary']['raw_change']:+.2f}, "
            f"relative = {web['retention_summary']['relative_pct_change']:+.2f}%, "
            f"retained = {web['retention_summary']['retention_ratio_pct']:.1f}% | "
            f"Paired t({web['paired_t']['df']:.0f}) = {web['paired_t']['statistic']:.2f}, "
            f"p = {su.format_p(web['paired_t']['p_value'])}, d_z = {web['effect_size_dz']['cohens_dz']:.2f}"
        )
        lines.append(
            f"  Between-Group Day 0 (VR - Web):  diff = {b0['welch_t']['mean_difference']:+.2f} "
            f"({b0['relative_difference_pct']:+.1f}% rel), "
            f"Welch t({b0['welch_t']['df']:.1f}) = {b0['welch_t']['statistic']:.2f}, "
            f"p = {su.format_p(b0['welch_t']['p_value'])}, "
            f"Cohen's d = {b0['effect_size']['cohens_d']:.2f} (Hedges' g = {b0['effect_size']['hedges_g']:.2f})"
        )
        lines.append(
            f"  Between-Group Day 14 (VR - Web): diff = {b14['welch_t']['mean_difference']:+.2f} "
            f"({b14['relative_difference_pct']:+.1f}% rel), "
            f"Welch t({b14['welch_t']['df']:.1f}) = {b14['welch_t']['statistic']:.2f}, "
            f"p = {su.format_p(b14['welch_t']['p_value'])} "
            f"(Holm-adj p = {su.format_p(b14['welch_t'].get('p_value_holm'))}), "
            f"Cohen's d = {b14['effect_size']['cohens_d']:.2f} (Hedges' g = {b14['effect_size']['hedges_g']:.2f}), "
            f"Mann-Whitney p = {su.format_p(b14['mann_whitney_u']['p_value'])}"
        )
        lines.append(
            f"  Difference-in-Differences (Delta_VR - Delta_Web): "
            f"diff = {did['welch_t']['mean_difference']:+.2f}, "
            f"Welch t({did['welch_t']['df']:.1f}) = {did['welch_t']['statistic']:.2f}, "
            f"p = {su.format_p(did['welch_t']['p_value'])} "
            f"(Holm-adj p = {su.format_p(did['welch_t'].get('p_value_holm'))}), "
            f"Cohen's d = {did['effect_size']['cohens_d']:.2f}"
        )

    sus = results["system_usability_scale"]
    sb = sus["between"]
    lines.append(f"\n[{sus['label']}]")
    lines.append(
        f"  Immersive VR: {sus['vr']['mean']:.2f} +/- {sus['vr']['sd']:.2f} "
        f"(median {sus['vr']['median']:.1f}, IQR {sus['vr']['q3'] - sus['vr']['q1']:.1f}) | "
        f"2D Web: {sus['web']['mean']:.2f} +/- {sus['web']['sd']:.2f} "
        f"(median {sus['web']['median']:.1f}, IQR {sus['web']['q3'] - sus['web']['q1']:.1f})"
    )
    lines.append(
        f"  Between-Group (VR - Web): diff = {sb['welch_t']['mean_difference']:+.2f} "
        f"({sb['relative_difference_pct']:+.1f}% rel), "
        f"Welch t({sb['welch_t']['df']:.1f}) = {sb['welch_t']['statistic']:.2f}, "
        f"p = {su.format_p(sb['welch_t']['p_value'])}, "
        f"Cohen's d = {sb['effect_size']['cohens_d']:.2f}"
    )

    lines.append("\n" + "-" * 82)
    lines.append("2. RECONCILIATION: DOCUMENTED GENERATOR PARAMETERS VS SAMPLE CSV REALIZATION")
    lines.append("-" * 82)
    for row in results["reconciliation_table"]:
        lines.append(
            f"  * {row['metric']:34s} | Doc Web: {row['documented_web']:20s} -> CSV Web: {row['computed_web']:18s} "
            f"| Doc VR: {row['documented_vr']:20s} -> CSV VR: {row['computed_vr']}"
        )
    lines.append("=" * 82)
    return "\n".join(lines)


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Run the Group 12 Human Factors & 14-Day Retention statistical analysis."
    )
    parser.add_argument(
        "--csv",
        default=DEFAULT_BENCHMARK_CSV,
        help="Path to input benchmark CSV file (read-only).",
    )
    parser.add_argument(
        "--json-out",
        default=None,
        help="Optional path to save machine-readable JSON analysis report.",
    )
    args = parser.parse_args(argv)

    results = run_full_retention_analysis(args.csv)
    print(format_analysis_report(results))

    if args.json_out:
        assert_not_protected(args.json_out, [DEFAULT_BENCHMARK_CSV])
        exportable = {k: v for k, v in results.items() if k != "records"}
        with open(args.json_out, "w", encoding="utf-8") as handle:
            json.dump(exportable, handle, indent=2)
        print(f"[OK] Saved JSON analysis summary to: {args.json_out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
