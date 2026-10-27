"""
Unity Project Automation & CI/CD Pipeline
Antigravity Production-Grade Development Environment
"""

import argparse
import json
import os
import socket
import subprocess
import sys
from pathlib import Path

DEFAULT_UNITY_PATH = r"C:\Program Files\Unity\Hub\Editor\6000.3.3f1\Editor\Unity.exe"
PROJECT_ROOT = Path(__file__).resolve().parent.parent

def find_unity_binary():
    custom_path = os.environ.get("UNITY_PATH")
    if custom_path and Path(custom_path).exists():
        return custom_path
    if Path(DEFAULT_UNITY_PATH).exists():
        return DEFAULT_UNITY_PATH
    hub_editors = Path(r"C:\Program Files\Unity\Hub\Editor")
    if hub_editors.exists():
        for editor_dir in hub_editors.iterdir():
            exe = editor_dir / "Editor" / "Unity.exe"
            if exe.exists():
                return str(exe)
    raise FileNotFoundError("Unity.exe could not be found. Set UNITY_PATH environment variable.")

def check_mcp_socket(host="127.0.0.1", port=6400, timeout=2.0):
    print(f"[*] Checking Unity MCP Bridge at {host}:{port}...")
    try:
        with socket.create_connection((host, port), timeout=timeout) as s:
            print(f"[+] SUCCESS: Connected to Unity MCP Bridge on {host}:{port}")
            return True
    except (socket.timeout, ConnectionRefusedError, OSError) as e:
        print(f"[-] MCP Bridge not responding on {host}:{port} ({e})")
        print("    Ensure Unity Editor is open with McpBridge.cs compiled.")
        return False

def run_project_validation():
    unity_exe = find_unity_binary()
    logs_dir = PROJECT_ROOT / "Logs"
    logs_dir.mkdir(exist_ok=True)
    log_file = logs_dir / "validator_cli.log"

    print(f"[*] Running ProjectValidator in batchmode using {unity_exe}...")
    cmd = [
        unity_exe,
        "-batchmode",
        "-quit",
        "-projectPath", str(PROJECT_ROOT),
        "-executeMethod", "ProductionEngine.EditorTools.ProjectValidator.RunValidationFromCommandLine",
        "-logFile", str(log_file)
    ]
    
    proc = subprocess.run(cmd, capture_output=True, text=True)
    report_file = logs_dir / "validation_report.json"
    if report_file.exists():
        try:
            with open(report_file, "r", encoding="utf-8") as f:
                report = json.load(f)
                print("==================================================")
                print(f" Validation Report: {'PASSED' if report.get('passed') else 'FAILED'}")
                print(f" Timestamp: {report.get('timestamp')}")
                print(f" Unity Version: {report.get('unityVersion')}")
                print(f" Scenes Checked: {report.get('totalScenesChecked')}")
                print(f" Prefabs Checked: {report.get('totalPrefabsChecked')}")
                print(f" Missing Scripts: {report.get('missingScriptCount')}")
                print(f" Dangling References: {report.get('danglingReferenceCount')}")
                print("==================================================")
                if report.get('errors'):
                    print("Errors:")
                    for err in report['errors']:
                        print(f"  - {err}")
                if report.get('warnings'):
                    print("Warnings:")
                    for warn in report['warnings']:
                        print(f"  - {warn}")
        except Exception as e:
            print(f"[!] Could not parse validation_report.json: {e}")
    else:
        print(f"[!] Validation report not generated. Check {log_file}")
    
    return proc.returncode == 0

def run_tests(test_platform="EditMode"):
    unity_exe = find_unity_binary()
    logs_dir = PROJECT_ROOT / "Logs"
    logs_dir.mkdir(exist_ok=True)
    test_results = logs_dir / f"test_results_{test_platform.lower()}.xml"
    log_file = logs_dir / f"test_runner_{test_platform.lower()}.log"

    print(f"[*] Running {test_platform} tests...")
    cmd = [
        unity_exe,
        "-batchmode",
        "-quit",
        "-projectPath", str(PROJECT_ROOT),
        "-runTests",
        "-testPlatform", test_platform,
        "-testResults", str(test_results),
        "-logFile", str(log_file)
    ]

    proc = subprocess.run(cmd)
    if test_results.exists():
        print(f"[+] Test results written to {test_results}")
    else:
        print(f"[-] Test execution completed. See log at {log_file}")
    return proc.returncode == 0

def main():
    parser = argparse.ArgumentParser(description="Unity Automation & CI/CD Pipeline")
    subparsers = parser.add_subparsers(dest="command", required=True)

    subparsers.add_parser("mcp-status", help="Check Unity MCP Bridge connection")
    subparsers.add_parser("validate", help="Run automated asset, scene, and script validation")
    
    test_parser = subparsers.add_parser("test", help="Execute Unity Test Framework")
    test_parser.add_argument("--platform", choices=["EditMode", "PlayMode"], default="EditMode")

    args = parser.parse_args()

    if args.command == "mcp-status":
        success = check_mcp_socket()
        sys.exit(0 if success else 1)
    elif args.command == "validate":
        success = run_project_validation()
        sys.exit(0 if success else 1)
    elif args.command == "test":
        success = run_tests(args.platform)
        sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()
