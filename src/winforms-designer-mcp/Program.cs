using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using WinFormsDesignerMcp.Cli;
using WinFormsDesignerMcp.Services;
using WinFormsDesignerMcp.Services.CSharp;
using WinFormsDesignerMcp.Services.VisualBasic;

// ── CLI mode: if any command-line arguments are passed, run as a one-shot CLI ──
if (args.Length > 0)
{
    var rootCommand = CliCommands.BuildRootCommand();
    var exitCode = await rootCommand.Parse(args).InvokeAsync();
    return exitCode;
}

// ── MCP server mode: no arguments → start the stdio MCP server ──
// Use the minimal HostBuilder instead of Host.CreateApplicationBuilder to avoid
// loading file configuration providers, environment variable scanning, appsettings.json,
// user secrets, and other overhead that an stdio MCP server doesn't need.
var builder = new HostBuilder();

builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    // MCP uses stdout for protocol messages — route all logs to stderr.
    logging.AddConsole(consoleLogOptions =>
    {
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });
});

builder.ConfigureServices(services =>
{
    // Register parser and writer services.
    services.AddSingleton<IDesignerFileParser, CSharpDesignerFileParser>();
    services.AddSingleton<IDesignerFileParser, VbDesignerFileParser>();
    services.AddSingleton<IDesignerFileWriter, CSharpDesignerFileWriter>();
    services.AddSingleton<IDesignerFileWriter, VbDesignerFileWriter>();
    services.AddSingleton<DesignerFileService>();

    // Register MCP server with stdio transport and auto-discover tools from this assembly.
    services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
});

await builder.Build().RunAsync();
return 0;
