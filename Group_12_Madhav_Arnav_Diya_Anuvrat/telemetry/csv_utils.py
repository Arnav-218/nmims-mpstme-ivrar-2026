"""
Small, dependency-free CSV helpers shared by the Group 12 telemetry tools.

Why this module exists
----------------------
Every analysis script in this folder reads CSV files. Doing the reading in one
place means every script handles the same edge cases in the same way:

* Excel-style UTF-8 files that start with an invisible "byte order mark" (BOM).
* Blank cells, which must become "missing" (None) and never silently become 0.
* Text such as "nan" or "inf", which is not a usable measurement.
* Output files that must never overwrite a protected source dataset.

Author: I080 Anuvrat Tripathi (Human Factors & Retention Analyst)
"""
from __future__ import annotations

import csv
import hashlib
import math
import os
from typing import Dict, Iterable, List, Optional, Sequence, Tuple


class CsvFormatError(ValueError):
    """Raised when a CSV file cannot be used at all (missing, empty, no header)."""


def read_csv_rows(path: str) -> Tuple[List[str], List[Dict[str, str]]]:
    """Read a CSV file and return ``(header, rows)``.

    Each row is a dictionary mapping column name -> raw text (whitespace trimmed).
    The function never guesses values; converting text to numbers is done by
    :func:`parse_optional_float` so that problems can be reported per cell.
    """
    if not os.path.isfile(path):
        raise CsvFormatError(f"CSV file not found: {path}")
    # "utf-8-sig" transparently removes the BOM that Excel adds to UTF-8 files.
    with open(path, "r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        if reader.fieldnames is None:
            raise CsvFormatError(f"CSV file is empty or has no header row: {path}")
        header = [name.strip() for name in reader.fieldnames]
        rows: List[Dict[str, str]] = []
        for raw in reader:
            row = {}
            for original_name, clean_name in zip(reader.fieldnames, header):
                value = raw.get(original_name)
                row[clean_name] = "" if value is None else str(value).strip()
            rows.append(row)
    return header, rows


def parse_optional_float(text: Optional[str]) -> Tuple[Optional[float], Optional[str]]:
    """Convert one CSV cell into a float.

    Returns ``(value, problem)``:
    * ``(12.5, None)``   for a valid number,
    * ``(None, None)``   for a blank cell (a *missing* value, not an error),
    * ``(None, "...")``  for text that is not a finite number.
    """
    if text is None or str(text).strip() == "":
        return None, None
    try:
        value = float(str(text).strip())
    except ValueError:
        return None, f"not a number: {text!r}"
    if not math.isfinite(value):
        # "nan", "inf" and "-inf" parse as floats but are not measurements.
        return None, f"not a finite number: {text!r}"
    return value, None


def file_sha256(path: str) -> str:
    """Return the SHA-256 fingerprint of a file.

    The fingerprint is written into every report so that anyone can confirm
    exactly which version of the dataset produced the numbers.
    """
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def assert_not_protected(output_path: str, protected_paths: Iterable[str]) -> None:
    """Refuse to write to a protected input file (for example a source dataset)."""
    target = os.path.normcase(os.path.abspath(output_path))
    for protected in protected_paths:
        if target == os.path.normcase(os.path.abspath(protected)):
            raise PermissionError(
                f"Refusing to write to protected source file: {output_path}. "
                "Analysis outputs must go to a separate file."
            )


def write_csv_rows(path: str, fieldnames: Sequence[str], rows: Iterable[Dict[str, object]],
                   protected_paths: Iterable[str] = ()) -> None:
    """Write rows to CSV with Unix line endings, refusing protected targets."""
    assert_not_protected(path, protected_paths)
    folder = os.path.dirname(os.path.abspath(path))
    os.makedirs(folder, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(fieldnames), lineterminator="\n",
                                extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({key: format_cell(row.get(key)) for key in fieldnames})


def format_cell(value: object) -> str:
    """Format a value for CSV output; missing / non-finite numbers become blank."""
    if value is None:
        return ""
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, float):
        if not math.isfinite(value):
            return ""
        return f"{value:.6g}"
    return str(value)
