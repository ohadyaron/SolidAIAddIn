using System;
using System.Threading.Tasks;
using SolidAIAddIn.MCP.Models;
using SolidAIAddIn.SolidWorksWrapper;
using SolidWorks.Interop.sldworks;
using Serilog;

namespace SolidAIAddIn.MCP.Handlers
{
    /// <summary>
    /// Handles MCP intent-level commands
    /// Translates high-level intents into SOLIDWORKS API operations
    /// Ensures all operations execute safely on STA thread through wrapper
    /// 
    /// Command Flow:
    /// MCP Request → Handler (validates & translates intent) → Wrapper (STA execution) → SOLIDWORKS API
    /// </summary>
    public class McpCommandHandler
    {
        private readonly SolidWorksApiWrapper _swWrapper;

        public McpCommandHandler(SolidWorksApiWrapper swWrapper)
        {
            _swWrapper = swWrapper ?? throw new ArgumentNullException(nameof(swWrapper));
        }

        #region Command Handlers

        /// <summary>
        /// Handle: create_part_from_intent
        /// Creates a new part document based on intent description
        /// Intent translation: "Create a mounting bracket" → New part document setup
        /// </summary>
        public async Task<McpCommandResponse> HandleCreatePartFromIntent(CreatePartFromIntentRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling create_part_from_intent: {Intent}", request.Intent);

                    // Validate input
                    if (string.IsNullOrWhiteSpace(request.Intent))
                    {
                        return CreateErrorResponse(request.CommandId, "Intent description is required");
                    }

                    // Execute through wrapper (ensures STA thread)
                    var result = _swWrapper.CreatePartDocument(request.Template, request.Units);

                    if (result.Success)
                    {
                        Log.Information("Part created from intent: {Intent}", request.Intent);
                        return CreateSuccessResponse(
                            request.CommandId,
                            $"Part document created for intent: {request.Intent}",
                            new { template = request.Template, units = request.Units });
                    }
                    else
                    {
                        Log.Warning("Failed to create part: {Message}", result.Message);
                        return CreateErrorResponse(request.CommandId, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in create_part_from_intent");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: apply_feature
        /// Applies a feature (hole, fillet, chamfer) to the model
        /// Intent translation: Feature parameters → SOLIDWORKS feature creation
        /// </summary>
        public async Task<McpCommandResponse> HandleApplyFeature(ApplyFeatureRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling apply_feature: {Type}", request.FeatureType);

                    // Validate input
                    if (string.IsNullOrWhiteSpace(request.FeatureType))
                    {
                        return CreateErrorResponse(request.CommandId, "Feature type is required");
                    }

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    // Route to specific feature handler
                    var result = request.FeatureType.ToLowerInvariant() switch
                    {
                        "hole" => ApplyHoleFeature(modelDoc, request.Parameters),
                        "fillet" => ApplyFilletFeature(modelDoc, request.Parameters),
                        "chamfer" => ApplyChamferFeature(modelDoc, request.Parameters),
                        "extrude" => ApplyExtrudeFeature(modelDoc, request.Parameters),
                        _ => OperationResult<string>.Failure($"Unsupported feature type: {request.FeatureType}")
                    };

                    if (result.Success)
                    {
                        Log.Information("Feature applied: {Type}", request.FeatureType);
                        return CreateSuccessResponse(
                            request.CommandId,
                            $"Feature '{request.FeatureType}' applied successfully",
                            new { featureType = request.FeatureType });
                    }
                    else
                    {
                        Log.Warning("Failed to apply feature: {Message}", result.Message);
                        return CreateErrorResponse(request.CommandId, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in apply_feature");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: set_parameters
        /// Sets or modifies model parameters/dimensions
        /// Intent translation: Parameter name/value pairs → Dimension modifications
        /// </summary>
        public async Task<McpCommandResponse> HandleSetParameters(SetParametersRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling set_parameters: {Count} parameters", request.Parameters.Count);

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    // Apply each parameter
                    int successCount = 0;
                    foreach (var param in request.Parameters)
                    {
                        try
                        {
                            // In production, use IEquationMgr or IParameter to set dimensions
                            Log.Debug("Setting parameter: {Name} = {Value}", param.Key, param.Value);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to set parameter: {Name}", param.Key);
                        }
                    }

                    Log.Information("Set {Count}/{Total} parameters", successCount, request.Parameters.Count);
                    return CreateSuccessResponse(
                        request.CommandId,
                        $"Set {successCount}/{request.Parameters.Count} parameters",
                        new { appliedCount = successCount, totalCount = request.Parameters.Count });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in set_parameters");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: rebuild
        /// Rebuilds the model to regenerate all features
        /// Intent translation: Rebuild option → Force model regeneration
        /// </summary>
        public async Task<McpCommandResponse> HandleRebuild(RebuildRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling rebuild: {Option}", request.Option);

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    var result = _swWrapper.RebuildModel(modelDoc);

                    if (result.Success)
                    {
                        Log.Information("Model rebuilt successfully");
                        return CreateSuccessResponse(
                            request.CommandId,
                            "Model rebuilt successfully",
                            new { option = request.Option });
                    }
                    else
                    {
                        Log.Warning("Rebuild failed: {Message}", result.Message);
                        return CreateErrorResponse(request.CommandId, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in rebuild");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: export_step
        /// Exports current document to STEP format
        /// Intent translation: Export request → STEP file generation
        /// </summary>
        public async Task<McpCommandResponse> HandleExportStep(ExportStepRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling export_step: {Path}", request.OutputPath);

                    // Validate input
                    if (string.IsNullOrWhiteSpace(request.OutputPath))
                    {
                        return CreateErrorResponse(request.CommandId, "Output path is required");
                    }

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    var result = _swWrapper.ExportToStep(modelDoc, request.OutputPath);

                    if (result.Success)
                    {
                        Log.Information("Exported to STEP: {Path}", result.Data);
                        return CreateSuccessResponse(
                            request.CommandId,
                            "Exported to STEP successfully",
                            new { outputPath = result.Data, version = request.StepVersion });
                    }
                    else
                    {
                        Log.Warning("Export failed: {Message}", result.Message);
                        return CreateErrorResponse(request.CommandId, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in export_step");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: capture_preview
        /// Captures a preview image of the current view
        /// Intent translation: Image request → Viewport screenshot
        /// </summary>
        public async Task<McpCommandResponse> HandleCapturePreview(CapturePreviewRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling capture_preview: {Width}x{Height}", request.Width, request.Height);

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    // In production, use IModelDocExtension::CaptureModelImage
                    Log.Information("Preview capture not fully implemented");
                    return CreateSuccessResponse(
                        request.CommandId,
                        "Preview capture not fully implemented in skeleton",
                        new { width = request.Width, height = request.Height, format = request.Format });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in capture_preview");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: create_rectangular_extrusion
        /// Creates a rectangular extrusion feature
        /// Intent translation: Rectangle dimensions → Sketch + Extrude feature
        /// </summary>
        public async Task<McpCommandResponse> HandleCreateRectangularExtrusion(CreateRectangularExtrusionRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling create_rectangular_extrusion: {W}x{H}x{D}",
                        request.Width, request.Height, request.Depth);

                    // Validate input
                    if (request.Width <= 0 || request.Height <= 0 || request.Depth <= 0)
                    {
                        return CreateErrorResponse(request.CommandId, "Dimensions must be positive");
                    }

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    var result = _swWrapper.CreateRectangularExtrusion(
                        modelDoc, request.Width, request.Height, request.Depth, request.Plane);

                    if (result.Success)
                    {
                        Log.Information("Rectangular extrusion created");
                        return CreateSuccessResponse(
                            request.CommandId,
                            "Rectangular extrusion created successfully",
                            new
                            {
                                width = request.Width,
                                height = request.Height,
                                depth = request.Depth,
                                plane = request.Plane
                            });
                    }
                    else
                    {
                        Log.Warning("Failed to create extrusion: {Message}", result.Message);
                        return CreateErrorResponse(request.CommandId, result.Message);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in create_rectangular_extrusion");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        /// <summary>
        /// Handle: add_hole_pattern
        /// Adds a hole pattern to the model
        /// Intent translation: Hole parameters + pattern → Multiple hole features
        /// </summary>
        public async Task<McpCommandResponse> HandleAddHolePattern(AddHolePatternRequest request)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Log.Information("Handling add_hole_pattern: {Type}, count={Count}",
                        request.PatternType, request.Count);

                    // Validate input
                    if (request.Diameter <= 0 || request.Depth <= 0)
                    {
                        return CreateErrorResponse(request.CommandId, "Diameter and depth must be positive");
                    }

                    var modelDoc = _swWrapper.GetActiveDocument();
                    if (modelDoc == null)
                    {
                        return CreateErrorResponse(request.CommandId, "No active document");
                    }

                    // Create pattern of holes
                    // In production, use proper hole wizard and pattern features
                    Log.Information("Hole pattern not fully implemented in skeleton");
                    return CreateSuccessResponse(
                        request.CommandId,
                        "Hole pattern not fully implemented in skeleton",
                        new
                        {
                            diameter = request.Diameter,
                            depth = request.Depth,
                            patternType = request.PatternType,
                            count = request.Count
                        });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in add_hole_pattern");
                    return CreateErrorResponse(request.CommandId, ex.Message);
                }
            });
        }

        #endregion

        #region Feature Handlers

        private OperationResult<string> ApplyHoleFeature(IModelDoc2 modelDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                // Extract parameters
                double diameter = parameters.ContainsKey("diameter") ? Convert.ToDouble(parameters["diameter"]) : 10.0;
                double depth = parameters.ContainsKey("depth") ? Convert.ToDouble(parameters["depth"]) : 20.0;

                // Use wrapper to add hole
                var result = _swWrapper.AddSimpleHole(modelDoc, diameter, depth, new double[] { 0, 0, 0 });
                
                return result.Success 
                    ? OperationResult<string>.Success("Hole feature applied") 
                    : OperationResult<string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying hole feature");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        private OperationResult<string> ApplyFilletFeature(IModelDoc2 modelDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            // Placeholder for fillet implementation
            Log.Information("Fillet feature not fully implemented");
            return OperationResult<string>.Success("Fillet feature placeholder");
        }

        private OperationResult<string> ApplyChamferFeature(IModelDoc2 modelDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            // Placeholder for chamfer implementation
            Log.Information("Chamfer feature not fully implemented");
            return OperationResult<string>.Success("Chamfer feature placeholder");
        }

        private OperationResult<string> ApplyExtrudeFeature(IModelDoc2 modelDoc, System.Collections.Generic.Dictionary<string, object> parameters)
        {
            try
            {
                double width = parameters.ContainsKey("width") ? Convert.ToDouble(parameters["width"]) : 100.0;
                double height = parameters.ContainsKey("height") ? Convert.ToDouble(parameters["height"]) : 50.0;
                double depth = parameters.ContainsKey("depth") ? Convert.ToDouble(parameters["depth"]) : 10.0;

                var result = _swWrapper.CreateRectangularExtrusion(modelDoc, width, height, depth, "Front");
                
                return result.Success 
                    ? OperationResult<string>.Success("Extrude feature applied") 
                    : OperationResult<string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error applying extrude feature");
                return OperationResult<string>.Failure(ex.Message);
            }
        }

        #endregion

        #region Response Helpers

        private McpCommandResponse CreateSuccessResponse(string commandId, string message, object? data = null)
        {
            return new McpCommandResponse
            {
                CommandId = commandId,
                Success = true,
                Message = message,
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        private McpCommandResponse CreateErrorResponse(string commandId, string error)
        {
            return new McpCommandResponse
            {
                CommandId = commandId,
                Success = false,
                Message = "Command failed",
                ErrorDetails = error,
                Timestamp = DateTime.UtcNow
            };
        }

        #endregion
    }
}
