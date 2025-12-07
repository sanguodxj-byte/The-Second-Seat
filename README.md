# The Second Seat - AI Narrator Assistant

An AI-powered narrator assistant mod for RimWorld that features dynamic personality based on favorability mechanics and can execute batch commands to help manage your colony.

## Features

### ?? AI-Powered Narrator
- Integrates with OpenAI API or local LLM endpoints
- Observes your colony state and provides commentary
- Can execute game commands based on colony needs

### ?? Favorability System
The narrator's personality changes based on your relationship:
- **Hostile** (< -50): Mocking, refuses to help
- **Cold** (-50 to -10): Distant and unhelpful  
- **Neutral** (-10 to 30): Professional and objective
- **Warm** (30 to 60): Friendly and helpful
- **Devoted** (60 to 85): Protective and proactive
- **Infatuated** (> 85): Flirty and overly eager to help

### ? Batch Commands
The narrator can execute powerful batch operations:
- **BatchHarvest**: Designate all mature crops for harvest
- **BatchEquip**: Equip colonists with best available weapons
- **PriorityRepair**: Set damaged structures to priority repair
- **EmergencyRetreat**: Draft all colonists for retreat
- **ChangePolicy**: Suggest colony policy modifications

## Installation

1. Download and extract to your RimWorld mods folder
2. Enable in mod settings
3. Configure your API endpoint and key in mod options

## Configuration

### API Setup

#### OpenAI API
1. Get an API key from https://platform.openai.com/api-keys
2. In RimWorld mod settings, enter:
   - Endpoint: `https://api.openai.com/v1/chat/completions`
   - API Key: Your OpenAI API key

#### Local LLM (LM Studio, Ollama, etc.)
1. Set up your local LLM server
2. In RimWorld mod settings, enter:
   - Endpoint: Your local endpoint (e.g., `http://localhost:1234/v1/chat/completions`)
   - API Key: Leave empty or enter your local key

### Update Settings
- **Auto Updates**: Enable/disable automatic narrator updates
- **Update Interval**: How often the narrator checks colony status (1-60 minutes)

## Usage

### Opening the Narrator Interface
1. Select any colonist
2. Click the "AI Narrator" button in the gizmo bar
3. The narrator window will open

### Interacting with the Narrator
- Type messages in the input box to talk directly
- Click "Talk to Narrator" to send your message
- Click "Request Status Update" for a quick colony overview

### Understanding Favorability
- Favorability changes based on narrator interactions
- Successful command executions increase favorability
- Failed commands or ignoring suggestions decrease favorability
- Watch the colored bar to track your relationship

## Technical Architecture

### Phase 1: Communication Layer
- `LLMService`: Async HTTP client for LLM communication
- Non-blocking to prevent game freezing
- Supports OpenAI format and compatible endpoints

### Phase 2: Observation Layer  
- `GameStateObserver`: Captures colony state
- Token-efficient JSON serialization
- Tracks colonists, resources, threats, weather

### Phase 3: Favorability System
- `NarratorManager`: Manages AI personality
- 6 personality tiers with unique behaviors
- Dynamic system prompt generation

### Phase 4: Command Execution
- Command Pattern implementation
- 5 built-in batch operations
- Extensible for custom commands

## Development

### Adding Custom Commands

```csharp
using TheSecondSeat.Commands;

public class MyCustomCommand : BaseAICommand
{
    public override string ActionName => "MyCommand";
    
    public override string GetDescription()
    {
        return "Description of what this does";
    }
    
    public override bool Execute(string? target = null, object? parameters = null)
    {
        // Your command logic here
        return true;
    }
}

// Register in your mod initialization:
CommandParser.RegisterCommand("MyCommand", () => new MyCustomCommand());
```

### Project Structure
```
Source/TheSecondSeat/
©À©¤©¤ Commands/
©¦   ©À©¤©¤ IAICommand.cs           # Command interface
©¦   ©À©¤©¤ CommandParser.cs        # Command registry
©¦   ©¸©¤©¤ Implementations/
©¦       ©¸©¤©¤ ConcreteCommands.cs # Built-in commands
©À©¤©¤ Core/
©¦   ©¸©¤©¤ NarratorController.cs   # Main game loop
©À©¤©¤ LLM/
©¦   ©À©¤©¤ LLMDataStructures.cs    # API data models
©¦   ©¸©¤©¤ LLMService.cs           # HTTP client
©À©¤©¤ Narrator/
©¦   ©¸©¤©¤ NarratorManager.cs      # Favorability system
©À©¤©¤ Observer/
©¦   ©¸©¤©¤ GameStateObserver.cs    # State capture
©À©¤©¤ Settings/
©¦   ©¸©¤©¤ ModSettings.cs          # Configuration
©¸©¤©¤ UI/
    ©¸©¤©¤ NarratorWindow.cs       # User interface
```

## Troubleshooting

### "Connection failed" error
- Check your API endpoint URL
- Verify your API key is correct
- Ensure internet connection (for cloud APIs)
- For local LLMs, verify the server is running

### Narrator not responding
- Check RimWorld log for errors
- Verify API quota/credits
- Try clicking "Test Connection" in settings

### Commands not executing
- Check that colonists are available
- Verify resources exist for equipment commands
- Look for error messages in the log

## Credits

Developed using the GitHub Copilot prompt engineering strategy for RimWorld AI integration.

## License

Use freely for personal or commercial RimWorld mods.

## Support

For issues and feature requests, check the RimWorld mod forums.
