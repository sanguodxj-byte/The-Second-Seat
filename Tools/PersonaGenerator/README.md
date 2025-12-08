# RimWorld Persona Generator

A Python tool that converts image analysis tags into RimWorld XML definitions (PawnKindDef & BackstoryDef).

## Features

- ? Convert visual tags to RimWorld Traits and Skills
- ? Export to valid RimWorld XML format
- ? Handle trait conflicts automatically (priority-based)
- ? Skill bonus stacking mechanism
- ? Vision AI interface (placeholder for WD14/CLIP/GPT-4V)
- ? Batch processing for multiple images
- ? Simple tkinter GUI

## Installation

```bash
# No external dependencies required for core functionality
python --version  # Python 3.7+

# Optional: For GUI
# tkinter is usually included with Python
```

## Quick Start

### Command Line

```bash
# Run tests and see example output
python rimworld_persona_generator.py

# Launch GUI
python rimworld_persona_generator.py --gui

# Batch process images
python rimworld_persona_generator.py --batch ./input_images ./output_mods
```

### Python API

```python
from rimworld_persona_generator import RimWorldPersonaGenerator, PawnData

# Create generator
generator = RimWorldPersonaGenerator()

# Input visual tags
tags = ['angry', 'bionic_eye', 'red_jacket', 'scar']

# Resolve to traits and skills
result = generator.resolve_stats(tags)
print(result['summary'])
# Output: Traits: Bloodlust, Volatile, Transhumanist, Tough | Skills: Melee+7 (Minor), Shooting+5 (Minor)

# Export to XML
pawn_data = PawnData.from_resolve_result(
    name="Sideria",
    result=result,
    backstory_title="The Awakened One",
    backstory_desc="A mysterious being from the void."
)
generator.export_xml(pawn_data, "Sideria_Def.xml")
```

## Generated XML Structure

```xml
<Defs>
  <PawnKindDef>
    <!-- Generated from tags: angry, bionic_eye, scar -->
    <defName>Sideria</defName>
    <label>Sideria</label>
    <race>Human</race>
    <forcedTraits>
      <li>
        <def>Bloodlust</def>
      </li>
      <li>
        <def>Transhumanist</def>
      </li>
    </forcedTraits>
  </PawnKindDef>
  <BackstoryDef>
    <defName>Sideria_Backstory</defName>
    <slot>Adulthood</slot>
    <title>The Awakened One</title>
    <titleShort>The</titleShort>
    <baseDesc>A mysterious being from the void.</baseDesc>
    <skillGains>
      <Melee>7</Melee>
      <Shooting>5</Shooting>
      <Intellectual>2</Intellectual>
    </skillGains>
    <spawnCategories>
      <li>Offworld</li>
    </spawnCategories>
  </BackstoryDef>
</Defs>
```

## Vision AI Integration

The `analyze_image()` method is a placeholder for real Vision AI backends£º

```python
# Currently returns random tags for testing
tags = generator.analyze_image("character.png")

# To integrate real AI, replace the simulation code with£º

# === WD14 Tagger (local) ===
# import onnxruntime as ort
# session = ort.InferenceSession("wd14_model.onnx")
# ... process image and return tags

# === OpenAI GPT-4 Vision ===
# import openai
# response = openai.ChatCompletion.create(
#     model="gpt-4-vision-preview",
#     messages=[{
#         "role": "user",
#         "content": [
#             {"type": "text", "text": "List visual tags for this character..."},
#             {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{base64_image}"}}
#         ]
#     }]
# )
```

## Conflict Resolution

### Priority System (0-100)
- Higher priority = kept when conflicts occur
- Same category traits: highest priority wins

### Trait Categories
| Category | Examples | Behavior |
|----------|----------|----------|
| `mood` | Kind, Bloodlust, Psychopath | Only one per pawn |
| `work` | Industrious, Lazy | Only one per pawn |
| `combat` | Brawler, Careful | Only one per pawn |
| `mental` | Steadfast, Nervous | Only one per pawn |
| `physical` | Tough, Wimp | Only one per pawn |
| `uncategorized` | Transhumanist, NightOwl | Can stack |

### Example Conflict Resolution
```
Input: ['angry', 'kind']
  - angry ¡ú Bloodlust (priority 60)
  - kind ¡ú Kind (priority 70)
Result: Kind wins (higher priority in same 'mood' category)
```

## Built-in Tags

### Emotion Tags
| Tag | Traits | Skills |
|-----|--------|--------|
| `angry` | Bloodlust, Volatile | Melee+3 |
| `kind` | Kind | Social+2 |
| `cold` | Psychopath | - |
| `sad` | DepressiveMood | Artistic+2 |
| `confident` | Steadfast | Social+2 |

### Physical Tags
| Tag | Traits | Skills |
|-----|--------|--------|
| `scar` | Tough | Melee+2, Shooting+1 |
| `bionic_eye` | Transhumanist | Shooting+4, Intellectual+2 |
| `cybernetic` | Transhumanist | Intellectual+3 |
| `muscular` | Tough | Melee+3, Construction+2 |

### Clothing Tags
| Tag | Traits | Skills |
|-----|--------|--------|
| `military_uniform` | Tough | Shooting+4, Melee+2 |
| `lab_coat` | Ascetic | Intellectual+5, Medicine+3 |
| `crown` | Greedy | Social+4 |
| `hood` | Careful | Shooting+2 |

### Special Elements
| Tag | Traits | Skills |
|-----|--------|--------|
| `horns` | Aggressive | Melee+2 |
| `wings` | Fast | - |
| `halo` | Kind | Social+3 |
| `tail` | NightOwl | - |

### Background Tags
| Tag | Traits | Skills |
|-----|--------|--------|
| `fire_background` | Pyromaniac | - |
| `nature_background` | Ascetic | Plants+3, Animals+2 |
| `tech_background` | Transhumanist | Intellectual+3, Crafting+2 |

## API Reference

### RimWorldPersonaGenerator

```python
class RimWorldPersonaGenerator:
    def __init__(self, rules_path: str = None)
    def load_rules(self, rules_path: str) -> bool
    def resolve_stats(self, tags: List[str]) -> Dict
    def export_xml(self, pawn_data: PawnData, output_path: str) -> bool
    def analyze_image(self, image_path: str) -> List[str]
    def process_image_folder(self, input: str, output: str) -> Dict
    def get_all_tags(self) -> List[str]
    def add_rule(self, rule: TagRule) -> None
    def export_rules(self, output_path: str) -> bool
```

### PawnData

```python
@dataclass
class PawnData:
    name: str
    def_name: str
    traits: List[Dict]
    skills: Dict[str, Dict]
    tags: List[str]
    backstory_title: str
    backstory_description: str
    
    @classmethod
    def from_resolve_result(cls, name, result, backstory_title, backstory_desc)
    
    @staticmethod
    def sanitize_def_name(name: str) -> str
```

## Batch Processing

```bash
# Create input folder with PNG images
mkdir input_images
# Place character images: Sideria.png, Guardian.png, etc.

# Run batch processing
python rimworld_persona_generator.py --batch ./input_images ./output_mods

# Output:
# [PersonaGenerator] Found 3 images to process
# [Processing] Sideria.png
#   Success: Traits: Bloodlust, Tough | Skills: Melee+5
# [Processing] Guardian.png
#   Success: Traits: Kind | Skills: Social+5
# ...
# BATCH PROCESSING COMPLETE
#   Processed: 3
#   Failed: 0
```

## GUI Mode

```bash
python rimworld_persona_generator.py --gui
```

Features:
- **Select Image** - Browse for PNG file
- **Tag Editor** - View and manually edit detected tags
- **Preview** - See resolved traits and skills before export
- **Generate XML** - Save to file

## Custom Rules (rules.json)

```json
{
  "conflict_groups": {
    "mood": ["Kind", "Bloodlust", "Psychopath"]
  },
  "tag_rules": [
    {
      "tag": "custom_tag",
      "description": "My custom visual tag",
      "traits": [
        {
          "traitDef": "CustomTrait",
          "degree": 0,
          "priority": 60,
          "category": "uncategorized",
          "conflictsWith": []
        }
      ],
      "skills": [
        {"skillName": "Crafting", "bonus": 5, "passion": 2}
      ]
    }
  ]
}
```

## Integration with The Second Seat Mod

This tool is designed to work with the RimWorld mod "The Second Seat":

1. **Image Analysis** - Analyze narrator portrait with AI
2. **Tag Extraction** - Get visual characteristics
3. **Persona Generation** - Convert to RimWorld attributes
4. **XML Export** - Generate NarratorPersonaDef-compatible data

## License

MIT License - Distributed with The Second Seat Mod
