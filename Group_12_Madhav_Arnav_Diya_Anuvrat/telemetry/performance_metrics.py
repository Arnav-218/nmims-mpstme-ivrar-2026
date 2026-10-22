"""
Trial-level phishing performance metrics (accuracy, malicious click rate, ...).

Why this module exists
----------------------
The benchmark CSV (``phishing_simulation_benchmark.csv``) only contains
percentages that were *already* aggregated. It does not say how many emails
each person saw, so its denominators cannot be checked. When the Unity
simulation produces REAL trial-by-trial logs, this module turns them into
per-participant, per-session metrics using explicit, documented denominators.

Scoring rules (mirrors ``SubmitUserAction`` in PhishingThreatSimulationManager.cs)
---------------------------------------------------------------------------------
* Legitimate email: correct if the participant did NOT report it
  (clicking the link or ignoring the email are both "correct").
* Phishing email (DeceptivePhish or SpearPhishTargeted): correct ONLY if the
  participant reported it. Clicking is a malicious click. Ignoring is scored
  as incorrect by the current C# code (flagged for team confirmation).

Metric definitions (all in percent)
-----------------------------------
* accuracy                 = correct decisions / all trials
* malicious_click_rate     = clicks on phishing emails / phishing trials
* phishing_report_rate     = reports of phishing emails / phishing trials
  (often called "detection rate" or "hit rate")
* false_positive_rate      = reports of legitimate emails / legitimate trials
* phishing_ignore_rate     = phishing emails neither clicked nor reported / phishing trials

A rate whose denominator is zero is returned as ``None`` (undefined), never 0.

With Madhav's current 12-scenario deck (8 phishing, 4 legitimate) every
participant contributes 12 trials per session, so accuracy moves in steps of
8.33 percentage points and the click rate in steps of 12.5 points.

Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)
"""
from __future__ import annotations

from typing import Dict, Iterable, List, Optional, Tuple

PHISHING_CLASSES = {"deceptivephish", "spearphishtargeted", "phishing", "phish"}
LEGITIMATE_CLASSES = {"legitimate", "legit"}
VALID_ACTIONS = {"click", "report", "ignore"}


class TrialDataError(ValueError):
    """Raised when a trial record cannot be scored."""


def classify_scenario(classification: str) -> str:
    """Map a scenario classification label to ``"phishing"`` or ``"legitimate"``."""
    key = (classification or "").strip().lower().replace("_", "").replace(" ", "")
    if key in PHISHING_CLASSES:
        return "phishing"
    if key in LEGITIMATE_CLASSES:
        return "legitimate"
    raise TrialDataError(f"Unknown scenario classification: {classification!r}")


def normalise_action(action: str) -> str:
    key = (action or "").strip().lower()
    aliases = {"clicked": "click", "click_link": "click", "reported": "report",
               "report_phish": "report", "ignored": "ignore", "none": "ignore"}
    key = aliases.get(key, key)
    if key not in VALID_ACTIONS:
        raise TrialDataError(f"Unknown participant action: {action!r} (expected click/report/ignore)")
    return key


def score_trial(classification: str, action: str) -> bool:
    """Return True when the decision is correct under the project's scoring rules."""
    kind = classify_scenario(classification)
    act = normalise_action(action)
    if kind == "legitimate":
        return act != "report"
    return act == "report"


def _rate(numerator: int, denominator: int) -> Optional[float]:
    return None if denominator == 0 else 100.0 * numerator / denominator


def compute_session_metrics(trials: Iterable[Dict[str, str]]) -> Dict[str, object]:
    """Metrics for ONE participant in ONE session (e.g. Day 0).

    Each trial needs ``scenario_classification`` and ``participant_action``.
    """
    counts = {"trials": 0, "correct": 0, "phishing": 0, "legitimate": 0,
              "phish_click": 0, "phish_report": 0, "phish_ignore": 0, "legit_report": 0}
    for trial in trials:
        kind = classify_scenario(trial.get("scenario_classification", ""))
        act = normalise_action(trial.get("participant_action", ""))
        counts["trials"] += 1
        counts["correct"] += int(score_trial(kind, act))
        if kind == "phishing":
            counts["phishing"] += 1
            counts["phish_" + act] += 1
        else:
            counts["legitimate"] += 1
            counts["legit_report"] += int(act == "report")
    return {
        "n_trials": counts["trials"],
        "n_phishing_trials": counts["phishing"],
        "n_legitimate_trials": counts["legitimate"],
        "n_correct": counts["correct"],
        "n_malicious_clicks": counts["phish_click"],
        "accuracy_pct": _rate(counts["correct"], counts["trials"]),
        "malicious_click_rate_pct": _rate(counts["phish_click"], counts["phishing"]),
        "phishing_report_rate_pct": _rate(counts["phish_report"], counts["phishing"]),
        "phishing_ignore_rate_pct": _rate(counts["phish_ignore"], counts["phishing"]),
        "false_positive_rate_pct": _rate(counts["legit_report"], counts["legitimate"]),
    }


def compute_participant_metrics(trials: Iterable[Dict[str, str]]) -> List[Dict[str, object]]:
    """Group trial rows by (participant_id, training_condition, day_interval) and score each group.

    Duplicate (participant, day, scenario) rows raise an error instead of being
    double-counted, because a repeated scenario would distort the denominator.
    """
    groups: Dict[Tuple[str, str, str], List[Dict[str, str]]] = {}
    seen = set()
    for trial in trials:
        pid = (trial.get("participant_id") or "").strip()
        if not pid:
            raise TrialDataError("Trial row without participant_id")
        day = (trial.get("day_interval") or "").strip()
        scenario = (trial.get("scenario_id") or "").strip()
        key = (pid, day, scenario)
        if scenario and key in seen:
            raise TrialDataError(f"Duplicate trial for participant {pid}, day {day}, scenario {scenario}")
        seen.add(key)
        group_key = (pid, (trial.get("training_condition") or "").strip(), day)
        groups.setdefault(group_key, []).append(trial)
    output = []
    for (pid, condition, day), rows in sorted(groups.items()):
        metrics = compute_session_metrics(rows)
        metrics.update({"participant_id": pid, "training_condition": condition, "day_interval": day})
        output.append(metrics)
    return output
