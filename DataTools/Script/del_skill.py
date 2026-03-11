#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
from pathlib import Path
from typing import Any, Dict, List


SKILL_TABLE = "SkillTable.json"
BUFF_TABLE = "BuffTable.json"
DDM_TABLE = "DbmTable.json"

OUTPUT_DIR = Path("output")
SKILL_OUTPUT_1 = OUTPUT_DIR / "SkillTable_1.json"
SKILL_OUTPUT_2 = OUTPUT_DIR / "SkillTable_2.json"
BUFF_OUTPUT = OUTPUT_DIR / "BuffTable_1.json"
DDM_OUTPUT = OUTPUT_DIR / "DdmTable_1.json"
UNUSED_NAME_1 = "场地标记01"
UNUSED_NAME_2 = "气刃突刺计数"


def normalize_to_list(value: Any) -> List[str]:
    """
    EffectIDs / SkillPreloadGroup を安全に list[str] にそろえる。
    空文字、None、空配列は除外する。
    """
    if value is None:
        return []

    if isinstance(value, list):
        result = []
        for item in value:
            if item is None:
                continue
            s = str(item).strip()
            if s != "":
                result.append(s)
        return result

    s = str(value).strip()
    return [s] if s != "" else []


def get_display_name(record: Dict[str, Any], unused_name) -> str:
    """
    値に使う名前を決める。
    Name が '场地标记01' なら NameDesign、それ以外は Name。
    """
    name = str(record.get("Name", "")).strip()
    name_design = str(record.get("NameDesign", "")).strip()

    if name == unused_name and name_design:
        return name_design
    return name

def process(data: Dict[str, Dict[str, Any]]) -> Dict[str, str]:
    """
    処理内容:
    1. 各レコードから EffectIDs を取得（空でなければ）
    2. 各レコードから SkillPreloadGroup を取得（空でなければ）
    3. 取得した SkillPreloadGroup の各要素をキーに、同じ JSON を辞書検索
    4. 検索先レコードの EffectIDs を取得（空でなければ）
    5. 保存するキーは
       - 元レコードの EffectIDs
       - 元レコードの SkillPreloadGroup
       - 検索先レコードの EffectIDs
    6. 値はすべて「最初の元レコード」の Name。
       ただしその Name が '场地标记01' なら NameDesign を使う
    7. 同じキーが重複した場合は、最初に入った値を保持する
    """
    result: Dict[str, str] = {}

    for record_key, record in data.items():
        if not isinstance(record, dict):
            continue

        display_name = get_display_name(record, UNUSED_NAME_1)
        if display_name == "":
            continue

        source_effect_ids = normalize_to_list(record.get("EffectIDs"))
        source_spg = normalize_to_list(record.get("SkillPreloadGroup"))

        for effect_id in source_effect_ids:
            result.setdefault(effect_id, display_name)

        for spg_id in source_spg:
            result.setdefault(spg_id, display_name)

        for spg_id in source_spg:
            target = data.get(spg_id)
            if not isinstance(target, dict):
                continue

            target_effect_ids = normalize_to_list(target.get("EffectIDs"))
            for effect_id in target_effect_ids:
                result.setdefault(effect_id, display_name)

    return result

def build_id_to_name_or_namedesign(data, unused_name):
    result = {}

    for record_id, record in data.items():
        if not isinstance(record, dict):
            continue

        display_name = get_display_name(record, unused_name)
        if display_name == "":
            continue

        result[str(record_id)] = display_name

    return result

def build_id_to_valuekey(data, valuekey):
    result = {}

    for record_id, record in data.items():
        if not isinstance(record, dict):
            continue

        value = str(record.get(valuekey, "")).strip()

        if value == "":
            continue

        result[str(record_id)] = value

    return result

def main() -> None:
    skill_table = Path(SKILL_TABLE)
    buff_table = Path(BUFF_TABLE)
    ddm_table = Path(DDM_TABLE)
    skill_output_1 = Path(SKILL_OUTPUT_1)
    skill_output_2 = Path(SKILL_OUTPUT_2)
    buff_output = Path(BUFF_OUTPUT)
    ddm_output = Path(DDM_OUTPUT)

    with skill_table.open("r", encoding="utf-8") as f:
        skill_data = json.load(f)

    with buff_table.open("r", encoding="utf-8") as f:
        buff_data = json.load(f)

    with ddm_table.open("r", encoding="utf-8") as f:
        ddm_data = json.load(f)

    if not isinstance(skill_data, dict):
        raise TypeError("SkillTable.json: トップレベルは dict（例: {'1202': {...}}）を想定しています。")
    
    if not isinstance(buff_data, dict):
        raise TypeError("BuffTable.json: トップレベルは dict（例: {'1202': {...}}）を想定しています。")
    
    if not isinstance(ddm_data, dict):
        raise TypeError("DdmTable.json: トップレベルは dict（例: {'1202': {...}}）を想定しています。")

    skill_result_1 = process(skill_data)
    skill_result_2 = build_id_to_name_or_namedesign(skill_data, UNUSED_NAME_1)
    buff_result = build_id_to_name_or_namedesign(buff_data, UNUSED_NAME_2)
    ddm_result = build_id_to_valuekey(ddm_data, "Content")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    
    with skill_output_1.open("w", encoding="utf-8") as f:
        json.dump(skill_result_1, f, ensure_ascii=False, indent=2)

    with skill_output_2.open("w", encoding="utf-8") as f:
        json.dump(skill_result_2, f, ensure_ascii=False, indent=2)
    
    with buff_output.open("w", encoding="utf-8") as f:
        json.dump(buff_result, f, ensure_ascii=False, indent=2)

    with ddm_output.open("w", encoding="utf-8") as f:
        json.dump(ddm_result, f, ensure_ascii=False, indent=2)

    print(f"Done: {SKILL_OUTPUT_1}")
    print(f"Done: {SKILL_OUTPUT_2}")
    print(f"Done: {BUFF_OUTPUT}")
    print(f"Done: {DDM_OUTPUT}")


if __name__ == "__main__":
    main()