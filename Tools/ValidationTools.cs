using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using WinFormsDesignerMcp.Models;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tools for validating WinForms designer files for accessibility and layout issues.
/// </summary>
[McpServerToolType]
public class ValidationTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "check_accessibility_compliance")]
    [Description(
        "Scans a WinForms designer file for accessibility issues: missing AccessibleName, " +
        "AccessibleDescription, TabIndex problems, missing Text on labels, and other common " +
        "accessibility compliance gaps. Returns a detailed report with severity levels.")]
    public static async Task<string> CheckAccessibilityCompliance(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        CancellationToken cancellationToken)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);
        var issues = new List<object>();

        // Controls that should have AccessibleName set.
        var interactiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Button", "TextBox", "ComboBox", "ListBox", "CheckBox", "RadioButton",
            "NumericUpDown", "DateTimePicker", "DataGridView", "ListView", "TreeView",
            "RichTextBox", "PictureBox", "TrackBar", "ProgressBar"
        };

        foreach (var control in model.Controls)
        {
            var shortType = GetShortTypeName(control.ControlType);

            // Check for missing AccessibleName on interactive controls.
            if (interactiveTypes.Contains(shortType) &&
                !control.Properties.ContainsKey("AccessibleName"))
            {
                issues.Add(new
                {
                    control = control.Name,
                    controlType = shortType,
                    severity = "warning",
                    rule = "MissingAccessibleName",
                    message = $"'{control.Name}' ({shortType}) is missing AccessibleName. " +
                              "Screen readers need this to identify the control."
                });
            }

            // Check for missing AccessibleDescription.
            if (interactiveTypes.Contains(shortType) &&
                !control.Properties.ContainsKey("AccessibleDescription"))
            {
                issues.Add(new
                {
                    control = control.Name,
                    controlType = shortType,
                    severity = "info",
                    rule = "MissingAccessibleDescription",
                    message = $"'{control.Name}' ({shortType}) has no AccessibleDescription. " +
                              "Consider adding one for better screen reader support."
                });
            }

            // Check for missing Text on interactive controls (should have a label or Text).
            if (shortType is "Button" or "CheckBox" or "RadioButton" or "GroupBox" or "Label" &&
                !control.Properties.ContainsKey("Text"))
            {
                issues.Add(new
                {
                    control = control.Name,
                    controlType = shortType,
                    severity = "warning",
                    rule = "MissingText",
                    message = $"'{control.Name}' ({shortType}) has no Text property set."
                });
            }

            // Check for duplicate or missing TabIndex.
            if (!control.Properties.ContainsKey("TabIndex") && interactiveTypes.Contains(shortType))
            {
                issues.Add(new
                {
                    control = control.Name,
                    controlType = shortType,
                    severity = "warning",
                    rule = "MissingTabIndex",
                    message = $"'{control.Name}' ({shortType}) has no TabIndex set. " +
                              "Keyboard navigation order may be unexpected."
                });
            }
        }

        // Check for duplicate TabIndex values.
        var tabIndices = model.Controls
            .Where(c => c.Properties.ContainsKey("TabIndex"))
            .Select(c => new
            {
                c.Name,
                TabIndex = int.TryParse(c.Properties["TabIndex"], out var idx) ? idx : -1
            })
            .Where(x => x.TabIndex >= 0)
            .GroupBy(x => x.TabIndex)
            .Where(g => g.Count() > 1);

        foreach (var group in tabIndices)
        {
            var names = string.Join(", ", group.Select(x => x.Name));
            issues.Add(new
            {
                control = names,
                controlType = "",
                severity = "warning",
                rule = "DuplicateTabIndex",
                message = $"Controls [{names}] share TabIndex {group.Key}. Tab order will be ambiguous."
            });
        }

        // Check form-level accessibility.
        if (!model.FormProperties.ContainsKey("Text") ||
            string.IsNullOrWhiteSpace(model.FormProperties.GetValueOrDefault("Text")))
        {
            issues.Add(new
            {
                control = model.FormName,
                controlType = "Form",
                severity = "warning",
                rule = "MissingFormText",
                message = "The form has no Text (title bar caption). This is used as the accessible name of the window."
            });
        }

        var warningCount = issues.Count(i => ((dynamic)i).severity == "warning");
        var infoCount = issues.Count(i => ((dynamic)i).severity == "info");

        var result = new
        {
            filePath,
            formName = model.FormName,
            totalControls = model.Controls.Count,
            issueCount = issues.Count,
            warnings = warningCount,
            informational = infoCount,
            passed = issues.Count == 0,
            issues
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;
    }
}
