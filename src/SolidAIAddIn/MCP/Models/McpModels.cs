using System;

namespace SolidAIAddIn.MCP.Models
{
    /// <summary>
    /// Base class for all MCP command requests
    /// Provides common structure for intent-based commands
    /// </summary>
    public abstract class McpCommandRequest
    {
        /// <summary>
        /// Unique identifier for tracking the command
        /// </summary>
        public string CommandId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Timestamp when command was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Command type identifier
        /// </summary>
        public abstract string CommandType { get; }
    }

    /// <summary>
    /// Response structure for all MCP commands
    /// Provides consistent error handling and result reporting
    /// </summary>
    public class McpCommandResponse
    {
        public string CommandId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public string? ErrorDetails { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    #region Intent-Level Command DTOs

    /// <summary>
    /// Command: Create a new part document from intent description
    /// Intent-level: User describes what they want, not how to create it
    /// </summary>
    public class CreatePartFromIntentRequest : McpCommandRequest
    {
        public override string CommandType => "create_part_from_intent";

        /// <summary>
        /// Natural language description of the part to create
        /// Example: "Create a mounting bracket with base plate"
        /// </summary>
        public string Intent { get; set; } = string.Empty;

        /// <summary>
        /// Template to use (e.g., "Part", "Assembly", "Drawing")
        /// </summary>
        public string Template { get; set; } = "Part";

        /// <summary>
        /// Units system (IPS, MMGS, CGS)
        /// </summary>
        public string Units { get; set; } = "MMGS";
    }

    /// <summary>
    /// Command: Apply a feature to the current part
    /// Supports holes, fillets, chamfers with intelligent parameter handling
    /// </summary>
    public class ApplyFeatureRequest : McpCommandRequest
    {
        public override string CommandType => "apply_feature";

        /// <summary>
        /// Feature type: "hole", "fillet", "chamfer", "extrude", etc.
        /// </summary>
        public string FeatureType { get; set; } = string.Empty;

        /// <summary>
        /// Parameters specific to the feature type
        /// Example for hole: { "diameter": 10, "depth": 20, "positions": [...] }
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Parameters { get; set; } 
            = new System.Collections.Generic.Dictionary<string, object>();

        /// <summary>
        /// Target entities (edges, faces, vertices) for the feature
        /// </summary>
        public string[]? TargetEntities { get; set; }
    }

    /// <summary>
    /// Command: Set or modify model parameters/dimensions
    /// Allows dynamic adjustment of model dimensions
    /// </summary>
    public class SetParametersRequest : McpCommandRequest
    {
        public override string CommandType => "set_parameters";

        /// <summary>
        /// Dictionary of parameter names and values
        /// Example: { "Length": 100, "Width": 50, "Height": 30 }
        /// </summary>
        public System.Collections.Generic.Dictionary<string, double> Parameters { get; set; }
            = new System.Collections.Generic.Dictionary<string, double>();
    }

    /// <summary>
    /// Command: Rebuild the model
    /// Forces regeneration of all features
    /// </summary>
    public class RebuildRequest : McpCommandRequest
    {
        public override string CommandType => "rebuild";

        /// <summary>
        /// Rebuild options: "Active", "All", "TopLevelOnly"
        /// </summary>
        public string Option { get; set; } = "Active";
    }

    /// <summary>
    /// Command: Export current document to STEP format
    /// Supports various export options and validation
    /// </summary>
    public class ExportStepRequest : McpCommandRequest
    {
        public override string CommandType => "export_step";

        /// <summary>
        /// Output file path for STEP file
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// STEP version (e.g., "AP203", "AP214", "AP242")
        /// </summary>
        public string StepVersion { get; set; } = "AP214";

        /// <summary>
        /// Include geometry, PMI, attributes, etc.
        /// </summary>
        public bool IncludeAttributes { get; set; } = true;
    }

    /// <summary>
    /// Command: Capture a preview image of the current view
    /// Returns image data or path to saved image
    /// </summary>
    public class CapturePreviewRequest : McpCommandRequest
    {
        public override string CommandType => "capture_preview";

        /// <summary>
        /// Output file path for preview image
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// Image width in pixels
        /// </summary>
        public int Width { get; set; } = 1024;

        /// <summary>
        /// Image height in pixels
        /// </summary>
        public int Height { get; set; } = 768;

        /// <summary>
        /// Image format: "PNG", "JPEG", "BMP"
        /// </summary>
        public string Format { get; set; } = "PNG";
    }

    /// <summary>
    /// Command: Create a simple rectangular extrusion
    /// High-level intent command for common operation
    /// </summary>
    public class CreateRectangularExtrusionRequest : McpCommandRequest
    {
        public override string CommandType => "create_rectangular_extrusion";

        public double Width { get; set; }
        public double Height { get; set; }
        public double Depth { get; set; }
        public string Plane { get; set; } = "Front"; // Front, Top, Right
    }

    /// <summary>
    /// Command: Add a hole pattern to existing geometry
    /// Intent-level: Specify hole parameters and pattern type
    /// </summary>
    public class AddHolePatternRequest : McpCommandRequest
    {
        public override string CommandType => "add_hole_pattern";

        public double Diameter { get; set; }
        public double Depth { get; set; }
        public string PatternType { get; set; } = "Linear"; // Linear, Circular
        public int Count { get; set; } = 4;
        public double Spacing { get; set; } = 10.0;
    }

    #endregion

    #region JSON-RPC Models

    /// <summary>
    /// JSON-RPC 2.0 request envelope
    /// </summary>
    public class JsonRpcRequest
    {
        public string JsonRpc { get; set; } = "2.0";
        public string Method { get; set; } = string.Empty;
        public object? Params { get; set; }
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// JSON-RPC 2.0 response envelope
    /// </summary>
    public class JsonRpcResponse
    {
        public string JsonRpc { get; set; } = "2.0";
        public object? Result { get; set; }
        public JsonRpcError? Error { get; set; }
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    /// JSON-RPC 2.0 error structure
    /// </summary>
    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    #endregion
}
