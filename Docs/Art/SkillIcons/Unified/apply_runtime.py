"""Preflight or apply the approved 192px skill-role icons to Unity Assets.

No runtime files are changed unless --apply is passed. A preserved Sources/
snapshot is required; if current Unity files no longer match either that
snapshot or the exact staged result, the operation refuses to continue.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from build_icons import ATLAS, META, OUT, RUNTIME_SRC, SRC, sha, sprite_rects
from validate_icons import main as validate_stage


def proposed_meta(original: str, rects: dict) -> str:
    pattern = re.compile(
        r"(?m)^(?P<head>      name: role(?P<n>\d+)\r?\n"
        r"      rect:\r?\n        serializedVersion: 2\r?\n)"
        r"        x: \d+(?P<nl1>\r?\n)"
        r"        y: \d+(?P<nl2>\r?\n)"
        r"        width: \d+(?P<nl3>\r?\n)"
        r"        height: \d+(?P<nl4>\r?\n)"
    )
    seen = set()

    def replace(match: re.Match) -> str:
        n = int(match["n"])
        if n not in range(1, 16) or n in seen:
            raise ValueError(f"Unexpected or duplicate atlas sprite role{n}")
        seen.add(n)
        rect = rects[f"role{n}"]
        return (
            match["head"]
            + f"        x: {rect['x']}" + match["nl1"]
            + f"        y: {rect['y']}" + match["nl2"]
            + f"        width: {rect['width']}" + match["nl3"]
            + f"        height: {rect['height']}" + match["nl4"]
        )

    updated = pattern.sub(replace, original)
    if seen != set(range(1, 16)):
        raise ValueError(f"Expected to update 15 sprite rects, found {sorted(seen)}")
    return updated


def files_and_expected(manifest: dict):
    original_meta = META.read_bytes()
    edited_meta = proposed_meta(
        original_meta.decode("utf-8"),
        manifest["proposed_atlas_192_rects_bottom_origin"]
    ).encode("utf-8")
    targets = {
        RUNTIME_SRC / ATLAS.name: (
            ATLAS.read_bytes(),
            (OUT / "proposed-atlas-1-15-192.png").read_bytes()
        ),
        RUNTIME_SRC / META.name: (original_meta, edited_meta),
    }
    for n in range(16, 21):
        filename = f"role{n}.png"
        targets[RUNTIME_SRC / filename] = (
            (SRC / filename).read_bytes(),
            (OUT / "Runtime192" / filename).read_bytes()
        )
    return targets, edited_meta


def check_identity(manifest: dict, edited_meta: bytes):
    source_rects = sprite_rects(META)
    actual_rects = sprite_rects(RUNTIME_SRC / META.name)
    new_rects = manifest["proposed_atlas_192_rects_bottom_origin"]
    for n in range(1, 16):
        old, actual = source_rects[n], actual_rects[n]
        for identity in ("spriteID", "internalID"):
            if old[identity] != actual[identity]:
                raise AssertionError(f"role{n} {identity} changed")
        expected = new_rects[f"role{n}"]
        for coord in ("x", "y", "width", "height"):
            if actual[coord] != expected[coord]:
                raise AssertionError(f"role{n} {coord} differs from approved atlas")
    # Exact metadata comparison also guards GUID, nameFileIdTable,
    # importer filter, PPU, compression and standalone GUIDs/settings.
    if (RUNTIME_SRC / META.name).read_bytes() != edited_meta:
        raise AssertionError("Atlas metadata has unrelated changes")
    for n in range(16, 21):
        filename = f"role{n}.png.meta"
        if (RUNTIME_SRC / filename).read_bytes() != (SRC / filename).read_bytes():
            raise AssertionError(f"{filename} importer metadata changed")


def state(targets: dict[Path, tuple[bytes, bytes]], manifest: dict, edited_meta: bytes):
    originals = all(path.read_bytes() == original
                    for path, (original, _) in targets.items())
    applied = all(path.read_bytes() == candidate
                  for path, (_, candidate) in targets.items())
    standalone_meta_clean = all(
        (RUNTIME_SRC / f"role{n}.png.meta").read_bytes()
        == (SRC / f"role{n}.png.meta").read_bytes()
        for n in range(16, 21)
    )
    if not standalone_meta_clean:
        raise AssertionError("One or more standalone importer .meta files changed")
    if originals:
        return "ready"
    if applied:
        check_identity(manifest, edited_meta)
        return "applied"
    raise AssertionError(
        "Runtime icons or atlas metadata differ from both the preserved originals "
        "and staged output; resolve this before applying."
    )


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true",
                      help="Validate stage and report if runtime assets are ready/already applied")
    mode.add_argument("--apply", action="store_true",
                      help="Replace runtime PNGs and atlas rects after strict preflight")
    args = parser.parse_args()
    validate_stage()
    manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    targets, edited_meta = files_and_expected(manifest)
    before = state(targets, manifest, edited_meta)
    if args.check:
        print(f"RUNTIME CHECK: {before.upper()} / 15 preserved atlas sprite IDs / 5 preserved standalone meta files.")
        return
    if before == "applied":
        print("RUNTIME APPLY: already applied; no files changed.")
        return

    backups = {path: path.read_bytes() for path in targets}
    try:
        for path, (_, candidate) in targets.items():
            path.write_bytes(candidate)
        if state(targets, manifest, edited_meta) != "applied":
            raise AssertionError("Post-apply validation failed")
    except Exception:
        for path, data in backups.items():
            path.write_bytes(data)
        raise
    print("RUNTIME APPLY: 15-sprite atlas and role16..20 PNGs updated; all IDs and importer metadata preserved.")


if __name__ == "__main__":
    main()

