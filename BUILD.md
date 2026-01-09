# Building SolidAI Add-In

## Prerequisites

### Required Software

1. **SOLIDWORKS** (2020 or later)
   - The SOLIDWORKS API DLLs must be present on your system
   - Default location: `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\`

2. **Visual Studio** (2019 or later)
   - Workload: **.NET desktop development**
   - Component: **.NET Framework 4.8 SDK**

3. **.NET Framework 4.8** (Runtime and SDK)

### SOLIDWORKS API DLLs

The project references three SOLIDWORKS Interop assemblies:
- `SolidWorks.Interop.sldworks.dll`
- `SolidWorks.Interop.swconst.dll`
- `SolidWorks.Interop.swpublished.dll`

These are referenced from the standard SOLIDWORKS installation path. If SOLIDWORKS is installed in a different location, update the `HintPath` in `SolidAIAddIn.csproj`.

## Building from Command Line

### 1. Restore NuGet Packages

```bash
cd SolidAIAddIn
dotnet restore SolidAIAddIn.sln
```

### 2. Build Solution

```bash
dotnet build SolidAIAddIn.sln --configuration Release
```

### 3. Build Output

The compiled DLL will be located at:
```
src\SolidAIAddIn\bin\Release\net48\SolidAIAddIn.dll
```

## Building from Visual Studio

1. Open `SolidAIAddIn.sln` in Visual Studio
2. Select **Release** configuration
3. Build → Build Solution (or press `Ctrl+Shift+B`)

## Registering the Add-In

After building, register the Add-In with SOLIDWORKS using RegAsm:

### For 64-bit SOLIDWORKS

```batch
cd src\SolidAIAddIn\bin\Release\net48
RegAsm /codebase SolidAIAddIn.dll
```

### Verification

1. Open SOLIDWORKS
2. Go to **Tools → Add-Ins**
3. You should see **SolidAI Add-In** in the list
4. Check the box to enable it

## Running Tests

### Command Line

```bash
dotnet test tests\SolidAIAddIn.Tests\SolidAIAddIn.Tests.csproj
```

### Visual Studio

1. Open Test Explorer (Test → Test Explorer)
2. Click **Run All**

**Note**: Unit tests use Moq to mock SOLIDWORKS interfaces, so they can run without SOLIDWORKS installed.

## Troubleshooting

### Build Error: Cannot Find SOLIDWORKS DLLs

**Problem**: `Could not locate the assembly "SolidWorks.Interop.sldworks"`

**Solution**:
1. Verify SOLIDWORKS is installed
2. Check the `HintPath` in `src/SolidAIAddIn/SolidAIAddIn.csproj`
3. Update paths if SOLIDWORKS is installed in a non-default location

Example fix:
```xml
<Reference Include="SolidWorks.Interop.sldworks">
  <HintPath>YOUR_SOLIDWORKS_PATH\api\redist\SolidWorks.Interop.sldworks.dll</HintPath>
  <EmbedInteropTypes>False</EmbedInteropTypes>
</Reference>
```

### Registration Error: Access Denied

**Problem**: RegAsm fails with access denied

**Solution**: Run Command Prompt or PowerShell as Administrator

### Add-In Not Appearing in SOLIDWORKS

**Problem**: Add-In doesn't show in Tools → Add-Ins

**Solutions**:
1. Ensure RegAsm completed successfully (no errors)
2. Check registry keys:
   - `HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}`
   - `HKEY_CURRENT_USER\SOFTWARE\SolidWorks\Addins\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}`
3. Restart SOLIDWORKS after registration
4. Check SOLIDWORKS version compatibility

### MCP Server Port Already in Use

**Problem**: Error starting MCP server - port 5000 in use

**Solution**: 
1. Change port in `SolidAIAddIn.cs`:
   ```csharp
   _mcpServer = new McpServer(_swWrapper, port: 5001); // Change port
   ```
2. Update client code to use new port
3. Rebuild and re-register

## Development Build

For development with debugging:

```bash
dotnet build SolidAIAddIn.sln --configuration Debug
```

Debug builds include:
- Debug symbols (.pdb files)
- Additional logging
- No code optimization

Attach Visual Studio debugger to SOLIDWORKS process:
1. Build in Debug configuration
2. Start SOLIDWORKS
3. In Visual Studio: Debug → Attach to Process
4. Select `SLDWORKS.exe`
5. Test Add-In functionality

## CI/CD Considerations

### Without SOLIDWORKS Installation

The project **cannot be fully built** in CI environments without SOLIDWORKS:
- SOLIDWORKS API DLLs are not redistributable
- Build will fail with missing assembly errors
- Unit tests will run successfully (using Moq)

### With SOLIDWORKS Installation

If SOLIDWORKS is available in CI:
1. Install SOLIDWORKS
2. Run `dotnet build`
3. Run `dotnet test`
4. Optional: Run RegAsm for registration testing

### Recommended CI Strategy

1. **Syntax/Structure Validation**: Use Roslyn analyzers without full build
2. **Unit Tests Only**: Run tests that don't require SOLIDWORKS
3. **Integration Tests**: Run on dedicated machine with SOLIDWORKS

## Clean Build

To perform a clean build:

```bash
# Clean
dotnet clean SolidAIAddIn.sln

# Restore
dotnet restore SolidAIAddIn.sln

# Build
dotnet build SolidAIAddIn.sln --configuration Release
```

Or in Visual Studio:
- Build → Clean Solution
- Build → Rebuild Solution

## Next Steps

After successful build:
1. Register the Add-In (see above)
2. Start SOLIDWORKS
3. Enable the Add-In in Tools → Add-Ins
4. Test MCP server: Run `examples/example_mcp_client.py`
5. Click "Test MCP Workflow" button in SOLIDWORKS

## Additional Resources

- [SOLIDWORKS API Documentation](https://help.solidworks.com/API)
- [.NET Framework 4.8 Documentation](https://docs.microsoft.com/en-us/dotnet/framework/)
- [RegAsm Tool Documentation](https://docs.microsoft.com/en-us/dotnet/framework/tools/regasm-exe-assembly-registration-tool)
