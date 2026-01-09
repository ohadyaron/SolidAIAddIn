# SolidAI Add-In

A professional SOLIDWORKS Add-In with embedded MCP (Model Context Protocol) server for executing high-level intent commands safely.

## Overview

This project provides a complete, production-ready skeleton for a SOLIDWORKS Add-In that implements:

- ✅ **ISwAddin Interface** - Full COM registration and lifecycle management
- ✅ **MCP Server** - Embedded JSON-RPC server for intent-based commands
- ✅ **Thread Safety** - All SOLIDWORKS API calls execute on STA thread
- ✅ **Intent-Level Commands** - High-level operations, not raw API calls
- ✅ **Error Handling** - Comprehensive validation and error recovery
- ✅ **Logging** - Structured logging with Serilog
- ✅ **Unit Tests** - Mock-based testing without SOLIDWORKS installation

## Architecture

### Three-Layer Design

```
┌─────────────────────────────────────────────────────────────┐
│                    External Client / UI                      │
│                  (HTTP/JSON-RPC requests)                     │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                 Layer 1: MCP Command Layer                    │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ McpServer (JSON-RPC)                                  │   │
│  │ - HTTP endpoint (port 5000)                           │   │
│  │ - Request validation                                  │   │
│  │ - Command routing                                     │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                     │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │ McpCommandHandler (Intent Translation)                │   │
│  │ - create_part_from_intent                             │   │
│  │ - apply_feature                                       │   │
│  │ - set_parameters                                      │   │
│  │ - rebuild                                             │   │
│  │ - export_step                                         │   │
│  │ - capture_preview                                     │   │
│  └──────────────────────┬───────────────────────────────┘   │
└─────────────────────────┼───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│           Layer 2: SOLIDWORKS API Wrapper                    │
│  ┌──────────────────────────────────────────────────────┐   │
│  │ SolidWorksApiWrapper (Thread Safety)                  │   │
│  │ - Lock-based synchronization                          │   │
│  │ - CreatePartDocument()                                │   │
│  │ - CreateRectangularExtrusion()                        │   │
│  │ - AddSimpleHole()                                     │   │
│  │ - RebuildModel()                                      │   │
│  │ - ExportToStep()                                      │   │
│  └──────────────────────┬───────────────────────────────┘   │
└─────────────────────────┼───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│               Layer 3: SOLIDWORKS API (COM)                  │
│  - ISldWorks                                                 │
│  - IModelDoc2                                                │
│  - IFeatureManager                                           │
│  - ISketchManager                                            │
└─────────────────────────────────────────────────────────────┘
```

### MCP Command Flow

```
Client Request → JSON-RPC → McpServer → CommandRouter → McpCommandHandler
                                                              │
                                                              ▼
                                                    SolidWorksApiWrapper
                                                    (Ensures STA thread)
                                                              │
                                                              ▼
                                                      SOLIDWORKS API
```

## Features

### Supported MCP Commands

| Command | Description | Parameters |
|---------|-------------|------------|
| `create_part_from_intent` | Create new part from intent description | intent, template, units |
| `apply_feature` | Apply feature (hole, fillet, chamfer) | featureType, parameters |
| `set_parameters` | Set/modify model parameters | parameters (dict) |
| `rebuild` | Rebuild model | option |
| `export_step` | Export to STEP format | outputPath, stepVersion |
| `capture_preview` | Capture preview image | width, height, format |
| `create_rectangular_extrusion` | Create rectangular extrusion | width, height, depth, plane |
| `add_hole_pattern` | Add hole pattern | diameter, depth, patternType, count |

### Example MCP Request

```json
{
  "jsonrpc": "2.0",
  "method": "create_rectangular_extrusion",
  "params": {
    "width": 100,
    "height": 50,
    "depth": 10,
    "plane": "Front"
  },
  "id": "req-001"
}
```

### Example MCP Response

```json
{
  "jsonrpc": "2.0",
  "result": {
    "commandId": "12345",
    "success": true,
    "message": "Rectangular extrusion created successfully",
    "data": {
      "width": 100,
      "height": 50,
      "depth": 10,
      "plane": "Front"
    },
    "timestamp": "2024-01-09T20:00:00Z"
  },
  "id": "req-001"
}
```

## Project Structure

```
SolidAIAddIn/
├── src/
│   └── SolidAIAddIn/
│       ├── SolidAIAddIn.cs              # Main Add-In class (ISwAddin)
│       ├── MCP/
│       │   ├── McpServer.cs             # HTTP/JSON-RPC server
│       │   ├── Handlers/
│       │   │   └── McpCommandHandler.cs # Intent command handlers
│       │   └── Models/
│       │       └── McpModels.cs         # DTOs and request/response types
│       └── SolidWorksWrapper/
│           └── SolidWorksApiWrapper.cs  # Safe SOLIDWORKS API wrapper
├── tests/
│   └── SolidAIAddIn.Tests/
│       └── McpCommandHandlerTests.cs    # Unit tests with mocks
└── SolidAIAddIn.sln                     # Visual Studio solution
```

## Getting Started

### Prerequisites

- **SOLIDWORKS 2020 or later** (for runtime)
- **Visual Studio 2019 or later** (.NET Framework 4.8)
- **.NET Framework 4.8 SDK**
- **SOLIDWORKS API DLLs** (located in `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\`)

### Building

1. Clone the repository:
   ```bash
   git clone https://github.com/ohadyaron/SolidAIAddIn.git
   cd SolidAIAddIn
   ```

2. Open solution:
   ```bash
   SolidAIAddIn.sln
   ```

3. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

4. Build the solution:
   ```bash
   dotnet build --configuration Release
   ```

### Registration

Register the Add-In with SOLIDWORKS using RegAsm:

```bash
cd src\SolidAIAddIn\bin\Release
RegAsm /codebase SolidAIAddIn.dll
```

Or use the Visual Studio Post-Build event (already configured in project).

### Running Tests

```bash
dotnet test
```

Tests use Moq to simulate SOLIDWORKS API without requiring installation.

## Usage

### From SOLIDWORKS UI

1. Open SOLIDWORKS
2. Go to **Tools → Add-Ins**
3. Check **SolidAI Add-In**
4. Click the **Test MCP Workflow** button in the toolbar

This executes the example workflow:
- Creates a new part
- Adds a 100x50x10mm rectangular extrusion
- Adds a ø10mm hole
- Rebuilds the model
- Exports to STEP format

### From External Client

Send HTTP POST requests to `http://localhost:5000` with JSON-RPC payload:

```python
import requests
import json

# Example: Create rectangular extrusion
payload = {
    "jsonrpc": "2.0",
    "method": "create_rectangular_extrusion",
    "params": {
        "width": 100,
        "height": 50,
        "depth": 10,
        "plane": "Front"
    },
    "id": "req-001"
}

response = requests.post("http://localhost:5000", json=payload)
result = response.json()
print(json.dumps(result, indent=2))
```

## Thread Safety

All SOLIDWORKS API calls **must execute on the STA thread** that created the COM objects. This Add-In ensures thread safety through:

1. **Wrapper Lock**: `SolidWorksApiWrapper` uses a lock object for synchronization
2. **Single-Threaded Execution**: All COM calls execute within locked sections
3. **Async Handlers**: MCP handlers use `Task.Run()` to avoid blocking HTTP thread, but delegate to wrapper

### Example Thread-Safe Pattern

```csharp
public OperationResult<IModelDoc2> CreatePartDocument(...)
{
    lock (_lock)  // ← Ensures STA thread execution
    {
        var modelDoc = _swApp.NewDocument(...);
        return OperationResult.Success(modelDoc);
    }
}
```

## Logging

Logs are written to:
```
%APPDATA%\SolidAI\logs\solidai-YYYYMMDD.log
```

Example log output:
```
2024-01-09 20:00:00.123 [INF] === SolidAI Add-In Starting ===
2024-01-09 20:00:00.456 [INF] SOLIDWORKS Version: 28.0
2024-01-09 20:00:00.789 [INF] MCP Server started on port 5000
2024-01-09 20:00:01.234 [INF] Executing MCP command: create_rectangular_extrusion
2024-01-09 20:00:02.567 [INF] ✓ Step 1: Part created
2024-01-09 20:00:03.890 [INF] ✓ Step 2: Rectangular extrusion created
```

## Error Handling

### Validation

All commands validate inputs before execution:

```csharp
if (request.Width <= 0 || request.Height <= 0)
{
    return CreateErrorResponse("Dimensions must be positive");
}
```

### Rollback

On failure, the wrapper returns detailed error information:

```csharp
return OperationResult<T>.Failure($"Failed with errors={errors}");
```

### Exception Handling

All layers catch and log exceptions:

```csharp
catch (Exception ex)
{
    Log.Error(ex, "Error in create_part_from_intent");
    return CreateErrorResponse(ex.Message);
}
```

## Extending

### Adding New MCP Commands

1. **Define DTO** in `MCP/Models/McpModels.cs`:
   ```csharp
   public class MyNewCommandRequest : McpCommandRequest
   {
       public override string CommandType => "my_new_command";
       public string MyParameter { get; set; }
   }
   ```

2. **Add Handler** in `MCP/Handlers/McpCommandHandler.cs`:
   ```csharp
   public async Task<McpCommandResponse> HandleMyNewCommand(MyNewCommandRequest request)
   {
       // Implementation
   }
   ```

3. **Register Route** in `MCP/McpServer.cs`:
   ```csharp
   "my_new_command" => await _commandHandler.HandleMyNewCommand(
       paramsJson.ToObject<MyNewCommandRequest>()),
   ```

### Adding SOLIDWORKS Operations

Add methods to `SolidWorksWrapper/SolidWorksApiWrapper.cs`:

```csharp
public OperationResult<IFeature> MyNewOperation(IModelDoc2 modelDoc, params)
{
    lock (_lock)
    {
        // Safe SOLIDWORKS API calls here
    }
}
```

## Future Extensions

- [ ] **CadQuery Integration**: Import/export CadQuery scripts
- [ ] **STEP Import**: Parse and import STEP files
- [ ] **Advanced Features**: Sweep, loft, patterns
- [ ] **Assembly Support**: Create and manage assemblies
- [ ] **Configuration Management**: Handle design configurations
- [ ] **Batch Operations**: Execute multiple commands atomically
- [ ] **Rollback Support**: Transaction-style operations

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new features
4. Ensure all tests pass
5. Submit a pull request

## Support

For issues or questions:
- Open an issue on GitHub
- Check logs in `%APPDATA%\SolidAI\logs\`

## Acknowledgments

- Built with SOLIDWORKS API
- Uses Serilog for logging
- Kestrel for HTTP server
- Newtonsoft.Json for JSON-RPC
- xUnit, Moq, and FluentAssertions for testing
