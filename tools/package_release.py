#!/usr/bin/env python3
"""Export a clean Unity source archive or the files referenced by its WebGL entry."""

from __future__ import annotations

import argparse
import hashlib
import re
from datetime import datetime
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

PROJECT = Path(__file__).resolve().parents[1]
SOURCE_ENTRIES = (
    "Assets", "Packages", "ProjectSettings", "docs", "tools", ".github",
    "README.md", "LICENSE", "THIRD_PARTY_NOTICES.md", "CONTRIBUTING.md",
    ".gitignore", ".gitattributes", ".editorconfig",
)
GENERATED_TEST_FILES = {
    "PerformanceTestRunInfo.json", "PerformanceTestRunInfo.json.meta",
    "PerformanceTestRunSettings.json", "PerformanceTestRunSettings.json.meta",
}


def files_under(entry: Path) -> list[Path]:
    if not entry.exists():
        raise FileNotFoundError(entry)
    files = sorted(entry.rglob("*")) if entry.is_dir() else [entry]
    return [p for p in files if p.is_file() and not p.is_symlink()
            and p.name != ".DS_Store" and "__pycache__" not in p.parts
            and p.name not in GENERATED_TEST_FILES]


def source_files() -> list[Path]:
    # An allowlist is deliberate: local logs, caches, credentials and old builds stay out.
    return [p for entry in SOURCE_ENTRIES for p in files_under(PROJECT / entry)]


def web_files() -> list[Path]:
    root = PROJECT / "Build" / "WebGL"
    index = root / "index.html"
    html = index.read_text(encoding="utf-8")
    folder = re.search(r'\bbuildUrl\s*=\s*["\']([^"\']+)["\']', html)
    names = re.findall(
        r'(?:loaderUrl\s*=|dataUrl\s*:|frameworkUrl\s*:|codeUrl\s*:)'
        r'\s*buildUrl\s*\+\s*["\']([^"\']+)["\']', html)
    if folder is None or len(names) != 4:
        raise ValueError("Expected Unity's four explicit build URLs in index.html")
    files = [index, *files_under(root / "TemplateData")]
    for name in names:
        path = (root / folder.group(1) / name.lstrip("/")).resolve()
        if not path.is_relative_to(root.resolve()) or not path.is_file():
            raise ValueError(f"Invalid or missing WebGL build file: {name}")
        files.append(path)
    if (root / "StreamingAssets").exists():
        files.extend(files_under(root / "StreamingAssets"))
    files.extend(PROJECT / name for name in
                 ("tools/serve_webgl.py", "LICENSE", "THIRD_PARTY_NOTICES.md", "docs/TESTING.md",
                  "Assets/Resources/OFL-NotoSansSC.txt"))
    return files


def export(kind: str, output: Path, stamp: str) -> Path:
    files = source_files() if kind == "source" else web_files()
    target = output / f"haoxi-kaiyan-{kind}-{stamp}.zip"
    # Do not overwrite a previous snapshot, even if a command is repeated in one second.
    with ZipFile(target, "x", compression=ZIP_DEFLATED, compresslevel=6) as archive:
        for path in sorted(set(files)):
            archive.write(path, path.relative_to(PROJECT).as_posix())
    with ZipFile(target) as archive:
        bad = archive.testzip()
        if bad is not None:
            raise ValueError(f"Archive integrity failure: {bad}")
    checksum = hashlib.sha256(target.read_bytes()).hexdigest()
    print(f"{target.name}: {target.stat().st_size / 1024 / 1024:.2f} MiB; {len(set(files))} files")
    print(f"SHA-256 {checksum}")
    return target


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--kind", choices=("source", "web", "all"), default="source")
    parser.add_argument("--output", type=Path, default=PROJECT / "Releases")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    for kind in ("source", "web") if args.kind == "all" else (args.kind,):
        export(kind, args.output.resolve(), stamp)


if __name__ == "__main__":
    main()
