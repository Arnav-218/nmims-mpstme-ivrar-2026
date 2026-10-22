"""
Technoeconomic Operational Parity, Telecom Latency, and Enterprise Cybersecurity Incident Mitigation Model
===========================================================================================================
Group 12: Immersive VR Phishing Simulation vs 2D Web-Based Training
Course: IVRAR (Immersive Virtual, Real & Augmented Reality - 702COI002)
Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)

Purpose & Scientific Scope
--------------------------
This module models the projected organizational impact of deploying immersive VR
experiential phishing simulations versus conventional 2D web-based compliance modules.
All outputs are MODELED SCENARIO ESTIMATES derived from explicit parametric equations
and must NOT be interpreted as measured financial outcomes from a live enterprise.

CONSTRAINTS (Course Rubric Compliance):
- ZERO currency symbols (dimensionless ratios, labor hours, and payback months only).
- Express all cost metrics as dimensionless ratios, labor hours, and payback months.

Equations & Traceability
------------------------
1. Annual compromise click events:
       Clicks_mode = N_workforce * N_campaigns * (ClickRate_Day14_pct / 100)
2. Annual escalated security incidents (1.2% escalation rate per click):
       Escalations_mode = Clicks_mode * 0.012
3. Annual SOC investigation labor hours:
       Hours_SOC = Escalations_mode * Hours_per_incident
4. Annual IT helpdesk credential-reset labor hours (15% ticket rate per click):
       Hours_IT = Clicks_mode * 0.15 * Hours_per_compromise
5. Total annual remediation labor hours:
       Hours_total = Hours_SOC + Hours_IT
6. Dimensionless Cost Parity Ratio (kappa):
       kappa = OpEx_VR / OpEx_Web = (0.10 + 0.09 + 0.05) / (0.25 + 0.52 + 0.23) = 0.24 / 1.00 = 0.24
7. Capital Payback Horizon (months):
       Payback_months = (12.0 * K_capex) / (OpEx_Web - OpEx_VR)
"""
from __future__ import annotations

import os
from typing import Dict, List, Optional, Sequence

from human_factors_retention_analysis import (
    DEFAULT_BENCHMARK_CSV,
    run_full_retention_analysis,
)


class TelecomLatencyDebiasEval:
    def __init__(
        self,
        enterprise_workforce_size: int = 2500,
        baseline_phishing_click_rate_pct: float = 28.5,
        web_day14_click_rate_pct: float = 22.4,        # Prescribed baseline scenario parameter
        vr_day14_click_rate_pct: float = 6.8,          # Prescribed baseline scenario parameter
        simulated_campaigns_per_year: int = 12,
        soc_analyst_hours_per_incident: float = 45.0,  # Triage, forensics, credential rotation
        it_helpdesk_hours_per_compromise: float = 4.5, # Password resets, token reissue, device scans
        capex_ratio_baseline: float = 1.0,             # Normalized initial VR headset fleet CapEx
        network_rtt_latency_budget_ms: float = 20.0,   # Target motion-to-photon / cloud XR RTT budget
        escalation_probability_per_click: float = 0.012,
        helpdesk_ticket_probability_per_click: float = 0.15,
    ):
        if enterprise_workforce_size <= 0:
            raise ValueError("enterprise_workforce_size must be positive.")
        if simulated_campaigns_per_year <= 0:
            raise ValueError("simulated_campaigns_per_year must be positive.")
        if not (0.0 <= web_day14_click_rate_pct <= 100.0):
            raise ValueError("web_day14_click_rate_pct must be in [0, 100].")
        if not (0.0 <= vr_day14_click_rate_pct <= 100.0):
            raise ValueError("vr_day14_click_rate_pct must be in [0, 100].")

        self.workforce = enterprise_workforce_size
        self.baseline_click = baseline_phishing_click_rate_pct / 100.0
        self.web_click = web_day14_click_rate_pct / 100.0
        self.vr_click = vr_day14_click_rate_pct / 100.0
        self.campaigns = simulated_campaigns_per_year
        self.soc_hours_per_inc = soc_analyst_hours_per_incident
        self.helpdesk_hours_per_comp = it_helpdesk_hours_per_compromise
        self.capex = capex_ratio_baseline
        self.latency_budget_ms = network_rtt_latency_budget_ms
        self.escalation_prob = escalation_probability_per_click
        self.helpdesk_prob = helpdesk_ticket_probability_per_click

    def compute_standard_web_training_burden(self) -> Dict[str, float]:
        """Calculate modeled annual operational overhead under standard 2D web-based training."""
        annual_click_events = self.workforce * self.campaigns * self.web_click
        annual_escalated_incidents = annual_click_events * self.escalation_prob
        annual_soc_investigation_hours = annual_escalated_incidents * self.soc_hours_per_inc
        annual_helpdesk_remediation_hours = (
            annual_click_events * self.helpdesk_prob * self.helpdesk_hours_per_comp
        )

        total_security_remediation_hours = (
            annual_soc_investigation_hours + annual_helpdesk_remediation_hours
        )

        web_lms_license_and_content_factor = 0.25
        soc_investigation_overhead_factor = 0.52
        employee_downtime_ticket_factor = 0.23
        normalized_traditional_opex = (
            web_lms_license_and_content_factor
            + soc_investigation_overhead_factor
            + employee_downtime_ticket_factor
        )

        return {
            "annual_click_events": annual_click_events,
            "annual_escalated_incidents": annual_escalated_incidents,
            "total_security_remediation_hours": total_security_remediation_hours,
            "annual_soc_investigation_hours": annual_soc_investigation_hours,
            "annual_helpdesk_remediation_hours": annual_helpdesk_remediation_hours,
            "normalized_traditional_opex": normalized_traditional_opex,
        }

    def compute_immersive_vr_training_burden(self) -> Dict[str, float]:
        """Calculate modeled annual operational overhead under immersive VR experiential training."""
        annual_click_events = self.workforce * self.campaigns * self.vr_click
        annual_escalated_incidents = annual_click_events * self.escalation_prob
        annual_soc_investigation_hours = annual_escalated_incidents * self.soc_hours_per_inc
        annual_helpdesk_remediation_hours = (
            annual_click_events * self.helpdesk_prob * self.helpdesk_hours_per_comp
        )
        total_security_remediation_hours = (
            annual_soc_investigation_hours + annual_helpdesk_remediation_hours
        )

        vr_headset_hygiene_and_device_mdm = 0.10
        threat_scenario_content_updates = 0.09
        residual_incident_containment = 0.05
        normalized_vr_opex = (
            vr_headset_hygiene_and_device_mdm
            + threat_scenario_content_updates
            + residual_incident_containment
        )

        return {
            "annual_click_events": annual_click_events,
            "annual_escalated_incidents": annual_escalated_incidents,
            "total_security_remediation_hours": total_security_remediation_hours,
            "annual_soc_investigation_hours": annual_soc_investigation_hours,
            "annual_helpdesk_remediation_hours": annual_helpdesk_remediation_hours,
            "normalized_vr_opex": normalized_vr_opex,
        }

    def compute_telecom_and_operational_parity(self) -> Dict[str, float]:
        """Compute the dimensionless operational cost parity ratio (kappa), payback horizon,
        and modeled labor reclamation metrics.
        """
        web = self.compute_standard_web_training_burden()
        vr = self.compute_immersive_vr_training_burden()

        clicks_avoided = web["annual_click_events"] - vr["annual_click_events"]
        escalated_incidents_prevented = (
            web["annual_escalated_incidents"] - vr["annual_escalated_incidents"]
        )
        soc_analyst_hours_reclaimed = (
            web["total_security_remediation_hours"] - vr["total_security_remediation_hours"]
        )

        kappa = vr["normalized_vr_opex"] / web["normalized_traditional_opex"]
        annual_operational_savings = web["normalized_traditional_opex"] - vr["normalized_vr_opex"]

        if annual_operational_savings > 0:
            payback_months = (12.0 * self.capex) / annual_operational_savings
        else:
            payback_months = float("inf")

        if self.web_click > 0:
            retention_resilience_ratio = (1.0 - (self.vr_click / self.web_click)) * 100.0
        else:
            retention_resilience_ratio = 0.0

        return {
            "kappa_ratio": round(kappa, 4),
            "annual_compromise_clicks_avoided": round(clicks_avoided, 1),
            "annual_escalated_breaches_prevented": round(escalated_incidents_prevented, 1),
            "annual_security_remediation_hours_reclaimed": round(soc_analyst_hours_reclaimed, 1),
            "payback_period_months": round(payback_months, 2) if payback_months != float("inf") else float("inf"),
            "retention_resilience_advantage_pct": round(retention_resilience_ratio, 1),
            "network_latency_budget_ms": self.latency_budget_ms,
            "normalized_opex_reduction_pct": round((1.0 - kappa) * 100.0, 1),
        }

    @classmethod
    def from_benchmark_csv(
        cls,
        csv_path: str = DEFAULT_BENCHMARK_CSV,
        **kwargs: object,
    ) -> Dict[str, object]:
        """Instantiate and evaluate the technoeconomic model using the actual Day 14 mean click
        rates computed from ``phishing_simulation_benchmark.csv`` (26.86% Web vs 6.86% VR),
        contrasting it with the prescribed README baseline scenario (22.4% Web vs 6.8% VR).
        """
        analysis = run_full_retention_analysis(csv_path)
        clk = analysis["metrics"]["malicious_click_rate_pct"]
        web_d14 = float(clk["web"]["day14"]["mean"])
        vr_d14 = float(clk["vr"]["day14"]["mean"])

        model = cls(
            web_day14_click_rate_pct=web_d14,
            vr_day14_click_rate_pct=vr_d14,
            **kwargs,
        )
        return {
            "scenario_type": "SYNTHETIC_BENCHMARK_CSV_DERIVED_ESTIMATE",
            "provenance_label": analysis["provenance"]["label"],
            "input_web_day14_click_rate_pct": round(web_d14, 2),
            "input_vr_day14_click_rate_pct": round(vr_d14, 2),
            "results": model.compute_telecom_and_operational_parity(),
        }

    @classmethod
    def run_sensitivity_analysis(
        cls,
        workforce_sizes: Sequence[int] = (500, 1000, 2500, 5000),
        escalation_rates: Sequence[float] = (0.006, 0.012, 0.020),
    ) -> List[Dict[str, object]]:
        """Run a parametric sensitivity sweep across workforce sizes and breach escalation rates."""
        scenarios: List[Dict[str, object]] = []
        for wf in workforce_sizes:
            for esc in escalation_rates:
                m = cls(
                    enterprise_workforce_size=wf,
                    escalation_probability_per_click=esc,
                )
                res = m.compute_telecom_and_operational_parity()
                scenarios.append(
                    {
                        "workforce_size": wf,
                        "escalation_prob_per_click": esc,
                        "annual_compromise_clicks_avoided": res["annual_compromise_clicks_avoided"],
                        "annual_escalated_breaches_prevented": res["annual_escalated_breaches_prevented"],
                        "annual_security_remediation_hours_reclaimed": res[
                            "annual_security_remediation_hours_reclaimed"
                        ],
                        "kappa_ratio": res["kappa_ratio"],
                        "payback_period_months": res["payback_period_months"],
                    }
                )
        return scenarios


if __name__ == "__main__":
    model = TelecomLatencyDebiasEval()
    results = model.compute_telecom_and_operational_parity()
    print("=" * 75)
    print("IVRAR GROUP 12: TELECOM LATENCY & CYBERSECURITY DEBIAS EVALUATION MODEL")
    print("NOTE: All outputs below are MODELED SCENARIO ESTIMATES (Zero Currency Units)")
    print("=" * 75)
    print("[Scenario A: Prescribed Baseline Parameters (Web=22.4%, VR=6.8%)]")
    for k, v in results.items():
        print(f"  {k:45s}: {v}")

    if os.path.isfile(DEFAULT_BENCHMARK_CSV):
        csv_scenario = TelecomLatencyDebiasEval.from_benchmark_csv(DEFAULT_BENCHMARK_CSV)
        print("-" * 75)
        print(
            f"[Scenario B: Derived from {os.path.basename(DEFAULT_BENCHMARK_CSV)} "
            f"(Web={csv_scenario['input_web_day14_click_rate_pct']}%, "
            f"VR={csv_scenario['input_vr_day14_click_rate_pct']}%)]"
        )
        for k, v in csv_scenario["results"].items():
            print(f"  {k:45s}: {v}")
    print("=" * 75)
