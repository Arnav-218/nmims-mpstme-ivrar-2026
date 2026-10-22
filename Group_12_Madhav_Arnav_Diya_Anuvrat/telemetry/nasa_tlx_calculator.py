"""
NASA Task Load Index (NASA-TLX) Cognitive Workload Engine
=========================================================
Conforming to Hart & Staveland (1988) Cognitive Ergonomics & Byers et al. (1989)
Raw TLX (RTLX) unweighted averaging protocol.

Formula & Assumptions (Hart & Staveland, 1988; Byers et al., 1989)
------------------------------------------------------------------
* Evaluates 6 subjective workload subscales, each rated on a 0 to 100 scale:
  1. ``mental``      (Mental Demand)
  2. ``physical``    (Physical Demand)
  3. ``temporal``    (Temporal Demand / Time Pressure)
  4. ``performance`` (Perceived Failure / Performance Workload: 0 = Good, 100 = Poor)
  5. ``effort``      (Effort Required)
  6. ``frustration`` (Frustration Level)
* Raw TLX (RTLX) = unweighted arithmetic mean of the 6 subscales:
      RTLX = (mental + physical + temporal + performance + effort + frustration) / 6.0
* Lower RTLX scores represent lower perceived cognitive/physical workload.

CSV Input Support
-----------------
1. Raw 6-subscale CSVs containing columns ``mental``, ``physical``, ``temporal``,
   ``performance``, ``effort``, ``frustration`` (or ``tlx_mental``, etc.):
   validates each subscale in [0, 100] and computes ``calculate_tlx(subscales)``.
2. Pre-aggregated CSVs (such as ``sample_experiment_data.csv`` containing
   ``raw_tlx_score``): validates scores in [0, 100] and explicitly reports the
   limitation that individual subscale ratings are absent from the CSV.
3. Datasets lacking any TLX column (such as ``phishing_simulation_benchmark.csv``):
   raises a descriptive ValueError explaining that NASA-TLX data was not collected
   in that file rather than fabricating numbers.
"""
from __future__ import annotations

import math
import os
from typing import Dict, List, Mapping, Optional, Sequence, Tuple

from csv_utils import parse_optional_float, read_csv_rows
import stats_utils as su


TLX_SUBSCALES: Tuple[str, ...] = (
    "mental",
    "physical",
    "temporal",
    "performance",
    "effort",
    "frustration",
)
PREAGGREGATED_TLX_COLUMNS: Tuple[str, ...] = ("raw_tlx_score", "nasa_tlx", "tlx_score")


def validate_tlx_subscales(subscales: Mapping[str, object]) -> Dict[str, float]:
    """Validate that a dictionary contains all 6 NASA-TLX subscales in [0.0, 100.0]."""
    if not isinstance(subscales, Mapping):
        raise ValueError("NASA-TLX subscales must be passed as a dictionary/mapping.")

    missing = [k for k in TLX_SUBSCALES if k not in subscales]
    if missing:
        raise ValueError(
            f"NASA-TLX requires all 6 subscales {list(TLX_SUBSCALES)}; missing: {missing}"
        )

    cleaned: Dict[str, float] = {}
    for k in TLX_SUBSCALES:
        raw_val = subscales[k]
        if raw_val is None or isinstance(raw_val, bool):
            raise ValueError(f"NASA-TLX subscale '{k}' is missing or boolean: {raw_val!r}")
        try:
            val = float(raw_val)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"NASA-TLX subscale '{k}' is not numeric: {raw_val!r}") from exc
        if not math.isfinite(val):
            raise ValueError(f"NASA-TLX subscale '{k}' is not finite: {raw_val!r}")
        if not (0.0 <= val <= 100.0):
            raise ValueError(
                f"NASA-TLX subscale '{k}'={val} is outside valid range [0.0, 100.0]."
            )
        cleaned[k] = val
    return cleaned


def calculate_tlx(subscales: Mapping[str, object]) -> float:
    """Calculate Raw NASA-TLX (unweighted mean of the 6 subscales in [0, 100])."""
    cleaned = validate_tlx_subscales(subscales)
    return sum(cleaned[k] for k in TLX_SUBSCALES) / len(TLX_SUBSCALES)


def load_tlx_from_csv(csv_file: str) -> Dict[str, object]:
    """Load NASA-TLX scores from a CSV file (6 raw subscales or pre-aggregated raw_tlx_score)."""
    header, rows = read_csv_rows(csv_file)
    if not rows:
        raise ValueError(f"CSV file contains zero data rows: {csv_file}")

    lower_map = {h.lower(): h for h in header}

    # Check if all 6 subscales are present (either plain or prefixed with tlx_)
    subscale_col_map: Optional[Dict[str, str]] = None
    if all(k in lower_map for k in TLX_SUBSCALES):
        subscale_col_map = {k: lower_map[k] for k in TLX_SUBSCALES}
    elif all(f"tlx_{k}" in lower_map for k in TLX_SUBSCALES):
        subscale_col_map = {k: lower_map[f"tlx_{k}"] for k in TLX_SUBSCALES}

    scores: List[float] = []
    limitation_note: Optional[str] = None

    if subscale_col_map is not None:
        source_mode = "raw_6_subscales"
        for line_no, row in enumerate(rows, start=2):
            sub_dict: Dict[str, float] = {}
            for k, col in subscale_col_map.items():
                val, prob = parse_optional_float(row.get(col))
                if prob is not None or val is None:
                    raise ValueError(f"Line {line_no}: invalid or missing NASA-TLX subscale '{col}'")
                sub_dict[k] = val
            scores.append(calculate_tlx(sub_dict))
    else:
        pre_col = next((lower_map[c] for c in PREAGGREGATED_TLX_COLUMNS if c in lower_map), None)
        if pre_col is None:
            raise ValueError(
                f"CSV '{os.path.basename(csv_file)}' does not contain NASA-TLX columns "
                f"(neither the 6 subscales {list(TLX_SUBSCALES)} nor {list(PREAGGREGATED_TLX_COLUMNS)}). "
                f"Refusing to derive workload scores from unrelated columns."
            )
        source_mode = f"preaggregated_column:{pre_col}"
        limitation_note = (
            f"NOTE: '{os.path.basename(csv_file)}' provides only pre-aggregated '{pre_col}' "
            f"(0-100) without the 6 individual NASA-TLX subscales (mental, physical, temporal, "
            f"performance, effort, frustration)."
        )
        for line_no, row in enumerate(rows, start=2):
            val, prob = parse_optional_float(row.get(pre_col))
            if prob is not None or val is None:
                raise ValueError(f"Line {line_no}: invalid or missing '{pre_col}'")
            if not (0.0 <= val <= 100.0):
                raise ValueError(f"Line {line_no}: '{pre_col}'={val} outside valid range [0, 100]")
            scores.append(val)

    return {
        "csv_file": os.path.abspath(csv_file),
        "source_mode": source_mode,
        "limitation_note": limitation_note,
        "summary": su.describe(scores),
        "scores": scores,
    }


def evaluate_tlx_comparison(csv_file: Optional[str] = None) -> Dict[str, object]:
    """Evaluate NASA-TLX from ``csv_file`` if provided, or run the synthetic reference comparison."""
    if csv_file is not None:
        loaded = load_tlx_from_csv(csv_file)
        s = loaded["summary"]
        print("=" * 65)
        print(f"NASA TASK LOAD INDEX (NASA-TLX) REPORT ({os.path.basename(csv_file)})")
        print("=" * 65)
        if loaded["limitation_note"]:
            print(loaded["limitation_note"])
            print("-" * 65)
        print(f"Sample Size (N):         {s['n']} participants")
        print(f"Mean Raw TLX Score:      {s['mean']:.2f} / 100.0 (SD = {s['sd']:.2f})")
        print(f"95% Confidence Interval: [{s['ci95_low']:.2f}, {s['ci95_high']:.2f}]")
        print("=" * 65)
        return loaded

    # Synthetic reference comparison (Baseline 2D vs Proposed Immersive VR Intervention)
    baseline = {
        "mental": 68.5,
        "physical": 45.0,
        "temporal": 62.0,
        "performance": 54.0,
        "effort": 71.0,
        "frustration": 58.0,
    }
    vr_intervention = {
        "mental": 42.0,
        "physical": 38.0,
        "temporal": 35.0,
        "performance": 22.0,
        "effort": 44.0,
        "frustration": 24.0,
    }

    tlx_base = calculate_tlx(baseline)
    tlx_vr = calculate_tlx(vr_intervention)
    reduction = ((tlx_base - tlx_vr) / tlx_base) * 100.0

    print("=" * 65)
    print("NASA TASK LOAD INDEX (NASA-TLX) WORKLOAD COMPARISON (SYNTHETIC REFERENCE)")
    print("=" * 65)
    print(f"Traditional Baseline Raw TLX:    {tlx_base:.2f} / 100 (High Cognitive Load)")
    print(f"VR Spatial Intervention Raw TLX: {tlx_vr:.2f} / 100 (Optimized Cognitive Flow)")
    print(f"Cognitive Workload Compression:  -{reduction:.1f}% reduction")
    print("=" * 65)
    return {
        "csv_file": None,
        "source_mode": "synthetic_reference_profiles",
        "baseline_tlx": tlx_base,
        "vr_tlx": tlx_vr,
        "relative_reduction_pct": reduction,
    }


if __name__ == "__main__":
    evaluate_tlx_comparison()
