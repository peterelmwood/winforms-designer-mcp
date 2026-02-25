using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WinFormsDesignerMcp.Services;
using WinFormsDesignerMcp.Services.CSharp;
using WinFormsDesignerMcp.Services.VisualBasic;

var builder = Host.CreateApplicationBuilder(args);

// MCP uses stdout for protocol messages — route all logs to stderr.
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register parser and writer services.
builder.Services.AddSingleton<IDesignerFileParser, CSharpDesignerFileParser>();
builder.Services.AddSingleton<IDesignerFileParser, VbDesignerFileParser>();
builder.Services.AddSingleton<IDesignerFileWriter, CSharpDesignerFileWriter>();
builder.Services.AddSingleton<IDesignerFileWriter, VbDesignerFileWriter>();
builder.Services.AddSingleton<DesignerFileService>();

// Register MCP server with stdio transport and auto-discover tools from this assembly.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
