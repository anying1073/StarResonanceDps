import json
import argparse
from pathlib import Path

def merge_json_files(file1, file2, output_file, priority='file2'):
    """
    Merge two JSON files with configurable priority.
    
    Args:
        file1: Path to the first JSON file
        file2: Path to the second JSON file
        output_file: Path to save the merged result
        priority: Which file takes priority ('file1' or 'file2')
    """
    # Read both files
    with open(file1, 'r', encoding='utf-8') as f:
        data1 = json.load(f)
    
    with open(file2, 'r', encoding='utf-8') as f:
        data2 = json.load(f)
    
    # Merge based on priority
    if priority == 'file1':
        merged_data = {**data2, **data1}
        print(f"Priority: {file1}")
    else:
        merged_data = {**data1, **data2}
        print(f"Priority: {file2}")
    
    # Sort by keys for better readability
    merged_data = dict(sorted(merged_data.items(), key=lambda x: int(x[0])))
    
    # Write the merged result
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(merged_data, f, ensure_ascii=False, indent=4)
    
    # Print statistics
    print(f"File 1 entries: {len(data1)}")
    print(f"File 2 entries: {len(data2)}")
    print(f"Merged entries: {len(merged_data)}")
    print(f"Overlapping keys: {len(set(data1.keys()) & set(data2.keys()))}")
    print(f"\nMerged file saved to: {output_file}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Merge two JSON files with configurable priority."
    )
    parser.add_argument(
        "file1",
        type=str,
        help="Path to the first JSON file"
    )
    parser.add_argument(
        "file2",
        type=str,
        help="Path to the second JSON file"
    )
    parser.add_argument(
        "-o", "--output",
        type=str,
        default=None,
        help="Path to save the merged result (default: skill_names_merged.json in script directory)"
    )
    parser.add_argument(
        "-p", "--priority",
        type=str,
        choices=['file1', 'file2'],
        default='file2',
        help="Which file takes priority for duplicate keys (default: file2)"
    )
    
    args = parser.parse_args()
    
    # Set default output file if not provided
    if args.output is None:
        script_dir = Path(__file__).parent
        output_file = script_dir / "skill_names_merged.json"
    else:
        output_file = args.output
    
    # Perform merge
    merge_json_files(args.file1, args.file2, output_file, args.priority)
