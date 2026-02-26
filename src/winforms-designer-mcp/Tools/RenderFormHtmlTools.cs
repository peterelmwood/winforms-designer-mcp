using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using WinFormsDesignerMcp.Models;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tool that renders a WinForms designer file as an interactive HTML page
/// with native HTML form elements, a control tree sidebar, and a property inspector.
/// The HTML shell, CSS, and JavaScript live in Templates/FormPreview.html (embedded resource).
/// This class generates the dynamic fragments and substitutes them into the template.
/// </summary>
[McpServerToolType]
public partial class RenderFormHtmlTools
{
    private const string TemplateResourceName = "WinFormsDesignerMcp.Templates.FormPreview.html";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [McpServerTool(Name = "render_form_html")]
    [Description(
        "Renders a WinForms designer file (.Designer.cs or .Designer.vb) as an interactive HTML page "
            + "that maps controls to native HTML elements (buttons, inputs, selects, tables, etc.) "
            + "positioned at their actual designer coordinates. Includes a collapsible control tree "
            + "sidebar and a property inspector panel. The output is a self-contained .html file "
            + "that can be opened directly in a browser. Always saves to a file."
    )]
    public static async Task<string> RenderFormHtml(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description("Output file path for the HTML file (e.g., /tmp/form.html)")]
            string outputPath,
        CancellationToken cancellationToken = default
    )
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        var formWidth = ExtractDimension(model.FormProperties, "ClientSize", 0) ?? 300;
        var formHeight = ExtractDimension(model.FormProperties, "ClientSize", 1) ?? 300;
        var formTitle = model
            .FormProperties.GetValueOrDefault("Text", $"\"{model.FormName}\"")
            .Trim('"');

        // Generate the dynamic HTML fragments.
        var treeNodesSb = new StringBuilder();
        RenderTreeNodes(treeNodesSb, model.RootControls, 8);

        var controlsSb = new StringBuilder();
        RenderHtmlControls(controlsSb, model.RootControls, 8);

        var jsonSb = new StringBuilder();
        EmitControlDataJson(jsonSb, model);

        // Load the template and perform token substitution.
        var template = LoadTemplate();
        var html = template
            .Replace("{{FORM_TITLE}}", Esc(formTitle))
            .Replace("{{FORM_WIDTH}}", formWidth.ToString(CultureInfo.InvariantCulture))
            .Replace("{{FORM_HEIGHT}}", formHeight.ToString(CultureInfo.InvariantCulture))
            .Replace(
                "{{CONTROL_COUNT}}",
                model.Controls.Count.ToString(CultureInfo.InvariantCulture)
            )
            .Replace("{{LANGUAGE}}", Esc(model.Language.ToString()))
            .Replace("{{FILE_NAME}}", Esc(Path.GetFileName(filePath)))
            .Replace("{{TREE_NODES}}", treeNodesSb.ToString())
            .Replace("{{CONTROLS_HTML}}", controlsSb.ToString())
            .Replace("{{CONTROL_DATA_JSON}}", jsonSb.ToString());

        await File.WriteAllTextAsync(outputPath, html, cancellationToken);

        return JsonSerializer.Serialize(
            new
            {
                success = true,
                format = "html",
                outputPath,
                formName = model.FormName,
                dimensions = new { width = formWidth, height = formHeight },
                controlCount = model.Controls.Count,
                message = $"Interactive HTML preview saved to {outputPath}. Open in a browser to inspect.",
            },
            JsonOptions
        );
    }

    // ─── Template loading ────────────────────────────────────────────────

    private static string? s_cachedTemplate;

    /// <summary>
    /// Load the HTML template from the embedded resource. Cached after first load.
    /// </summary>
    private static string LoadTemplate()
    {
        if (s_cachedTemplate is not null)
            return s_cachedTemplate;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream =
            assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{TemplateResourceName}' not found."
            );
        using var reader = new StreamReader(stream, Encoding.UTF8);
        s_cachedTemplate = reader.ReadToEnd();
        return s_cachedTemplate;
    }

    // ─── HTML control rendering ──────────────────────────────────────────

    /// <summary>
    /// Recursively render controls as positioned HTML elements using native form elements
    /// where applicable. Renders in reverse for correct Z-order (WinForms Controls[0] is on top).
    /// </summary>
    private static void RenderHtmlControls(
        StringBuilder sb,
        List<ControlNode> controls,
        int indent,
        bool isTabControlChildren = false
    )
    {
        var pad = new string(' ', indent);

        for (var i = controls.Count - 1; i >= 0; i--)
        {
            var ctrl = controls[i];

            var x = ExtractPoint(ctrl.Properties, "Location", 0) ?? 0;
            var y = ExtractPoint(ctrl.Properties, "Location", 1) ?? 0;
            var w = ExtractDimension(ctrl.Properties, "Size", 0) ?? 75;
            var h = ExtractDimension(ctrl.Properties, "Size", 1) ?? 23;

            var shortType = GetShortTypeName(ctrl.ControlType);
            var text = ctrl.Properties.GetValueOrDefault("Text", "")?.Trim('"') ?? "";
            var name = ctrl.Name;

            var zIndex = controls.Count - i; // first in list = highest z-order
            var posStyle = $"left:{x}px;top:{y}px;width:{w}px;height:{h}px;z-index:{zIndex}";

            // Hide non-first tab pages so only the default tab is visible.
            if (isTabControlChildren && shortType == "TabPage" && i != 0)
            {
                posStyle += ";display:none";
            }

            // Mark controls that have Visible = False so the toggle can hide them.
            var visibleAttr = "";
            if (
                ctrl.Properties.TryGetValue("Visible", out var visVal)
                && visVal.Equals("False", StringComparison.OrdinalIgnoreCase)
            )
            {
                visibleAttr = " data-visible=\"false\"";
            }

            sb.AppendLine(
                $"{pad}<div class=\"ctrl ctrl-{shortType.ToLowerInvariant()}\" "
                    + $"data-name=\"{Esc(name)}\" data-type=\"{Esc(shortType)}\"{visibleAttr} "
                    + $"style=\"{posStyle}\" title=\"{Esc(name)} ({Esc(shortType)})\">"
            );

            // Render the inner content based on control type.
            RenderControlInner(sb, ctrl, shortType, text, name, w, h, indent + 2);

            // Render children inside the container.
            if (ctrl.Children.Count > 0)
            {
                RenderHtmlControls(sb, ctrl.Children, indent + 2, shortType == "TabControl");
            }

            sb.AppendLine($"{pad}</div>");
        }
    }

    /// <summary>
    /// Render the inner HTML content for a control, mapping to native HTML elements.
    /// </summary>
    private static void RenderControlInner(
        StringBuilder sb,
        ControlNode ctrl,
        string shortType,
        string text,
        string name,
        int w,
        int h,
        int indent
    )
    {
        var pad = new string(' ', indent);

        switch (shortType)
        {
            case "Button":
                var btnText = string.IsNullOrEmpty(text) ? name : text;
                sb.AppendLine(
                    $"{pad}<button class=\"native-btn\" tabindex=\"-1\">{Esc(btnText)}</button>"
                );
                break;

            case "TextBox":
                var placeholder =
                    ctrl.Properties.GetValueOrDefault("PlaceholderText", "")?.Trim('"') ?? "";
                sb.AppendLine(
                    $"{pad}<input type=\"text\" class=\"native-input\" value=\"{Esc(text)}\" "
                        + $"placeholder=\"{Esc(placeholder)}\" tabindex=\"-1\" />"
                );
                break;

            case "RichTextBox":
                sb.AppendLine(
                    $"{pad}<textarea class=\"native-textarea\" tabindex=\"-1\">{Esc(text)}</textarea>"
                );
                break;

            case "Label":
                sb.AppendLine($"{pad}<span class=\"native-label\">{Esc(text)}</span>");
                break;

            case "ComboBox":
                sb.AppendLine($"{pad}<select class=\"native-select\" tabindex=\"-1\">");
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine($"{pad}  <option>{Esc(text)}</option>");
                else
                    sb.AppendLine($"{pad}  <option>{Esc(name)}</option>");
                sb.AppendLine($"{pad}</select>");
                break;

            case "ListBox":
                sb.AppendLine(
                    $"{pad}<select class=\"native-listbox\" multiple size=\"{Math.Max(2, h / 18)}\" tabindex=\"-1\">"
                );
                sb.AppendLine($"{pad}  <option>(items)</option>");
                sb.AppendLine($"{pad}</select>");
                break;

            case "CheckBox":
                sb.AppendLine(
                    $"{pad}<label class=\"native-check\"><input type=\"checkbox\" tabindex=\"-1\" /> {Esc(text)}</label>"
                );
                break;

            case "RadioButton":
                sb.AppendLine(
                    $"{pad}<label class=\"native-radio\"><input type=\"radio\" tabindex=\"-1\" /> {Esc(text)}</label>"
                );
                break;

            case "NumericUpDown":
                sb.AppendLine(
                    $"{pad}<input type=\"number\" class=\"native-input\" tabindex=\"-1\" />"
                );
                break;

            case "DateTimePicker":
                sb.AppendLine(
                    $"{pad}<input type=\"date\" class=\"native-input\" tabindex=\"-1\" />"
                );
                break;

            case "ProgressBar":
                sb.AppendLine(
                    $"{pad}<progress class=\"native-progress\" max=\"100\" value=\"50\" style=\"width:100%;height:100%\"></progress>"
                );
                break;

            case "DataGridView":
                RenderDataGrid(sb, w, indent);
                break;

            case "TreeView":
                sb.AppendLine($"{pad}<div class=\"native-treeview\">");
                sb.AppendLine($"{pad}  <div class=\"tv-item\">&#9654; Node 1</div>");
                sb.AppendLine($"{pad}  <div class=\"tv-item indent\">&#9654; Child</div>");
                sb.AppendLine($"{pad}  <div class=\"tv-item\">&#9654; Node 2</div>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "ListView":
                sb.AppendLine($"{pad}<div class=\"native-listview\">");
                sb.AppendLine($"{pad}  <div class=\"lv-header\">Column 1</div>");
                sb.AppendLine($"{pad}  <div class=\"lv-row\">Item 1</div>");
                sb.AppendLine($"{pad}  <div class=\"lv-row\">Item 2</div>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "PictureBox":
                sb.AppendLine($"{pad}<div class=\"native-picturebox\">&#128444; {Esc(name)}</div>");
                break;

            case "GroupBox":
                sb.AppendLine(
                    $"{pad}<fieldset class=\"native-groupbox\"><legend>{Esc(text)}</legend></fieldset>"
                );
                break;

            case "TabControl":
                sb.AppendLine($"{pad}<div class=\"native-tabcontrol\">");
                sb.AppendLine($"{pad}  <div class=\"tab-header\">");
                for (var ti = 0; ti < ctrl.Children.Count; ti++)
                {
                    var tabChild = ctrl.Children[ti];
                    var tabText = tabChild.Properties.GetValueOrDefault("Text", "")?.Trim('"');
                    if (string.IsNullOrEmpty(tabText))
                        tabText = tabChild.Name;
                    var activeClass = ti == 0 ? " active" : "";
                    sb.AppendLine(
                        $"{pad}    <span class=\"tab{activeClass}\" data-tab-target=\"{Esc(tabChild.Name)}\">{Esc(tabText)}</span>"
                    );
                }
                sb.AppendLine($"{pad}  </div>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "MenuStrip":
                sb.AppendLine($"{pad}<div class=\"native-menustrip\">");
                sb.AppendLine($"{pad}  <span class=\"menu-item\">File</span>");
                sb.AppendLine($"{pad}  <span class=\"menu-item\">Edit</span>");
                sb.AppendLine($"{pad}  <span class=\"menu-item\">View</span>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "ToolStrip":
                sb.AppendLine($"{pad}<div class=\"native-toolstrip\">");
                sb.AppendLine($"{pad}  <span class=\"tool-btn\">&#128295;</span>");
                sb.AppendLine($"{pad}  <span class=\"tool-btn\">&#128196;</span>");
                sb.AppendLine($"{pad}  <span class=\"tool-btn\">&#128190;</span>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "StatusStrip":
                sb.AppendLine($"{pad}<div class=\"native-statusstrip\">");
                sb.AppendLine($"{pad}  <span class=\"status-text\">Ready</span>");
                sb.AppendLine($"{pad}</div>");
                break;

            case "Panel":
            case "SplitContainer":
            case "FlowLayoutPanel":
            case "TableLayoutPanel":
            case "TabPage":
                // Containers: just show a subtle label; children are rendered separately.
                if (ctrl.Children.Count == 0)
                {
                    sb.AppendLine($"{pad}<span class=\"container-label\">{Esc(name)}</span>");
                }
                break;

            default:
                // Unknown control type — show name and type.
                // If the control has children, render as container to avoid covering them.
                if (ctrl.Children.Count > 0)
                {
                    sb.AppendLine(
                        $"{pad}<span class=\"container-label\">{Esc(name)} ({Esc(shortType)})</span>"
                    );
                }
                else
                {
                    sb.AppendLine(
                        $"{pad}<span class=\"generic-ctrl\">{Esc(name)}<br/><small>{Esc(shortType)}</small></span>"
                    );
                }
                break;
        }
    }

    /// <summary>
    /// Render a placeholder DataGridView as an HTML table.
    /// </summary>
    private static void RenderDataGrid(StringBuilder sb, int w, int indent)
    {
        var pad = new string(' ', indent);
        var cols = Math.Max(2, Math.Min(5, w / 80));

        sb.AppendLine($"{pad}<table class=\"native-datagrid\">");
        sb.AppendLine($"{pad}  <thead><tr>");
        for (var c = 0; c < cols; c++)
            sb.AppendLine($"{pad}    <th>Column {c + 1}</th>");
        sb.AppendLine($"{pad}  </tr></thead>");
        sb.AppendLine($"{pad}  <tbody>");
        for (var r = 0; r < 3; r++)
        {
            sb.AppendLine($"{pad}    <tr>");
            for (var c = 0; c < cols; c++)
                sb.AppendLine($"{pad}      <td></td>");
            sb.AppendLine($"{pad}    </tr>");
        }
        sb.AppendLine($"{pad}  </tbody>");
        sb.AppendLine($"{pad}</table>");
    }

    // ─── Control tree sidebar ────────────────────────────────────────────

    /// <summary>
    /// Render collapsible tree nodes for the sidebar.
    /// </summary>
    private static void RenderTreeNodes(StringBuilder sb, List<ControlNode> controls, int indent)
    {
        var pad = new string(' ', indent);
        foreach (var ctrl in controls)
        {
            var shortType = GetShortTypeName(ctrl.ControlType);
            var hasChildren = ctrl.Children.Count > 0;
            sb.AppendLine(
                $"{pad}<li class=\"tree-node{(hasChildren ? " has-children" : "")}\" "
                    + $"data-target=\"{Esc(ctrl.Name)}\">"
            );
            sb.AppendLine(
                $"{pad}  <span class=\"tree-label\">"
                    + $"{(hasChildren ? "<span class=\"toggle\">&#9654;</span> " : "")}"
                    + $"<span class=\"tn-name\">{Esc(ctrl.Name)}</span> "
                    + $"<span class=\"tn-type\">{Esc(shortType)}</span>"
                    + $"</span>"
            );
            if (hasChildren)
            {
                sb.AppendLine($"{pad}  <ul class=\"tree\">");
                RenderTreeNodes(sb, ctrl.Children, indent + 4);
                sb.AppendLine($"{pad}  </ul>");
            }
            sb.AppendLine($"{pad}</li>");
        }
    }

    // ─── Control data JSON for property inspector ────────────────────────

    /// <summary>
    /// Emit all control properties as a JavaScript object for the property inspector.
    /// </summary>
    private static void EmitControlDataJson(StringBuilder sb, FormModel model)
    {
        // Form-level properties.
        sb.Append("  \"$form\": {");
        EmitPropertyEntries(sb, model.FormProperties);
        if (model.FormEvents.Count > 0)
        {
            sb.Append(", ");
            foreach (var evt in model.FormEvents)
            {
                sb.Append(
                    $"\"Event:{EscJs(evt.EventName)}\": \"{EscJs(evt.HandlerMethodName)}\", "
                );
            }
        }
        sb.AppendLine("},");

        // Each control.
        foreach (var ctrl in model.Controls)
        {
            sb.Append($"  \"{EscJs(ctrl.Name)}\": {{");
            sb.Append($"\"Type\": \"{EscJs(ctrl.ControlType)}\"");
            if (ctrl.Properties.Count > 0)
            {
                sb.Append(", ");
                EmitPropertyEntries(sb, ctrl.Properties);
            }
            if (ctrl.Events.Count > 0)
            {
                sb.Append(", ");
                foreach (var evt in ctrl.Events)
                {
                    sb.Append(
                        $"\"Event:{EscJs(evt.EventName)}\": \"{EscJs(evt.HandlerMethodName)}\", "
                    );
                }
            }
            sb.AppendLine("},");
        }
    }

    private static void EmitPropertyEntries(StringBuilder sb, Dictionary<string, string> props)
    {
        var first = true;
        foreach (var (key, value) in props)
        {
            if (!first)
                sb.Append(", ");
            sb.Append($"\"{EscJs(key)}\": \"{EscJs(value)}\"");
            first = false;
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static int? ExtractDimension(
        Dictionary<string, string> properties,
        string propertyName,
        int index
    )
    {
        if (!properties.TryGetValue(propertyName, out var value))
            return null;
        return ExtractNumberFromParens(value, index);
    }

    private static int? ExtractPoint(
        Dictionary<string, string> properties,
        string propertyName,
        int index
    )
    {
        if (!properties.TryGetValue(propertyName, out var value))
            return null;
        return ExtractNumberFromParens(value, index);
    }

    private static int? ExtractNumberFromParens(string value, int index)
    {
        var match = ParenNumbersRegex().Match(value);
        if (!match.Success)
            return null;

        var inner = match.Groups[1].Value;
        var parts = inner.Split(',');
        if (index >= parts.Length)
            return null;

        var part = parts[index].Trim().TrimEnd('!');
        if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
            return intVal;
        if (
            float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal)
        )
            return (int)floatVal;

        return null;
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;
    }

    /// <summary>
    /// HTML-escape a string.
    /// </summary>
    private static string Esc(string text)
    {
        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Escape a string for use inside a JavaScript string literal.
    /// </summary>
    private static string EscJs(string text)
    {
        return text.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex ParenNumbersRegex();
}
