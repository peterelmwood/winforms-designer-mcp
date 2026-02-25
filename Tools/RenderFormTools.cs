using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using WinFormsDesignerMcp.Models;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tool that renders a WinForms designer file as an SVG wireframe diagram.
/// </summary>
[McpServerToolType]
public partial class RenderFormTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Color palette for different control categories.
    private static readonly Dictionary<
        string,
        (string Fill, string Stroke, string Icon)
    > ControlStyles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = ("#E3F2FD", "#1565C0", "\U0001F532"),
        ["TextBox"] = ("#FFF3E0", "#E65100", "\u270F"),
        ["RichTextBox"] = ("#FFF3E0", "#E65100", "\U0001F4DD"),
        ["Label"] = ("#F3E5F5", "#6A1B9A", "A"),
        ["ComboBox"] = ("#E8F5E9", "#2E7D32", "\u25BE"),
        ["ListBox"] = ("#E8F5E9", "#2E7D32", "\u2630"),
        ["CheckBox"] = ("#FCE4EC", "#AD1457", "\u2611"),
        ["RadioButton"] = ("#FCE4EC", "#AD1457", "\u25C9"),
        ["DataGridView"] = ("#E0F7FA", "#00695C", "\u2637"),
        ["Panel"] = ("#ECEFF1", "#37474F", ""),
        ["GroupBox"] = ("#ECEFF1", "#37474F", ""),
        ["TabControl"] = ("#ECEFF1", "#37474F", "\U0001F4C1"),
        ["SplitContainer"] = ("#ECEFF1", "#37474F", "\u2194"),
        ["FlowLayoutPanel"] = ("#ECEFF1", "#37474F", "\u2192"),
        ["TableLayoutPanel"] = ("#ECEFF1", "#37474F", "\u2637"),
        ["PictureBox"] = ("#F1F8E9", "#33691E", "\U0001F5BC"),
        ["ProgressBar"] = ("#E8EAF6", "#283593", "\u2587"),
        ["NumericUpDown"] = ("#FFF3E0", "#E65100", "#"),
        ["DateTimePicker"] = ("#FFF3E0", "#E65100", "\U0001F4C5"),
        ["TreeView"] = ("#E0F7FA", "#00695C", "\U0001F333"),
        ["ListView"] = ("#E0F7FA", "#00695C", "\u2630"),
        ["MenuStrip"] = ("#EFEBE9", "#3E2723", "\u2630"),
        ["ToolStrip"] = ("#EFEBE9", "#3E2723", "\U0001F527"),
        ["StatusStrip"] = ("#EFEBE9", "#3E2723", "\u2500"),
    };

    private static readonly (string Fill, string Stroke, string Icon) DefaultStyle = (
        "#F5F5F5",
        "#757575",
        ""
    );

    [McpServerTool(Name = "render_form_image")]
    [Description(
        "Renders a WinForms designer file (.Designer.cs or .Designer.vb) as an SVG wireframe diagram "
            + "showing all controls at their actual positions and sizes with type-specific colors and labels. "
            + "Useful for visualizing layout, spotting alignment issues, and understanding spatial relationships. "
            + "Returns SVG content as base64 or saves to a file."
    )]
    public static async Task<string> RenderFormImage(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description(
            "Optional output file path to save the SVG. If omitted, returns base64-encoded SVG in the response."
        )]
            string? outputPath = null,
        [Description("Padding in pixels around the form content (default: 20)")] int padding = 20,
        CancellationToken cancellationToken = default
    )
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        // Extract form dimensions from properties, with sensible defaults.
        var formWidth = ExtractDimension(model.FormProperties, "ClientSize", 0) ?? 300;
        var formHeight = ExtractDimension(model.FormProperties, "ClientSize", 1) ?? 300;
        var titleBarHeight = 30;

        var svgWidth = formWidth + padding * 2;
        var svgHeight = formHeight + titleBarHeight + padding * 2;

        var sb = new StringBuilder();

        // SVG header with embedded styles.
        sb.AppendLine(
            $"""<svg xmlns="http://www.w3.org/2000/svg" width="{svgWidth}" height="{svgHeight}" viewBox="0 0 {svgWidth} {svgHeight}" font-family="Segoe UI, sans-serif" font-size="11">"""
        );
        sb.AppendLine("  <defs>");
        sb.AppendLine("""    <style>""");
        sb.AppendLine("""      .control-label { font-size: 10px; fill: #424242; }""");
        sb.AppendLine("""      .control-type { font-size: 9px; fill: #9E9E9E; }""");
        sb.AppendLine("""      .control-text { font-size: 11px; fill: #212121; }""");
        sb.AppendLine(
            """      .form-title { font-size: 13px; font-weight: bold; fill: #212121; }"""
        );
        sb.AppendLine(
            """      .form-border { fill: #FAFAFA; stroke: #424242; stroke-width: 2; }"""
        );
        sb.AppendLine("""      .title-bar { fill: #1976D2; }""");
        sb.AppendLine("""      .title-text { font-size: 12px; fill: white; font-weight: bold; }""");
        sb.AppendLine("""    </style>""");
        sb.AppendLine("  </defs>");

        // Form background with title bar.
        var formTitle = model
            .FormProperties.GetValueOrDefault("Text", $"\"{model.FormName}\"")
            .Trim('"');

        sb.AppendLine(
            $"""  <rect x="{padding}" y="{padding}" width="{formWidth}" height="{formHeight + titleBarHeight}" class="form-border" rx="4" />"""
        );
        sb.AppendLine(
            $"""  <rect x="{padding}" y="{padding}" width="{formWidth}" height="{titleBarHeight}" class="title-bar" rx="4" />"""
        );
        sb.AppendLine(
            $"""  <rect x="{padding}" y="{padding + titleBarHeight - 4}" width="{formWidth}" height="4" class="title-bar" />"""
        );
        sb.AppendLine(
            $"""  <text x="{padding + 10}" y="{padding + 20}" class="title-text">{EscapeXml(formTitle)}</text>"""
        );

        // Window control buttons (decorative).
        var btnY = padding + 10;
        var btnX = padding + formWidth - 20;
        sb.AppendLine(
            $"""  <rect x="{btnX}" y="{btnY}" width="10" height="10" fill="#EF5350" rx="2" />"""
        );
        sb.AppendLine(
            $"""  <rect x="{btnX - 16}" y="{btnY}" width="10" height="10" fill="#FFC107" rx="2" />"""
        );
        sb.AppendLine(
            $"""  <rect x="{btnX - 32}" y="{btnY}" width="10" height="10" fill="#4CAF50" rx="2" />"""
        );

        // Offset for form client area (below title bar).
        var clientOffsetX = padding;
        var clientOffsetY = padding + titleBarHeight;

        // Render controls recursively.
        RenderControls(sb, model.RootControls, clientOffsetX, clientOffsetY);

        sb.AppendLine("</svg>");

        var svgContent = sb.ToString();

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            await File.WriteAllTextAsync(outputPath, svgContent, cancellationToken);
            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    format = "svg",
                    outputPath,
                    formName = model.FormName,
                    dimensions = new { width = svgWidth, height = svgHeight },
                    controlCount = model.Controls.Count,
                    message = $"SVG wireframe saved to {outputPath}",
                },
                JsonOptions
            );
        }
        else
        {
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svgContent));
            return JsonSerializer.Serialize(
                new
                {
                    success = true,
                    format = "svg",
                    mimeType = "image/svg+xml",
                    formName = model.FormName,
                    dimensions = new { width = svgWidth, height = svgHeight },
                    controlCount = model.Controls.Count,
                    base64Content = base64,
                    message = "SVG wireframe rendered successfully. Use the base64Content to display the image.",
                },
                JsonOptions
            );
        }
    }

    /// <summary>
    /// Recursively render controls as SVG elements.
    /// Renders in reverse order for correct Z-order: in WinForms Controls[0] has the highest
    /// Z-order (painted on top), while in SVG later elements appear on top.
    /// </summary>
    private static void RenderControls(
        StringBuilder sb,
        List<ControlNode> controls,
        int parentX,
        int parentY
    )
    {
        // Iterate back-to-front so that the first control in the collection (highest Z-order
        // in WinForms) is the last SVG element drawn and therefore appears on top.
        for (var i = controls.Count - 1; i >= 0; i--)
        {
            var control = controls[i];

            var x = ExtractPoint(control.Properties, "Location", 0) ?? 0;
            var y = ExtractPoint(control.Properties, "Location", 1) ?? 0;
            var w = ExtractDimension(control.Properties, "Size", 0) ?? 75;
            var h = ExtractDimension(control.Properties, "Size", 1) ?? 23;

            var absX = parentX + x;
            var absY = parentY + y;

            var shortType = GetShortTypeName(control.ControlType);
            var (fill, stroke, icon) = ControlStyles.GetValueOrDefault(shortType, DefaultStyle);

            var text = control.Properties.GetValueOrDefault("Text", "")?.Trim('"') ?? "";
            var controlName = control.Name;

            var isContainer =
                shortType
                    is "Panel"
                        or "GroupBox"
                        or "TabControl"
                        or "SplitContainer"
                        or "FlowLayoutPanel"
                        or "TableLayoutPanel";

            // Draw control rectangle.
            sb.AppendLine(
                $"""  <rect x="{absX}" y="{absY}" width="{w}" height="{h}" fill="{fill}" stroke="{stroke}" stroke-width="1.5" rx="3" />"""
            );

            // For containers, draw a subtle inner border to show nesting.
            if (isContainer)
            {
                sb.AppendLine(
                    $"""  <rect x="{absX + 1}" y="{absY + 1}" width="{w - 2}" height="{h - 2}" fill="none" stroke="{stroke}" stroke-width="0.5" stroke-dasharray="4,2" rx="2" />"""
                );
            }

            // Containers WITH children: only draw a minimal name tag to avoid overlapping
            // the child controls that are rendered on top.
            if (isContainer && control.Children.Count > 0)
            {
                if (shortType == "GroupBox" && !string.IsNullOrEmpty(text))
                {
                    // GroupBox caption positioned above the box border (doesn't overlap children).
                    sb.AppendLine(
                        $"""  <text x="{absX + 10}" y="{absY - 2}" class="control-label" font-weight="bold">{EscapeXml(text)}</text>"""
                    );
                }
                else
                {
                    // Subtle semi-transparent container name in the top-left corner.
                    sb.AppendLine(
                        $"""  <text x="{absX + 3}" y="{absY + 9}" class="control-type" opacity="0.5">{EscapeXml(controlName)}</text>"""
                    );
                }

                // Render children on top of the container background.
                RenderControls(sb, control.Children, absX, absY);
            }
            else
            {
                // Non-containers and empty containers: full label rendering.
                if (shortType == "GroupBox" && !string.IsNullOrEmpty(text))
                {
                    sb.AppendLine(
                        $"""  <text x="{absX + 10}" y="{absY - 2}" class="control-label" font-weight="bold">{EscapeXml(text)}</text>"""
                    );
                }
                // Label — display text prominently.
                else if (shortType == "Label" && !string.IsNullOrEmpty(text))
                {
                    var textY = absY + h / 2 + 4;
                    sb.AppendLine(
                        $"""  <text x="{absX + 3}" y="{textY}" class="control-text">{EscapeXml(text)}</text>"""
                    );
                }
                // Button — centered text.
                else if (shortType == "Button")
                {
                    var textX = absX + w / 2;
                    var textY = absY + h / 2 + 4;
                    var displayText = string.IsNullOrEmpty(text) ? controlName : text;
                    sb.AppendLine(
                        $"""  <text x="{textX}" y="{textY}" class="control-text" text-anchor="middle">{EscapeXml(displayText)}</text>"""
                    );
                }
                // Other controls — show type icon + name, and text if present.
                else
                {
                    var labelY = absY + 12;
                    if (h > 20)
                    {
                        var iconPrefix = !string.IsNullOrEmpty(icon) ? $"{icon} " : "";
                        sb.AppendLine(
                            $"""  <text x="{absX + 4}" y="{labelY}" class="control-label">{iconPrefix}{EscapeXml(controlName)}</text>"""
                        );
                        if (!string.IsNullOrEmpty(text) && shortType != "GroupBox")
                        {
                            sb.AppendLine(
                                $"""  <text x="{absX + 4}" y="{labelY + 13}" class="control-type">{EscapeXml(Truncate(text, 30))}</text>"""
                            );
                        }
                    }
                    else
                    {
                        // Short control — just show the name.
                        var textY = absY + h / 2 + 4;
                        sb.AppendLine(
                            $"""  <text x="{absX + 4}" y="{textY}" class="control-label">{EscapeXml(controlName)}</text>"""
                        );
                    }
                }

                // Draw a small type badge in the bottom-right corner for non-trivial controls.
                if (w > 60 && h > 25 && shortType != "Label" && shortType != "Button")
                {
                    sb.AppendLine(
                        $"""  <text x="{absX + w - 4}" y="{absY + h - 4}" class="control-type" text-anchor="end">{EscapeXml(shortType)}</text>"""
                    );
                }

                // Recursively render children (for empty containers that may later gain children).
                if (control.Children.Count > 0)
                {
                    RenderControls(sb, control.Children, absX, absY);
                }
            }
        }
    }

    /// <summary>
    /// Extract a numeric dimension from a Size or ClientSize property value like "new System.Drawing.Size(284, 261)".
    /// </summary>
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

    /// <summary>
    /// Extract a numeric value from a Point property like "new System.Drawing.Point(12, 80)".
    /// </summary>
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

    /// <summary>
    /// Extract the nth number from parenthesized arguments like "(284, 261)" or "(12, 80)".
    /// </summary>
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
        // Handle VB literal suffixes and float values.
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

    private static string EscapeXml(string text)
    {
        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string Truncate(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : text[..(maxLength - 1)] + "\u2026";
    }

    [GeneratedRegex(@"\(([^)]+)\)")]
    private static partial Regex ParenNumbersRegex();
}
