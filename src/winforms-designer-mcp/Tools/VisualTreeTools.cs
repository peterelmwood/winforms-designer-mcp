using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tools for inspecting the visual tree and state of a WinForms designer file.
/// </summary>
[McpServerToolType]
public class VisualTreeTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "list_controls")]
    [Description(
        "Returns a hierarchical list of all controls on a WinForms form, " +
        "including their types, names, and parent-child relationships. " +
        "Supports both C# (.Designer.cs) and VB.NET (.Designer.vb) designer files.")]
    public static async Task<string> ListControls(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        CancellationToken cancellationToken)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        var hierarchy = model.RootControls.Select(FormatControlHierarchy).ToList();

        var result = new
        {
            formName = model.FormName,
            language = model.Language.ToString(),
            @namespace = model.Namespace,
            controls = hierarchy
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "get_control_properties")]
    [Description(
        "Fetches the full property bag (Size, Location, Anchor, Dock, etc.) " +
        "for a specific control by name, including its events. " +
        "Supports both C# and VB.NET designer files.")]
    public static async Task<string> GetControlProperties(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description("The name of the control (e.g., 'button1')")] string controlName,
        CancellationToken cancellationToken)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        var control = model.Controls.FirstOrDefault(
            c => c.Name.Equals(controlName, StringComparison.OrdinalIgnoreCase));

        if (control is null)
        {
            // Check if they're asking for form-level properties.
            if (controlName.Equals(model.FormName, StringComparison.OrdinalIgnoreCase) ||
                controlName.Equals("form", StringComparison.OrdinalIgnoreCase))
            {
                var formResult = new
                {
                    name = model.FormName,
                    type = "Form",
                    properties = model.FormProperties,
                    events = model.FormEvents.Select(e => new { e.EventName, e.HandlerMethodName }),
                    childCount = model.RootControls.Count
                };
                return JsonSerializer.Serialize(formResult, JsonOptions);
            }

            return JsonSerializer.Serialize(new { error = $"Control '{controlName}' not found." }, JsonOptions);
        }

        var result = new
        {
            name = control.Name,
            type = control.ControlType,
            properties = control.Properties,
            events = control.Events.Select(e => new { e.EventName, e.HandlerMethodName }),
            childCount = control.Children.Count,
            children = control.Children.Select(c => c.Name)
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "parse_designer_file")]
    [Description(
        "Parses a WinForms designer file (.Designer.cs or .Designer.vb) into a " +
        "complete structured JSON representation of the form, including all controls, " +
        "properties, events, and parent-child relationships.")]
    public static async Task<string> ParseDesignerFile(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        CancellationToken cancellationToken)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);
        return JsonSerializer.Serialize(model, JsonOptions);
    }

    private static object FormatControlHierarchy(Models.ControlNode node)
    {
        return new
        {
            name = node.Name,
            type = node.ControlType,
            propertyCount = node.Properties.Count,
            eventCount = node.Events.Count,
            children = node.Children.Select(FormatControlHierarchy).ToList()
        };
    }
}
