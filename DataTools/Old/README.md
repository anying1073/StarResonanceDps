# DataTools

Data processing and management tools for StarResonance DPS analysis.

## Directory Structure

### Data/
Contains JSON data files organized by category:

#### monster/
Monster and skill-related data files:
- `skill_names.json` - Base skill names data downloaded from https://github.com/dmlgzs/StarResonanceDamageCounter/blob/master/tables/skill_names.json
- `skill_names_new.json` - New skill names to be merged which is downloaded https://github.com/dmlgzs/StarResonanceDamageCounter/blob/master/tables/skill_names_new.json
- `skill_names_merged.json` - Result of merging skill names
- `skill_names_simplified.json` - Simplified version of skill_names.json
- `skill_name_mapping.json` - Mapping table for skill names (Extracted from client but the mapping is not accurate)

#### recount/
Recount addon related data:
- `recount_name_mapping.json` - Name mapping for Recount data

#### skill/
Skill-related mapping data:
- `monster_name_mapping.json` - Mapping table for monster names

### Script/
Python scripts for data processing:
- `merge_skills.py` - Utility script to merge two JSON files with configurable priority. Supports merging skill data from different sources and provides statistics on the merge operation.

## Usage

### Merging JSON Files

Use the `merge_skills.py` script to combine JSON data files:

```bash
python Script/merge_skills.py <file1> <file2> <output_file> [--priority file1|file2]
```

The script will:
- Merge two JSON files based on priority (default: file2)
- Sort entries by numeric keys
- Display statistics (entry counts, overlapping keys)
- Save the merged result to the specified output file

## Purpose

This toolset facilitates the management and merging of game data for DPS (Damage Per Second) analysis in StarResonance, particularly handling skill names, monster names, and recount data mapping.
