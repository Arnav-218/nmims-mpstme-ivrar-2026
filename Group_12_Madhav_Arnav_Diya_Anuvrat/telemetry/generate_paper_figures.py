"""
Non-Destructive Empirical Figure Pipeline (300 DPI Publication Figures)
=======================================================================
Group 12: Immersive VR Phishing Simulation vs 2D Web-Based Training
Course: IVRAR (Immersive Virtual, Real & Augmented Reality - 702COI002)
Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)

Outputs:
1. docs/figures/figure1_system_architecture.png (300 DPI)
2. docs/figures/figure2_kinematic_telemetry.png (300 DPI)
3. docs/figures/figure3_comparative_performance.png (300 DPI)

Key Safety & Scientific Guarantees:
- NEVER overwrites or modifies telemetry/phishing_simulation_benchmark.csv during
  normal execution.
- Loads and validates the existing benchmark CSV via human_factors_retention_analysis.py.
- Computes all means, 95% confidence intervals, p-values, and Cohen's d effect sizes
  directly from the loaded dataset.
- Explicitly labels every figure with data provenance (Synthetic Benchmark Dataset,
  N=50) and separates observed CSV quantities from theoretical reference curves.
- Zero currency symbols and zero emojis.
"""
from __future__ import annotations

import argparse
import os
from typing import Dict, List, Optional, Sequence

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np

from csv_utils import file_sha256
from human_factors_retention_analysis import (
    DEFAULT_BENCHMARK_CSV,
    VR_MODALITY,
    WEB_MODALITY,
    evaluate_exponential_forgetting_curve,
    generate_synthetic_benchmark_dataset,
    run_full_retention_analysis,
)
import stats_utils as su


BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS_DIR = os.path.join(BASE_DIR, "docs")
FIGURES_DIR = os.path.join(DOCS_DIR, "figures")
TELEMETRY_DIR = os.path.join(BASE_DIR, "telemetry")


def render_figure1_architecture(output_path: str) -> str:
    """Render Figure 1: Multi-Tier System Architecture (300 DPI)."""
    fig, ax = plt.subplots(figsize=(10.5, 6.2), dpi=300)
    ax.set_xlim(0, 10)
    ax.set_ylim(0, 6.2)
    ax.axis("off")

    # Layer 1: Threat Scenario Engine
    rect1 = plt.Rectangle(
        (0.5, 3.8), 2.7, 1.9, facecolor="#E3F2FD", edgecolor="#1565C0", linewidth=2, zorder=2
    )
    ax.add_patch(rect1)
    ax.text(
        1.85, 5.3, "Threat Scenario Engine", ha="center", va="center",
        fontsize=10.5, fontweight="bold", color="#0D47A1"
    )
    ax.text(
        1.85, 4.55,
        "D021 Madhav Gaonkar\n* 12 Scenario Deck (8 Phish / 4 Legit)\n* Authority, Urgency, Scarcity, Social\n* Domain Homograph Spoof Cues",
        ha="center", va="center", fontsize=8.3, color="#1565C0"
    )

    # Layer 2: VR Office & UI Core
    rect2 = plt.Rectangle(
        (3.65, 3.8), 2.7, 1.9, facecolor="#E8F5E9", edgecolor="#2E7D32", linewidth=2, zorder=2
    )
    ax.add_patch(rect2)
    ax.text(
        5.0, 5.3, "Immersive VR Office Core", ha="center", va="center",
        fontsize=10.5, fontweight="bold", color="#1B5E20"
    )
    ax.text(
        5.0, 4.55,
        "D030 Arnav Jain\n* 3D Floating Workstation\n* Interactive Spatial Email Client\n* Physical Report Alarm Lever",
        ha="center", va="center", fontsize=8.3, color="#2E7D32"
    )

    # Layer 3: Eye-Gaze Tracking Engine
    rect3 = plt.Rectangle(
        (6.8, 3.8), 2.7, 1.9, facecolor="#FFF3E0", edgecolor="#E65100", linewidth=2, zorder=2
    )
    ax.add_patch(rect3)
    ax.text(
        8.15, 5.3, "Eye-Gaze & Attention Engine", ha="center", va="center",
        fontsize=10.5, fontweight="bold", color="#BF360C"
    )
    ax.text(
        8.15, 4.55,
        "D065 Diya Shah\n* 5 Email AOI Raycast Colliders\n* 150 ms Fixation Thresholding\n* Active Gaze Interval Flushing",
        ha="center", va="center", fontsize=8.3, color="#E65100"
    )

    # Layer 4: Analytics & Retention Modeling (I080)
    rect4 = plt.Rectangle(
        (1.6, 0.55), 6.8, 2.35, facecolor="#F3E5F5", edgecolor="#6A1B9A", linewidth=2, zorder=2
    )
    ax.add_patch(rect4)
    ax.text(
        5.0, 2.5, "Human Factors, Gaze Telemetry & 14-Day Retention Analytics",
        ha="center", va="center", fontsize=11.5, fontweight="bold", color="#4A148C"
    )
    ax.text(
        5.0, 1.45,
        "I080 Anuvrat Tripathi\n"
        "* Threat Inspection Ratio (TIR) & Gaze CSV Schema Export (EyeGazeAttentionTracker.cs)\n"
        "* Paired Day 0 vs Day 14 Retention & Between-Cohort Welch / Non-Parametric Statistical Engine\n"
        "* Psychometric Validation (SUS, NASA-TLX, Kennedy SSQ) & Dimensionless Technoeconomic Model (kappa = 0.24)",
        ha="center", va="center", fontsize=8.7, color="#6A1B9A"
    )

    # Interconnecting arrows
    arrow_kw = dict(arrowstyle="->", lw=2, color="#37474F")
    ax.annotate("", xy=(3.65, 4.75), xytext=(3.2, 4.75), arrowprops=arrow_kw)
    ax.annotate("", xy=(6.8, 4.75), xytext=(6.35, 4.75), arrowprops=arrow_kw)
    ax.annotate("", xy=(3.2, 2.9), xytext=(1.85, 3.8), arrowprops=arrow_kw)
    ax.annotate("", xy=(5.0, 2.9), xytext=(5.0, 3.8), arrowprops=arrow_kw)
    ax.annotate("", xy=(6.8, 2.9), xytext=(8.15, 3.8), arrowprops=arrow_kw)

    ax.set_title(
        "Figure 1: Multi-Tier Architecture of Immersive VR Phishing Simulation Platform (IVRAR Group 12)",
        fontsize=12.5, fontweight="bold", pad=12
    )
    fig.tight_layout()
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    fig.savefig(output_path, dpi=300, bbox_inches="tight")
    plt.close(fig)
    return output_path


def render_figure2_telemetry_and_retention(
    analysis: Dict[str, object],
    output_path: str,
) -> str:
    """Render Figure 2: Diagnostic AOI Dwell Time & 14-Day Retention Trajectory (300 DPI)
    using actual statistics computed from phishing_simulation_benchmark.csv.
    """
    prov = analysis["provenance"]
    dwell_stats = analysis["metrics"]["security_aoi_dwell_sec"]
    acc_stats = analysis["metrics"]["detection_accuracy_pct"]

    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12.8, 5.4), dpi=300)

    # ------------------------------------------------------------------
    # Subplot A: Diagnostic Security AOI Dwell Time (Day 0 vs Day 14)
    # ------------------------------------------------------------------
    x_pos = np.array([0, 1])
    width = 0.34

    web_d0_mean = dwell_stats["web"]["day0"]["mean"]
    web_d14_mean = dwell_stats["web"]["day14"]["mean"]
    web_d0_ci = (dwell_stats["web"]["day0"]["ci95_high"] - dwell_stats["web"]["day0"]["ci95_low"]) / 2.0
    web_d14_ci = (dwell_stats["web"]["day14"]["ci95_high"] - dwell_stats["web"]["day14"]["ci95_low"]) / 2.0

    vr_d0_mean = dwell_stats["vr"]["day0"]["mean"]
    vr_d14_mean = dwell_stats["vr"]["day14"]["mean"]
    vr_d0_ci = (dwell_stats["vr"]["day0"]["ci95_high"] - dwell_stats["vr"]["day0"]["ci95_low"]) / 2.0
    vr_d14_ci = (dwell_stats["vr"]["day14"]["ci95_high"] - dwell_stats["vr"]["day14"]["ci95_low"]) / 2.0

    bars_web = ax1.bar(
        x_pos - width / 2,
        [web_d0_mean, web_d14_mean],
        width,
        yerr=[web_d0_ci, web_d14_ci],
        capsize=5,
        label=f"2D Web Training (n={dwell_stats['web']['day0']['n']})",
        color="#90CAF9",
        edgecolor="#1565C0",
        linewidth=1.5,
    )
    bars_vr = ax1.bar(
        x_pos + width / 2,
        [vr_d0_mean, vr_d14_mean],
        width,
        yerr=[vr_d0_ci, vr_d14_ci],
        capsize=5,
        label=f"Immersive VR Training (n={dwell_stats['vr']['day0']['n']})",
        color="#A5D6A7",
        edgecolor="#2E7D32",
        linewidth=1.5,
    )

    for bar_group in (bars_web, bars_vr):
        for bar in bar_group:
            height = bar.get_height()
            ax1.annotate(
                f"{height:.2f} s",
                xy=(bar.get_x() + bar.get_width() / 2, height),
                xytext=(0, 8),
                textcoords="offset points",
                ha="center",
                va="bottom",
                fontsize=8.5,
                fontweight="bold",
            )

    d14_dwell_d = dwell_stats["between_day14"]["effect_size"]["cohens_d"]
    d14_dwell_p = su.format_p(dwell_stats["between_day14"]["welch_t"]["p_value"])
    ax1.set_ylabel("Mean Diagnostic AOI Dwell Time (s, +/- 95% CI)", fontsize=9.5, fontweight="bold")
    ax1.set_title(
        f"(A) Diagnostic Security Cue Dwell Time\n(Day 14 Welch p {d14_dwell_p}, Cohen's d = {d14_dwell_d:.2f})",
        fontsize=10.5,
        fontweight="bold",
    )
    ax1.set_xticks(x_pos)
    ax1.set_xticklabels(["Day 0 (Immediate Post-Test)", "Day 14 (Delayed Retention)"], fontsize=9.5)
    ax1.set_ylim(0, max(vr_d0_mean + vr_d0_ci, 3.0) * 1.15)
    ax1.grid(axis="y", linestyle="--", alpha=0.6)
    ax1.legend(frameon=True, loc="upper right", fontsize=8.5)

    # ------------------------------------------------------------------
    # Subplot B: 14-Day Threat Detection Retention Trajectory
    # ------------------------------------------------------------------
    # Plot empirical/benchmark sample means (+/- 95% CI) at measured timepoints (Day 0 & Day 14)
    # alongside the theoretical exponential forgetting curve from Guide Eq. 1.2.
    measured_days = np.array([0.0, 14.0])
    vr_acc_means = np.array([acc_stats["vr"]["day0"]["mean"], acc_stats["vr"]["day14"]["mean"]])
    vr_acc_cis = np.array([
        (acc_stats["vr"]["day0"]["ci95_high"] - acc_stats["vr"]["day0"]["ci95_low"]) / 2.0,
        (acc_stats["vr"]["day14"]["ci95_high"] - acc_stats["vr"]["day14"]["ci95_low"]) / 2.0,
    ])
    web_acc_means = np.array([acc_stats["web"]["day0"]["mean"], acc_stats["web"]["day14"]["mean"]])
    web_acc_cis = np.array([
        (acc_stats["web"]["day0"]["ci95_high"] - acc_stats["web"]["day0"]["ci95_low"]) / 2.0,
        (acc_stats["web"]["day14"]["ci95_high"] - acc_stats["web"]["day14"]["ci95_low"]) / 2.0,
    ])

    # Smooth reference trajectory between Day 0 and Day 14 anchored to observed sample means
    curve_days = np.linspace(0.0, 14.0, 50)
    # Fit asymptotic floor consistent with Guide Eq. 1.2 decay coefficients (lambda_VR=0.12, lambda_Web=0.24)
    vr_e14 = np.exp(-0.12 * 14.0)
    web_e14 = np.exp(-0.24 * 14.0)
    vr_a_inf = (vr_acc_means[1] - vr_acc_means[0] * vr_e14) / (1.0 - vr_e14)
    web_a_inf = (web_acc_means[1] - web_acc_means[0] * web_e14) / (1.0 - web_e14)

    vr_curve = evaluate_exponential_forgetting_curve(curve_days, vr_acc_means[0], vr_a_inf, 0.12)
    web_curve = evaluate_exponential_forgetting_curve(curve_days, web_acc_means[0], web_a_inf, 0.24)

    ax2.plot(
        curve_days, vr_curve, "-", color="#2E7D32", linewidth=2.0, alpha=0.75,
        label="VR Exponential Model (lambda=0.12/day)"
    )
    ax2.plot(
        curve_days, web_curve, "--", color="#C62828", linewidth=2.0, alpha=0.75,
        label="2D Web Exponential Model (lambda=0.24/day)"
    )
    ax2.errorbar(
        measured_days, vr_acc_means, yerr=vr_acc_cis, fmt="o", color="#1B5E20",
        markersize=8, capsize=5, linewidth=2.2,
        label=f"VR Observed Mean +/- 95% CI (Delta={acc_stats['vr']['retention_summary']['raw_change']:+.2f} pp)"
    )
    ax2.errorbar(
        measured_days, web_acc_means, yerr=web_acc_cis, fmt="s", color="#B71C1C",
        markersize=8, capsize=5, linewidth=2.2,
        label=f"2D Web Observed Mean +/- 95% CI (Delta={acc_stats['web']['retention_summary']['raw_change']:+.2f} pp)"
    )
    ax2.fill_between(
        curve_days, vr_curve, web_curve, color="#E8F5E9", alpha=0.45,
        label=f"Day 14 Retention Gap (+{acc_stats['between_day14']['welch_t']['mean_difference']:.2f} pp)"
    )

    did_p = su.format_p(acc_stats["difference_in_differences"]["welch_t"]["p_value"])
    did_d = acc_stats["difference_in_differences"]["effect_size"]["cohens_d"]
    ax2.set_xlabel("Retention Interval (Days Post-Training, Measured at Day 0 & Day 14)", fontsize=9.5, fontweight="bold")
    ax2.set_ylabel("Threat Detection Accuracy (%)", fontsize=9.5, fontweight="bold")
    ax2.set_title(
        f"(B) 14-Day Threat Recognition Retention Trajectory\n(DiD Welch p {did_p}, Cohen's d = {did_d:.2f})",
        fontsize=10.5,
        fontweight="bold",
    )
    ax2.set_xticks([0, 3, 7, 10, 14])
    ax2.set_ylim(45, 100)
    ax2.grid(True, linestyle="--", alpha=0.6)
    ax2.legend(frameon=True, loc="lower left", fontsize=8.0)

    fig.suptitle(
        f"Figure 2: Diagnostic AOI Fixation Dwell & 14-Day Knowledge Retention Dynamics\n"
        f"[Data Source: {prov['label']}]",
        fontsize=12.0,
        fontweight="bold",
        y=1.03,
    )
    fig.tight_layout()
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    fig.savefig(output_path, dpi=300, bbox_inches="tight")
    plt.close(fig)
    return output_path


def render_figure3_comparative_performance(
    analysis: Dict[str, object],
    output_path: str,
) -> str:
    """Render Figure 3: Comparative Performance & Usability Boxplots with Mean/CI Overlay (300 DPI)."""
    records = analysis["records"]
    prov = analysis["provenance"]
    metrics = analysis["metrics"]
    sus_stats = analysis["system_usability_scale"]

    fig, axes = plt.subplots(2, 2, figsize=(11.8, 9.4), dpi=300)

    panels = [
        {
            "ax": axes[0, 0],
            "col": "day14_detection_accuracy_pct",
            "stats": metrics["detection_accuracy_pct"]["between_day14"],
            "vr_desc": metrics["detection_accuracy_pct"]["vr"]["day14"],
            "web_desc": metrics["detection_accuracy_pct"]["web"]["day14"],
            "ylabel": "Day 14 Detection Accuracy (%)",
            "title_prefix": "(A) Day 14 Threat Detection Accuracy",
            "box_face": "#BBDEFB",
            "box_edge": "#0D47A1",
            "med_color": "#D50000",
        },
        {
            "ax": axes[0, 1],
            "col": "day14_malicious_click_rate_pct",
            "stats": metrics["malicious_click_rate_pct"]["between_day14"],
            "vr_desc": metrics["malicious_click_rate_pct"]["vr"]["day14"],
            "web_desc": metrics["malicious_click_rate_pct"]["web"]["day14"],
            "ylabel": "Day 14 Malicious Click Rate (%)",
            "title_prefix": "(B) Day 14 Compromise Click Rate",
            "box_face": "#FFCDD2",
            "box_edge": "#B71C1C",
            "med_color": "#1B5E20",
        },
        {
            "ax": axes[1, 0],
            "col": "day14_cognitive_bias_score",
            "stats": metrics["cognitive_bias_score"]["between_day14"],
            "vr_desc": metrics["cognitive_bias_score"]["vr"]["day14"],
            "web_desc": metrics["cognitive_bias_score"]["web"]["day14"],
            "ylabel": "Day 14 Bias Susceptibility Score (1-10)",
            "title_prefix": "(C) Day 14 Cognitive Bias Score (Lower = Better)",
            "box_face": "#FFE0B2",
            "box_edge": "#E65100",
            "med_color": "#311B92",
        },
        {
            "ax": axes[1, 1],
            "col": "system_usability_scale",
            "stats": sus_stats["between"],
            "vr_desc": sus_stats["vr"],
            "web_desc": sus_stats["web"],
            "ylabel": "System Usability Scale Score (0-100)",
            "title_prefix": "(D) System Usability Scale (SUS)",
            "box_face": "#C8E6C9",
            "box_edge": "#1B5E20",
            "med_color": "#BF360C",
        },
    ]

    for panel in panels:
        ax = panel["ax"]
        col = panel["col"]
        web_vals = [float(r[col]) for r in records if r["training_modality"] == WEB_MODALITY]
        vr_vals = [float(r[col]) for r in records if r["training_modality"] == VR_MODALITY]

        ax.boxplot(
            [web_vals, vr_vals],
            tick_labels=[
                f"2D Web (n={len(web_vals)})\nMean: {panel['web_desc']['mean']:.2f} +/- {panel['web_desc']['sd']:.2f}",
                f"Immersive VR (n={len(vr_vals)})\nMean: {panel['vr_desc']['mean']:.2f} +/- {panel['vr_desc']['sd']:.2f}",
            ],
            patch_artist=True,
            boxprops=dict(facecolor=panel["box_face"], color=panel["box_edge"], linewidth=1.4),
            medianprops=dict(color=panel["med_color"], linewidth=2.0),
            whiskerprops=dict(color=panel["box_edge"], linewidth=1.2),
            capprops=dict(color=panel["box_edge"], linewidth=1.2),
        )

        # Plot group means as diamonds
        ax.plot([1, 2], [panel["web_desc"]["mean"], panel["vr_desc"]["mean"]],
                "D", color="#212121", markersize=6, label="Group Mean")

        p_str = su.format_p(panel["stats"]["welch_t"]["p_value"])
        d_val = panel["stats"]["effect_size"]["cohens_d"]
        ax.set_ylabel(panel["ylabel"], fontsize=9.5, fontweight="bold")
        ax.set_title(
            f"{panel['title_prefix']}\n(Welch p {p_str}, Cohen's d = {d_val:.2f})",
            fontsize=10.0,
            fontweight="bold",
        )
        ax.grid(axis="y", linestyle="--", alpha=0.6)

    fig.suptitle(
        f"Figure 3: Statistical Evaluation of Immersive VR vs Standard 2D Web Training\n"
        f"[Data Source: {prov['label']}]",
        fontsize=12.0,
        fontweight="bold",
        y=0.995,
    )
    fig.tight_layout()
    os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
    fig.savefig(output_path, dpi=300, bbox_inches="tight")
    plt.close(fig)
    return output_path


def generate_all_figures(
    csv_path: str = DEFAULT_BENCHMARK_CSV,
    figures_dir: str = FIGURES_DIR,
) -> Dict[str, object]:
    """Validate and analyze ``csv_path`` (without modifying it) and render Figures 1, 2, and 3."""
    sha_before = file_sha256(csv_path)
    analysis = run_full_retention_analysis(csv_path)
    if not analysis["validation"]["is_valid"]:
        raise ValueError(
            f"Refusing to generate figures from invalid dataset: {analysis['validation']['errors']}"
        )

    os.makedirs(figures_dir, exist_ok=True)
    fig1_path = render_figure1_architecture(os.path.join(figures_dir, "figure1_system_architecture.png"))
    fig2_path = render_figure2_telemetry_and_retention(
        analysis, os.path.join(figures_dir, "figure2_kinematic_telemetry.png")
    )
    fig3_path = render_figure3_comparative_performance(
        analysis, os.path.join(figures_dir, "figure3_comparative_performance.png")
    )

    sha_after = file_sha256(csv_path)
    if sha_before != sha_after:
        raise RuntimeError("CRITICAL SAFETY FAILURE: Input CSV checksum changed during figure generation!")

    return {
        "csv_path": csv_path,
        "csv_sha256": sha_after,
        "figures": [fig1_path, fig2_path, fig3_path],
        "analysis": analysis,
    }


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Render 300 DPI publication figures from an existing benchmark CSV dataset."
    )
    parser.add_argument(
        "--csv",
        default=DEFAULT_BENCHMARK_CSV,
        help="Input benchmark CSV dataset path (read-only).",
    )
    parser.add_argument(
        "--figures-dir",
        default=FIGURES_DIR,
        help="Output directory for generated 300 DPI PNG figures.",
    )
    parser.add_argument(
        "--regenerate-synthetic-to",
        default=None,
        help="Optional explicit path to write a freshly generated synthetic CSV (cannot be the protected source CSV).",
    )
    args = parser.parse_args(argv)

    if args.regenerate_synthetic_to:
        recs = generate_synthetic_benchmark_dataset(
            args.regenerate_synthetic_to, seed=42, allow_overwrite=False
        )
        print(f"[OK] Generated {len(recs)} synthetic records to explicit path: {args.regenerate_synthetic_to}")

    result = generate_all_figures(csv_path=args.csv, figures_dir=args.figures_dir)
    print(f"[OK] Verified read-only input dataset ({len(result['analysis']['records'])} rows): {result['csv_path']}")
    print(f"[OK] Input SHA-256 preserved: {result['csv_sha256']}")
    for fpath in result["figures"]:
        print(f"[OK] Rendered 300 DPI figure ({os.path.getsize(fpath)} bytes): {fpath}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
