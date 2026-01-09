using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidAIAddIn.MCP.Models;
using SolidAIAddIn.MCP.Handlers;
using SolidAIAddIn.SolidWorksWrapper;
using Serilog;

namespace SolidAIAddIn.MCP
{
    /// <summary>
    /// Embedded MCP (Model Context Protocol) Server
    /// Exposes JSON-RPC API for intent-based SOLIDWORKS commands
    /// Ensures thread-safe execution of all SOLIDWORKS operations
    /// 
    /// MCP Command Flow:
    /// 1. External client sends JSON-RPC request to HTTP endpoint
    /// 2. McpServer deserializes and validates request
    /// 3. CommandRouter dispatches to appropriate handler
    /// 4. Handler translates intent to SOLIDWORKS operations
    /// 5. SolidWorksApiWrapper executes on STA thread
    /// 6. Result returned through JSON-RPC response
    /// </summary>
    public class McpServer
    {
        #region Private Fields

        private readonly SolidWorksApiWrapper _swWrapper;
        private readonly int _port;
        private IWebHost? _webHost;
        private readonly CommandRouter _commandRouter;
        private CancellationTokenSource? _cancellationTokenSource;

        #endregion

        #region Constructor

        public McpServer(SolidWorksApiWrapper swWrapper, int port = 5000)
        {
            _swWrapper = swWrapper ?? throw new ArgumentNullException(nameof(swWrapper));
            _port = port;
            _commandRouter = new CommandRouter(swWrapper);
            
            Log.Information("MCP Server initialized on port {Port}", _port);
        }

        #endregion

        #region Server Lifecycle

        /// <summary>
        /// Start the MCP server
        /// Begins listening for JSON-RPC requests
        /// </summary>
        public void Start()
        {
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                _webHost = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Loopback, _port);
                    })
                    .Configure(app =>
                    {
                        app.Run(async context => await HandleRequest(context));
                    })
                    .Build();

                // Start on background thread to avoid blocking SOLIDWORKS UI
                Task.Run(() => _webHost.Run(_cancellationTokenSource.Token));

                Log.Information("MCP Server started successfully on http://localhost:{Port}", _port);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to start MCP Server");
                throw;
            }
        }

        /// <summary>
        /// Stop the MCP server
        /// Gracefully shuts down the HTTP listener
        /// </summary>
        public void Stop()
        {
            try
            {
                Log.Information("Stopping MCP Server");
                
                _cancellationTokenSource?.Cancel();
                _webHost?.StopAsync(TimeSpan.FromSeconds(5)).Wait();
                _webHost?.Dispose();
                
                Log.Information("MCP Server stopped");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error stopping MCP Server");
            }
        }

        #endregion

        #region Request Handling

        /// <summary>
        /// Handle incoming HTTP requests
        /// Processes JSON-RPC commands and returns responses
        /// </summary>
        private async Task HandleRequest(HttpContext context)
        {
            try
            {
                // Log request
                Log.Debug("Received {Method} request to {Path}", context.Request.Method, context.Request.Path);

                // Only accept POST requests
                if (context.Request.Method != "POST")
                {
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync("Only POST requests are supported");
                    return;
                }

                // Read request body
                string requestBody;
                using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                Log.Debug("Request body: {Body}", requestBody);

                // Parse JSON-RPC request
                JsonRpcRequest? rpcRequest;
                try
                {
                    rpcRequest = JsonConvert.DeserializeObject<JsonRpcRequest>(requestBody);
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "Invalid JSON in request");
                    await SendErrorResponse(context, null, -32700, "Parse error", "Invalid JSON");
                    return;
                }

                if (rpcRequest == null)
                {
                    await SendErrorResponse(context, null, -32600, "Invalid Request", "Request is null");
                    return;
                }

                // Validate JSON-RPC version
                if (rpcRequest.JsonRpc != "2.0")
                {
                    await SendErrorResponse(context, rpcRequest.Id, -32600, "Invalid Request", "JSON-RPC version must be 2.0");
                    return;
                }

                // Execute command
                var response = await ExecuteCommand(rpcRequest);

                // Send response
                await SendJsonResponse(context, response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error in request handler");
                await SendErrorResponse(context, null, -32603, "Internal error", ex.Message);
            }
        }

        /// <summary>
        /// Execute MCP command by routing to appropriate handler
        /// This is where the MCP intent layer translates to SOLIDWORKS operations
        /// </summary>
        private async Task<JsonRpcResponse> ExecuteCommand(JsonRpcRequest request)
        {
            try
            {
                Log.Information("Executing MCP command: {Method}", request.Method);

                // Route command to handler
                var result = await _commandRouter.RouteCommand(request.Method, request.Params);

                // Create JSON-RPC response
                var response = new JsonRpcResponse
                {
                    Id = request.Id,
                    Result = result
                };

                Log.Information("Command executed successfully: {Method}", request.Method);
                return response;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing command: {Method}", request.Method);
                
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32000,
                        Message = "Command execution failed",
                        Data = ex.Message
                    }
                };
            }
        }

        #endregion

        #region Response Helpers

        private async Task SendJsonResponse(HttpContext context, object response)
        {
            context.Response.ContentType = "application/json";
            var json = JsonConvert.SerializeObject(response, Formatting.Indented);
            await context.Response.WriteAsync(json);
        }

        private async Task SendErrorResponse(HttpContext context, string? id, int code, string message, string? data = null)
        {
            var response = new JsonRpcResponse
            {
                Id = id ?? string.Empty,
                Error = new JsonRpcError
                {
                    Code = code,
                    Message = message,
                    Data = data
                }
            };

            await SendJsonResponse(context, response);
        }

        #endregion
    }

    /// <summary>
    /// Routes MCP commands to appropriate handlers
    /// Handles intent-level command dispatch
    /// </summary>
    public class CommandRouter
    {
        private readonly SolidWorksApiWrapper _swWrapper;
        private readonly McpCommandHandler _commandHandler;

        public CommandRouter(SolidWorksApiWrapper swWrapper)
        {
            _swWrapper = swWrapper;
            _commandHandler = new McpCommandHandler(swWrapper);
        }

        /// <summary>
        /// Route command to appropriate handler based on method name
        /// </summary>
        public async Task<McpCommandResponse> RouteCommand(string method, object? parameters)
        {
            try
            {
                Log.Debug("Routing command: {Method}", method);

                // Convert parameters to JObject for flexible deserialization
                var paramsJson = parameters != null 
                    ? JObject.FromObject(parameters) 
                    : new JObject();

                // Route based on command type
                return method switch
                {
                    "create_part_from_intent" => await _commandHandler.HandleCreatePartFromIntent(
                        paramsJson.ToObject<CreatePartFromIntentRequest>() ?? new CreatePartFromIntentRequest()),
                    
                    "apply_feature" => await _commandHandler.HandleApplyFeature(
                        paramsJson.ToObject<ApplyFeatureRequest>() ?? new ApplyFeatureRequest()),
                    
                    "set_parameters" => await _commandHandler.HandleSetParameters(
                        paramsJson.ToObject<SetParametersRequest>() ?? new SetParametersRequest()),
                    
                    "rebuild" => await _commandHandler.HandleRebuild(
                        paramsJson.ToObject<RebuildRequest>() ?? new RebuildRequest()),
                    
                    "export_step" => await _commandHandler.HandleExportStep(
                        paramsJson.ToObject<ExportStepRequest>() ?? new ExportStepRequest()),
                    
                    "capture_preview" => await _commandHandler.HandleCapturePreview(
                        paramsJson.ToObject<CapturePreviewRequest>() ?? new CapturePreviewRequest()),
                    
                    "create_rectangular_extrusion" => await _commandHandler.HandleCreateRectangularExtrusion(
                        paramsJson.ToObject<CreateRectangularExtrusionRequest>() ?? new CreateRectangularExtrusionRequest()),
                    
                    "add_hole_pattern" => await _commandHandler.HandleAddHolePattern(
                        paramsJson.ToObject<AddHolePatternRequest>() ?? new AddHolePatternRequest()),
                    
                    _ => new McpCommandResponse
                    {
                        Success = false,
                        Message = $"Unknown command: {method}",
                        ErrorDetails = "Command not recognized by router"
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error routing command: {Method}", method);
                return new McpCommandResponse
                {
                    Success = false,
                    Message = "Command routing failed",
                    ErrorDetails = ex.Message
                };
            }
        }
    }
}
