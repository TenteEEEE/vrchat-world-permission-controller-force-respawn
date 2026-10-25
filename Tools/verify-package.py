#!/usr/bin/env python3
"""Fast, dependency-free VPM package structure and policy check."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path


PACKAGE_NAME = "com.tentee.permission-controller-force-respawn"
EDITOR_ASSEMBLY = "TenteEEEE.PermissionControllerForceRespawn.Editor"
RUNTIME_ASSEMBLY = "TenteEEEE.PermissionControllerForceRespawn"
UDON_SCRIPTS = ("ForceRespawnPanel", "ForceRespawnRow")
REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
PACKAGE_ROOT = REPOSITORY_ROOT / "Packages" / PACKAGE_NAME
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def package_relative(path: Path) -> Path:
    return path.relative_to(PACKAGE_ROOT)


def guid(path: Path) -> str | None:
    match = re.search(r"^guid:\s*([0-9a-f]{32})\s*$", text(path), re.MULTILINE)
    return match.group(1) if match else None


def check_manifest() -> None:
    try:
        manifest = json.loads(text(PACKAGE_ROOT / "package.json"))
    except (OSError, json.JSONDecodeError) as exc:
        fail(f"invalid package.json: {exc}")
        return

    if manifest.get("name") != PACKAGE_NAME:
        fail(f"package name must be {PACKAGE_NAME}")
    version = manifest.get("version")
    if not isinstance(version, str) or not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        fail("package version must be SemVer")
    if manifest.get("unity") != "2022.3":
        fail("package must target Unity 2022.3")
    # Parameterized [NetworkCallable] events and NetworkCalling.CallingPlayer need Worlds SDK 3.8.1 or newer.
    if manifest.get("vpmDependencies", {}).get("com.vrchat.worlds") != "^3.8.1":
        fail("package must declare com.vrchat.worlds ^3.8.1 in vpmDependencies")


def check_meta_pairs() -> None:
    seen: dict[str, Path] = {}

    def ignored(path: Path) -> bool:
        return any(part.endswith("~") for part in package_relative(path).parts)

    for path in PACKAGE_ROOT.rglob("*"):
        if ignored(path) or path.suffix == ".meta" or path.name.startswith("."):
            continue
        if not Path(str(path) + ".meta").is_file():
            fail(f"missing .meta: {package_relative(path)}")

    for meta in PACKAGE_ROOT.rglob("*.meta"):
        if ignored(meta):
            continue
        if not Path(str(meta)[:-5]).exists():
            fail(f"orphan .meta: {package_relative(meta)}")
        value = guid(meta)
        if value is None:
            fail(f"missing GUID: {package_relative(meta)}")
        elif value in seen:
            fail(f"duplicate GUID {value}: {package_relative(seen[value])} and {package_relative(meta)}")
        else:
            seen[value] = meta


def check_asmdef() -> None:
    found: dict[str, dict] = {}
    for path in PACKAGE_ROOT.rglob("*.asmdef"):
        try:
            data = json.loads(text(path))
        except json.JSONDecodeError as exc:
            fail(f"invalid asmdef JSON {package_relative(path)}: {exc.msg}")
            continue
        found[data.get("name")] = data

    editor = found.get(EDITOR_ASSEMBLY)
    if editor is None:
        fail(f"required assembly missing: {EDITOR_ASSEMBLY}")
    else:
        if editor.get("includePlatforms") != ["Editor"]:
            fail(f"{EDITOR_ASSEMBLY} must be Editor-only")
        if RUNTIME_ASSEMBLY not in editor.get("references", []):
            fail(f"{EDITOR_ASSEMBLY} must reference {RUNTIME_ASSEMBLY}")

    runtime = found.get(RUNTIME_ASSEMBLY)
    if runtime is None:
        fail(f"required assembly missing: {RUNTIME_ASSEMBLY}")
    elif runtime.get("includePlatforms"):
        fail(f"{RUNTIME_ASSEMBLY} must not be platform-restricted (it runs in the world)")

    unexpected = set(found) - {EDITOR_ASSEMBLY, RUNTIME_ASSEMBLY}
    if unexpected:
        fail(f"unexpected assemblies: {sorted(unexpected)}")


def check_udonsharp_assets() -> None:
    """UdonSharp needs an assembly definition asset and a program asset per behaviour, pointing at our files."""
    runtime = PACKAGE_ROOT / "Runtime"
    asmdef_guid = guid(runtime / f"{RUNTIME_ASSEMBLY}.asmdef.meta")
    usharp_asmdef = runtime / f"{RUNTIME_ASSEMBLY}.asset"
    if not usharp_asmdef.is_file() or asmdef_guid is None or f"guid: {asmdef_guid}" not in text(usharp_asmdef):
        fail("UdonSharp assembly definition asset must reference the runtime asmdef")

    for name in UDON_SCRIPTS:
        script_guid = guid(runtime / f"{name}.cs.meta")
        program = runtime / f"{name}.asset"
        if not program.is_file() or script_guid is None:
            fail(f"{name} program asset is missing")
            continue
        match = re.search(r"sourceCsScript:\s*\{fileID: 11500000, guid: ([0-9a-f]{32})", text(program))
        if not match or match.group(1) != script_guid:
            fail(f"{name} program asset must point at {name}.cs")


def check_runtime_scripts() -> None:
    for path in (PACKAGE_ROOT / "Runtime").rglob("*.cs"):
        source = text(path)
        # Udon does not expose 64-bit integer operations such as remainder ("Method is not exposed to Udon").
        if re.search(r"\blong\b|\b\d+L\b", source):
            fail(f"runtime script uses 64-bit integers: {package_relative(path)}")
        # Network events are refused on behaviours with sync mode None; rows must stay unsynced.
        if "[UdonBehaviourSyncMode(" not in source:
            fail(f"runtime script must declare its sync mode: {package_relative(path)}")

    panel = PACKAGE_ROOT / "Runtime" / "ForceRespawnPanel.cs"
    source = text(panel)
    if "BehaviourSyncMode.Manual" not in source:
        fail("ForceRespawnPanel must use BehaviourSyncMode.Manual to receive network events")
    if "NetworkCalling.CallingPlayer" not in source:
        fail("ForceRespawnPanel must check the sender of ForceRespawn on the receiving client")


def check_no_upstream_source() -> None:
    """The package integrates by program-variable names only and must never ship PermissionController files."""
    for path in PACKAGE_ROOT.rglob("*"):
        if path.is_dir() or path.suffix == ".meta":
            continue
        try:
            source = text(path)
        except UnicodeDecodeError:
            fail(f"unexpected binary file: {package_relative(path)}")
            continue
        if "namespace Yodokorochan" in source or "class PermissionController :" in source:
            fail(f"upstream source signature found: {package_relative(path)}")


def check_release_files() -> None:
    required = (
        REPOSITORY_ROOT / ".github" / "workflows" / "release.yml",
        REPOSITORY_ROOT / "Packages" / ".gitignore",
        REPOSITORY_ROOT / "ProjectSettings" / "ProjectVersion.txt",
    )
    for path in required:
        if not path.is_file():
            fail(f"VPM project file missing: {path.relative_to(REPOSITORY_ROOT)}")


def main() -> int:
    try:
        if not PACKAGE_ROOT.is_dir():
            fail(f"package directory is missing: {PACKAGE_ROOT.relative_to(REPOSITORY_ROOT)}")
        else:
            check_manifest()
            check_meta_pairs()
            check_asmdef()
            check_udonsharp_assets()
            check_runtime_scripts()
            check_no_upstream_source()
        check_release_files()
    except OSError as exc:
        fail(f"file access error: {exc}")

    if ERRORS:
        print("PACKAGE CHECK FAILED")
        for error in ERRORS:
            print("- " + error)
        return 1
    print("PACKAGE CHECK PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
