#!/usr/bin/env python3
"""Validate the repository layout without external dependencies."""

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REQUIRED_DIRECTORIES = (
    "assets/audio",
    "assets/images",
    "assets/overlays",
    "assets/video",
    "cvxr",
    "docs",
    "models",
    "obs/profiles",
    "obs/scene-collections",
    "scripts",
    "tests",
)
IGNORED_DIRECTORY_NAMES = {
    ".git",
    ".venv",
    "Library",
    "Logs",
    "Temp",
    "build",
    "dist",
    "node_modules",
    "obj",
    "output",
    "recordings",
    "renders",
}
LARGE_FILE_BYTES = 50 * 1024 * 1024


@dataclass(frozen=True)
class Finding:
    level: str
    message: str


def iter_project_files(root: Path):
    """Yield regular project files while skipping generated directories."""
    for path in root.rglob("*"):
        if any(part in IGNORED_DIRECTORY_NAMES for part in path.parts):
            continue
        if path.is_file() and not path.is_symlink():
            yield path


def check_required_directories(root: Path) -> list[Finding]:
    return [
        Finding("ERROR", f"missing required directory: {relative_path}")
        for relative_path in REQUIRED_DIRECTORIES
        if not (root / relative_path).is_dir()
    ]


def check_json_files(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    for path in sorted(file for file in iter_project_files(root) if file.suffix == ".json"):
        try:
            with path.open(encoding="utf-8-sig") as file:
                json.load(file)
        except (OSError, UnicodeError, json.JSONDecodeError) as error:
            findings.append(
                Finding("ERROR", f"invalid JSON: {path.relative_to(root)} ({error})")
            )
    return findings


def check_overlay_entrypoints(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    overlays_root = root / "assets" / "overlays"
    if not overlays_root.exists():
        return findings

    for overlay in sorted(overlays_root.iterdir()):
        if overlay.is_dir() and not overlay.name.startswith("."):
            if not (overlay / "index.html").is_file():
                findings.append(
                    Finding(
                        "ERROR",
                        f"overlay has no index.html: {overlay.relative_to(root)}",
                    )
                )
    return findings


def check_large_files(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    for path in iter_project_files(root):
        try:
            size = path.stat().st_size
        except OSError as error:
            findings.append(
                Finding("WARNING", f"could not inspect {path.relative_to(root)}: {error}")
            )
            continue

        if size > LARGE_FILE_BYTES:
            mebibytes = size / (1024 * 1024)
            findings.append(
                Finding(
                    "WARNING",
                    f"large file ({mebibytes:.1f} MiB): {path.relative_to(root)}",
                )
            )
    return findings


def run_checks(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    findings.extend(check_required_directories(root))
    findings.extend(check_json_files(root))
    findings.extend(check_overlay_entrypoints(root))
    findings.extend(check_large_files(root))
    return findings


def main() -> int:
    findings = run_checks(ROOT)
    if not findings:
        print("Repository check passed.")
        return 0

    for finding in findings:
        print(f"[{finding.level}] {finding.message}")

    errors = sum(finding.level == "ERROR" for finding in findings)
    warnings = sum(finding.level == "WARNING" for finding in findings)
    print(f"Repository check finished: {errors} error(s), {warnings} warning(s).")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
