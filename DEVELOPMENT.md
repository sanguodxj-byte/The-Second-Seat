# Development Guide - The Second Seat

## Architecture Overview

This mod follows a 4-phase architecture designed for AI-powered game interaction:

### Phase 1: Communication Layer (LLMService)
**Purpose**: Enable non-blocking communication with LLM endpoints

**Key Components**:
- `LLMService.cs`: Async HTTP client using HttpClient
- `LLMDataStructures.cs`: OpenAI-compatible request/response models

**Design Decisions**:
- Async/await pattern to prevent main thread blocking
- 30-second timeout to handle slow responses
- Fallback to plain text if JSON parsing fails
- Support for both cloud and local endpoints

**Example Usage**:
```csharp
var response = await LLMService.Instance.SendStateAndGetActionAsync(
    systemPrompt,
    gameStateJson,
    userMessage
);
```

---

### Phase 2: Observation Layer (GameStateObserver)
**Purpose**: Convert complex game state into LLM-readable format

**Key Components**:
- `GameStateSnapshot`: Simplified data model
- Token-efficient serialization
- Limits to prevent prompt overflow (e.g., max 10 colonists)

**What Gets Captured**:
1. Colony Info: wealth, biome, days passed
2. Colonists: mood, health, current job, injuries
3. Resources: food, wood, steel, medicine
4. Threats: raids, events
5. Weather: current conditions, temperature

**Token Optimization**:
- Only top 10 colonists
- Only top 3 injuries per colonist
- Simplified resource counts
- Percentage-based values (0-100)

---

### Phase 3: Favorability System (NarratorManager)
**Purpose**: Create dynamic AI personality that evolves with player relationship

**Favorability Tiers**:
```
Hostile      < -50   : Mocking, refuses help
Cold         -50~-10 : Distant, clinical
Neutral      -10~30  : Professional
Warm         30~60   : Friendly, helpful
Devoted      60~85   : Protective, proactive
Infatuated   >85     : Flirty, overly eager
```

**How Favorability Changes**:
- Command execution success: +2
- Command failure: -1 to -2
- Player ignores suggestions: -0.5 (manual tracking)
- Colony success/disaster: +5/-5 (can be triggered by events)

**System Prompt Generation**:
The `GetDynamicSystemPrompt()` method constructs different AI personas:

```csharp
// Example: Infatuated tier
"You are completely devoted to the player. You speak with affection, 
use flirty language, and are overly eager to help..."
```

---

### Phase 4: Command Execution (Command Pattern)
**Purpose**: Safe, extensible execution of AI-suggested actions

**Pattern Structure**:
```
IAICommand (Interface)
    ¡ý
BaseAICommand (Abstract base)
    ¡ý
ConcreteCommands (BatchHarvest, BatchEquip, etc.)
```

**Command Registry**:
Dictionary-based lookup enables:
- Runtime command registration
- Easy modding support
- Reflection-free execution

**Safety Features**:
- Try-catch wrappers in `ExecuteSafe()`
- Logging of all executions
- Favorability impact tracking
- Main thread execution guarantees

---

## Data Flow

```
User Action
    ¡ý
Trigger Update (Manual or Timer)
    ¡ý
[ASYNC] Capture Game State ¡ú GameStateObserver
    ¡ý
[ASYNC] Get System Prompt ¡ú NarratorManager
    ¡ý
[ASYNC] Send to LLM ¡ú LLMService
    ¡ý
[ASYNC] Receive Response
    ¡ý
[MAIN THREAD] Process Dialogue
    ¡ý
[MAIN THREAD] Execute Command ¡ú CommandParser
    ¡ý
Update Favorability
```

**Thread Safety**:
- All LLM communication happens in Task thread
- Game state modification happens only on main thread via `LongEventHandler`

---

## Extending the System

### Adding New Commands

1. Create command class:
```csharp
public class MyCommand : BaseAICommand
{
    public override string ActionName => "MyAction";
    
    public override bool Execute(string? target, object? parameters)
    {
        // Your logic
        return true;
    }
    
    public override string GetDescription()
    {
        return "What this command does";
    }
}
```

2. Register command:
```csharp
[StaticConstructorOnStartup]
public static class MyModInit
{
    static MyModInit()
    {
        CommandParser.RegisterCommand("MyAction", () => new MyCommand());
    }
}
```

3. Update system prompt to include new command in available list

### Adding New Observation Data

1. Add fields to `GameStateSnapshot`:
```csharp
public class GameStateSnapshot
{
    // Existing fields...
    public MyNewData customData { get; set; } = new MyNewData();
}
```

2. Capture in `GameStateObserver.CaptureSnapshot()`:
```csharp
snapshot.customData = CaptureMyData(map);
```

3. Update system prompt to explain new data to AI

### Modifying Favorability Logic

Add new event triggers:
```csharp
// In your event handler
var narrator = Current.Game.GetComponent<NarratorManager>();
narrator.ModifyFavorability(5f, "Player saved colonist from death");
```

---

## Performance Considerations

### Token Usage
- Average request: ~500 tokens (state) + 300 tokens (system prompt) = 800 tokens
- Average response: ~200 tokens
- Cost per update (GPT-4): ~$0.01

**Optimization Strategies**:
1. Limit colonist count (10 max)
2. Use percentages instead of absolute values
3. Omit zero-value resources
4. Sample injuries instead of full list

### Game Performance
- LLM requests are fully async (no frame drops)
- State capture happens in <50ms
- Command execution varies (harvest can be slow for large farms)

**Best Practices**:
1. Default 60-second update interval (3600 ticks)
2. Manual triggers for player-initiated conversations
3. Disable auto-updates for low-end systems

---

## Testing

### Unit Testing Commands
```csharp
// Create test map
var map = Find.CurrentMap;

// Execute command
var cmd = new BatchHarvestCommand();
var result = cmd.ExecuteSafe();

// Verify
Assert.IsTrue(result.Success);
```

### Testing LLM Integration
1. Use "Test Connection" in mod settings
2. Check RimWorld log for response JSON
3. Verify command parsing with debug mode

### Testing Favorability
```csharp
var narrator = Current.Game.GetComponent<NarratorManager>();

// Set specific tier for testing
narrator.ModifyFavorability(60f - narrator.Favorability, "Test");

// Verify prompt changes
var prompt = narrator.GetDynamicSystemPrompt();
Log.Message(prompt);
```

---

## Common Patterns

### Safe Map Access
```csharp
var map = Find.CurrentMap;
if (map == null)
{
    LogError("No active map");
    return false;
}
```

### Main Thread Execution
```csharp
UnityEngine.Application.CallOnMainThread(() => {
    // Code that modifies game state
});
```

### Async LLM Calls
```csharp
Task.Run(async () => {
    var response = await LLMService.Instance.SendStateAndGetActionAsync(...);
    // Process on main thread
    UnityEngine.Application.CallOnMainThread(() => ProcessResponse(response));
});
```

---

## Debugging

### Enable Debug Logging
1. Turn on "Debug Mode" in mod settings
2. Watch RimWorld log (`%AppData%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log`)

### Common Issues

**"No response from LLM"**
- Check API endpoint and key
- Verify network connection
- Check LLM service logs

**"Command failed to execute"**
- Verify preconditions (e.g., colonists available)
- Check for mod conflicts
- Review game state in save file

**"Favorability not changing"**
- Verify NarratorManager is registered as GameComponent
- Check ExposeData() is saving values
- Review favorability event list

---

## Future Extensions

### Potential Features
1. **Voice Integration**: Text-to-speech for narrator dialogue
2. **Visual Indicators**: Floating text over colony when narrator speaks
3. **Event Triggers**: React to specific game events (raids, marriages, deaths)
4. **Memory System**: Narrator remembers past events
5. **Multiple Narrators**: Choose different AI personalities

### Modding Support
The system is designed to be extended:
- Custom commands via CommandParser
- Custom observation data
- Custom favorability triggers
- Custom UI elements

---

## Performance Metrics

Typical performance on average colony (10 colonists):
- State capture: 20-40ms
- JSON serialization: 5-10ms  
- LLM request: 2-5 seconds (network dependent)
- Command execution: 10-100ms (depends on command)

Total update cycle: ~3-6 seconds
