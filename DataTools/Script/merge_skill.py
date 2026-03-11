#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
from pathlib import Path
from collections import defaultdict


# ここを書き換える
OUTPUT_DIR = Path("output")
OUTPUT_FILE = Path("result.json")

# サブフォルダも含めるなら True
RECURSIVE = True


def numeric_sort_key(key):
    """
    キーを数値の小さい順で並べる。
    数字でないキーは後ろに回す。
    """
    try:
        return (0, int(str(key)))
    except (ValueError, TypeError):
        return (1, str(key))


def load_json_file(path: Path):
    with path.open("r", encoding="utf-8") as f:
        data = json.load(f)

    if not isinstance(data, dict):
        raise TypeError(f"トップレベルが dict ではありません: {path}")

    return data


def merge_json_files(input_dir: Path, output_file: Path, recursive: bool = True):
    pattern = "**/*.json" if recursive else "*.json"
    json_files = sorted(input_dir.glob(pattern))

    if not json_files:
        raise FileNotFoundError(f"JSON ファイルが見つかりません: {input_dir}")

    merged_lists = defaultdict(list)
    output_resolved = output_file.resolve()

    for json_path in json_files:
        # 出力先自身が入力フォルダ内にある場合、自分自身を再読込しない
        try:
            if json_path.resolve() == output_resolved:
                continue
        except FileNotFoundError:
            pass

        data = load_json_file(json_path)
        file_name = json_path.name   # 拡張子なしにしたいなら json_path.stem

        for raw_key, raw_value in data.items():
            key = str(raw_key)
            name_value = str(raw_value)

            item = {file_name: name_value}
            merged_lists[key].append(item)

    sorted_keys = sorted(merged_lists.keys(), key=numeric_sort_key)

    result = {}
    for key in sorted_keys:
        items = merged_lists[key]

        if len(items) == 1:
            result[key] = items[0]
        else:
            result[key] = items

    return result


def main():
    if not OUTPUT_DIR.exists():
        raise FileNotFoundError("出力フォルダが存在しません、先にほかのpyファイルを実行してください")
    if not OUTPUT_DIR.is_dir():
        raise NotADirectoryError(f"フォルダではありません: {OUTPUT_DIR}")

    result = merge_json_files(
        input_dir=OUTPUT_DIR,
        output_file=OUTPUT_FILE,
        recursive=RECURSIVE,
    )

    with OUTPUT_FILE.open("w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"Done: {OUTPUT_FILE}")


if __name__ == "__main__":
    main()