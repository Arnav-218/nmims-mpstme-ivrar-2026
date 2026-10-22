"""
Kennedy Simulator Sickness Questionnaire (SSQ) Diagnostic Engine
================================================================
Conforming to Kennedy, Lane, Berbaum & Lilienthal (1993),
"Simulator Sickness Questionnaire: An Enhanced Method for Quantifying Simulator Sickness",
The International Journal of Aviation Psychology, 3(3), 203-220.

Canonical Scoring Rules (Kennedy et al., 1993, Table 4, p. 212)
---------------------------------------------------------------
16 standard symptoms are rated on a 4-point ordinal scale:
    0 = None, 1 = Slight, 2 = Moderate, 3 = Severe.

Each symptom loads onto one or more of three overlapping 7-item subscales:
1. Nausea (N) - 7 items:
   * ``general_discomfort``
   * ``increased_salivation``
   * ``sweating``
   * ``nausea``
   * ``difficulty_concentrating``
   * ``stomach_awareness``
   * ``burping``
2. Oculomotor (O) - 7 items:
   * ``general_discomfort``
   * ``fatigue``
   * ``headache``
   * ``eyestrain``
   * ``difficulty_focusing``
   * ``difficulty_concentrating``
   * ``blurred_vision``
3. Disorientation (D) - 7 items:
   * ``difficulty_focusing``
   * ``nausea``
   * ``fullness_of_head``
   * ``blurred_vision``
   * ``dizziness_eyes_open``
   * ``dizziness_eyes_closed``
   * ``vertigo``

Subscale & Total Severity Multipliers (Kennedy et al., 1993):
* ``Nausea``         = raw_N * 9.54
* ``Oculomotor``     = raw_O * 7.58   (Fixed bug: original starter scaffold erroneously used 3.74)
* ``Disorientation`` = raw_D * 13.92  (Fixed bug: original starter scaffold omitted fullness_of_head,
                                       blurred_vision, and nausea, and erroneously put stomach_awareness
                                       and burping into Disorientation)
* ``Total_Severity`` = (raw_N + raw_O + raw_D) * 3.74
"""
from __future__ import annotations

import math
import os
from typing import Dict, List, Mapping, Optional, Sequence, Tuple

from csv_utils import parse_optional_float, read_csv_rows
import stats_utils as su


WEIGHTS: Dict[str, float] = {
    "Nausea": 9.54,
    "Oculomotor": 7.58,
    "Disorientation": 13.92,
    "Total": 3.74,
}

NAUSEA_SYMPTOMS: Tuple[str, ...] = (
    "general_discomfort",
    "increased_salivation",
    "sweating",
    "nausea",
    "difficulty_concentrating",
    "stomach_awareness",
    "burping",
)

OCULOMOTOR_SYMPTOMS: Tuple[str, ...] = (
    "general_discomfort",
    "fatigue",
    "headache",
    "eyestrain",
    "difficulty_focusing",
    "difficulty_concentrating",
    "blurred_vision",
)

DISORIENTATION_SYMPTOMS: Tuple[str, ...] = (
    "difficulty_focusing",
    "nausea",
    "fullness_of_head",
    "blurred_vision",
    "dizziness_eyes_open",
    "dizziness_eyes_closed",
    "vertigo",
)

ALL_16_SYMPTOMS: Tuple[str, ...] = (
    "general_discomfort",
    "fatigue",
    "headache",
    "eyestrain",
    "difficulty_focusing",
    "increased_salivation",
    "sweating",
    "nausea",
    "difficulty_concentrating",
    "fullness_of_head",
    "blurred_vision",
    "dizziness_eyes_open",
    "dizziness_eyes_closed",
    "vertigo",
    "stomach_awareness",
    "burping",
)

PREAGGREGATED_SSQ_COLUMNS: Tuple[str, ...] = ("ssq_total_severity", "ssq_total", "ssq_score")


def validate_ssq_symptoms(
    symptoms: Mapping[str, object],
    strict_16_items: bool = True,
) -> Dict[str, float]:
    """Validate SSQ symptom ratings (each in [0, 3]).

    If ``strict_16_items=True`` (the default), all 16 Kennedy (1993) symptoms must be
    present. If ``strict_16_items=False``, ``fullness_of_head`` defaults to 0.0 only if
    the other 15 symptoms are present (for backward compatibility with legacy 15-item
    dicts).
    """
    if not isinstance(symptoms, Mapping):
        raise ValueError("SSQ symptoms must be provided as a dictionary/mapping.")

    required = list(ALL_16_SYMPTOMS)
    missing = [s for s in required if s not in symptoms]
    if missing:
        if not strict_16_items and missing == ["fullness_of_head"]:
            pass
        else:
            raise ValueError(
                f"Kennedy SSQ requires all 16 standard symptoms; missing: {missing}"
            )

    cleaned: Dict[str, float] = {}
    for s in ALL_16_SYMPTOMS:
        if s not in symptoms and not strict_16_items and s == "fullness_of_head":
            cleaned[s] = 0.0
            continue
        raw_val = symptoms[s]
        if raw_val is None or isinstance(raw_val, bool):
            raise ValueError(f"SSQ symptom '{s}' is missing or boolean: {raw_val!r}")
        try:
            val = float(raw_val)
        except (TypeError, ValueError) as exc:
            raise ValueError(f"SSQ symptom '{s}' is not numeric: {raw_val!r}") from exc
        if not math.isfinite(val):
            raise ValueError(f"SSQ symptom '{s}' is not finite: {raw_val!r}")
        if not (0.0 <= val <= 3.0):
            raise ValueError(
                f"SSQ symptom '{s}'={val} is outside valid rating range [0, 3] (0=None..3=Severe)."
            )
        cleaned[s] = val
    return cleaned


def calculate_ssq(
    symptoms: Mapping[str, object],
    strict_16_items: bool = True,
) -> Dict[str, float]:
    """Compute Kennedy et al. (1993) Nausea, Oculomotor, Disorientation, and Total Severity scores."""
    cleaned = validate_ssq_symptoms(symptoms, strict_16_items=strict_16_items)

    raw_n = sum(cleaned[k] for k in NAUSEA_SYMPTOMS)
    raw_o = sum(cleaned[k] for k in OCULOMOTOR_SYMPTOMS)
    raw_d = sum(cleaned[k] for k in DISORIENTATION_SYMPTOMS)
    raw_total = raw_n + raw_o + raw_d

    score_n = raw_n * WEIGHTS["Nausea"]
    score_o = raw_o * WEIGHTS["Oculomotor"]
    score_d = raw_d * WEIGHTS["Disorientation"]
    score_ts = raw_total * WEIGHTS["Total"]

    return {
        "Nausea": score_n,
        "Oculomotor": score_o,
        "Disorientation": score_d,
        "Total_Severity": score_ts,
        "raw_nausea": raw_n,
        "raw_oculomotor": raw_o,
        "raw_disorientation": raw_d,
    }


def load_ssq_from_csv(csv_file: str) -> Dict[str, object]:
    """Load SSQ data from a CSV file (either 16 raw symptom columns or pre-aggregated ssq_total_severity)."""
    header, rows = read_csv_rows(csv_file)
    if not rows:
        raise ValueError(f"CSV file contains zero data rows: {csv_file}")

    lower_map = {h.lower(): h for h in header}

    if all(s in lower_map for s in ALL_16_SYMPTOMS):
        source_mode = "raw_16_symptoms"
        limitation_note = None
        profiles: List[Dict[str, float]] = []
        for line_no, row in enumerate(rows, start=2):
            sym_dict: Dict[str, float] = {}
            for s in ALL_16_SYMPTOMS:
                col = lower_map[s]
                val, prob = parse_optional_float(row.get(col))
                if prob is not None or val is None:
                    raise ValueError(f"Line {line_no}: invalid or missing SSQ symptom '{col}'")
                sym_dict[s] = val
            profiles.append(calculate_ssq(sym_dict))
        total_scores = [p["Total_Severity"] for p in profiles]
    else:
        pre_col = next((lower_map[c] for c in PREAGGREGATED_SSQ_COLUMNS if c in lower_map), None)
        if pre_col is None:
            raise ValueError(
                f"CSV '{os.path.basename(csv_file)}' does not contain Kennedy SSQ columns "
                f"(neither the 16 symptom columns nor {list(PREAGGREGATED_SSQ_COLUMNS)})."
            )
        source_mode = f"preaggregated_column:{pre_col}"
        limitation_note = (
            f"NOTE: '{os.path.basename(csv_file)}' contains only pre-aggregated '{pre_col}' "
            f"without the 16 individual Kennedy (1993) symptom ratings. Subscale breakdowns "
            f"(Nausea, Oculomotor, Disorientation) cannot be reconstructed from a single total score."
        )
        profiles = []
        total_scores = []
        for line_no, row in enumerate(rows, start=2):
            val, prob = parse_optional_float(row.get(pre_col))
            if prob is not None or val is None:
                raise ValueError(f"Line {line_no}: invalid or missing '{pre_col}'")
            if not (0.0 <= val <= 300.0):
                raise ValueError(f"Line {line_no}: '{pre_col}'={val} outside valid range [0, 300]")
            total_scores.append(val)

    return {
        "csv_file": os.path.abspath(csv_file),
        "source_mode": source_mode,
        "limitation_note": limitation_note,
        "summary": su.describe(total_scores),
        "total_scores": total_scores,
        "profiles": profiles,
    }


def evaluate_ssq_safety(csv_file: Optional[str] = None) -> Dict[str, object]:
    """Evaluate Kennedy SSQ safety from ``csv_file`` or from the calibrated 16-item profile."""
    if csv_file is not None:
        loaded = load_ssq_from_csv(csv_file)
        s = loaded["summary"]
        print("=" * 65)
        print(f"KENNEDY SIMULATOR SICKNESS (SSQ) SAFETY AUDIT ({os.path.basename(csv_file)})")
        print("=" * 65)
        if loaded["limitation_note"]:
            print(loaded["limitation_note"])
            print("-" * 65)
        print(f"Sample Size (N):              {s['n']} participants")
        print(f"Mean Total Severity (TS):     {s['mean']:.2f} (SD = {s['sd']:.2f})")
        print(f"95% Confidence Interval:      [{s['ci95_low']:.2f}, {s['ci95_high']:.2f}]")
        safety_status = (
            "SAFE (Mean TS <= 15.0 - Motion-to-photon latency optimal)"
            if s["mean"] <= 15.0
            else "RISK (Mean TS > 15.0 - Frame drops detected)"
        )
        print(f"Cybersickness Ergonomic Gate: {safety_status}")
        print("=" * 65)
        return loaded

    # Calibrated post-immersion 16-symptom profile (synthetic diagnostic fixture)
    typical_profile = {
        "general_discomfort": 1,
        "fatigue": 0,
        "headache": 0,
        "eyestrain": 1,
        "difficulty_focusing": 0,
        "increased_salivation": 0,
        "sweating": 0,
        "nausea": 0,
        "difficulty_concentrating": 0,
        "fullness_of_head": 0,
        "blurred_vision": 0,
        "dizziness_eyes_open": 0,
        "dizziness_eyes_closed": 0,
        "vertigo": 0,
        "stomach_awareness": 0,
        "burping": 0,
    }
    res = calculate_ssq(typical_profile)
    print("=" * 65)
    print("KENNEDY SIMULATOR SICKNESS (SSQ) SAFETY AUDIT (SYNTHETIC PROFILE)")
    print("=" * 65)
    print(f"Nausea Subscale (N):          {res['Nausea']:.2f}")
    print(f"Oculomotor Subscale (O):      {res['Oculomotor']:.2f}")
    print(f"Disorientation Subscale (D):  {res['Disorientation']:.2f}")
    print(f"Total Severity Score (TS):    {res['Total_Severity']:.2f}")
    safety_status = (
        "SAFE (TS <= 15.0 - Motion-to-photon latency optimal)"
        if res["Total_Severity"] <= 15.0
        else "RISK (TS > 15.0 - Frame drops detected)"
    )
    print(f"Cybersickness Ergonomic Gate: {safety_status}")
    print("=" * 65)
    return res


if __name__ == "__main__":
    evaluate_ssq_safety()
