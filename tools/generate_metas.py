#!/usr/bin/env python3
"""Create missing Unity .meta files with deterministic GUIDs.

Why: files added outside the Unity editor (by an AI agent in the cloud, a script, Git on
another machine) get a .meta file only when Unity first imports them, with a random GUID.
If two machines import the same new file before one of them commits, the GUIDs differ and
references break. Generating the .meta at creation time, with a GUID derived from the path,
avoids that.

Rules:
  * never overwrites an existing .meta (Unity owns them once created);
  * GUID = md5("poingmort:" + path relative to the project root);
  * scripts, asmdefs and folders get the standard importer block; other assets get a
    minimal .meta that Unity completes with default import settings.

Usage: python3 tools/generate_metas.py [--dry-run]
"""
import hashlib
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "Assets")

SKIP_SUFFIXES = (".meta", ".tmp", "~")
SKIP_NAMES = {".DS_Store", "Thumbs.db", "desktop.ini"}

MONO = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MINIMAL = """fileFormatVersion: 2
guid: {guid}
"""


def guid_for(rel_path: str) -> str:
    return hashlib.md5(("poingmort:" + rel_path.replace("\\", "/")).encode("utf-8")).hexdigest()


def template_for(path: str, is_dir: bool) -> str:
    if is_dir:
        return FOLDER
    ext = os.path.splitext(path)[1].lower()
    if ext == ".cs":
        return MONO
    if ext in (".asmdef", ".asmref"):
        return ASMDEF
    if ext in (".txt", ".md", ".json", ".csv", ".xml", ".bytes"):
        return TEXT
    return MINIMAL


def main() -> int:
    dry = "--dry-run" in sys.argv
    created = 0
    for dirpath, dirnames, filenames in os.walk(ASSETS):
        dirnames[:] = [d for d in dirnames if not d.startswith(".") and not d.endswith("~")]
        entries = [(d, True) for d in dirnames] + [(f, False) for f in filenames]
        for name, is_dir in entries:
            if name in SKIP_NAMES or name.endswith(SKIP_SUFFIXES) or name.startswith("."):
                continue
            full = os.path.join(dirpath, name)
            meta = full + ".meta"
            if os.path.exists(meta):
                continue
            rel = os.path.relpath(full, ROOT)
            content = template_for(full, is_dir).format(guid=guid_for(rel))
            created += 1
            if dry:
                print("would create", os.path.relpath(meta, ROOT))
            else:
                with open(meta, "w", newline="\n") as fh:
                    fh.write(content)
    print(f"{created} fichier(s) .meta {'à créer' if dry else 'créé(s)'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
