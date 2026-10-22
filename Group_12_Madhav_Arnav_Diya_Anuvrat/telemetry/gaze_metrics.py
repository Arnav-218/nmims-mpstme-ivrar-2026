"""
Eye-gaze telemetry metrics: Threat Inspection Ratio (TIR) and gaze CSV checks.

This is the Python twin of ``Assets/Scripts/ThreatInspectionRatio.cs``. Both
implementations follow the SAME definition so numbers computed in Unity and
numbers recomputed in Python agree (see ``tests/test_gaze_metrics.py``).

Threat Inspection Ratio - canonical definition used by this project
-------------------------------------------------------------------
                    dwell on security-cue AOIs (seconds)
    TIR  =  ---------------------------------------------------
             dwell on ALL five email AOIs (seconds)

Email AOIs (denominator) - the five regions defined in EyeGazeAttentionTracker.cs:
    SenderDomainHeader, SecurityPadlockBadge, SubjectLineUrgency,
    BodyTextContent, HyperlinkCallToAction
    (IrrelevantBackground and invalid / missing gaze samples are EXCLUDED.)

Security-cue AOIs (numerator) depend on the selected definition:

* ``sender_domain_hyperlink`` (PRIMARY / default)
      SenderDomainHeader + HyperlinkCallToAction
      Matches RESEARCH_AND_IMPLEMENTATION_GUIDE.md section 1.1
      (T_Sender + T_Domain + T_Hyperlink; Sender and Domain share ONE AOI in
      the Unity scene) and the original I080 TODO in the C# tracker.
* ``all_security_cues`` (sensitivity variant)
      SenderDomainHeader + SecurityPadlockBadge + HyperlinkCallToAction
      Matches the README / abstract wording that padlock mismatches are also
      "diagnostic technical indicators".
* ``sender_domain_only`` (sensitivity variant)
      SenderDomainHeader
      Matches RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md section 3.4.

Properties
----------
* Unit-free ratio between 0 and 1.
* Zero total email dwell -> TIR is UNDEFINED (None), never 0. A participant
  who never looked at the email has not "inspected 0% of cues"; we simply
  have no information. Negative or missing dwell -> undefined as well.
* TIR measures where attention went. It does NOT prove that a participant
  understood a cue or made a safe decision.

Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)
"""
from __future__ import annotations

import math
from typing import Dict, Iterable, List, Mapping, Optional, Tuple

from csv_utils import parse_optional_float

AOI_COLUMNS: Dict[str, str] = {
    "sender_domain": "dwell_sender_domain_sec",
    "security_badge": "dwell_security_badge_sec",
    "subject_line": "dwell_subject_line_sec",
    "body_text": "dwell_body_text_sec",
    "hyperlink": "dwell_hyperlink_sec",
}

TIR_DEFINITIONS: Dict[str, Tuple[str, ...]] = {
    "sender_domain_hyperlink": ("sender_domain", "hyperlink"),
    "all_security_cues": ("sender_domain", "security_badge", "hyperlink"),
    "sender_domain_only": ("sender_domain",),
}
PRIMARY_TIR_DEFINITION = "sender_domain_hyperlink"

# Columns written by EyeGazeAttentionTracker.GetTelemetryCsvHeader() (schema v2).
GAZE_CSV_REQUIRED_COLUMNS: List[str] = [
    "schema_version", "participant_id", "training_condition", "day_interval", "trial_index",
    "scenario_id", "trial_duration_sec", "valid_gaze_sec", "invalid_gaze_sec",
] + list(AOI_COLUMNS.values())

# Assumption (needs Diya's confirmation): trials where more than 25% of the
# samples had no valid gaze are flagged as low quality.
DEFAULT_MAX_INVALID_GAZE_RATIO = 0.25


def compute_tir(dwell_seconds: Mapping[str, Optional[float]],
                definition: str = PRIMARY_TIR_DEFINITION) -> Optional[float]:
    """Compute the Threat Inspection Ratio from per-AOI dwell times in seconds.

    ``dwell_seconds`` maps AOI keys (see ``AOI_COLUMNS``) to seconds.
    Returns ``None`` when the ratio is undefined (missing / invalid input or
    zero total email dwell).
    """
    if definition not in TIR_DEFINITIONS:
        raise ValueError(f"Unknown TIR definition {definition!r}; choose one of {sorted(TIR_DEFINITIONS)}")
    values: Dict[str, float] = {}
    for aoi in AOI_COLUMNS:
        value = dwell_seconds.get(aoi)
        if value is None:
            return None
        value = float(value)
        if math.isnan(value) or math.isinf(value) or value < 0:
            return None
        values[aoi] = value
    total = sum(values.values())
    if total <= 0:
        return None
    numerator = sum(values[aoi] for aoi in TIR_DEFINITIONS[definition])
    return numerator / total


def security_cue_dwell(dwell_seconds: Mapping[str, float],
                       definition: str = PRIMARY_TIR_DEFINITION) -> float:
    """Total seconds spent on the security-cue AOIs of a definition."""
    return sum(float(dwell_seconds[aoi]) for aoi in TIR_DEFINITIONS[definition])


def validate_gaze_rows(header: List[str], rows: List[Dict[str, str]],
                       max_invalid_ratio: float = DEFAULT_MAX_INVALID_GAZE_RATIO) -> Tuple[List[Dict[str, object]], List[str]]:
    """Parse gaze telemetry rows exported by Unity and report problems.

    Returns ``(parsed_rows, problems)``. Each parsed row contains the AOI
    dwell times, the recomputed TIR for every definition and a quality flag.
    Rows with unusable dwell values are kept but their TIR is ``None``.
    """
    problems: List[str] = []
    missing = [c for c in GAZE_CSV_REQUIRED_COLUMNS if c not in header]
    if missing:
        raise ValueError(f"Gaze telemetry CSV is missing required columns: {missing}")
    parsed: List[Dict[str, object]] = []
    seen = set()
    for line_no, row in enumerate(rows, start=2):
        key = (row["participant_id"], row["day_interval"], row["trial_index"])
        if key in seen:
            problems.append(f"line {line_no}: duplicate trial {key}")
        seen.add(key)
        dwell: Dict[str, Optional[float]] = {}
        for aoi, column in AOI_COLUMNS.items():
            value, problem = parse_optional_float(row.get(column))
            if problem:
                problems.append(f"line {line_no}: {column} {problem}")
            elif value is not None and value < 0:
                problems.append(f"line {line_no}: {column} is negative ({value})")
                value = None
            dwell[aoi] = value
        duration, _ = parse_optional_float(row.get("trial_duration_sec"))
        invalid, _ = parse_optional_float(row.get("invalid_gaze_sec"))
        invalid_ratio = (invalid / duration) if (duration and invalid is not None and duration > 0) else None
        record: Dict[str, object] = {
            "participant_id": row["participant_id"],
            "training_condition": row["training_condition"],
            "day_interval": row["day_interval"],
            "trial_index": row["trial_index"],
            "scenario_id": row["scenario_id"],
            "invalid_gaze_ratio": invalid_ratio,
            "low_quality": invalid_ratio is not None and invalid_ratio > max_invalid_ratio,
        }
        record.update({f"dwell_{aoi}": value for aoi, value in dwell.items()})
        for name in TIR_DEFINITIONS:
            record[f"tir_{name}"] = compute_tir(dwell, name)
        if record[f"tir_{PRIMARY_TIR_DEFINITION}"] is None:
            problems.append(f"line {line_no}: TIR undefined (zero or invalid email dwell)")
        parsed.append(record)
    return parsed, problems


def summarise_tir_by_session(parsed_rows: Iterable[Dict[str, object]],
                             definition: str = PRIMARY_TIR_DEFINITION,
                             include_low_quality: bool = False) -> List[Dict[str, object]]:
    """Mean TIR per (participant, condition, day) over trials with a defined TIR.

    Averaging per participant BEFORE comparing groups keeps each person as one
    independent observation (trials from the same person are not independent).
    """
    buckets: Dict[Tuple[str, str, str], List[float]] = {}
    for row in parsed_rows:
        if row.get("low_quality") and not include_low_quality:
            continue
        value = row.get(f"tir_{definition}")
        if value is None:
            continue
        key = (str(row["participant_id"]), str(row["training_condition"]), str(row["day_interval"]))
        buckets.setdefault(key, []).append(float(value))
    return [{"participant_id": p, "training_condition": c, "day_interval": d,
             "n_trials_with_tir": len(v), "mean_tir": sum(v) / len(v)}
            for (p, c, d), v in sorted(buckets.items())]
