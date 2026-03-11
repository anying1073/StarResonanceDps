#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
from pathlib import Path


# ここを書き換える
INPUT_JSON = Path("result.json")
OUTPUT_JSON = Path("resolved_result.json")
SOURCE_JSON_DIR = Path("output")

# True にすると、参照先ファイルやキーが見つからない場合にエラーで止まる
STRICT_MISSING_FILE = True
STRICT_MISSING_KEY = True


def load_json_file(path: Path):
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)

    if not isinstance(data, dict):
        raise TypeError(f"トップレベルが dict ではありません: {path}")

    return data


def get_source_filename(ref_value, current_key: str) -> str:
    """
    想定形式:
      "1202": { "a.json": "何かの文字列" }

    この場合、使うのは dict のキー側 = "a.json"
    値側の "何かの文字列" は使わない
    """
    if not isinstance(ref_value, dict):
        raise TypeError(
            f"キー {current_key} の値が dict ではありません。想定形式: "
            f'"{current_key}": {{"xxx.json": "..."}}'
        )

    if len(ref_value) != 1:
        raise ValueError(
            f"キー {current_key} の値 dict の要素数が 1 ではありません: {ref_value}"
        )

    file_name = next(iter(ref_value.keys()))
    return str(file_name)


def main():
    if not INPUT_JSON.exists():
        raise FileNotFoundError(f"入力 JSON が見つかりません: {INPUT_JSON}")

    base_dir = SOURCE_JSON_DIR
    source_data = load_json_file(INPUT_JSON)

    result = {}

    # 同じファイルを何度も開かないようにキャッシュ
    file_cache = {}

    for raw_key, ref_value in source_data.items():
        current_key = str(raw_key)
        file_name = get_source_filename(ref_value, current_key)
        file_path = base_dir / file_name

        if file_name not in file_cache:
            if not file_path.exists():
                if STRICT_MISSING_FILE:
                    raise FileNotFoundError(
                        f"キー {current_key} が参照しているファイルが見つかりません: {file_path}"
                    )
                else:
                    continue

            file_cache[file_name] = load_json_file(file_path)

        target_data = file_cache[file_name]

        if current_key not in target_data:
                print(f"キー {current_key} が参照先ファイル {file_name} に存在しません")
                continue

        result[current_key] = target_data[current_key]

    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)

    with OUTPUT_JSON.open("w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"Done: {OUTPUT_JSON}")


if __name__ == "__main__":
    main()