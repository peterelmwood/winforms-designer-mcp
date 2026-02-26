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
/// MCP tool that renders a WinForms designer file as an interactive HTML page
/// with native HTML form elements, a control tree sidebar, and a property inspector.
/// </summary>
[McpServerToolType]
public partial class RenderFormHtmlTools
{
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

        var sb = new StringBuilder();

        // Build complete self-contained HTML page.
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>{Esc(formTitle)} — WinForms Designer Preview</title>");
        AppendStyles(sb);
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header bar.
        sb.AppendLine("<header id=\"header\">");
        sb.AppendLine(
            $"  <span class=\"header-title\">WinForms Designer Preview &mdash; {Esc(formTitle)}</span>"
        );
        sb.AppendLine(
            "  <label class=\"header-toggle\"><input type=\"checkbox\" id=\"toggle-visible\" /> Respect Visible</label>"
        );
        sb.AppendLine(
            $"  <span class=\"header-info\">{model.Controls.Count} controls &middot; {model.Language} &middot; {Esc(Path.GetFileName(filePath))}</span>"
        );
        sb.AppendLine("</header>");

        // Main layout: sidebar + canvas area.
        sb.AppendLine("<div id=\"main\">");

        // Sidebar: control tree.
        sb.AppendLine("  <aside id=\"sidebar\">");
        sb.AppendLine("    <div class=\"sidebar-section\">");
        sb.AppendLine("      <h3>Control Tree</h3>");
        sb.AppendLine("      <ul class=\"tree\">");
        RenderTreeNodes(sb, model.RootControls, 8);
        sb.AppendLine("      </ul>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"sidebar-section\" id=\"property-panel\">");
        sb.AppendLine("      <h3>Properties</h3>");
        sb.AppendLine("      <p class=\"hint\">Click a control to inspect its properties.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </aside>");

        // Canvas area: form window.
        sb.AppendLine("  <div id=\"canvas-area\">");
        sb.AppendLine($"    <div class=\"form-window\" style=\"width:{formWidth}px;\">");

        // Title bar.
        sb.AppendLine("      <div class=\"form-titlebar\">");
        sb.AppendLine($"        <span class=\"form-titlebar-text\">{Esc(formTitle)}</span>");
        sb.AppendLine("        <span class=\"form-titlebar-buttons\">");
        sb.AppendLine("          <span class=\"tb-btn minimize\">&#x2013;</span>");
        sb.AppendLine("          <span class=\"tb-btn maximize\">&#x25A1;</span>");
        sb.AppendLine("          <span class=\"tb-btn close\">&#x2715;</span>");
        sb.AppendLine("        </span>");
        sb.AppendLine("      </div>");

        // Client area.
        sb.AppendLine(
            $"      <div class=\"form-client\" style=\"width:{formWidth}px;height:{formHeight}px;\">"
        );
        RenderHtmlControls(sb, model.RootControls, 8);
        sb.AppendLine("      </div>");

        sb.AppendLine("    </div>"); // form-window
        sb.AppendLine("  </div>"); // canvas-area
        sb.AppendLine("</div>"); // main

        // Embed control data as JSON for the property inspector.
        sb.AppendLine("<script>");
        sb.AppendLine("const controlData = {");
        EmitControlDataJson(sb, model);
        sb.AppendLine("};");
        AppendScript(sb);
        sb.AppendLine("</script>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var html = sb.ToString();
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

    // ─── Inline CSS ──────────────────────────────────────────────────────

    private static void AppendStyles(StringBuilder sb)
    {
        sb.AppendLine("<style>");
        sb.AppendLine(
            """
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
body { font-family: 'Segoe UI', system-ui, -apple-system, sans-serif; font-size: 13px;
  background: #1e1e1e; color: #d4d4d4; display: flex; flex-direction: column; height: 100vh; overflow: hidden; }

/* Header */
#header { background: #252526; border-bottom: 1px solid #333; padding: 8px 16px;
  display: flex; justify-content: space-between; align-items: center; flex-shrink: 0; }
.header-title { font-weight: 600; color: #e0e0e0; }
.header-toggle { display: flex; align-items: center; gap: 6px; color: #ccc; font-size: 12px;
  cursor: pointer; user-select: none; }
.header-toggle input { cursor: pointer; }
.header-info { font-size: 11px; color: #888; }

/* Visible property toggle — hides controls with Visible=False (cascades via DOM nesting) */
body.respect-visible .ctrl[data-visible="false"] { display: none !important; }

/* Main layout */
#main { display: flex; flex: 1; overflow: hidden; }

/* Sidebar */
#sidebar { width: 280px; min-width: 200px; background: #252526; border-right: 1px solid #333;
  overflow-y: auto; flex-shrink: 0; display: flex; flex-direction: column; }
.sidebar-section { padding: 12px; border-bottom: 1px solid #333; }
.sidebar-section h3 { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px;
  color: #888; margin-bottom: 8px; }
.hint { color: #666; font-size: 11px; font-style: italic; }

/* Tree */
.tree { list-style: none; padding-left: 14px; }
.tree-node { cursor: pointer; padding: 2px 0; }
.tree-label { display: inline-flex; align-items: center; gap: 4px; padding: 2px 6px;
  border-radius: 3px; user-select: none; }
.tree-label:hover { background: #2a2d2e; }
.tree-node.selected > .tree-label { background: #094771; color: #fff; }
.tn-name { color: #dcdcaa; }
.tn-type { color: #4ec9b0; font-size: 11px; }
.toggle { display: inline-block; font-size: 9px; width: 12px; transition: transform 0.15s; }
.tree-node.expanded > .tree-label .toggle { transform: rotate(90deg); }
.tree-node.has-children > ul { display: none; }
.tree-node.has-children.expanded > ul { display: block; }

/* Property panel */
#property-panel table { width: 100%; border-collapse: collapse; }
#property-panel th, #property-panel td { text-align: left; padding: 3px 6px;
  border-bottom: 1px solid #333; font-size: 12px; vertical-align: top; }
#property-panel th { color: #888; width: 40%; white-space: nowrap; }
#property-panel td { color: #ce9178; word-break: break-all; }
#property-panel tr.event th { color: #569cd6; }
#property-panel tr.event td { color: #dcdcaa; }

/* Canvas area */
#canvas-area { flex: 1; overflow: auto; padding: 32px; display: flex;
  justify-content: center; align-items: flex-start; }

/* Form window */
.form-window { background: #f0f0f0; border-radius: 8px; box-shadow: 0 8px 32px rgba(0,0,0,0.5);
  overflow: hidden; flex-shrink: 0; }
.form-titlebar { background: linear-gradient(180deg, #0078d4, #005fa3); color: white;
  height: 32px; display: flex; align-items: center; justify-content: space-between;
  padding: 0 8px; user-select: none; }
.form-titlebar-text { font-size: 12px; font-weight: 500; }
.form-titlebar-buttons { display: flex; gap: 2px; }
.tb-btn { width: 28px; height: 22px; display: flex; align-items: center; justify-content: center;
  border-radius: 3px; font-size: 11px; cursor: default; }
.tb-btn:hover { background: rgba(255,255,255,0.15); }
.tb-btn.close:hover { background: #e81123; }

/* Client area */
.form-client { position: relative; background: #f0f0f0; font-family: 'Segoe UI', sans-serif;
  font-size: 12px; color: #1e1e1e; overflow: hidden; }

/* Controls — base */
.ctrl { position: absolute; overflow: visible; transition: outline 0.1s; }
.ctrl-textbox, .ctrl-richtextbox, .ctrl-listbox, .ctrl-datagridview,
.ctrl-treeview, .ctrl-listview, .ctrl-picturebox { overflow: hidden; }
.ctrl:hover { outline: 2px solid #0078d4; outline-offset: -1px; cursor: pointer; }
.ctrl.highlighted { outline: 2px solid #ff0; outline-offset: -1px; }

/* Button */
.ctrl-button { background: #e1e1e1; border: 1px solid #adadad; border-radius: 3px;
  display: flex; align-items: center; justify-content: center; }
.native-btn { width: 100%; height: 100%; border: none; background: transparent;
  font: inherit; cursor: default; padding: 0 8px; }

/* TextBox / NumericUpDown / DateTimePicker */
.ctrl-textbox, .ctrl-numericupdown, .ctrl-datetimepicker {
  background: #fff; border: 1px solid #7a7a7a; }
.native-input { width: 100%; height: 100%; border: none; background: transparent;
  font: inherit; padding: 2px 4px; outline: none; }

/* RichTextBox */
.ctrl-richtextbox { background: #fff; border: 1px solid #7a7a7a; }
.native-textarea { width: 100%; height: 100%; border: none; background: transparent;
  font: inherit; padding: 2px 4px; outline: none; resize: none; }

/* Label */
.ctrl-label { background: transparent; display: flex; align-items: center; }
.native-label { padding: 0 2px; }

/* ComboBox */
.ctrl-combobox { background: #fff; border: 1px solid #7a7a7a; }
.native-select { width: 100%; height: 100%; border: none; background: transparent;
  font: inherit; padding: 0 4px; outline: none; appearance: auto; }

/* ListBox */
.ctrl-listbox { background: #fff; border: 1px solid #7a7a7a; }
.native-listbox { width: 100%; height: 100%; border: none; background: transparent;
  font: inherit; padding: 0; outline: none; }

/* CheckBox / RadioButton */
.ctrl-checkbox, .ctrl-radiobutton { background: transparent; display: flex; align-items: center; }
.native-check, .native-radio { display: flex; align-items: center; gap: 4px;
  font: inherit; cursor: default; white-space: nowrap; }

/* ProgressBar */
.ctrl-progressbar { background: #e6e6e6; border: 1px solid #bcbcbc; }
.native-progress { display: block; }

/* DataGridView */
.ctrl-datagridview { background: #fff; border: 1px solid #7a7a7a; overflow: auto; }
.native-datagrid { width: 100%; border-collapse: collapse; font-size: 11px; }
.native-datagrid th { background: #f0f0f0; border: 1px solid #d0d0d0; padding: 4px 8px;
  font-weight: 500; text-align: left; position: sticky; top: 0; }
.native-datagrid td { border: 1px solid #e0e0e0; padding: 4px 8px; height: 22px; }

/* TreeView */
.ctrl-treeview { background: #fff; border: 1px solid #7a7a7a; overflow: auto; padding: 4px; }
.native-treeview { font-size: 12px; }
.tv-item { padding: 2px 4px; }
.tv-item.indent { padding-left: 20px; }

/* ListView */
.ctrl-listview { background: #fff; border: 1px solid #7a7a7a; overflow: auto; }
.native-listview { font-size: 12px; }
.lv-header { background: #f0f0f0; border-bottom: 1px solid #d0d0d0; padding: 4px 8px; font-weight: 500; }
.lv-row { padding: 3px 8px; border-bottom: 1px solid #f0f0f0; }

/* PictureBox */
.ctrl-picturebox { background: #e0e0e0; border: 1px solid #ababab; display: flex;
  align-items: center; justify-content: center; }
.native-picturebox { color: #888; font-size: 12px; text-align: center; }

/* GroupBox */
.ctrl-groupbox { background: transparent; border: 1px solid #999; border-radius: 3px; padding-top: 4px; }
.native-groupbox { border: none; padding: 0; margin: 0; position: absolute; top: -10px;
  left: 6px; font-size: 12px; }
.native-groupbox legend { padding: 0 4px; color: #333; }

/* TabControl */
.ctrl-tabcontrol { background: #f0f0f0; border: 1px solid #999; }
.native-tabcontrol { height: 100%; }
.tab-header { display: flex; border-bottom: 1px solid #999; background: #e0e0e0; }
.tab { padding: 4px 12px; border-right: 1px solid #ccc; cursor: pointer; font-size: 11px;
  user-select: none; }
.tab:hover { background: #d0d0d0; }
.tab.active { background: #f0f0f0; border-bottom: 1px solid #f0f0f0; }
.tab.active:hover { background: #f0f0f0; }

/* Panel and container types */
.ctrl-panel, .ctrl-splitcontainer, .ctrl-flowlayoutpanel, .ctrl-tablelayoutpanel,
.ctrl-tabpage {
  background: rgba(0,0,0,0.02); border: 1px dashed #bbb; overflow: hidden; }
.container-label { position: absolute; top: 2px; left: 4px; font-size: 9px;
  color: #999; pointer-events: none; }

/* MenuStrip */
.ctrl-menustrip { background: #f0f0f0; border-bottom: 1px solid #d0d0d0; display: flex; align-items: center; }
.native-menustrip { display: flex; height: 100%; align-items: center; }
.menu-item { padding: 4px 10px; cursor: default; font-size: 12px; }
.menu-item:hover { background: #e0e0e0; }

/* ToolStrip */
.ctrl-toolstrip { background: #f0f0f0; border-bottom: 1px solid #d0d0d0; display: flex; align-items: center; }
.native-toolstrip { display: flex; height: 100%; align-items: center; gap: 2px; padding: 0 4px; }
.tool-btn { padding: 2px 6px; cursor: default; }

/* StatusStrip */
.ctrl-statusstrip { background: #007acc; color: #fff; display: flex; align-items: center; }
.native-statusstrip { display: flex; height: 100%; align-items: center; padding: 0 8px; }
.status-text { font-size: 11px; }

/* Generic fallback */
.generic-ctrl { display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 100%; color: #666; text-align: center; background: #f5f5f5; border: 1px solid #ccc; border-radius: 3px; }
"""
        );
        sb.AppendLine("</style>");
    }

    // ─── Inline JavaScript ───────────────────────────────────────────────

    private static void AppendScript(StringBuilder sb)
    {
        sb.AppendLine(
            """
// Visible property toggle — adds/removes body class to hide controls with Visible=False.
// Invisibility cascades to children via DOM nesting (parent hidden = children hidden).
document.getElementById('toggle-visible').addEventListener('change', e => {
  document.body.classList.toggle('respect-visible', e.target.checked);
});

// Tree toggle — also select the control so tab pages switch when clicked.
document.querySelectorAll('.tree-node.has-children > .tree-label').forEach(label => {
  label.addEventListener('click', e => {
    e.stopPropagation();
    const node = label.parentElement;
    node.classList.toggle('expanded');
    selectControl(node.dataset.target);
  });
});

// Tab switching — show the target tab page, hide its siblings.
function switchTab(tabPageName) {
  const tabPage = document.querySelector(`.ctrl-tabpage[data-name="${tabPageName}"]`);
  if (!tabPage) return;
  const parent = tabPage.parentElement;
  if (!parent) return;
  // Toggle visibility of sibling tab pages.
  parent.querySelectorAll(':scope > .ctrl-tabpage').forEach(tp => {
    tp.style.display = tp.dataset.name === tabPageName ? '' : 'none';
  });
  // Update active tab header.
  parent.querySelectorAll('.tab[data-tab-target]').forEach(tab => {
    tab.classList.toggle('active', tab.dataset.tabTarget === tabPageName);
  });
}

// Ensure a control's ancestor tab pages are all visible.
function ensureVisible(element) {
  if (!element) return;
  // Collect ancestor tab pages from innermost to outermost.
  const ancestors = [];
  let el = element.closest('.ctrl-tabpage');
  while (el) {
    ancestors.push(el);
    el = el.parentElement?.closest('.ctrl-tabpage');
  }
  // Switch from outermost to innermost so parent tabs are visible first.
  for (let i = ancestors.length - 1; i >= 0; i--) {
    switchTab(ancestors[i].dataset.name);
  }
}

// Tab header click handlers.
document.querySelectorAll('.tab[data-tab-target]').forEach(tab => {
  tab.addEventListener('click', e => {
    e.stopPropagation();
    switchTab(tab.dataset.tabTarget);
  });
});

// Control click — highlight + show properties
function selectControl(name) {
  // Remove previous highlights.
  document.querySelectorAll('.ctrl.highlighted, .tree-node.selected').forEach(el => {
    el.classList.remove('highlighted', 'selected');
  });

  // Highlight control in canvas.
  const ctrl = document.querySelector(`.ctrl[data-name="${name}"]`);
  if (ctrl) {
    // Make sure any ancestor tab pages are visible.
    ensureVisible(ctrl);
    // If this IS a tab page, switch to it.
    if (ctrl.classList.contains('ctrl-tabpage')) {
      switchTab(name);
    }
    ctrl.classList.add('highlighted');
    ctrl.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  // Highlight tree node.
  const treeNode = document.querySelector(`.tree-node[data-target="${name}"]`);
  if (treeNode) {
    treeNode.classList.add('selected');
    // Expand parents.
    let parent = treeNode.parentElement?.closest('.tree-node');
    while (parent) {
      parent.classList.add('expanded');
      parent = parent.parentElement?.closest('.tree-node');
    }
  }

  // Show properties.
  const panel = document.getElementById('property-panel');
  const data = controlData[name];
  if (!data) {
    panel.innerHTML = '<h3>Properties</h3><p class="hint">No data for ' + name + '</p>';
    return;
  }

  let html = '<h3>' + name + '</h3><table>';
  for (const [key, value] of Object.entries(data)) {
    const isEvent = key.startsWith('Event:');
    const displayKey = isEvent ? key.substring(6) : key;
    html += `<tr class="${isEvent ? 'event' : ''}"><th>${displayKey}</th><td>${value}</td></tr>`;
  }
  html += '</table>';
  panel.innerHTML = html;
}

// Click handlers on controls in canvas.
document.querySelectorAll('.ctrl').forEach(el => {
  el.addEventListener('click', e => {
    e.stopPropagation();
    selectControl(el.dataset.name);
  });
});

// Click handlers on tree nodes.
document.querySelectorAll('.tree-node').forEach(el => {
  el.addEventListener('click', e => {
    e.stopPropagation();
    selectControl(el.dataset.target);
  });
});

// Expand all tree nodes by default.
document.querySelectorAll('.tree-node.has-children').forEach(el => el.classList.add('expanded'));
"""
        );
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
