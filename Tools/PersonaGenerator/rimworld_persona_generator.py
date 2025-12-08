# -*- coding: utf-8 -*-
"""
RimWorld Persona Generator
Convert image analysis tags to RimWorld XML definitions

Features:
- Load visual tag to trait/skill mappings from JSON configuration
- Parse input tags and return consolidated traits and skills
- Handle conflicting traits based on priority
- Export to RimWorld XML format (PawnKindDef, BackstoryDef)
- Vision AI interface for image analysis
"""

import json
import os
import re
import random
import glob
from typing import List, Dict, Any, Optional, Tuple
from dataclasses import dataclass, field
from enum import Enum
import xml.etree.ElementTree as ET
from xml.dom import minidom


class TraitCategory(Enum):
    """Trait categories for conflict detection"""
    MOOD = "mood"           # Mood: Kind vs Bloodlust
    WORK_ETHIC = "work"     # Work ethic: Industrious vs Lazy
    SOCIAL = "social"       # Social: Sociable vs Annoying
    COMBAT = "combat"       # Combat: Brawler vs Careful
    MENTAL = "mental"       # Mental: Steadfast vs Nervous
    PHYSICAL = "physical"   # Physical: Tough vs Wimp
    UNCATEGORIZED = "uncategorized"


@dataclass
class TraitRule:
    """Trait rule definition"""
    trait_def: str                  # RimWorld TraitDef name
    degree: int = 0                 # Trait degree (-2 to +2)
    priority: int = 50              # Priority (0-100, higher means higher priority)
    category: TraitCategory = TraitCategory.UNCATEGORIZED
    conflicts_with: List[str] = field(default_factory=list)  # Conflicting traits list
    
    def to_dict(self) -> Dict[str, Any]:
        return {
            "traitDef": self.trait_def,
            "degree": self.degree,
            "priority": self.priority,
            "category": self.category.value,
            "conflictsWith": self.conflicts_with
        }


@dataclass
class SkillRule:
    """Skill rule definition"""
    skill_name: str                 # RimWorld skill name
    bonus: int = 0                  # Skill bonus (-20 to +20)
    passion: int = 0                # Passion level (0=None, 1=Minor, 2=Major)
    
    def to_dict(self) -> Dict[str, Any]:
        return {
            "skillName": self.skill_name,
            "bonus": self.bonus,
            "passion": self.passion
        }


@dataclass
class TagRule:
    """Tag rule - map visual tags to traits and skills"""
    tag: str
    traits: List[TraitRule] = field(default_factory=list)
    skills: List[SkillRule] = field(default_factory=list)
    description: str = ""


@dataclass
class PawnData:
    """Complete pawn data for XML export"""
    name: str
    def_name: str
    traits: List[Dict[str, Any]]
    skills: Dict[str, Dict[str, Any]]
    tags: List[str]
    backstory_title: str = ""
    backstory_description: str = ""
    
    @classmethod
    def from_resolve_result(cls, name: str, result: Dict[str, Any], 
                           backstory_title: str = "", backstory_desc: str = "") -> 'PawnData':
        """Create PawnData from resolve_stats result"""
        # Sanitize defName (remove spaces and special characters)
        def_name = cls.sanitize_def_name(name)
        
        return cls(
            name=name,
            def_name=def_name,
            traits=result.get("traits", []),
            skills=result.get("skills", {}),
            tags=result.get("matched_tags", []),
            backstory_title=backstory_title or f"{name}'s Past",
            backstory_description=backstory_desc or f"A mysterious figure known as {name}."
        )
    
    @staticmethod
    def sanitize_def_name(name: str) -> str:
        """Sanitize name to be a valid RimWorld defName"""
        # Remove non-alphanumeric characters (except underscore)
        sanitized = re.sub(r'[^a-zA-Z0-9_]', '', name.replace(' ', '_'))
        # Ensure it starts with a letter
        if sanitized and not sanitized[0].isalpha():
            sanitized = 'Pawn_' + sanitized
        return sanitized or 'UnnamedPawn'


class RimWorldPersonaGenerator:
    """
    RimWorld Persona Generator
    
    Convert visual tags from image analysis to RimWorld traits and skills,
    then export to RimWorld-compatible XML definitions.
    
    Usage example:
        generator = RimWorldPersonaGenerator("rules.json")
        tags = ['angry', 'bionic_eye', 'red_jacket', 'scar']
        result = generator.resolve_stats(tags)
        generator.export_xml(
            PawnData.from_resolve_result("Sideria", result),
            "output.xml"
        )
    """
    
    # Default trait conflict groups
    DEFAULT_CONFLICT_GROUPS = {
        "mood": ["Kind", "Bloodlust", "Psychopath"],
        "work": ["Industrious", "HardWorker", "Lazy", "Slothful"],
        "social": ["Sociable", "Annoying", "AnnoyingVoice"],
        "combat": ["Brawler", "Tough", "Wimp", "Careful"],
        "mental": ["Steadfast", "Nervous", "NervousBreakdown", "IronWilled"],
        "beauty": ["Beautiful", "Pretty", "Ugly", "Staggeringly Ugly"],
    }
    
    def __init__(self, rules_path: Optional[str] = None):
        """
        Initialize the generator
        
        Args:
            rules_path: JSON rules file path, use default rules if None
        """
        self.rules: Dict[str, TagRule] = {}
        self.conflict_groups: Dict[str, List[str]] = self.DEFAULT_CONFLICT_GROUPS.copy()
        
        if rules_path and os.path.exists(rules_path):
            self.load_rules(rules_path)
        else:
            self._load_default_rules()
    
    def load_rules(self, rules_path: str) -> bool:
        """
        Load rules from a JSON file
        
        Args:
            rules_path: JSON rules file path
            
        Returns:
            Whether the loading was successful
        """
        try:
            with open(rules_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            
            # Load conflict groups
            if "conflict_groups" in data:
                self.conflict_groups.update(data["conflict_groups"])
            
            # Load tag rules
            if "tag_rules" in data:
                for tag_data in data["tag_rules"]:
                    tag_rule = self._parse_tag_rule(tag_data)
                    if tag_rule:
                        self.rules[tag_rule.tag.lower()] = tag_rule
            
            print(f"[PersonaGenerator] Loaded {len(self.rules)} rules")
            return True
            
        except Exception as e:
            print(f"[PersonaGenerator] Failed to load rules: {e}")
            return False
    
    def _parse_tag_rule(self, data: Dict[str, Any]) -> Optional[TagRule]:
        """Parse a single tag rule"""
        try:
            tag = data.get("tag", "")
            if not tag:
                return None
            
            # Parse traits
            traits = []
            for trait_data in data.get("traits", []):
                trait = TraitRule(
                    trait_def=trait_data.get("traitDef", ""),
                    degree=trait_data.get("degree", 0),
                    priority=trait_data.get("priority", 50),
                    category=TraitCategory(trait_data.get("category", "uncategorized")),
                    conflicts_with=trait_data.get("conflictsWith", [])
                )
                if trait.trait_def:
                    traits.append(trait)
            
            # Parse skills
            skills = []
            for skill_data in data.get("skills", []):
                skill = SkillRule(
                    skill_name=skill_data.get("skillName", ""),
                    bonus=skill_data.get("bonus", 0),
                    passion=skill_data.get("passion", 0)
                )
                if skill.skill_name:
                    skills.append(skill)
            
            return TagRule(
                tag=tag,
                traits=traits,
                skills=skills,
                description=data.get("description", "")
            )
            
        except Exception as e:
            print(f"[PersonaGenerator] Failed to parse rule: {e}")
            return None
    
    def _load_default_rules(self):
        """Load default rules"""
        default_rules = [
            # === Mood/Emotion Tags ===
            TagRule(
                tag="angry",
                traits=[
                    TraitRule("Bloodlust", degree=0, priority=60, category=TraitCategory.MOOD),
                    TraitRule("Volatile", degree=0, priority=50, category=TraitCategory.MENTAL)
                ],
                skills=[
                    SkillRule("Melee", bonus=3, passion=1)
                ],
                description="Angry expression implies violent tendencies"
            ),
            TagRule(
                tag="kind",
                traits=[
                    TraitRule("Kind", degree=0, priority=70, category=TraitCategory.MOOD)
                ],
                skills=[
                    SkillRule("Social", bonus=2, passion=1)
                ],
                description="Friendly and compassionate demeanor"
            ),
            TagRule(
                tag="cold",
                traits=[
                    TraitRule("Psychopath", degree=0, priority=55, category=TraitCategory.MOOD)
                ],
                description="Cold expression implies lack of empathy"
            ),
            TagRule(
                tag="sad",
                traits=[
                    TraitRule("DepressiveMood", degree=0, priority=50, category=TraitCategory.MENTAL)
                ],
                description="Sadness, possibly linked to trauma"
            ),
            TagRule(
                tag="confident",
                traits=[
                    TraitRule("Steadfast", degree=0, priority=60, category=TraitCategory.MENTAL)
                ],
                skills=[
                    SkillRule("Social", bonus=2, passion=0)
                ],
                description="Confidence, possibly a leader or experienced individual"
            ),
            
            # === Physical Feature Tags ===
            TagRule(
                tag="scar",
                traits=[
                    TraitRule("Tough", degree=0, priority=55, category=TraitCategory.PHYSICAL)
                ],
                skills=[
                    SkillRule("Melee", bonus=2, passion=0),
                    SkillRule("Shooting", bonus=1, passion=0)
                ],
                description="Scar suggests experience in combat or dangerous situations"
            ),
            TagRule(
                tag="bionic_eye",
                traits=[
                    TraitRule("Transhumanist", degree=0, priority=65, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Shooting", bonus=4, passion=1),
                    SkillRule("Intellectual", bonus=2, passion=0)
                ],
                description="Bionic eye suggests proficiency with technology"
            ),
            TagRule(
                tag="cybernetic",
                traits=[
                    TraitRule("Transhumanist", degree=0, priority=70, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Intellectual", bonus=3, passion=1)
                ],
                description="Cybernetic parts suggest enhancement and technology affinity"
            ),
            TagRule(
                tag="muscular",
                traits=[
                    TraitRule("Tough", degree=0, priority=50, category=TraitCategory.PHYSICAL)
                ],
                skills=[
                    SkillRule("Melee", bonus=3, passion=1),
                    SkillRule("Construction", bonus=2, passion=0)
                ],
                description="Well-developed muscles indicate strength and physical labor capabilities"
            ),
            
            # === Clothing/Accessory Tags ===
            TagRule(
                tag="red_jacket",
                traits=[
                    TraitRule("Brawler", degree=0, priority=45, category=TraitCategory.COMBAT)
                ],
                skills=[
                    SkillRule("Melee", bonus=2, passion=0)
                ],
                description="Red jacket implies a combative or rebellious nature"
            ),
            TagRule(
                tag="military_uniform",
                traits=[
                    TraitRule("Tough", degree=0, priority=60, category=TraitCategory.PHYSICAL)
                ],
                skills=[
                    SkillRule("Shooting", bonus=4, passion=2),
                    SkillRule("Melee", bonus=2, passion=1)
                ],
                description="Military uniform suggests a military background"
            ),
            TagRule(
                tag="lab_coat",
                traits=[
                    TraitRule("Ascetic", degree=0, priority=40, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Intellectual", bonus=5, passion=2),
                    SkillRule("Medicine", bonus=3, passion=1)
                ],
                description="Lab coat suggests a scientific or medical background"
            ),
            TagRule(
                tag="crown",
                traits=[
                    TraitRule("Greedy", degree=0, priority=50, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Social", bonus=4, passion=2)
                ],
                description="Crown suggests nobility or leadership qualities"
            ),
            TagRule(
                tag="hood",
                traits=[
                    TraitRule("Careful", degree=0, priority=45, category=TraitCategory.COMBAT)
                ],
                skills=[
                    SkillRule("Shooting", bonus=2, passion=0)
                ],
                description="Hood implies stealth and caution"
            ),
            
            # === Special Visual Elements ===
            TagRule(
                tag="horns",
                traits=[
                    TraitRule("Aggressive", degree=0, priority=50, category=TraitCategory.MOOD)
                ],
                skills=[
                    SkillRule("Melee", bonus=2, passion=1)
                ],
                description="Horns imply aggression and a combative nature"
            ),
            TagRule(
                tag="wings",
                traits=[
                    TraitRule("Fast", degree=0, priority=55, category=TraitCategory.PHYSICAL)
                ],
                description="Wings suggest agility and swift movement"
            ),
            TagRule(
                tag="halo",
                traits=[
                    TraitRule("Kind", degree=0, priority=65, category=TraitCategory.MOOD)
                ],
                skills=[
                    SkillRule("Social", bonus=3, passion=1)
                ],
                description="Halo implies a benevolent and kind nature"
            ),
            TagRule(
                tag="tail",
                traits=[
                    TraitRule("NightOwl", degree=0, priority=40, category=TraitCategory.UNCATEGORIZED)
                ],
                description="Tail (Orc characteristic)"
            ),
            
            # === Background/Scene Tags ===
            TagRule(
                tag="fire_background",
                traits=[
                    TraitRule("Pyromaniac", degree=0, priority=55, category=TraitCategory.MENTAL)
                ],
                description="Fire background suggests arsonist tendencies"
            ),
            TagRule(
                tag="nature_background",
                traits=[
                    TraitRule("Ascetic", degree=0, priority=45, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Plants", bonus=3, passion=1),
                    SkillRule("Animals", bonus=2, passion=1)
                ],
                description="Nature background suggests a connection to the natural world"
            ),
            TagRule(
                tag="tech_background",
                traits=[
                    TraitRule("Transhumanist", degree=0, priority=50, category=TraitCategory.UNCATEGORIZED)
                ],
                skills=[
                    SkillRule("Intellectual", bonus=3, passion=1),
                    SkillRule("Crafting", bonus=2, passion=0)
                ],
                description="Technology background suggests a propensity for technology and innovation"
            ),
        ]
        
        for rule in default_rules:
            self.rules[rule.tag.lower()] = rule
        
        print(f"[PersonaGenerator] Loaded {len(self.rules)} default rules")
    
    def resolve_stats(self, tags: List[str]) -> Dict[str, Any]:
        """
        Resolve a list of tags and return merged traits and skills
        
        Args:
            tags: List of visual tags, e.g. ['angry', 'bionic_eye', 'red_jacket']
            
        Returns:
            A dictionary containing traits and skills
        """
        # Collect all matched traits and skills
        collected_traits: List[TraitRule] = []
        collected_skills: Dict[str, SkillRule] = {}
        matched_tags: List[str] = []
        unmatched_tags: List[str] = []
        
        # Normalize tags (to lowercase)
        normalized_tags = [tag.lower().strip() for tag in tags]
        
        for tag in normalized_tags:
            if tag in self.rules:
                rule = self.rules[tag]
                matched_tags.append(tag)
                
                # Collect traits
                collected_traits.extend(rule.traits)
                
                # Merge skills (stacking bonuses for the same skill)
                for skill in rule.skills:
                    skill_name = skill.skill_name
                    if skill_name in collected_skills:
                        # Merge skill bonuses
                        existing = collected_skills[skill_name]
                        collected_skills[skill_name] = SkillRule(
                            skill_name=skill_name,
                            bonus=existing.bonus + skill.bonus,
                            passion=max(existing.passion, skill.passion)  # Take the highest passion level
                        )
                    else:
                        collected_skills[skill_name] = SkillRule(
                            skill_name=skill.skill_name,
                            bonus=skill.bonus,
                            passion=skill.passion
                        )
            else:
                unmatched_tags.append(tag)
        
        # Resolve trait conflicts
        resolved_traits = self._resolve_trait_conflicts(collected_traits)
        
        return {
            "traits": [trait.to_dict() for trait in resolved_traits],
            "skills": {name: skill.to_dict() for name, skill in collected_skills.items()},
            "matched_tags": matched_tags,
            "unmatched_tags": unmatched_tags,
            "summary": self._generate_summary(resolved_traits, collected_skills)
        }
    
    def _resolve_trait_conflicts(self, traits: List[TraitRule]) -> List[TraitRule]:
        """Resolve trait conflicts"""
        if not traits:
            return []
        
        category_groups: Dict[TraitCategory, List[TraitRule]] = {}
        for trait in traits:
            category = trait.category
            if category not in category_groups:
                category_groups[category] = []
            category_groups[category].append(trait)
        
        resolved: List[TraitRule] = []
        
        for category, group_traits in category_groups.items():
            if category == TraitCategory.UNCATEGORIZED:
                resolved.extend(self._resolve_explicit_conflicts(group_traits))
            else:
                if group_traits:
                    best_trait = max(group_traits, key=lambda t: t.priority)
                    resolved.append(best_trait)
        
        resolved = self._resolve_conflict_groups(resolved)
        return resolved
    
    def _resolve_explicit_conflicts(self, traits: List[TraitRule]) -> List[TraitRule]:
        """Resolve explicitly declared conflicts"""
        if not traits:
            return []
        
        sorted_traits = sorted(traits, key=lambda t: t.priority, reverse=True)
        resolved = []
        excluded_traits = set()
        
        for trait in sorted_traits:
            if trait.trait_def in excluded_traits:
                continue
            resolved.append(trait)
            for conflict in trait.conflicts_with:
                excluded_traits.add(conflict)
        
        return resolved
    
    def _resolve_conflict_groups(self, traits: List[TraitRule]) -> List[TraitRule]:
        """Resolve conflicts using global conflict groups"""
        resolved = []
        handled_groups = set()
        
        for trait in traits:
            in_conflict_group = False
            
            for group_name, group_members in self.conflict_groups.items():
                if trait.trait_def in group_members:
                    in_conflict_group = True
                    
                    if group_name not in handled_groups:
                        group_traits = [t for t in traits if t.trait_def in group_members]
                        if group_traits:
                            best = max(group_traits, key=lambda t: t.priority)
                            resolved.append(best)
                            handled_groups.add(group_name)
                    break
            
            if not in_conflict_group:
                resolved.append(trait)
        
        # Remove duplicates
        seen = set()
        unique_resolved = []
        for trait in resolved:
            if trait.trait_def not in seen:
                seen.add(trait.trait_def)
                unique_resolved.append(trait)
        
        return unique_resolved
    
    def _generate_summary(self, traits: List[TraitRule], skills: Dict[str, SkillRule]) -> str:
        """Generate persona summary"""
        parts = []
        
        if traits:
            trait_names = [t.trait_def for t in traits]
            parts.append(f"Traits: {', '.join(trait_names)}")
        
        if skills:
            skill_parts = []
            for name, skill in skills.items():
                passion_str = " (Major)" if skill.passion >= 2 else (" (Minor)" if skill.passion == 1 else "")
                skill_parts.append(f"{name}+{skill.bonus}{passion_str}")
            parts.append(f"Skills: {', '.join(skill_parts)}")
        
        return " | ".join(parts) if parts else "No special attributes"
    
    # ==================== XML Export Methods ====================
    
    def export_xml(self, pawn_data: PawnData, output_path: str) -> bool:
        """
        Export pawn data to RimWorld XML format
        
        Creates both PawnKindDef and BackstoryDef in a single <Defs> root element.
        
        Args:
            pawn_data: PawnData object containing all pawn information
            output_path: Path to save the XML file
            
        Returns:
            True if export was successful, False otherwise
        """
        try:
            # Create root element
            root = ET.Element("Defs")
            
            # Add PawnKindDef
            self._create_pawn_kind_def(root, pawn_data)
            
            # Add BackstoryDef
            self._create_backstory_def(root, pawn_data)
            
            # Pretty print the XML
            xml_string = self._prettify_xml(root)
            
            # Ensure output directory exists
            os.makedirs(os.path.dirname(output_path) if os.path.dirname(output_path) else '.', exist_ok=True)
            
            # Write to file
            with open(output_path, 'w', encoding='utf-8') as f:
                f.write(xml_string)
            
            print(f"[PersonaGenerator] XML exported to: {output_path}")
            return True
            
        except Exception as e:
            print(f"[PersonaGenerator] XML export failed: {e}")
            return False
    
    def _create_pawn_kind_def(self, root: ET.Element, pawn_data: PawnData) -> ET.Element:
        """
        Create PawnKindDef element
        
        Structure:
        <PawnKindDef>
            <defName>...</defName>
            <label>...</label>
            <race>Human</race>
            <forcedTraits>
                <li>
                    <def>TraitDef</def>
                    <degree>0</degree>
                </li>
            </forcedTraits>
        </PawnKindDef>
        """
        pawn_kind = ET.SubElement(root, "PawnKindDef")
        
        # defName
        def_name = ET.SubElement(pawn_kind, "defName")
        def_name.text = pawn_data.def_name
        
        # label
        label = ET.SubElement(pawn_kind, "label")
        label.text = pawn_data.name
        
        # race (fixed as Human)
        race = ET.SubElement(pawn_kind, "race")
        race.text = "Human"
        
        # forcedTraits
        if pawn_data.traits:
            forced_traits = ET.SubElement(pawn_kind, "forcedTraits")
            for trait in pawn_data.traits:
                li = ET.SubElement(forced_traits, "li")
                
                trait_def = ET.SubElement(li, "def")
                trait_def.text = trait.get("traitDef", "")
                
                degree = trait.get("degree", 0)
                if degree != 0:
                    degree_elem = ET.SubElement(li, "degree")
                    degree_elem.text = str(degree)
        
        # Add comment about source tags
        if pawn_data.tags:
            comment = ET.Comment(f" Generated from tags: {', '.join(pawn_data.tags)} ")
            pawn_kind.insert(0, comment)
        
        return pawn_kind
    
    def _create_backstory_def(self, root: ET.Element, pawn_data: PawnData) -> ET.Element:
        """
        Create BackstoryDef element
        
        Structure:
        <BackstoryDef>
            <defName>...</defName>
            <slot>Adulthood</slot>
            <title>...</title>
            <titleShort>...</titleShort>
            <baseDesc>...</baseDesc>
            <skillGains>
                <Skill>value</Skill>
            </skillGains>
        </BackstoryDef>
        """
        backstory = ET.SubElement(root, "BackstoryDef")
        
        # defName (append _Backstory to distinguish)
        def_name = ET.SubElement(backstory, "defName")
        def_name.text = f"{pawn_data.def_name}_Backstory"
        
        # slot (Adulthood)
        slot = ET.SubElement(backstory, "slot")
        slot.text = "Adulthood"
        
        # title
        title = ET.SubElement(backstory, "title")
        title.text = pawn_data.backstory_title
        
        # titleShort
        title_short = ET.SubElement(backstory, "titleShort")
        # Create short title from first word or abbreviation
        short_title = pawn_data.backstory_title.split()[0] if pawn_data.backstory_title else pawn_data.name
        title_short.text = short_title[:12]  # Max 12 characters for short title
        
        # baseDesc
        base_desc = ET.SubElement(backstory, "baseDesc")
        base_desc.text = pawn_data.backstory_description
        
        # skillGains
        if pawn_data.skills:
            skill_gains = ET.SubElement(backstory, "skillGains")
            for skill_name, skill_data in pawn_data.skills.items():
                skill_elem = ET.SubElement(skill_gains, skill_name)
                skill_elem.text = str(skill_data.get("bonus", 0))
        
        # spawnCategories (optional, for spawning)
        spawn_categories = ET.SubElement(backstory, "spawnCategories")
        li = ET.SubElement(spawn_categories, "li")
        li.text = "Offworld"
        
        return backstory
    
    def _prettify_xml(self, elem: ET.Element) -> str:
        """
        Return a pretty-printed XML string for the Element.
        
        Args:
            elem: ElementTree Element
            
        Returns:
            Pretty-printed XML string with proper indentation
        """
        rough_string = ET.tostring(elem, encoding='unicode')
        reparsed = minidom.parseString(rough_string)
        
        # Get pretty printed string, skip the XML declaration
        pretty = reparsed.toprettyxml(indent="  ")
        
        # Remove the XML declaration line
        lines = pretty.split('\n')
        if lines[0].startswith('<?xml'):
            lines = lines[1:]
        
        # Remove empty lines
        lines = [line for line in lines if line.strip()]
        
        return '\n'.join(lines)
    
    # ==================== Vision AI Interface ====================
    
    def analyze_image(self, image_path: str) -> List[str]:
        """
        Analyze an image and return detected visual tags.
        
        This is a PLACEHOLDER method that simulates Vision AI output.
        In production, replace the simulation code with actual API calls.
        
        Supported AI backends (to be implemented):
        - WD14 Tagger (local, using onnxruntime)
        - CLIP (local, using transformers)
        - OpenAI GPT-4 Vision API
        - Google Gemini Vision API
        
        Args:
            image_path: Path to the image file
            
        Returns:
            List of detected tag strings
        
        Example implementation with real AI:
        ```python
        # === For WD14 Tagger (local) ===
        # import onnxruntime as ort
        # from PIL import Image
        # import numpy as np
        # 
        # def analyze_with_wd14(image_path):
        #     session = ort.InferenceSession("wd14_model.onnx")
        #     image = Image.open(image_path).resize((448, 448))
        #     input_array = np.array(image) / 255.0
        #     # ... process and return tags
        
        # === For OpenAI GPT-4 Vision ===
        # import openai
        # import base64
        # 
        # def analyze_with_gpt4v(image_path):
        #     with open(image_path, "rb") as f:
        #         base64_image = base64.b64encode(f.read()).decode()
        #     response = openai.ChatCompletion.create(
        #         model="gpt-4-vision-preview",
        #         messages=[{
        #             "role": "user",
        #             "content": [
        #                 {"type": "text", "text": "List visual tags for this character..."},
        #                 {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{base64_image}"}}
        #             ]
        #         }]
        #     )
        #     # Parse response and return tags
        ```
        """
        # Verify file exists
        if not os.path.exists(image_path):
            print(f"[PersonaGenerator] Warning: Image not found: {image_path}")
            return []
        
        # === SIMULATION MODE ===
        # For testing, return random valid tags from our rules
        # Replace this section with actual AI call in production
        
        available_tags = list(self.rules.keys())
        
        # Randomly select 3-6 tags for simulation
        num_tags = random.randint(3, min(6, len(available_tags)))
        simulated_tags = random.sample(available_tags, num_tags)
        
        print(f"[PersonaGenerator] SIMULATION: Detected tags for {os.path.basename(image_path)}")
        print(f"[PersonaGenerator]   Tags: {simulated_tags}")
        print(f"[PersonaGenerator]   NOTE: Replace with actual Vision AI for production use")
        
        return simulated_tags
    
    # ==================== Batch Processing ====================
    
    def process_image_folder(self, input_folder: str = "./input_images", 
                            output_folder: str = "./output_mods") -> Dict[str, Any]:
        """
        Process all images in a folder and generate XML definitions.
        
        Args:
            input_folder: Folder containing .png images
            output_folder: Folder to save generated XML files
            
        Returns:
            Summary dictionary with success/failure counts
        """
        # Ensure output folder exists
        os.makedirs(output_folder, exist_ok=True)
        
        # Find all PNG files
        image_files = glob.glob(os.path.join(input_folder, "*.png"))
        
        if not image_files:
            print(f"[PersonaGenerator] No .png files found in {input_folder}")
            return {"processed": 0, "failed": 0, "failed_files": []}
        
        print(f"[PersonaGenerator] Found {len(image_files)} images to process")
        print("=" * 60)
        
        processed = 0
        failed = 0
        failed_files = []
        
        for image_path in image_files:
            try:
                # Extract character name from filename
                filename = os.path.basename(image_path)
                char_name = os.path.splitext(filename)[0]
                
                print(f"\n[Processing] {filename}")
                
                # Analyze image
                tags = self.analyze_image(image_path)
                
                if not tags:
                    print(f"  Warning: No tags detected, skipping")
                    failed += 1
                    failed_files.append(filename)
                    continue
                
                # Resolve stats from tags
                result = self.resolve_stats(tags)
                
                # Create pawn data
                pawn_data = PawnData.from_resolve_result(
                    name=char_name,
                    result=result,
                    backstory_title=f"{char_name}'s Journey",
                    backstory_desc=f"{char_name} - a character generated from visual analysis. "
                                  f"Tags: {', '.join(result['matched_tags'])}."
                )
                
                # Export XML
                output_path = os.path.join(output_folder, f"{pawn_data.def_name}_Def.xml")
                
                if self.export_xml(pawn_data, output_path):
                    processed += 1
                    print(f"  Success: {result['summary']}")
                else:
                    failed += 1
                    failed_files.append(filename)
                    
            except Exception as e:
                print(f"  ERROR: {e}")
                failed += 1
                failed_files.append(os.path.basename(image_path))
        
        # Print summary
        print("\n" + "=" * 60)
        print(f"[PersonaGenerator] BATCH PROCESSING COMPLETE")
        print(f"  Processed: {processed}")
        print(f"  Failed: {failed}")
        if failed_files:
            print(f"  Failed files: {', '.join(failed_files)}")
        print("=" * 60)
        
        return {
            "processed": processed,
            "failed": failed,
            "failed_files": failed_files
        }
    
    # ==================== Utility Methods ====================
    
    def get_all_tags(self) -> List[str]:
        """Get all registered tags"""
        return list(self.rules.keys())
    
    def add_rule(self, rule: TagRule) -> None:
        """Dynamically add a rule"""
        self.rules[rule.tag.lower()] = rule
    
    def export_rules(self, output_path: str) -> bool:
        """Export rules to a JSON file"""
        try:
            data = {
                "conflict_groups": self.conflict_groups,
                "tag_rules": []
            }
            
            for tag, rule in self.rules.items():
                rule_data = {
                    "tag": rule.tag,
                    "description": rule.description,
                    "traits": [t.to_dict() for t in rule.traits],
                    "skills": [s.to_dict() for s in rule.skills]
                }
                data["tag_rules"].append(rule_data)
            
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, ensure_ascii=False, indent=2)
            
            print(f"[PersonaGenerator] Rules exported to: {output_path}")
            return True
            
        except Exception as e:
            print(f"[PersonaGenerator] Export failed: {e}")
            return False


# ==================== GUI Interface ====================

def create_gui():
    """
    Create a simple tkinter GUI for the Persona Generator.
    
    Features:
    - Select Image button
    - Editable tag text area
    - Generate XML button
    """
    try:
        import tkinter as tk
        from tkinter import filedialog, messagebox, scrolledtext
    except ImportError:
        print("[PersonaGenerator] tkinter not available. Run from command line instead.")
        return
    
    generator = RimWorldPersonaGenerator()
    
    class PersonaGeneratorGUI:
        def __init__(self, root):
            self.root = root
            self.root.title("RimWorld Persona Generator")
            self.root.geometry("600x500")
            
            self.current_image = None
            self.current_tags = []
            
            self._create_widgets()
        
        def _create_widgets(self):
            # Frame for image selection
            frame_top = tk.Frame(self.root, pady=10)
            frame_top.pack(fill=tk.X)
            
            self.btn_select = tk.Button(frame_top, text="Select Image", 
                                       command=self.select_image, width=15)
            self.btn_select.pack(side=tk.LEFT, padx=10)
            
            self.lbl_image = tk.Label(frame_top, text="No image selected")
            self.lbl_image.pack(side=tk.LEFT, padx=10)
            
            # Character name entry
            frame_name = tk.Frame(self.root, pady=5)
            frame_name.pack(fill=tk.X)
            
            tk.Label(frame_name, text="Character Name:").pack(side=tk.LEFT, padx=10)
            self.entry_name = tk.Entry(frame_name, width=30)
            self.entry_name.pack(side=tk.LEFT, padx=10)
            self.entry_name.insert(0, "MyCharacter")
            
            # Tags label
            tk.Label(self.root, text="Detected Tags (editable, comma-separated):").pack(anchor=tk.W, padx=10)
            
            # Editable tag text area
            self.txt_tags = scrolledtext.ScrolledText(self.root, height=6, width=70)
            self.txt_tags.pack(padx=10, pady=5, fill=tk.X)
            
            # Results preview
            tk.Label(self.root, text="Preview:").pack(anchor=tk.W, padx=10)
            
            self.txt_preview = scrolledtext.ScrolledText(self.root, height=10, width=70)
            self.txt_preview.pack(padx=10, pady=5, fill=tk.BOTH, expand=True)
            
            # Buttons frame
            frame_buttons = tk.Frame(self.root, pady=10)
            frame_buttons.pack(fill=tk.X)
            
            self.btn_analyze = tk.Button(frame_buttons, text="Analyze Tags", 
                                        command=self.analyze_tags, width=15)
            self.btn_analyze.pack(side=tk.LEFT, padx=10)
            
            self.btn_generate = tk.Button(frame_buttons, text="Generate XML", 
                                         command=self.generate_xml, width=15)
            self.btn_generate.pack(side=tk.LEFT, padx=10)
        
        def select_image(self):
            filepath = filedialog.askopenfilename(
                filetypes=[("PNG files", "*.png"), ("All files", "*.*")]
            )
            if filepath:
                self.current_image = filepath
                self.lbl_image.config(text=os.path.basename(filepath))
                
                # Auto-fill character name from filename
                char_name = os.path.splitext(os.path.basename(filepath))[0]
                self.entry_name.delete(0, tk.END)
                self.entry_name.insert(0, char_name)
                
                # Analyze image
                tags = generator.analyze_image(filepath)
                self.txt_tags.delete(1.0, tk.END)
                self.txt_tags.insert(tk.END, ", ".join(tags))
                
                # Auto-analyze
                self.analyze_tags()
        
        def analyze_tags(self):
            # Get tags from text area
            tags_text = self.txt_tags.get(1.0, tk.END).strip()
            tags = [t.strip() for t in tags_text.split(",") if t.strip()]
            
            if not tags:
                self.txt_preview.delete(1.0, tk.END)
                self.txt_preview.insert(tk.END, "No tags to analyze")
                return
            
            # Resolve stats
            result = generator.resolve_stats(tags)
            
            # Show preview
            preview = f"Character: {self.entry_name.get()}\n"
            preview += f"\nMatched Tags: {', '.join(result['matched_tags'])}\n"
            if result['unmatched_tags']:
                preview += f"Unmatched Tags: {', '.join(result['unmatched_tags'])}\n"
            preview += f"\nTraits:\n"
            for trait in result['traits']:
                preview += f"  - {trait['traitDef']}"
                if trait.get('degree', 0) != 0:
                    preview += f" (degree: {trait['degree']})"
                preview += "\n"
            preview += f"\nSkills:\n"
            for name, skill in result['skills'].items():
                passion = " (Major)" if skill['passion'] >= 2 else (" (Minor)" if skill['passion'] == 1 else "")
                preview += f"  - {name}: +{skill['bonus']}{passion}\n"
            preview += f"\nSummary: {result['summary']}"
            
            self.txt_preview.delete(1.0, tk.END)
            self.txt_preview.insert(tk.END, preview)
            
            self.current_tags = tags
        
        def generate_xml(self):
            if not self.current_tags:
                self.analyze_tags()
            
            char_name = self.entry_name.get().strip()
            if not char_name:
                messagebox.showerror("Error", "Please enter a character name")
                return
            
            # Get save location
            filepath = filedialog.asksaveasfilename(
                defaultextension=".xml",
                filetypes=[("XML files", "*.xml")],
                initialfile=f"{PawnData.sanitize_def_name(char_name)}_Def.xml"
            )
            
            if not filepath:
                return
            
            # Generate
            result = generator.resolve_stats(self.current_tags)
            pawn_data = PawnData.from_resolve_result(
                name=char_name,
                result=result,
                backstory_title=f"{char_name}'s Journey",
                backstory_desc=f"A character generated from visual analysis."
            )
            
            if generator.export_xml(pawn_data, filepath):
                messagebox.showinfo("Success", f"XML saved to:\n{filepath}")
            else:
                messagebox.showerror("Error", "Failed to generate XML")
    
    root = tk.Tk()
    app = PersonaGeneratorGUI(root)
    root.mainloop()


# ==================== CLI Entry Point ====================

if __name__ == "__main__":
    import sys
    
    # Check for GUI flag
    if len(sys.argv) > 1 and sys.argv[1] == "--gui":
        create_gui()
        sys.exit(0)
    
    # Get the script directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    
    # Create generator instance
    generator = RimWorldPersonaGenerator()
    
    # Check for batch processing mode
    if len(sys.argv) > 1 and sys.argv[1] == "--batch":
        input_folder = sys.argv[2] if len(sys.argv) > 2 else "./input_images"
        output_folder = sys.argv[3] if len(sys.argv) > 3 else "./output_mods"
        generator.process_image_folder(input_folder, output_folder)
        sys.exit(0)
    
    # Default: Run tests
    print("=" * 60)
    print("RimWorld Persona Generator Test")
    print("=" * 60)
    print("\nUsage:")
    print("  python rimworld_persona_generator.py           # Run tests")
    print("  python rimworld_persona_generator.py --gui     # Launch GUI")
    print("  python rimworld_persona_generator.py --batch [input] [output]  # Batch process")
    print()
    
    # Test cases
    test_cases = [
        ["angry", "bionic_eye", "red_jacket", "scar"],
        ["kind", "halo", "nature_background"],
        ["cold", "cybernetic", "military_uniform"],
    ]
    
    for tags in test_cases:
        print(f"\nInput tags: {tags}")
        result = generator.resolve_stats(tags)
        
        print(f"  Matched: {result['matched_tags']}")
        print(f"  Traits: {[t['traitDef'] for t in result['traits']]}")
        print(f"  Summary: {result['summary']}")
        print("-" * 40)
    
    # Test XML export
    print("\n" + "=" * 60)
    print("XML Export Test")
    print("=" * 60)
    
    test_result = generator.resolve_stats(["angry", "bionic_eye", "scar"])
    pawn_data = PawnData.from_resolve_result(
        name="Test Sideria",
        result=test_result,
        backstory_title="The Awakened One",
        backstory_desc="A mysterious being who emerged from the void, bearing scars of countless battles."
    )
    
    output_path = os.path.join(script_dir, "test_output.xml")
    generator.export_xml(pawn_data, output_path)
    
    # Show generated XML
    if os.path.exists(output_path):
        print("\nGenerated XML content:")
        print("-" * 40)
        with open(output_path, 'r', encoding='utf-8') as f:
            print(f.read())
