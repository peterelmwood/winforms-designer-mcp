using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using WinFormsDesignerMcp.Models;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tools for placing, modifying, and removing controls in WinForms designer files.
/// </summary>
[McpServerToolType]
public class LayoutTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Known default properties for common control types.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> SmartDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = new()
        {
            ["Size"] = "new System.Drawing.Size(75, 23)",
            ["UseVisualStyleBackColor"] = "true"
        },
        ["TextBox"] = new()
        {
            ["Size"] = "new System.Drawing.Size(100, 23)"
        },
        ["Label"] = new()
        {
            ["AutoSize"] = "true"
        },
        ["ComboBox"] = new()
        {
            ["DropDownStyle"] = "System.Windows.Forms.ComboBoxStyle.DropDownList",
            ["Size"] = "new System.Drawing.Size(121, 23)"
        },
        ["CheckBox"] = new()
        {
            ["AutoSize"] = "true",
            ["UseVisualStyleBackColor"] = "true"
        },
        ["RadioButton"] = new()
        {
            ["AutoSize"] = "true",
            ["UseVisualStyleBackColor"] = "true"
        },
        ["ListBox"] = new()
        {
            ["Size"] = "new System.Drawing.Size(120, 96)"
        },
        ["DataGridView"] = new()
        {
            ["Size"] = "new System.Drawing.Size(240, 150)",
            ["ColumnHeadersHeightSizeMode"] = "System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize"
        },
        ["Panel"] = new()
        {
            ["Size"] = "new System.Drawing.Size(200, 100)"
        },
        ["GroupBox"] = new()
        {
            ["Size"] = "new System.Drawing.Size(200, 100)"
        },
        ["TabControl"] = new()
        {
            ["Size"] = "new System.Drawing.Size(200, 100)"
        },
        ["PictureBox"] = new()
        {
            ["Size"] = "new System.Drawing.Size(100, 50)",
            ["SizeMode"] = "System.Windows.Forms.PictureBoxSizeMode.Zoom"
        },
        ["ProgressBar"] = new()
        {
            ["Size"] = "new System.Drawing.Size(100, 23)"
        },
        ["NumericUpDown"] = new()
        {
            ["Size"] = "new System.Drawing.Size(120, 23)"
        },
        ["DateTimePicker"] = new()
        {
            ["Size"] = "new System.Drawing.Size(200, 23)"
        },
        ["RichTextBox"] = new()
        {
            ["Size"] = "new System.Drawing.Size(100, 96)"
        },
        ["TreeView"] = new()
        {
            ["Size"] = "new System.Drawing.Size(121, 97)"
        },
        ["ListView"] = new()
        {
            ["Size"] = "new System.Drawing.Size(121, 97)"
        },
        ["MenuStrip"] = new()
        {
            ["Dock"] = "System.Windows.Forms.DockStyle.Top"
        },
        ["StatusStrip"] = new()
        {
            ["Dock"] = "System.Windows.Forms.DockStyle.Bottom"
        },
        ["ToolStrip"] = new()
        {
            ["Dock"] = "System.Windows.Forms.DockStyle.Top"
        }
    };

    [McpServerTool(Name = "place_control")]
    [Description(
        "Adds a new control to a WinForms designer file with smart defaults for common types. " +
        "The control is added to the specified parent control or the form itself. " +
        "Returns the updated control list.")]
    public static async Task<string> PlaceControl(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description("The control type (e.g., 'Button', 'TextBox', 'DataGridView'). " +
                     "Can be a short name or fully-qualified like 'System.Windows.Forms.Button'.")] string controlType,
        [Description("Optional name for the control. Auto-generated if omitted (e.g., 'button1').")] string? name = null,
        [Description("Optional parent control name. Defaults to the form itself.")] string? parentName = null,
        [Description("Optional JSON object of property overrides (e.g., {\"Text\": \"\\\"OK\\\"\", \"Location\": \"new System.Drawing.Point(10, 10)\"})")] string? properties = null,
        CancellationToken cancellationToken = default)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        // Resolve the full type name.
        var fullType = ResolveFullTypeName(controlType);
        var shortType = GetShortTypeName(fullType);

        // Auto-generate name if not provided.
        var controlName = name ?? GenerateControlName(shortType, model);

        // Build the control node with smart defaults.
        var newControl = new ControlNode
        {
            Name = controlName,
            ControlType = fullType
        };

        // Apply smart defaults.
        if (SmartDefaults.TryGetValue(shortType, out var defaults))
        {
            foreach (var (propName, value) in defaults)
            {
                newControl.Properties[propName] = value;
            }
        }

        // Always set Name and a default Location.
        newControl.Properties["Name"] = $"\"{controlName}\"";
        if (!newControl.Properties.ContainsKey("Location"))
        {
            newControl.Properties["Location"] = "new System.Drawing.Point(12, 12)";
        }

        // Apply user-specified property overrides.
        if (!string.IsNullOrWhiteSpace(properties))
        {
            var overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(properties);
            if (overrides is not null)
            {
                foreach (var (propName, value) in overrides)
                {
                    newControl.Properties[propName] = value;
                }
            }
        }

        // Assign TabIndex.
        var maxTabIndex = model.Controls
            .Where(c => c.Properties.ContainsKey("TabIndex"))
            .Select(c => int.TryParse(c.Properties["TabIndex"], out var idx) ? idx : 0)
            .DefaultIfEmpty(-1)
            .Max();
        newControl.Properties["TabIndex"] = (maxTabIndex + 1).ToString();

        // Add to model.
        model.Controls.Add(newControl);

        // Wire into parent-child hierarchy.
        if (!string.IsNullOrEmpty(parentName))
        {
            var parent = model.Controls.FirstOrDefault(
                c => c.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
            if (parent is not null)
            {
                parent.Children.Add(newControl);
            }
            else
            {
                model.RootControls.Add(newControl);
            }
        }
        else
        {
            model.RootControls.Add(newControl);
        }

        // Write back.
        await designerService.WriteAsync(filePath, model, cancellationToken);

        var result = new
        {
            success = true,
            controlName,
            controlType = fullType,
            appliedProperties = newControl.Properties,
            parentName = parentName ?? model.FormName,
            message = $"Added {shortType} '{controlName}' to {parentName ?? model.FormName}."
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }

    [McpServerTool(Name = "modify_control_property")]
    [Description(
        "Changes a single property on an existing control in a WinForms designer file. " +
        "The value should be a valid C# expression (e.g., '\"Hello\"' for strings, " +
        "'new System.Drawing.Point(50, 50)' for Point values).")]
    public static async Task<string> ModifyControlProperty(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description("The name of the control to modify (e.g., 'button1')")] string controlName,
        [Description("The property name to set (e.g., 'Text', 'Location', 'Size')")] string propertyName,
        [Description("The value expression (e.g., '\"OK\"', 'new System.Drawing.Size(100, 50)', 'true')")] string value,
        CancellationToken cancellationToken = default)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        // Check if it's a form-level property.
        if (controlName.Equals(model.FormName, StringComparison.OrdinalIgnoreCase) ||
            controlName.Equals("form", StringComparison.OrdinalIgnoreCase))
        {
            model.FormProperties[propertyName] = value;
            await designerService.WriteAsync(filePath, model, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = true,
                target = model.FormName,
                property = propertyName,
                value,
                message = $"Set {model.FormName}.{propertyName} = {value}"
            }, JsonOptions);
        }

        var control = model.Controls.FirstOrDefault(
            c => c.Name.Equals(controlName, StringComparison.OrdinalIgnoreCase));

        if (control is null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Control '{controlName}' not found."
            }, JsonOptions);
        }

        control.Properties[propertyName] = value;
        await designerService.WriteAsync(filePath, model, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            target = control.Name,
            property = propertyName,
            value,
            message = $"Set {control.Name}.{propertyName} = {value}"
        }, JsonOptions);
    }

    [McpServerTool(Name = "remove_control")]
    [Description(
        "Removes a control (and its children) from a WinForms designer file. " +
        "The control is removed from the visual tree, property assignments, and field declarations.")]
    public static async Task<string> RemoveControl(
        DesignerFileService designerService,
        [Description("Absolute path to the .Designer.cs or .Designer.vb file")] string filePath,
        [Description("The name of the control to remove")] string controlName,
        CancellationToken cancellationToken = default)
    {
        var model = await designerService.ParseAsync(filePath, cancellationToken);

        var control = model.Controls.FirstOrDefault(
            c => c.Name.Equals(controlName, StringComparison.OrdinalIgnoreCase));

        if (control is null)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Control '{controlName}' not found."
            }, JsonOptions);
        }

        // Collect all names to remove (control + descendants).
        var toRemove = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectDescendants(control, toRemove);

        // Remove from Controls list.
        model.Controls.RemoveAll(c => toRemove.Contains(c.Name));

        // Remove from RootControls.
        model.RootControls.RemoveAll(c => toRemove.Contains(c.Name));

        // Remove from any parent's Children list.
        foreach (var other in model.Controls)
        {
            other.Children.RemoveAll(c => toRemove.Contains(c.Name));
        }

        await designerService.WriteAsync(filePath, model, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = true,
            removedControls = toRemove.ToList(),
            message = $"Removed '{controlName}' and {toRemove.Count - 1} child control(s)."
        }, JsonOptions);
    }

    private static void CollectDescendants(ControlNode node, HashSet<string> names)
    {
        names.Add(node.Name);
        foreach (var child in node.Children)
        {
            CollectDescendants(child, names);
        }
    }

    private static string ResolveFullTypeName(string controlType)
    {
        // If already fully qualified, return as-is.
        if (controlType.Contains('.'))
            return controlType;

        // Common System.Windows.Forms types.
        return $"System.Windows.Forms.{controlType}";
    }

    private static string GetShortTypeName(string fullTypeName)
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot >= 0 ? fullTypeName[(lastDot + 1)..] : fullTypeName;
    }

    private static string GenerateControlName(string shortType, FormModel model)
    {
        // Lowercase first char + sequential number.
        var prefix = char.ToLowerInvariant(shortType[0]) + shortType[1..];
        var existing = model.Controls
            .Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(c =>
            {
                var suffix = c.Name[prefix.Length..];
                return int.TryParse(suffix, out var num) ? num : 0;
            })
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{existing + 1}";
    }
}
