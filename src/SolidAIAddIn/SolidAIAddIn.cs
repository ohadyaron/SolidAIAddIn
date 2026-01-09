using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using SolidWorks.Interop.swconst;
using SolidAIAddIn.MCP;
using SolidAIAddIn.SolidWorksWrapper;
using Serilog;

namespace SolidAIAddIn
{
    /// <summary>
    /// Main SOLIDWORKS Add-In implementation
    /// Implements ISwAddin interface for SOLIDWORKS integration
    /// Hosts embedded MCP server for intent-based command execution
    /// </summary>
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public class SolidAIAddIn : ISwAddin
    {
        #region Private Fields

        private ISldWorks? _swApp;
        private int _addinId;
        private McpServer? _mcpServer;
        private SolidWorksApiWrapper? _swWrapper;
        private CommandManager? _commandManager;
        private int _mainCmdGroupId;

        #endregion

        #region ISwAddin Implementation

        /// <summary>
        /// Called when the Add-In is loaded by SOLIDWORKS
        /// Initializes MCP server, command manager, and UI
        /// </summary>
        public bool ConnectToSW(object ThisSW, int cookie)
        {
            try
            {
                _swApp = (ISldWorks)ThisSW;
                _addinId = cookie;

                // Initialize logging
                InitializeLogging();
                Log.Information("=== SolidAI Add-In Starting ===");
                Log.Information("SOLIDWORKS Version: {Version}", _swApp.RevisionNumber());

                // Initialize SOLIDWORKS wrapper (STA thread safety)
                _swWrapper = new SolidWorksApiWrapper(_swApp);
                Log.Information("SOLIDWORKS API Wrapper initialized");

                // Initialize and start MCP server
                _mcpServer = new McpServer(_swWrapper, port: 5000);
                _mcpServer.Start();
                Log.Information("MCP Server started on port 5000");

                // Setup UI and commands
                SetupCommandManager();
                Log.Information("Command Manager initialized");

                // Setup event handlers
                SetupEventHandlers();

                Log.Information("=== SolidAI Add-In Connected Successfully ===");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to SOLIDWORKS");
                System.Windows.Forms.MessageBox.Show(
                    $"SolidAI Add-In failed to load:\n{ex.Message}",
                    "SolidAI Add-In Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Called when the Add-In is unloaded by SOLIDWORKS
        /// Cleans up resources and stops MCP server
        /// </summary>
        public bool DisconnectFromSW()
        {
            try
            {
                Log.Information("=== SolidAI Add-In Disconnecting ===");

                // Stop MCP server
                _mcpServer?.Stop();
                Log.Information("MCP Server stopped");

                // Cleanup command manager
                CleanupCommandManager();

                // Cleanup event handlers
                CleanupEventHandlers();

                // Release COM objects
                if (_swApp != null)
                {
                    Marshal.ReleaseComObject(_swApp);
                    _swApp = null;
                }

                Log.Information("=== SolidAI Add-In Disconnected ===");
                Log.CloseAndFlush();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during disconnect");
                return false;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize Serilog for structured logging
        /// Logs to file in user's AppData folder
        /// </summary>
        private void InitializeLogging()
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SolidAI",
                "logs",
                "solidai-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        /// <summary>
        /// Setup CommandManager with custom buttons and menu items
        /// Creates a command group for MCP testing
        /// </summary>
        private void SetupCommandManager()
        {
            if (_swApp == null) return;

            _commandManager = _swApp.GetCommandManager(_addinId);
            if (_commandManager == null)
            {
                Log.Warning("Failed to get CommandManager");
                return;
            }

            // Create command group
            _mainCmdGroupId = _commandManager.CreateCommandGroup2(
                1, // User ID
                "SolidAI MCP Commands",
                "Commands for testing MCP server",
                "",
                -1,
                true, // Ignore version
                out int cmdGroupErr);

            if (cmdGroupErr != (int)swCreateCommandGroupErrors.swCreateCommandGroup_Success)
            {
                Log.Warning("Failed to create command group: {Error}", cmdGroupErr);
                return;
            }

            // Add command: Test MCP Workflow
            var cmdGroup = _commandManager.GetCommandGroup(_mainCmdGroupId);
            if (cmdGroup != null)
            {
                int cmdIndex = cmdGroup.AddCommandItem2(
                    "Test MCP Workflow",
                    -1, // Position
                    "Execute example MCP workflow: create part, extrude, add hole, export",
                    "Test MCP Workflow",
                    0, // Image index
                    "OnTestMcpWorkflow",
                    "",
                    _mainCmdGroupId,
                    (int)swCommandItemType_e.swMenuItem);

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;
                cmdGroup.Activate();

                Log.Information("Added 'Test MCP Workflow' command");
            }
        }

        /// <summary>
        /// Setup event handlers for document lifecycle
        /// </summary>
        private void SetupEventHandlers()
        {
            // Add event handlers as needed for document open/close, etc.
            // For now, we keep it minimal
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleanup command manager resources
        /// </summary>
        private void CleanupCommandManager()
        {
            if (_commandManager != null)
            {
                try
                {
                    _commandManager.RemoveCommandGroup(_mainCmdGroupId);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error removing command group");
                }
            }
        }

        /// <summary>
        /// Cleanup event handlers
        /// </summary>
        private void CleanupEventHandlers()
        {
            // Remove event handlers
        }

        #endregion

        #region Command Callbacks

        /// <summary>
        /// Callback for "Test MCP Workflow" button
        /// Executes a full example workflow through MCP server
        /// Flow: MCP Command → Intent Handler → SOLIDWORKS Wrapper → SOLIDWORKS API
        /// </summary>
        public void OnTestMcpWorkflow()
        {
            try
            {
                Log.Information("=== Starting MCP Test Workflow ===");

                if (_swWrapper == null)
                {
                    ShowMessage("SOLIDWORKS wrapper not initialized", true);
                    return;
                }

                // Execute example workflow using the wrapper directly
                // In production, this would come through MCP server from external client
                var result = _swWrapper.ExecuteExampleWorkflow();

                if (result.Success)
                {
                    Log.Information("Workflow completed successfully: {Message}", result.Message);
                    ShowMessage($"Success!\n{result.Message}", false);
                }
                else
                {
                    Log.Error("Workflow failed: {Message}", result.Message);
                    ShowMessage($"Failed:\n{result.Message}", true);
                }

                Log.Information("=== MCP Test Workflow Complete ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error executing test workflow");
                ShowMessage($"Error: {ex.Message}", true);
            }
        }

        #endregion

        #region Helper Methods

        private void ShowMessage(string message, bool isError)
        {
            var icon = isError 
                ? System.Windows.Forms.MessageBoxIcon.Error 
                : System.Windows.Forms.MessageBoxIcon.Information;

            System.Windows.Forms.MessageBox.Show(
                message,
                "SolidAI Add-In",
                System.Windows.Forms.MessageBoxButtons.OK,
                icon);
        }

        #endregion

        #region COM Registration

        /// <summary>
        /// Register the Add-In with SOLIDWORKS
        /// Called by RegAsm during COM registration
        /// </summary>
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                var keyPath = $@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}";
                
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(null, 0);
                        key.SetValue("Description", "SolidAI Add-In with MCP Server");
                        key.SetValue("Title", "SolidAI Add-In");
                    }
                }

                // Also register in CurrentUser for easier development
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue(null, 0);
                        key.SetValue("Description", "SolidAI Add-In with MCP Server");
                        key.SetValue("Title", "SolidAI Add-In");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM Registration failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregister the Add-In from SOLIDWORKS
        /// Called by RegAsm during COM unregistration
        /// </summary>
        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                var keyPath = $@"SOFTWARE\SolidWorks\Addins\{{{t.GUID}}}";
                
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(keyPath, false);
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(keyPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"COM Unregistration failed: {ex.Message}");
            }
        }

        #endregion
    }
}
