#!/bin/sh
# 調整値が「持ち主の prefab」以外で上書きされていないかを検査する。
#
# 走りの調整値は base の Player.prefab が持ち、Player1/Player2 の Variant は見た目の差分だけを持つ。
# Race.prefab とレースシーンは、席の情報（playerIndex 等）以外を上書きしない。どこか 1 箇所で
# 上書きされると、base を直しても効かない／シーンごとに値が分岐する、が Inspector からは見えない。
#
# 検査対象は Assets/Scripts 配下のスクリプト（自前の MonoBehaviour）のプロパティだけ。
# マテリアル・VFX・Transform などの見た目の上書きは対象外。
#
# 使い方: sh .github/scripts/check-prefab-overrides.sh   （repo ルートで実行。標準ライブラリの python3 のみ使う）
set -eu

cd "$(git rev-parse --show-toplevel)"

exec python3 - "$@" <<'PY'
import fnmatch
import glob
import os
import re
import sys

# 対象ファイル → 上書きを許す (スクリプト名, トップレベルのフィールド名)
RULES = [
    ("Assets/Prefabs/Players/Player1.prefab", set()),
    ("Assets/Prefabs/Players/Player2.prefab", set()),
    ("Assets/Prefabs/Race/Race.prefab", {
        ("Racer", "playerIndex"),
        ("Racer", "startLateralOffset"),
        ("BoostConcentrationLine", "playerIndex"),
    }),
    ("Assets/Scenes/Race*.unity", set()),
]

SCRIPTS_DIR = "Assets/Scripts/"
# Variant 内のオブジェクトの fileID は「元 prefab での fileID XOR PrefabInstance の fileID」で振られる
FILEID_MASK = 0x7FFFFFFFFFFFFFFF

HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?")
REF = re.compile(r"\{fileID: (-?\d+)(?:, guid: ([0-9a-f]{32}))?")


def load_guids():
    guid_to_path = {}
    for meta in glob.glob("Assets/**/*.meta", recursive=True):
        with open(meta, encoding="utf-8", errors="replace") as f:
            for line in f:
                if line.startswith("guid: "):
                    guid_to_path[line[6:].strip()] = meta[: -len(".meta")]
                    break
    return guid_to_path


class UnityFile:
    def __init__(self, path):
        self.objects = {}
        self.instances = []  # (instance fileID, source prefab guid)
        self.modifications = []  # (line no, target fileID, target guid, propertyPath)
        cur = None
        in_mods = False
        target = None
        with open(path, encoding="utf-8", errors="replace") as f:
            for no, line in enumerate(f, 1):
                m = HEADER.match(line)
                if m:
                    cur = {"cls": int(m.group(1)), "stripped": bool(m.group(3)), "script": None,
                           "source": None, "corr": None}
                    self.objects[int(m.group(2))] = cur
                    cur_id = int(m.group(2))
                    in_mods = False
                    continue
                if cur is None:
                    continue
                s = line.strip()
                if s.startswith("m_Script:"):
                    r = REF.search(s)
                    if r and r.group(2):
                        cur["script"] = r.group(2)
                elif s.startswith("m_CorrespondingSourceObject:"):
                    r = REF.search(s)
                    if r and r.group(2):
                        cur["corr"] = (int(r.group(1)), r.group(2))
                elif s.startswith("m_SourcePrefab:"):
                    r = REF.search(s)
                    if r and r.group(2):
                        cur["source"] = r.group(2)
                        self.instances.append((cur_id, r.group(2)))
                elif s == "m_Modifications:":
                    in_mods = True
                elif in_mods and s.startswith("- target:"):
                    r = REF.search(s)
                    target = (int(r.group(1)), r.group(2)) if r else None
                elif in_mods and s.startswith("propertyPath:") and target:
                    self.modifications.append((no, target[0], target[1], s[len("propertyPath:"):].strip()))
                elif in_mods and s.startswith("m_"):
                    # m_RemovedComponents 等、m_Modifications の次のキーに入った
                    in_mods = False


class Resolver:
    def __init__(self, guid_to_path):
        self.guid_to_path = guid_to_path
        self.files = {}

    def file(self, guid):
        path = self.guid_to_path.get(guid)
        if path is None or not path.endswith(".prefab"):
            return None
        if path not in self.files:
            self.files[path] = UnityFile(path)
        return self.files[path]

    def script_of(self, guid, file_id, depth=0):
        """prefab(guid) 内の file_id が指すオブジェクトのスクリプト guid。MonoBehaviour でなければ None。"""
        uf = self.file(guid)
        if uf is None or depth > 16:
            return None
        obj = uf.objects.get(file_id)
        if obj is not None and not obj["stripped"]:
            return obj["script"] if obj["cls"] == 114 else None
        if obj is not None and obj["corr"]:
            return self.script_of(obj["corr"][1], obj["corr"][0], depth + 1)
        for inst_id, source in uf.instances:
            found = self.script_of(source, (file_id ^ inst_id) & FILEID_MASK, depth + 1)
            if found:
                return found
        return None


def main():
    guid_to_path = load_guids()
    resolver = Resolver(guid_to_path)
    errors = 0

    for pattern, allowed in RULES:
        paths = sorted(p for p in glob.glob(pattern) if fnmatch.fnmatch(p, pattern))
        for path in paths:
            for no, target_id, target_guid, prop in UnityFile(path).modifications:
                script_guid = resolver.script_of(target_guid, target_id)
                script_path = guid_to_path.get(script_guid or "", "")
                if not script_path.startswith(SCRIPTS_DIR):
                    continue
                script = os.path.splitext(os.path.basename(script_path))[0]
                field = re.split(r"[.\[]", prop, 1)[0]
                if (script, field) in allowed:
                    continue
                errors += 1
                print(f"::error file={path},line={no}::{script}.{prop} がここで上書きされている。"
                      f"調整値は持ち主の prefab（Player.prefab 等）側で変更する")

    if errors:
        print(f"{errors} 件の上書きが見つかった（docs/spec.md「プレイヤーのプレハブ構成」「レースシーンの構成」）")
        return 1
    print("OK: 許可外の上書きはない")
    return 0


sys.exit(main())
PY
