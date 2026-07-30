#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
convert_manifest.py

manifest.json (원본, JsonUtility가 못 읽는 구조 — unlockReq: [["stat", n]] 튜플,
null 혼재)을 JsonUtility-safe 한 catalog.json 으로 변환한다.

- 입력: Assets/JackpotRun/Editor/SourceData/manifest.json
- 출력: Assets/JackpotRun/Resources/JackpotRun/catalog.json
- 규칙: null 금지 · 키 생략 금지 — 모든 엔트리가 모든 키를 갖는다.
  결측 기본값: 문자열 "" / 숫자 -1(단 pick.diff 는 0) / bool false / 배열 []
- 경로는 스크립트 위치 기준 상대 경로로 계산한다 (어느 CWD 에서 실행해도 동작).

표준 라이브러리만 사용 (Python 3.11).
"""

import hashlib
import json
import os
import re
import sys

# Windows 콘솔 인코딩 문제 회피 — stdout 을 UTF-8 로 강제 재설정.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except AttributeError:
    pass

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
INPUT_PATH = os.path.join(SCRIPT_DIR, "..", "Assets", "JackpotRun", "Editor", "SourceData", "manifest.json")
OUTPUT_PATH = os.path.join(SCRIPT_DIR, "..", "Assets", "JackpotRun", "Resources", "JackpotRun", "catalog.json")
SPRITES_ROOT = os.path.join(SCRIPT_DIR, "..", "Assets", "JackpotRun", "Resources", "JackpotRun")

CATEGORY_ORDER = ["char", "mac", "dev", "aug", "rel", "cur", "item", "ach"]


# ── 결측 기본값 헬퍼 ────────────────────────────────────────────
def s(v):
    """문자열 필드 — 결측/None 은 "" """
    return v if isinstance(v, str) else ""


def i(v, default=-1):
    """정수 필드 — 결측/None 은 default(기본 -1)"""
    if isinstance(v, bool):
        return default
    if isinstance(v, (int, float)):
        return int(v)
    return default


def f(v, default=-1.0):
    """실수 필드 — 결측/None 은 default(기본 -1.0)"""
    if isinstance(v, bool):
        return default
    if isinstance(v, (int, float)):
        return float(v)
    return default


def b(v):
    """bool 필드 — 결측/None 은 false"""
    return v if isinstance(v, bool) else False


def arr(v):
    """배열 필드 — 결측/None 은 []"""
    return v if isinstance(v, list) else []


def str_arr(v):
    return [x for x in arr(v) if isinstance(x, str)]


# ── pick 변환 ──────────────────────────────────────────────────
def convert_pick(pick):
    hasPick = isinstance(pick, dict)
    p = pick if hasPick else {}
    pick_obj = {
        "emoji": s(p.get("e")),
        "name": s(p.get("n")),
        "role": s(p.get("role")),
        "theme": s(p.get("theme")),
        "eff": s(p.get("eff")),
        "kind": s(p.get("kind")),
        "cmd": s(p.get("cmd")),
        "cool": s(p.get("cool")),
        "when": s(p.get("when")),
        "build": s(p.get("build")),
        "unlock": s(p.get("unlock")),
        "tags": str_arr(p.get("tags")),
        "pros": str_arr(p.get("pros")),
        "cons": str_arr(p.get("cons")),
        "diff": i(p.get("diff"), 0),
        "ceiling": i(p.get("ceiling"), -1),
        "stab": i(p.get("stab"), -1),
        "risk": i(p.get("risk"), -1),
    }
    return hasPick, pick_obj


# ── unlockReq 변환: [["stat", n], ...] → [{"stat":"stat","value":n}, ...] ──
def convert_unlock_req(ur):
    result = []
    for pair in arr(ur):
        if isinstance(pair, list) and len(pair) == 2:
            result.append({"stat": s(pair[0]), "value": i(pair[1], -1)})
    return result


# ── spritePath 변환: "Sprites/Characters/x.png" → "JackpotRun/Sprites/Characters/x" ──
def convert_sprite_path(sprite):
    if not isinstance(sprite, str) or not sprite:
        return ""
    path = sprite
    if path.lower().endswith(".png"):
        path = path[: -len(".png")]
    return "JackpotRun/" + path


def convert_entry(e):
    hasPick, pick_obj = convert_pick(e.get("pick"))
    return {
        "id": s(e.get("id")),
        "category": s(e.get("category")),
        "categoryLabel": s(e.get("categoryLabel")),
        "key": s(e.get("key")),
        "emoji": s(e.get("emoji")),
        "nameKo": s(e.get("nameKo")),
        "descKo": s(e.get("descKo")),
        "tier": s(e.get("tier")),
        "price": i(e.get("price")),
        "coinCost": i(e.get("coinCost")),
        "scoreMod": f(e.get("scoreMod")),
        "deviceKind": s(e.get("deviceKind")),
        "command": s(e.get("command")),
        "rare": b(e.get("rare")),
        "cooldown": i(e.get("cooldown")),
        "unlockAch": s(e.get("unlockAch")),
        "itemKind": s(e.get("itemKind")),
        "unlockReq": convert_unlock_req(e.get("unlockReq")),
        "spritePath": convert_sprite_path(e.get("sprite")),
        "hasPick": hasPick,
        "pick": pick_obj,
    }


def main():
    with open(INPUT_PATH, "rb") as fp:
        raw_src = fp.read()
    manifest = json.loads(raw_src.decode("utf-8"))

    src_entries = manifest.get("entries", [])
    entries = [convert_entry(e) for e in src_entries]

    catalog = {
        # 소스 내용 해시 — 소스가 안 바뀌면 재실행해도 diff가 생기지 않는다.
        "generatedAt": "manifest-sha1:" + hashlib.sha1(raw_src).hexdigest()[:12],
        "total": len(entries),
        "entries": entries,
    }

    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)
    with open(OUTPUT_PATH, "w", encoding="utf-8") as fp:
        json.dump(catalog, fp, ensure_ascii=False, indent=1)

    ok = validate(entries, manifest)
    if not ok:
        sys.exit(1)


def validate(entries, manifest):
    failed = False
    print("=== convert_manifest.py validation ===")
    print("total entries:", len(entries))
    expected_total = manifest.get("counts", {}).get("total", 294)
    if len(entries) != expected_total:
        print("FAIL: expected %d entries, got %d" % (expected_total, len(entries)))
        failed = True

    by_cat = {}
    for e in entries:
        by_cat[e["category"]] = by_cat.get(e["category"], 0) + 1
    print("by category:")
    for cat in CATEGORY_ORDER:
        print("  %-4s : %d" % (cat, by_cat.get(cat, 0)))
    other = set(by_cat) - set(CATEGORY_ORDER)
    if other:
        print("  unexpected categories:", sorted(other))

    # sprite existence check
    missing_declared = set(manifest.get("missingImage", []))
    ok = 0
    bad = []
    empty_but_not_declared_missing = []
    for e in entries:
        if e["spritePath"] == "":
            if e["id"] not in missing_declared:
                empty_but_not_declared_missing.append(e["id"])
            continue
        # spritePath like "JackpotRun/Sprites/Characters/char_x" -> file under Resources/JackpotRun/Sprites/Characters/char_x.png
        rel = e["spritePath"][len("JackpotRun/"):]
        full = os.path.join(SPRITES_ROOT, *rel.split("/")) + ".png"
        if os.path.isfile(full):
            ok += 1
        else:
            bad.append((e["id"], full))

    print("sprite files found: %d / %d (with spritePath)" % (ok, ok + len(bad)))
    print("entries with no spritePath (expected missing images): %d" % len(
        [e for e in entries if e["spritePath"] == ""]))
    if bad:
        print("FAIL: MISSING sprite files (unexpected):")
        for eid, full in bad:
            print("  -", eid, full)
        failed = True
    if empty_but_not_declared_missing:
        print("FAIL: entries with empty spritePath NOT in manifest.missingImage (unexpected):")
        for eid in empty_but_not_declared_missing:
            print("  -", eid)
        failed = True

    # null-value scan on the raw catalog (defensive re-check)
    with open(OUTPUT_PATH, "r", encoding="utf-8") as fp:
        raw = fp.read()
    null_count = len(re.findall(r":\s*null", raw))
    if null_count:
        print("FAIL: literal null value tokens found: %d" % null_count)
        failed = True
    else:
        print("null check: OK (no null value tokens in output)")

    print("=== validation %s ===" % ("FAILED" if failed else "done"))
    return not failed


if __name__ == "__main__":
    main()
