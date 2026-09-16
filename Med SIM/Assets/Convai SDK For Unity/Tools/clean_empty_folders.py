#!/usr/bin/env python3
"""
Find and remove filesystem-empty directories under the Convai Unity package.

Unity keeps a sibling ``FolderName.meta`` next to each tracked folder; when a
folder is removed, that YAML meta should be deleted too so the asset database
stays consistent.

By default this tool only prints what it would remove. Pass ``--apply`` to
actually delete directories and matching folder ``.meta`` files.

Usage (from repo root or from inside the package folder):

    python Packages/com.convai.convai-sdk-for-unity/Tools/clean_empty_folders.py
    python Tools/clean_empty_folders.py
    python Tools/clean_empty_folders.py --apply
    python Tools/clean_empty_folders.py --apply --include-plugins
"""

from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

# Do not descend into these directory names (any depth under package root).
# ``Plugins`` is skipped by default so embedded third-party trees (e.g. LiveKit
# ffi stubs) are not stripped; use ``--include-plugins`` to process them too.
SKIP_DIR_NAMES: frozenset[str] = frozenset(
    {
        ".git",
        ".vs",
        "Library",
        "Temp",
        "obj",
        "bin",
        "node_modules",
        "Build",
        "build",
        "Plugins",
    }
)


def _prune_walk_dirnames(dirnames: list[str], *, include_plugins: bool) -> None:
    """Prune ``dirnames`` in-place for ``os.walk`` (``topdown=True``)."""

    def skip_dir(name: str) -> bool:
        if name == "Plugins" and include_plugins:
            return False
        return name in SKIP_DIR_NAMES

    dirnames[:] = [d for d in dirnames if not skip_dir(d)]


def _remove_folder_meta_if_present(folder: Path, *, dry_run: bool) -> Path | None:
    """
    Remove ``Parent/FolderName.meta`` for an empty folder ``Parent/FolderName``.

    Returns the meta path if it existed (and was removed or would be removed).
    """
    meta = folder.parent / f"{folder.name}.meta"
    if not meta.is_file():
        return None
    if dry_run:
        return meta
    try:
        meta.unlink()
    except OSError as exc:
        print(f"warning: could not delete folder meta: {meta} ({exc})", file=sys.stderr)
    return meta


def _rmdir_empty(
    folder: Path, *, dry_run: bool, pretend_removed: set[Path] | None
) -> bool:
    """
    Remove ``folder`` if it has no children (optionally treating ``pretend_removed``
    paths as already gone — required for consistent dry-run after child paths).
    """
    if not _is_filesystem_empty_dir(folder, pretend_removed=pretend_removed):
        return False

    meta = _remove_folder_meta_if_present(folder, dry_run=dry_run)
    rel_meta = f" + {meta.name}" if meta else ""

    if dry_run:
        print(f"would remove: {folder}{rel_meta}")
        return True

    try:
        folder.rmdir()
    except OSError as exc:
        print(f"warning: could not remove directory: {folder} ({exc})", file=sys.stderr)
        return False
    return True


def _is_filesystem_empty_dir(folder: Path, *, pretend_removed: set[Path] | None) -> bool:
    """True when ``folder`` has no files and no directories (ignoring ``pretend_removed``)."""
    if pretend_removed is not None and folder in pretend_removed:
        return False
    try:
        for child in folder.iterdir():
            if pretend_removed is not None and child in pretend_removed:
                continue
            return False
        return True
    except OSError:
        return False


def collect_empty_dirs(
    package_root: Path,
    *,
    pretend_removed: set[Path] | None = None,
    include_plugins: bool = False,
) -> list[Path]:
    """
    Return directories under ``package_root`` that are empty on disk, or empty
    if paths in ``pretend_removed`` are treated as already gone (dry-run).

    ``package_root`` itself is never included. Deepest paths are sorted first.
    """
    empty: list[Path] = []
    for dirpath, dirnames, filenames in os.walk(package_root, topdown=True):
        _prune_walk_dirnames(dirnames, include_plugins=include_plugins)
        p = Path(dirpath)
        if p == package_root:
            continue
        if filenames:
            continue
        if not _is_filesystem_empty_dir(p, pretend_removed=pretend_removed):
            continue
        empty.append(p)

    empty.sort(key=lambda path: len(path.parts), reverse=True)
    return empty


def clean_empty_tree(
    package_root: Path, *, dry_run: bool, include_plugins: bool
) -> tuple[int, int]:
    """
    Repeatedly remove leaf-empty directories until stable.

    Returns ``(removed_dir_count, removed_meta_count)`` for an ``--apply`` run;
    for dry-run, counts reflect what *would* be removed (simulated with a
    virtual removal set because nothing is deleted from disk).
    """
    removed_dirs = 0
    removed_metas = 0
    pretend_removed: set[Path] = set()

    while True:
        pretend_arg: set[Path] | None = pretend_removed if dry_run else None
        candidates = collect_empty_dirs(
            package_root,
            pretend_removed=pretend_arg,
            include_plugins=include_plugins,
        )
        if dry_run:
            candidates = [c for c in candidates if c not in pretend_removed]
        if not candidates:
            break

        progress = False
        for folder in candidates:
            meta_before = folder.parent / f"{folder.name}.meta"
            had_meta = meta_before.is_file()

            if not _rmdir_empty(
                folder, dry_run=dry_run, pretend_removed=pretend_arg
            ):
                continue

            progress = True
            removed_dirs += 1
            if had_meta:
                removed_metas += 1
            if dry_run:
                pretend_removed.add(folder)

        if not progress:
            break

    return removed_dirs, removed_metas


def main() -> int:
    parser = argparse.ArgumentParser(
        description=(
            "List or remove filesystem-empty directories under the Convai package "
            "(and delete matching FolderName.meta next to removed folders)."
        )
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=None,
        help="Package root (folder that contains Tools/). Defaults to parent of this script.",
    )
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Actually delete empty directories and their folder .meta files. Without this, dry-run only.",
    )
    parser.add_argument(
        "--include-plugins",
        action="store_true",
        help="Also scan under any folder named Plugins (default: skip embedded plugin trees).",
    )
    args = parser.parse_args()

    script_dir = Path(__file__).resolve().parent
    package_root = (args.root or script_dir.parent).resolve()

    if not package_root.is_dir():
        print(f"error: package root is not a directory: {package_root}", file=sys.stderr)
        return 2

    tools_dir = package_root / "Tools"
    if not tools_dir.is_dir() or not (tools_dir / "clean_empty_folders.py").is_file():
        print(
            "error: --root does not look like com.convai.convai-sdk-for-unity "
            f"(missing Tools/clean_empty_folders.py under {package_root})",
            file=sys.stderr,
        )
        return 2

    dry_run = not args.apply
    if dry_run:
        print(f"dry-run: scanning for empty directories under:\n  {package_root}\n")

    removed_dirs, removed_metas = clean_empty_tree(
        package_root,
        dry_run=dry_run,
        include_plugins=args.include_plugins,
    )

    suffix = " (dry-run)" if dry_run else ""
    print(
        f"Done{suffix}: removed or would remove {removed_dirs} empty folder(s); "
        f"{removed_metas} folder .meta file(s) alongside them."
    )
    if dry_run and removed_dirs:
        print("\nRe-run with --apply to delete the paths listed above.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
