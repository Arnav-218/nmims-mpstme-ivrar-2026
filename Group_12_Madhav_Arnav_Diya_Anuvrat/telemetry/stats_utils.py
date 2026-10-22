"""
Pure-Python statistics used by the Group 12 retention analysis.

Why not just use SciPy?
-----------------------
SciPy is not installed on the lab machines used by the team, and a beginner
should be able to read how each p-value is produced. Every function here is
short, documented, and checked in ``tests/test_stats_utils.py`` against
textbook critical values. During development the results were additionally
cross-checked against SciPy 1.x (installed in a scratch folder only).

Conventions
-----------
* Standard deviations use the sample formula (divide by n - 1).
* All hypothesis tests are TWO-SIDED.
* "Change" always means ``after - before`` (Day 14 minus Day 0), so a negative
  change in accuracy means performance dropped.

Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)
"""
from __future__ import annotations

import math
from typing import Dict, List, Optional, Sequence

NAN = float("nan")


# ---------------------------------------------------------------------------
# 1. Descriptive statistics
# ---------------------------------------------------------------------------
def _clean(values: Sequence[Optional[float]]) -> List[float]:
    """Drop missing values (None / NaN). Analyses report how many were dropped."""
    return [float(v) for v in values if v is not None and not math.isnan(float(v))]


def mean(values: Sequence[float]) -> float:
    data = _clean(values)
    return sum(data) / len(data) if data else NAN


def sample_sd(values: Sequence[float]) -> float:
    """Sample standard deviation (n - 1 in the denominator)."""
    data = _clean(values)
    if len(data) < 2:
        return NAN
    m = sum(data) / len(data)
    return math.sqrt(sum((x - m) ** 2 for x in data) / (len(data) - 1))


def quantile(values: Sequence[float], q: float) -> float:
    """Quantile with linear interpolation (same rule as NumPy's default)."""
    data = sorted(_clean(values))
    if not data:
        return NAN
    if not 0.0 <= q <= 1.0:
        raise ValueError("q must be between 0 and 1")
    position = (len(data) - 1) * q
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return data[int(position)]
    return data[lower] + (data[upper] - data[lower]) * (position - lower)


def median(values: Sequence[float]) -> float:
    return quantile(values, 0.5)


def skewness(values: Sequence[float]) -> float:
    """Adjusted Fisher-Pearson sample skewness (0 = symmetric).

    Used only as a rough assumption check: |skew| above about 1 suggests the
    mean may be pulled by a long tail, so the rank-based tests become more
    important as a cross-check.
    """
    data = _clean(values)
    n = len(data)
    if n < 3:
        return NAN
    m = sum(data) / n
    m2 = sum((x - m) ** 2 for x in data) / n
    m3 = sum((x - m) ** 3 for x in data) / n
    if m2 == 0:
        return NAN
    g1 = m3 / m2 ** 1.5
    return g1 * math.sqrt(n * (n - 1)) / (n - 2)


def mean_confidence_interval(values: Sequence[float], confidence: float = 0.95) -> Dict[str, float]:
    """t-based confidence interval for a mean."""
    data = _clean(values)
    n = len(data)
    if n < 2:
        return {"low": NAN, "high": NAN}
    m = mean(data)
    se = sample_sd(data) / math.sqrt(n)
    t_crit = t_ppf(1.0 - (1.0 - confidence) / 2.0, n - 1)
    return {"low": m - t_crit * se, "high": m + t_crit * se}


def describe(values: Sequence[Optional[float]]) -> Dict[str, float]:
    """Summary statistics for one group at one time point."""
    data = _clean(values)
    ci = mean_confidence_interval(data)
    return {
        "n": len(data),
        "n_missing": len(values) - len(data),
        "mean": mean(data),
        "sd": sample_sd(data),
        "median": median(data),
        "q1": quantile(data, 0.25),
        "q3": quantile(data, 0.75),
        "min": min(data) if data else NAN,
        "max": max(data) if data else NAN,
        "ci95_low": ci["low"],
        "ci95_high": ci["high"],
        "skewness": skewness(data),
    }


# ---------------------------------------------------------------------------
# 2. Probability distributions (Student t and standard normal)
# ---------------------------------------------------------------------------
def _beta_continued_fraction(a: float, b: float, x: float) -> float:
    """Continued fraction for the incomplete beta function (modified Lentz)."""
    max_iterations, epsilon, tiny = 500, 3e-16, 1e-300
    qab, qap, qam = a + b, a + 1.0, a - 1.0
    c, d = 1.0, 1.0 - qab * x / qap
    d = 1.0 / (d if abs(d) > tiny else tiny)
    h = d
    for m in range(1, max_iterations + 1):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1.0 + aa * d
        d = 1.0 / (d if abs(d) > tiny else tiny)
        c = 1.0 + aa / c
        c = c if abs(c) > tiny else tiny
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1.0 + aa * d
        d = 1.0 / (d if abs(d) > tiny else tiny)
        c = 1.0 + aa / c
        c = c if abs(c) > tiny else tiny
        delta = d * c
        h *= delta
        if abs(delta - 1.0) < epsilon:
            break
    return h


def regularized_incomplete_beta(a: float, b: float, x: float) -> float:
    """I_x(a, b), the building block of the t distribution."""
    if x <= 0.0:
        return 0.0
    if x >= 1.0:
        return 1.0
    log_front = (math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
                 + a * math.log(x) + b * math.log1p(-x))
    front = math.exp(log_front)
    if x < (a + 1.0) / (a + b + 2.0):
        return front * _beta_continued_fraction(a, b, x) / a
    return 1.0 - front * _beta_continued_fraction(b, a, 1.0 - x) / b


def t_two_sided_p(t_stat: float, df: float) -> float:
    """Two-sided p-value P(|T| >= |t|) for Student's t with ``df`` degrees of freedom."""
    if math.isnan(t_stat) or df <= 0:
        return NAN
    if math.isinf(t_stat):
        return 0.0
    x = df / (df + t_stat * t_stat)
    return min(1.0, max(0.0, regularized_incomplete_beta(df / 2.0, 0.5, x)))


def t_cdf(t_stat: float, df: float) -> float:
    """Cumulative probability P(T <= t)."""
    tail = 0.5 * t_two_sided_p(t_stat, df)
    return 1.0 - tail if t_stat >= 0 else tail


def t_ppf(probability: float, df: float) -> float:
    """Inverse of :func:`t_cdf` (critical value), found by bisection."""
    if not 0.0 < probability < 1.0:
        raise ValueError("probability must be strictly between 0 and 1")
    if probability == 0.5:
        return 0.0
    if probability < 0.5:
        return -t_ppf(1.0 - probability, df)
    low, high = 0.0, 1.0
    while t_cdf(high, df) < probability:
        high *= 2.0
        if high > 1e8:
            break
    for _ in range(200):
        middle = (low + high) / 2.0
        if t_cdf(middle, df) < probability:
            low = middle
        else:
            high = middle
    return (low + high) / 2.0


def normal_two_sided_p(z: float) -> float:
    return math.erfc(abs(z) / math.sqrt(2.0))


# ---------------------------------------------------------------------------
# 3. Parametric tests and effect sizes
# ---------------------------------------------------------------------------
def welch_t_test(group_a: Sequence[float], group_b: Sequence[float],
                 confidence: float = 0.95) -> Dict[str, object]:
    """Welch's two-sample t-test for INDEPENDENT groups (A minus B).

    Chosen over Student's t-test because it does not assume equal variances;
    with equal variances the two give almost identical answers, so Welch is
    the safer default (and the 2D group in this dataset is more spread out).
    """
    a, b = _clean(group_a), _clean(group_b)
    n1, n2 = len(a), len(b)
    result: Dict[str, object] = {"test": "Welch two-sample t-test", "n1": n1, "n2": n2}
    if n1 < 2 or n2 < 2:
        result.update(statistic=NAN, df=NAN, p_value=NAN, mean_difference=NAN,
                      ci_low=NAN, ci_high=NAN, note="need at least 2 values per group")
        return result
    m1, m2 = mean(a), mean(b)
    v1, v2 = sample_sd(a) ** 2, sample_sd(b) ** 2
    se_squared = v1 / n1 + v2 / n2
    diff = m1 - m2
    if se_squared == 0:
        result.update(statistic=NAN, df=NAN, p_value=NAN, mean_difference=diff,
                      ci_low=NAN, ci_high=NAN, note="both groups have zero variance")
        return result
    se = math.sqrt(se_squared)
    df = se_squared ** 2 / ((v1 / n1) ** 2 / (n1 - 1) + (v2 / n2) ** 2 / (n2 - 1))
    t_stat = diff / se
    t_crit = t_ppf(1.0 - (1.0 - confidence) / 2.0, df)
    result.update(statistic=t_stat, df=df, p_value=t_two_sided_p(t_stat, df),
                  mean_difference=diff, ci_low=diff - t_crit * se, ci_high=diff + t_crit * se,
                  note="")
    return result


def paired_t_test(before: Sequence[Optional[float]], after: Sequence[Optional[float]],
                  confidence: float = 0.95) -> Dict[str, object]:
    """Paired t-test on ``after - before`` for REPEATED measurements of the same people.

    Pairs where either value is missing are dropped (and counted), because a
    person's Day 14 score can only be compared with that same person's Day 0.
    """
    if len(before) != len(after):
        raise ValueError("before and after must have the same length (one entry per participant)")
    pairs = [(float(b), float(a)) for b, a in zip(before, after)
             if b is not None and a is not None and not math.isnan(float(b)) and not math.isnan(float(a))]
    diffs = [a - b for b, a in pairs]
    n = len(diffs)
    result: Dict[str, object] = {"test": "Paired t-test", "n1": n, "n2": n,
                                 "n_pairs_dropped": len(before) - n}
    if n < 2:
        result.update(statistic=NAN, df=NAN, p_value=NAN, mean_difference=NAN,
                      ci_low=NAN, ci_high=NAN, note="need at least 2 complete pairs")
        return result
    mean_diff, sd_diff = mean(diffs), sample_sd(diffs)
    if sd_diff == 0:
        result.update(statistic=NAN, df=n - 1, p_value=NAN, mean_difference=mean_diff,
                      ci_low=mean_diff, ci_high=mean_diff,
                      note="every participant changed by exactly the same amount")
        return result
    se = sd_diff / math.sqrt(n)
    t_stat = mean_diff / se
    t_crit = t_ppf(1.0 - (1.0 - confidence) / 2.0, n - 1)
    result.update(statistic=t_stat, df=n - 1, p_value=t_two_sided_p(t_stat, n - 1),
                  mean_difference=mean_diff, ci_low=mean_diff - t_crit * se,
                  ci_high=mean_diff + t_crit * se, note="")
    return result


def hedges_g(group_a: Sequence[float], group_b: Sequence[float]) -> Dict[str, float]:
    """Standardised mean difference for independent groups (A minus B).

    * Cohen's d  = (mean_A - mean_B) / pooled SD
    * Hedges' g  = d x J, where J = 1 - 3 / (4(n1 + n2) - 9) removes the small
      upward bias of d in small samples.
    The 95% CI uses the common large-sample approximation for the standard
    error of g, so it is labelled "approximate" in reports.
    """
    a, b = _clean(group_a), _clean(group_b)
    n1, n2 = len(a), len(b)
    if n1 < 2 or n2 < 2:
        return {"cohens_d": NAN, "hedges_g": NAN, "ci_low": NAN, "ci_high": NAN}
    v1, v2 = sample_sd(a) ** 2, sample_sd(b) ** 2
    pooled_sd = math.sqrt(((n1 - 1) * v1 + (n2 - 1) * v2) / (n1 + n2 - 2))
    if pooled_sd == 0:
        return {"cohens_d": NAN, "hedges_g": NAN, "ci_low": NAN, "ci_high": NAN}
    d = (mean(a) - mean(b)) / pooled_sd
    correction = 1.0 - 3.0 / (4.0 * (n1 + n2) - 9.0)
    g = d * correction
    se_g = math.sqrt((n1 + n2) / (n1 * n2) + g * g / (2.0 * (n1 + n2)))
    return {"cohens_d": d, "hedges_g": g, "ci_low": g - 1.96 * se_g, "ci_high": g + 1.96 * se_g}


def cohens_dz(before: Sequence[Optional[float]], after: Sequence[Optional[float]]) -> Dict[str, float]:
    """Effect size for paired data: mean(change) / SD(change).

    Note: d_z describes how CONSISTENT the change is across people, not how big
    it is relative to the spread of scores. It is therefore usually larger than
    the between-group d and the two should not be compared directly.
    """
    diffs = [float(a) - float(b) for b, a in zip(before, after)
             if b is not None and a is not None and not math.isnan(float(b)) and not math.isnan(float(a))]
    n = len(diffs)
    if n < 2 or sample_sd(diffs) == 0:
        return {"cohens_dz": NAN, "ci_low": NAN, "ci_high": NAN}
    dz = mean(diffs) / sample_sd(diffs)
    se = math.sqrt(1.0 / n + dz * dz / (2.0 * n))
    return {"cohens_dz": dz, "ci_low": dz - 1.96 * se, "ci_high": dz + 1.96 * se}


# ---------------------------------------------------------------------------
# 4. Rank-based (non-parametric) robustness checks
# ---------------------------------------------------------------------------
def rank_with_ties(values: Sequence[float]) -> List[float]:
    """Ranks starting at 1; tied values share the average of their ranks."""
    order = sorted(range(len(values)), key=lambda i: values[i])
    ranks = [0.0] * len(values)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and values[order[j + 1]] == values[order[i]]:
            j += 1
        average_rank = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            ranks[order[k]] = average_rank
        i = j + 1
    return ranks


def _tie_term(values: Sequence[float]) -> float:
    counts: Dict[float, int] = {}
    for value in values:
        counts[value] = counts.get(value, 0) + 1
    return float(sum(t ** 3 - t for t in counts.values()))


def mann_whitney_u(group_a: Sequence[float], group_b: Sequence[float]) -> Dict[str, object]:
    """Mann-Whitney U test (independent groups), normal approximation with
    tie and continuity corrections. Effect size: rank-biserial correlation
    r = P(A > B) - P(A < B), ranging from -1 to +1."""
    a, b = _clean(group_a), _clean(group_b)
    n1, n2 = len(a), len(b)
    result: Dict[str, object] = {"test": "Mann-Whitney U (normal approx.)", "n1": n1, "n2": n2}
    if n1 < 1 or n2 < 1:
        result.update(statistic=NAN, z=NAN, p_value=NAN, rank_biserial=NAN)
        return result
    combined = a + b
    ranks = rank_with_ties(combined)
    rank_sum_a = sum(ranks[:n1])
    u_a = rank_sum_a - n1 * (n1 + 1) / 2.0
    n = n1 + n2
    mu = n1 * n2 / 2.0
    variance = n1 * n2 / 12.0 * ((n + 1) - _tie_term(combined) / (n * (n - 1))) if n > 1 else 0.0
    if variance <= 0:
        result.update(statistic=u_a, z=NAN, p_value=NAN, rank_biserial=NAN)
        return result
    shift = u_a - mu
    z = (shift - 0.5 * math.copysign(1.0, shift)) / math.sqrt(variance) if shift != 0 else 0.0
    result.update(statistic=u_a, z=z, p_value=normal_two_sided_p(z),
                  rank_biserial=2.0 * u_a / (n1 * n2) - 1.0)
    return result


def wilcoxon_signed_rank(before: Sequence[Optional[float]], after: Sequence[Optional[float]]) -> Dict[str, object]:
    """Wilcoxon signed-rank test on ``after - before`` (paired data).

    Zero differences are dropped (Wilcoxon's original convention); normal
    approximation with tie and continuity corrections. Effect size: matched-
    pairs rank-biserial correlation r = (W+ - W-) / (W+ + W-).
    """
    diffs = [float(a) - float(b) for b, a in zip(before, after)
             if b is not None and a is not None and not math.isnan(float(b)) and not math.isnan(float(a))]
    nonzero = [d for d in diffs if d != 0]
    n = len(nonzero)
    result: Dict[str, object] = {"test": "Wilcoxon signed-rank (normal approx.)", "n1": len(diffs),
                                 "n2": len(diffs), "n_zero_differences": len(diffs) - n}
    if n < 1:
        result.update(statistic=NAN, z=NAN, p_value=NAN, rank_biserial=NAN)
        return result
    absolute = [abs(d) for d in nonzero]
    ranks = rank_with_ties(absolute)
    w_plus = sum(r for r, d in zip(ranks, nonzero) if d > 0)
    w_minus = sum(r for r, d in zip(ranks, nonzero) if d < 0)
    mu = n * (n + 1) / 4.0
    variance = n * (n + 1) * (2 * n + 1) / 24.0 - _tie_term(absolute) / 48.0
    if variance <= 0:
        result.update(statistic=w_plus, z=NAN, p_value=NAN, rank_biserial=NAN)
        return result
    shift = w_plus - mu
    z = (shift - 0.5 * math.copysign(1.0, shift)) / math.sqrt(variance) if shift != 0 else 0.0
    result.update(statistic=w_plus, z=z, p_value=normal_two_sided_p(z),
                  rank_biserial=(w_plus - w_minus) / (w_plus + w_minus))
    return result


# ---------------------------------------------------------------------------
# 5. Multiple comparisons
# ---------------------------------------------------------------------------
def holm_adjust(p_values: Sequence[float]) -> List[float]:
    """Holm-Bonferroni adjusted p-values (same order as the input).

    Running many tests raises the chance that at least one is "significant"
    by luck. Holm's step-down method controls that family-wise error rate and
    is never less powerful than plain Bonferroni. Missing p-values stay NaN.
    """
    indexed = [(p, i) for i, p in enumerate(p_values) if p is not None and not math.isnan(p)]
    m = len(indexed)
    adjusted = [NAN] * len(p_values)
    running_max = 0.0
    for rank, (p, original_index) in enumerate(sorted(indexed)):
        candidate = min(1.0, (m - rank) * p)
        running_max = max(running_max, candidate)
        adjusted[original_index] = running_max
    return adjusted


def format_p(p_value: float) -> str:
    """Human-readable p-value (APA style: never print p = 0.000)."""
    if p_value is None or (isinstance(p_value, float) and math.isnan(p_value)):
        return "n/a"
    if p_value < 0.001:
        return "< 0.001"
    return f"{p_value:.3f}"
