# NarratorPersonaDef.cs Modifications

## ?? File Location
```
Source/TheSecondSeat/PersonaGeneration/NarratorPersonaDef.cs
```

---

## ? Changes Required

### Add Two New Fields

**Location**: After the `toneTags` field declaration (around line 56)

```csharp
public List<string> toneTags = new List<string>();  // 语气标签（用于 LLM Prompt）

// ? ADD THESE TWO FIELDS:

/// <summary>
/// User-provided personality tags (for guided generation)
/// </summary>
public List<string> personalityTags = new List<string>();

/// <summary>
/// User-provided supplementary biography (for guided generation)
/// </summary>
public string supplementaryBiography = "";

// ... rest of existing code ...
public List<string> forbiddenWords = new List<string>();  // 禁用词
```

---

## ?? Complete Modified Section

### Before (Original Code)
```csharp
public List<string> toneTags = new List<string>();  // 语气标签（用于 LLM Prompt）
public List<string> forbiddenWords = new List<string>();  // 禁用词
```

### After (Modified Code)
```csharp
public List<string> toneTags = new List<string>();  // 语气标签（用于 LLM Prompt）

/// <summary>
/// User-provided personality tags (for guided generation)
/// Used to override or guide AI personality inference
/// </summary>
public List<string> personalityTags = new List<string>();

/// <summary>
/// User-provided supplementary biography (for guided generation)
/// This text prioritizes over visual-based personality inference
/// </summary>
public string supplementaryBiography = "";

public List<string> forbiddenWords = new List<string>();  // 禁用词
```

---

## ?? Purpose of New Fields

### `personalityTags`
- **Type**: `List<string>`
- **Purpose**: Store user-provided personality keywords (e.g., "善良", "聪明", "幽默")
- **Usage**: 
  * Displayed as colored badges in `PersonaSelectionWindow`
  * Guides AI personality inference during image analysis
  * Overrides visual-based personality suggestions

### `supplementaryBiography`
- **Type**: `string`
- **Purpose**: Store user-provided custom biography text
- **Usage**:
  * Used alongside AI-generated biography
  * Prioritized for personality trait extraction
  * Merged with visual analysis results

---

## ?? Example Usage

### In XML Def
```xml
<TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
  <defName>CustomPersona_Abc123</defName>
  <narratorName>温柔的守护者</narratorName>
  
  <!-- NEW FIELDS -->
  <personalityTags>
    <li>善良</li>
    <li>保护</li>
    <li>温柔</li>
  </personalityTags>
  <supplementaryBiography>一位总是保护弱小的守护者，性格温柔</supplementaryBiography>
  
  <!-- Existing fields -->
  <biography>...</biography>
  <overridePersonality>Protective</overridePersonality>
</TheSecondSeat.PersonaGeneration.NarratorPersonaDef>
```

### In C# Code
```csharp
var persona = new NarratorPersonaDef
{
    defName = "CustomPersona_Abc123",
    narratorName = "温柔的守护者",
    personalityTags = new List<string> { "善良", "保护", "温柔" },
    supplementaryBiography = "一位总是保护弱小的守护者，性格温柔",
    biography = "...",
    overridePersonality = "Protective"
};
```

---

## ? Verification

### Check List
- [ ] Added `personalityTags` field declaration
- [ ] Added `supplementaryBiography` field declaration
- [ ] Both fields initialized with default values (`new List<string>()` and `""`)
- [ ] Added XML comments for documentation
- [ ] No compilation errors
- [ ] Fields accessible in other classes

### Build Test
```powershell
cd Source
dotnet build TheSecondSeat.csproj
```

Expected: ? Build succeeded with 0 errors

---

**Status**: ? Ready to apply  
**Compatibility**: Backward compatible (existing saves will have empty values for new fields)
