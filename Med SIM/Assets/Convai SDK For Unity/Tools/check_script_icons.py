#!/usr/bin/env python3
"""
Check that all .cs.meta script files in the Convai SDK package have the standard
MonoImporter icon. Excludes Plugins/ and Reallusion paths.

Usage:
    python Tools/check_script_icons.py
    python Tools/check_script_icons.py --fix   # add or correct icon in place
"""

from pathlib import Path
import argparse
import re
import sys

# Unity built-in C# script icon (MonoImporter icon: line only; not the asset guid at file top)
SCRIPT_ICON_GUID = "11be21f3f1f4bac46a48d14a031f3d60"

# Full MonoImporter block Unity writes for C# scripts (when .meta has no MonoImporter yet)
MONOIMPORTER_BLOCK = f"""MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{fileID: 2800000, guid: {SCRIPT_ICON_GUID}, type: 3}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

# Path segments to exclude (package root Plugins, and any Reallusion)
EXCLUDE_SEGMENTS = ("Plugins", "Reallusion")

_SCRIPT_ICON_LINE_RE = re.compile(
    rf"(?m)^[ \t]+icon:.*\bguid:\s*{re.escape(SCRIPT_ICON_GUID)}\b",
)


def has_expected_script_icon(text: str) -> bool:
    """True when MonoImporter includes the standard script icon (indented icon: line)."""
    return bool(_SCRIPT_ICON_LINE_RE.search(text))


def fix_meta_text(text: str) -> str:
    """Return .meta contents with the expected script icon (idempotent)."""
    if has_expected_script_icon(text):
        return text

    if "MonoImporter:" not in text:
        return text.rstrip("\r\n") + "\n\n" + MONOIMPORTER_BLOCK

    icon_line = f"  icon: {{fileID: 2800000, guid: {SCRIPT_ICON_GUID}, type: 3}}"
    updated = re.sub(r"(?m)^[ \t]+icon:\s*.+$", icon_line, text)

    if has_expected_script_icon(updated):
        return updated

    lines = updated.splitlines()
    anchors = (
        re.compile(r"^\s+executionOrder:\s*"),
        re.compile(r"^\s+defaultReferences:\s*"),
    )
    for anchor in anchors:
        out: list[str] = []
        inserted = False
        for line in lines:
            out.append(line)
            if not inserted and anchor.match(line):
                out.append(icon_line)
                inserted = True
        if inserted:
            result = "\n".join(out) + "\n"
            return result

    out = []
    inserted = False
    for line in lines:
        out.append(line)
        if not inserted and line.strip() == "MonoImporter:":
            out.append(icon_line)
            inserted = True
    return "\n".join(out) + "\n"


def should_skip(rel_path: str) -> bool:
    parts = Path(rel_path).parts
    # Exclude top-level Plugins (first segment after package root)
    if "Plugins" in parts and parts[0] == "Plugins":
        return True
    # Exclude any path containing Reallusion or Plugins
    for segment in EXCLUDE_SEGMENTS:
        if segment in parts:
            return True
    return False


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Check or fix MonoImporter script icons on .cs.meta files in this package."
    )
    parser.add_argument(
        "--fix",
        action="store_true",
        help="Rewrite .cs.meta files to add or correct the standard C# script icon.",
    )
    args = parser.parse_args()

    script_dir = Path(__file__).resolve().parent
    package_root = script_dir.parent

    meta_files = sorted(package_root.rglob("*.cs.meta"))
    failed: list[tuple[Path, str]] = []
    skipped = 0

    for meta_path in meta_files:
        try:
            rel = meta_path.relative_to(package_root)
        except ValueError:
            continue
        rel_str = str(rel).replace("\\", "/")

        if should_skip(rel_str):
            skipped += 1
            continue

        text = meta_path.read_text(encoding="utf-8", errors="replace")
        if not has_expected_script_icon(text):
            failed.append((meta_path, rel_str))

    if args.fix and failed:
        fixed = 0
        for meta_path, rel_str in failed:
            original = meta_path.read_text(encoding="utf-8", errors="replace")
            new_text = fix_meta_text(original)
            if new_text != original:
                with meta_path.open("w", encoding="utf-8", newline="\n") as f:
                    f.write(new_text)
                fixed += 1
                print(f"fixed: {rel_str}")
        print(f"\nWrote {fixed} file(s). Re-checking...")
        failed = []
        for meta_path in sorted(package_root.rglob("*.cs.meta")):
            try:
                rel = meta_path.relative_to(package_root)
            except ValueError:
                continue
            rel_str = str(rel).replace("\\", "/")
            if should_skip(rel_str):
                continue
            text = meta_path.read_text(encoding="utf-8", errors="replace")
            if not has_expected_script_icon(text):
                failed.append((meta_path, rel_str))

    if failed:
        print("Script .meta files missing expected icon (excl. Plugins & Reallusion):")
        for _, rel_str in failed:
            print(f"  {rel_str}")
        print(f"\nTotal: {len(failed)} file(s) need the icon line.")
        return 1

    print(
        f"OK: All checked .cs.meta files have script icon guid {SCRIPT_ICON_GUID[:8]}... "
        f"(skipped Plugins/Reallusion: {skipped})"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
