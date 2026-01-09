# MCP Command Reference

Quick reference for all available MCP commands in the SolidAI Add-In.

## Base URL
```
http://localhost:5000
```

## JSON-RPC Format

All requests follow JSON-RPC 2.0 format:
```json
{
  "jsonrpc": "2.0",
  "method": "<command_name>",
  "params": { ... },
  "id": "<unique_id>"
}
```

## Available Commands

### 1. create_part_from_intent

Create a new part document from natural language intent.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "create_part_from_intent",
  "params": {
    "intent": "Create a mounting bracket with base plate",
    "template": "Part",
    "units": "MMGS"
  },
  "id": "req-001"
}
```

**Parameters:**
- `intent` (string, required): Natural language description
- `template` (string, optional): "Part", "Assembly", or "Drawing" (default: "Part")
- `units` (string, optional): "MMGS", "IPS", or "CGS" (default: "MMGS")

---

### 2. create_rectangular_extrusion

Create a simple rectangular extrusion feature.

**Request:**
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
  "id": "req-002"
}
```

**Parameters:**
- `width` (number, required): Rectangle width in mm
- `height` (number, required): Rectangle height in mm
- `depth` (number, required): Extrusion depth in mm
- `plane` (string, optional): "Front", "Top", or "Right" (default: "Front")

---

### 3. apply_feature

Apply a feature to the current model (hole, fillet, chamfer).

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "apply_feature",
  "params": {
    "featureType": "hole",
    "parameters": {
      "diameter": 10,
      "depth": 20
    }
  },
  "id": "req-003"
}
```

**Parameters:**
- `featureType` (string, required): "hole", "fillet", "chamfer", or "extrude"
- `parameters` (object, required): Feature-specific parameters
- `targetEntities` (array, optional): Target edges/faces/vertices

**Feature-Specific Parameters:**

**Hole:**
```json
{
  "diameter": 10,
  "depth": 20
}
```

**Fillet:**
```json
{
  "radius": 5,
  "edges": ["edge1", "edge2"]
}
```

**Chamfer:**
```json
{
  "distance": 2,
  "angle": 45
}
```

---

### 4. add_hole_pattern

Add a pattern of holes to the model.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "add_hole_pattern",
  "params": {
    "diameter": 10,
    "depth": 20,
    "patternType": "Linear",
    "count": 4,
    "spacing": 15
  },
  "id": "req-004"
}
```

**Parameters:**
- `diameter` (number, required): Hole diameter in mm
- `depth` (number, required): Hole depth in mm
- `patternType` (string, optional): "Linear" or "Circular" (default: "Linear")
- `count` (number, optional): Number of holes (default: 4)
- `spacing` (number, optional): Distance between holes in mm (default: 10)

---

### 5. set_parameters

Set or modify model parameters/dimensions.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "set_parameters",
  "params": {
    "parameters": {
      "Length": 100,
      "Width": 50,
      "Height": 30
    }
  },
  "id": "req-005"
}
```

**Parameters:**
- `parameters` (object, required): Dictionary of parameter names and values

---

### 6. rebuild

Rebuild/regenerate the model.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "rebuild",
  "params": {
    "option": "Active"
  },
  "id": "req-006"
}
```

**Parameters:**
- `option` (string, optional): "Active", "All", or "TopLevelOnly" (default: "Active")

---

### 7. export_step

Export the current model to STEP format.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "export_step",
  "params": {
    "outputPath": "C:\\output\\part.step",
    "stepVersion": "AP214",
    "includeAttributes": true
  },
  "id": "req-007"
}
```

**Parameters:**
- `outputPath` (string, required): Full path for output file
- `stepVersion` (string, optional): "AP203", "AP214", or "AP242" (default: "AP214")
- `includeAttributes` (boolean, optional): Include PMI and attributes (default: true)

---

### 8. capture_preview

Capture a preview image of the current view.

**Request:**
```json
{
  "jsonrpc": "2.0",
  "method": "capture_preview",
  "params": {
    "outputPath": "C:\\output\\preview.png",
    "width": 1920,
    "height": 1080,
    "format": "PNG"
  },
  "id": "req-008"
}
```

**Parameters:**
- `outputPath` (string, optional): Path to save image (if null, returns base64)
- `width` (number, optional): Image width in pixels (default: 1024)
- `height` (number, optional): Image height in pixels (default: 768)
- `format` (string, optional): "PNG", "JPEG", or "BMP" (default: "PNG")

---

## Response Format

### Success Response

```json
{
  "jsonrpc": "2.0",
  "result": {
    "commandId": "abc-123",
    "success": true,
    "message": "Operation completed successfully",
    "data": {
      "width": 100,
      "height": 50
    },
    "timestamp": "2024-01-09T20:00:00Z"
  },
  "id": "req-001"
}
```

### Error Response

```json
{
  "jsonrpc": "2.0",
  "result": {
    "commandId": "abc-123",
    "success": false,
    "message": "Command failed",
    "errorDetails": "No active document",
    "timestamp": "2024-01-09T20:00:00Z"
  },
  "id": "req-001"
}
```

### JSON-RPC Error

```json
{
  "jsonrpc": "2.0",
  "error": {
    "code": -32600,
    "message": "Invalid Request",
    "data": "Missing required parameter"
  },
  "id": "req-001"
}
```

## Error Codes

| Code | Message | Description |
|------|---------|-------------|
| -32700 | Parse error | Invalid JSON |
| -32600 | Invalid Request | Malformed JSON-RPC request |
| -32601 | Method not found | Unknown command |
| -32602 | Invalid params | Missing or invalid parameters |
| -32603 | Internal error | Server-side error |
| -32000 | Command execution failed | Command-specific error |

## Example Workflows

### Complete Part Creation

```json
// 1. Create part
{ "method": "create_part_from_intent", "params": {"intent": "Create base plate"}, "id": "1" }

// 2. Add extrusion
{ "method": "create_rectangular_extrusion", "params": {"width": 100, "height": 50, "depth": 10}, "id": "2" }

// 3. Add hole pattern
{ "method": "add_hole_pattern", "params": {"diameter": 10, "depth": 20, "count": 4}, "id": "3" }

// 4. Rebuild
{ "method": "rebuild", "params": {"option": "Active"}, "id": "4" }

// 5. Export
{ "method": "export_step", "params": {"outputPath": "C:\\output\\part.step"}, "id": "5" }
```

### Parameter Adjustment

```json
// 1. Set parameters
{ "method": "set_parameters", "params": {"parameters": {"Length": 150}}, "id": "1" }

// 2. Rebuild
{ "method": "rebuild", "params": {}, "id": "2" }

// 3. Capture preview
{ "method": "capture_preview", "params": {"width": 1920, "height": 1080}, "id": "3" }
```

## Testing

### Using cURL

```bash
curl -X POST http://localhost:5000 \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "create_rectangular_extrusion",
    "params": {"width": 100, "height": 50, "depth": 10},
    "id": "test-001"
  }'
```

### Using Python

```python
import requests

response = requests.post("http://localhost:5000", json={
    "jsonrpc": "2.0",
    "method": "create_rectangular_extrusion",
    "params": {"width": 100, "height": 50, "depth": 10},
    "id": "test-001"
})

print(response.json())
```

### Using JavaScript

```javascript
fetch('http://localhost:5000', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    jsonrpc: '2.0',
    method: 'create_rectangular_extrusion',
    params: { width: 100, height: 50, depth: 10 },
    id: 'test-001'
  })
})
.then(res => res.json())
.then(data => console.log(data));
```

## Notes

- All measurements are in millimeters (mm) for MMGS unit system
- Commands execute on SOLIDWORKS STA thread for thread safety
- MCP server starts automatically when Add-In loads
- Check logs at `%APPDATA%\SolidAI\logs\` for debugging
- Ensure SOLIDWORKS is running and Add-In is enabled
