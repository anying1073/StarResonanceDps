# DataTools

Data processing and management tools for StarResonance DPS analysis.

## Directory Structure

### Data/
Contains JSON data files (Extracted from client but the mapping is not accurate):
- `BuffTable.json`
- `DbmTable.json` 
- `MonsterTable.json`
- `RecountTable.json`
- `SkillTable.json` 

### Script/
Python scripts for data processing:
- `del_skill.py` Generates two intermediate files from the original data files.
- `merge_skill.py` Merges the two intermediate files and keeps conflicts/differences for manual review. You should choose the correct value for each conflicted key.
- `del_merge_skill.py` Converts the reviewed merge file into the final merged result.

## Usage

1. Run del_skill.py on the folder that contains the data files.
This script generates two intermediate JSON files.

2. Use those two generated files as the input for merge_skill.py.
This script creates a merged file with differences preserved in the following format:

```
{
  "key": [
    { "file1": "value1" },
    { "file2": "value2" }
  ],
  ...
}
```

3. Manually review the merged file and remove the unwanted differences so that each entry becomes a single selected result in the following format:

```
{
  "key": { "file1": "value1" },
  ...
}
```

4. Run del_merge_skill.py on the cleaned merge file.
This script resolves the selected entries and generates the final merged JSON file.


## Purpose

This toolset facilitates the management and merging of game data for DPS (Damage Per Second) analysis in StarResonance, particularly handling skill names and monster names data mapping.
