using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;

namespace WinFormsDesignerMcp.Tools;

/// <summary>
/// MCP tools for discovering WinForms control types and their metadata.
/// Uses a curated knowledge base rather than runtime reflection, so it works cross-platform.
/// </summary>
[McpServerToolType]
public class MetadataTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool(Name = "get_available_control_types")]
    [Description(
        "Returns a curated list of common WinForms control types with descriptions " +
        "and their typical use cases. Use this to discover what controls can be placed on a form.")]
    public static string GetAvailableControlTypes()
    {
        return JsonSerializer.Serialize(ControlCatalog, JsonOptions);
    }

    [McpServerTool(Name = "get_control_type_info")]
    [Description(
        "Returns detailed metadata for a specific WinForms control type, including " +
        "common properties with their types and descriptions, common events, and usage notes.")]
    public static string GetControlTypeInfo(
        [Description("The control type name (e.g., 'Button', 'DataGridView')")] string controlType)
    {
        var key = controlType.Replace("System.Windows.Forms.", "");

        if (ControlDetails.TryGetValue(key, out var info))
        {
            return JsonSerializer.Serialize(info, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            error = $"No metadata available for '{controlType}'. Use get_available_control_types to see known types."
        }, JsonOptions);
    }

    // ─── Curated Catalog ──────────────────────────────────────────

    private static readonly List<object> ControlCatalog =
    [
        new { name = "Button",          fullName = "System.Windows.Forms.Button",          category = "Common", description = "A push button that triggers an action when clicked." },
        new { name = "Label",           fullName = "System.Windows.Forms.Label",           category = "Common", description = "Displays static text or an image." },
        new { name = "TextBox",         fullName = "System.Windows.Forms.TextBox",         category = "Common", description = "Single-line or multi-line text input." },
        new { name = "RichTextBox",     fullName = "System.Windows.Forms.RichTextBox",     category = "Common", description = "Rich text editor supporting formatting." },
        new { name = "ComboBox",        fullName = "System.Windows.Forms.ComboBox",        category = "Common", description = "Drop-down list with optional text input." },
        new { name = "ListBox",         fullName = "System.Windows.Forms.ListBox",         category = "Common", description = "Scrollable list of selectable items." },
        new { name = "CheckBox",        fullName = "System.Windows.Forms.CheckBox",        category = "Common", description = "A check box with a text label." },
        new { name = "RadioButton",     fullName = "System.Windows.Forms.RadioButton",     category = "Common", description = "A radio button for mutually exclusive choices within a group." },
        new { name = "NumericUpDown",   fullName = "System.Windows.Forms.NumericUpDown",   category = "Common", description = "Numeric spinner control." },
        new { name = "DateTimePicker",  fullName = "System.Windows.Forms.DateTimePicker",  category = "Common", description = "Date and/or time selection control." },
        new { name = "DataGridView",    fullName = "System.Windows.Forms.DataGridView",    category = "Data",   description = "Tabular data grid with sorting, editing, and binding support." },
        new { name = "ListView",        fullName = "System.Windows.Forms.ListView",        category = "Data",   description = "Displays items in list, detail, tile, or icon views." },
        new { name = "TreeView",        fullName = "System.Windows.Forms.TreeView",        category = "Data",   description = "Hierarchical tree of expandable nodes." },
        new { name = "Panel",           fullName = "System.Windows.Forms.Panel",           category = "Container", description = "Generic container for grouping controls. Supports scrolling." },
        new { name = "GroupBox",        fullName = "System.Windows.Forms.GroupBox",        category = "Container", description = "Container with a border and caption text." },
        new { name = "TabControl",      fullName = "System.Windows.Forms.TabControl",      category = "Container", description = "Tabbed container — each tab is a TabPage." },
        new { name = "SplitContainer",  fullName = "System.Windows.Forms.SplitContainer",  category = "Container", description = "Two resizable panels separated by a splitter bar." },
        new { name = "FlowLayoutPanel", fullName = "System.Windows.Forms.FlowLayoutPanel", category = "Container", description = "Automatically arranges child controls in a flow (horizontal or vertical)." },
        new { name = "TableLayoutPanel",fullName = "System.Windows.Forms.TableLayoutPanel",category = "Container", description = "Grid-based layout with rows and columns." },
        new { name = "PictureBox",      fullName = "System.Windows.Forms.PictureBox",      category = "Display",  description = "Displays an image with various sizing modes." },
        new { name = "ProgressBar",     fullName = "System.Windows.Forms.ProgressBar",     category = "Display",  description = "Indicates progress of a long-running operation." },
        new { name = "MenuStrip",       fullName = "System.Windows.Forms.MenuStrip",       category = "Menu & Toolbar", description = "Main menu bar at the top of the form." },
        new { name = "ToolStrip",       fullName = "System.Windows.Forms.ToolStrip",       category = "Menu & Toolbar", description = "Toolbar with buttons, dropdowns, and other items." },
        new { name = "StatusStrip",     fullName = "System.Windows.Forms.StatusStrip",     category = "Menu & Toolbar", description = "Status bar at the bottom of the form." },
        new { name = "ContextMenuStrip",fullName = "System.Windows.Forms.ContextMenuStrip",category = "Menu & Toolbar", description = "Right-click context menu." },
        new { name = "ToolTip",         fullName = "System.Windows.Forms.ToolTip",         category = "Components", description = "Displays hover text for controls (non-visual component)." },
        new { name = "Timer",           fullName = "System.Windows.Forms.Timer",           category = "Components", description = "Triggers events at timed intervals (non-visual component)." },
        new { name = "OpenFileDialog",  fullName = "System.Windows.Forms.OpenFileDialog",  category = "Dialogs", description = "File open dialog." },
        new { name = "SaveFileDialog",  fullName = "System.Windows.Forms.SaveFileDialog",  category = "Dialogs", description = "File save dialog." },
        new { name = "FolderBrowserDialog", fullName = "System.Windows.Forms.FolderBrowserDialog", category = "Dialogs", description = "Folder selection dialog." }
    ];

    private static readonly Dictionary<string, object> ControlDetails = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] = new
        {
            name = "Button",
            fullName = "System.Windows.Forms.Button",
            description = "A clickable push button.",
            commonProperties = new[]
            {
                new { name = "Text",        type = "string",     description = "The button's display text." },
                new { name = "Size",        type = "Size",       description = "Width and height in pixels." },
                new { name = "Location",    type = "Point",      description = "Position relative to parent container." },
                new { name = "Enabled",     type = "bool",       description = "Whether the button is clickable." },
                new { name = "Visible",     type = "bool",       description = "Whether the button is visible." },
                new { name = "FlatStyle",   type = "FlatStyle",  description = "Visual style: Flat, Popup, Standard, System." },
                new { name = "BackColor",   type = "Color",      description = "Background color." },
                new { name = "ForeColor",   type = "Color",      description = "Text color." },
                new { name = "Font",        type = "Font",       description = "Text font." },
                new { name = "Anchor",      type = "AnchorStyles", description = "Edges anchored for responsive resizing." },
                new { name = "Dock",        type = "DockStyle",  description = "Docking behavior within parent." },
                new { name = "TabIndex",    type = "int",        description = "Tab order index." },
                new { name = "UseVisualStyleBackColor", type = "bool", description = "Use OS visual style for background." },
                new { name = "DialogResult", type = "DialogResult", description = "The dialog result returned when clicked in a dialog form." },
                new { name = "Image",       type = "Image",      description = "An image displayed on the button." },
                new { name = "ImageAlign",  type = "ContentAlignment", description = "Alignment of the image on the button." },
                new { name = "TextAlign",   type = "ContentAlignment", description = "Alignment of the text on the button." }
            },
            commonEvents = new[]
            {
                new { name = "Click",      description = "Fires when the button is clicked." },
                new { name = "MouseEnter", description = "Fires when the mouse enters the button." },
                new { name = "MouseLeave", description = "Fires when the mouse leaves the button." }
            }
        },
        ["TextBox"] = new
        {
            name = "TextBox",
            fullName = "System.Windows.Forms.TextBox",
            description = "A single-line or multi-line text input control.",
            commonProperties = new[]
            {
                new { name = "Text",        type = "string",     description = "The text content." },
                new { name = "Multiline",   type = "bool",       description = "Enable multi-line input." },
                new { name = "ReadOnly",    type = "bool",       description = "Prevent user editing." },
                new { name = "MaxLength",   type = "int",        description = "Maximum number of characters." },
                new { name = "PasswordChar", type = "char",      description = "Mask character for password input." },
                new { name = "ScrollBars",  type = "ScrollBars", description = "Scrollbar visibility in multi-line mode." },
                new { name = "WordWrap",    type = "bool",       description = "Word wrap in multi-line mode." },
                new { name = "PlaceholderText", type = "string", description = "Placeholder/watermark text." },
                new { name = "Size",        type = "Size",       description = "Width and height in pixels." },
                new { name = "Location",    type = "Point",      description = "Position relative to parent." },
                new { name = "Anchor",      type = "AnchorStyles", description = "Edges anchored for responsive resizing." },
                new { name = "Dock",        type = "DockStyle",  description = "Docking behavior within parent." }
            },
            commonEvents = new[]
            {
                new { name = "TextChanged", description = "Fires when the text content changes." },
                new { name = "KeyDown",     description = "Fires when a key is pressed." },
                new { name = "KeyPress",    description = "Fires when a character key is pressed." },
                new { name = "Validating",  description = "Fires when the control is being validated." }
            }
        },
        ["Label"] = new
        {
            name = "Label",
            fullName = "System.Windows.Forms.Label",
            description = "Displays static text or an image.",
            commonProperties = new[]
            {
                new { name = "Text",      type = "string",          description = "The display text." },
                new { name = "AutoSize",  type = "bool",            description = "Automatically resize to fit text." },
                new { name = "TextAlign", type = "ContentAlignment", description = "Text alignment within the label." },
                new { name = "Font",      type = "Font",            description = "Text font." },
                new { name = "ForeColor", type = "Color",           description = "Text color." },
                new { name = "BackColor", type = "Color",           description = "Background color." },
                new { name = "Size",      type = "Size",            description = "Width and height in pixels." },
                new { name = "Location",  type = "Point",           description = "Position relative to parent." }
            },
            commonEvents = new[]
            {
                new { name = "Click", description = "Fires when the label is clicked." }
            }
        },
        ["ComboBox"] = new
        {
            name = "ComboBox",
            fullName = "System.Windows.Forms.ComboBox",
            description = "Drop-down list with optional text input.",
            commonProperties = new[]
            {
                new { name = "DropDownStyle", type = "ComboBoxStyle", description = "Simple, DropDown, or DropDownList." },
                new { name = "SelectedIndex", type = "int",           description = "Index of the selected item." },
                new { name = "Text",          type = "string",        description = "Current text." },
                new { name = "Size",          type = "Size",          description = "Width and height in pixels." },
                new { name = "Location",      type = "Point",         description = "Position relative to parent." }
            },
            commonEvents = new[]
            {
                new { name = "SelectedIndexChanged", description = "Fires when the selected item changes." },
                new { name = "TextChanged",          description = "Fires when the text changes." }
            }
        },
        ["DataGridView"] = new
        {
            name = "DataGridView",
            fullName = "System.Windows.Forms.DataGridView",
            description = "Tabular data grid with sorting, editing, and data binding.",
            commonProperties = new[]
            {
                new { name = "DataSource",  type = "object",  description = "The data source to bind to." },
                new { name = "ReadOnly",    type = "bool",    description = "Prevent user editing." },
                new { name = "AllowUserToAddRows",    type = "bool", description = "Show new row at bottom." },
                new { name = "AllowUserToDeleteRows", type = "bool", description = "Allow row deletion." },
                new { name = "AutoSizeColumnsMode",   type = "DataGridViewAutoSizeColumnsMode", description = "Column auto-sizing behavior." },
                new { name = "SelectionMode", type = "DataGridViewSelectionMode", description = "Row vs cell selection." },
                new { name = "Size",        type = "Size",   description = "Width and height in pixels." },
                new { name = "Location",    type = "Point",  description = "Position relative to parent." },
                new { name = "Dock",        type = "DockStyle", description = "Docking behavior." }
            },
            commonEvents = new[]
            {
                new { name = "CellClick",        description = "Fires when a cell is clicked." },
                new { name = "CellValueChanged", description = "Fires when a cell value changes." },
                new { name = "SelectionChanged", description = "Fires when the selection changes." },
                new { name = "DataBindingComplete", description = "Fires after data binding completes." }
            }
        },
        ["Panel"] = new
        {
            name = "Panel",
            fullName = "System.Windows.Forms.Panel",
            description = "Generic container for grouping controls. Supports scrolling and borders.",
            commonProperties = new[]
            {
                new { name = "BorderStyle",  type = "BorderStyle",  description = "None, FixedSingle, or Fixed3D." },
                new { name = "AutoScroll",   type = "bool",         description = "Enable automatic scrollbars." },
                new { name = "BackColor",    type = "Color",        description = "Background color." },
                new { name = "Dock",         type = "DockStyle",    description = "Docking behavior." },
                new { name = "Size",         type = "Size",         description = "Width and height in pixels." },
                new { name = "Location",     type = "Point",        description = "Position relative to parent." }
            },
            commonEvents = new[]
            {
                new { name = "Paint", description = "Fires when the panel is painted." }
            }
        },
        ["CheckBox"] = new
        {
            name = "CheckBox",
            fullName = "System.Windows.Forms.CheckBox",
            description = "A check box with a text label.",
            commonProperties = new[]
            {
                new { name = "Text",      type = "string", description = "The label text." },
                new { name = "Checked",   type = "bool",   description = "Whether the checkbox is checked." },
                new { name = "AutoSize",  type = "bool",   description = "Automatically resize to fit text." },
                new { name = "ThreeState", type = "bool",  description = "Allow indeterminate state." },
                new { name = "Size",      type = "Size",   description = "Width and height in pixels." },
                new { name = "Location",  type = "Point",  description = "Position relative to parent." }
            },
            commonEvents = new[]
            {
                new { name = "CheckedChanged", description = "Fires when the checked state changes." }
            }
        },
        ["RadioButton"] = new
        {
            name = "RadioButton",
            fullName = "System.Windows.Forms.RadioButton",
            description = "A radio button for mutually exclusive choices within a group.",
            commonProperties = new[]
            {
                new { name = "Text",     type = "string", description = "The label text." },
                new { name = "Checked",  type = "bool",   description = "Whether the radio button is selected." },
                new { name = "AutoSize", type = "bool",   description = "Automatically resize to fit text." },
                new { name = "Size",     type = "Size",   description = "Width and height in pixels." },
                new { name = "Location", type = "Point",  description = "Position relative to parent." }
            },
            commonEvents = new[]
            {
                new { name = "CheckedChanged", description = "Fires when the selection state changes." }
            }
        },
        ["GroupBox"] = new
        {
            name = "GroupBox",
            fullName = "System.Windows.Forms.GroupBox",
            description = "Container with a border and caption text.",
            commonProperties = new[]
            {
                new { name = "Text",     type = "string", description = "The caption text displayed at the top." },
                new { name = "Size",     type = "Size",   description = "Width and height in pixels." },
                new { name = "Location", type = "Point",  description = "Position relative to parent." },
                new { name = "Dock",     type = "DockStyle", description = "Docking behavior." }
            },
            commonEvents = Array.Empty<object>()
        },
        ["TabControl"] = new
        {
            name = "TabControl",
            fullName = "System.Windows.Forms.TabControl",
            description = "Tabbed container where each tab is a TabPage.",
            commonProperties = new[]
            {
                new { name = "SelectedIndex", type = "int",       description = "The index of the active tab." },
                new { name = "Alignment",     type = "TabAlignment", description = "Tab position: Top, Bottom, Left, Right." },
                new { name = "Size",          type = "Size",      description = "Width and height in pixels." },
                new { name = "Location",      type = "Point",     description = "Position relative to parent." },
                new { name = "Dock",          type = "DockStyle", description = "Docking behavior." }
            },
            commonEvents = new[]
            {
                new { name = "SelectedIndexChanged", description = "Fires when the active tab changes." }
            }
        },
        ["ListView"] = new
        {
            name = "ListView",
            fullName = "System.Windows.Forms.ListView",
            description = "Displays items with icons in list, detail, tile, or icon views.",
            commonProperties = new[]
            {
                new { name = "View",         type = "View",      description = "Display mode: LargeIcon, Details, SmallIcon, List, Tile." },
                new { name = "FullRowSelect", type = "bool",     description = "Select entire rows in Details view." },
                new { name = "GridLines",    type = "bool",      description = "Show grid lines in Details view." },
                new { name = "Size",         type = "Size",      description = "Width and height in pixels." },
                new { name = "Location",     type = "Point",     description = "Position relative to parent." },
                new { name = "Dock",         type = "DockStyle", description = "Docking behavior." }
            },
            commonEvents = new[]
            {
                new { name = "SelectedIndexChanged", description = "Fires when the selection changes." },
                new { name = "ItemActivate",         description = "Fires when an item is activated (double-click)." }
            }
        },
        ["TreeView"] = new
        {
            name = "TreeView",
            fullName = "System.Windows.Forms.TreeView",
            description = "Hierarchical tree of expandable nodes.",
            commonProperties = new[]
            {
                new { name = "ShowLines",    type = "bool",      description = "Show lines between nodes." },
                new { name = "ShowPlusMinus", type = "bool",     description = "Show expand/collapse icons." },
                new { name = "CheckBoxes",   type = "bool",      description = "Show checkboxes on nodes." },
                new { name = "Size",         type = "Size",      description = "Width and height in pixels." },
                new { name = "Location",     type = "Point",     description = "Position relative to parent." },
                new { name = "Dock",         type = "DockStyle", description = "Docking behavior." }
            },
            commonEvents = new[]
            {
                new { name = "AfterSelect",  description = "Fires after a node is selected." },
                new { name = "AfterExpand",  description = "Fires after a node is expanded." },
                new { name = "AfterCheck",   description = "Fires after a node's check state changes." }
            }
        },
        ["PictureBox"] = new
        {
            name = "PictureBox",
            fullName = "System.Windows.Forms.PictureBox",
            description = "Displays an image with various sizing modes.",
            commonProperties = new[]
            {
                new { name = "Image",    type = "Image",           description = "The image to display." },
                new { name = "SizeMode", type = "PictureBoxSizeMode", description = "Normal, StretchImage, AutoSize, CenterImage, Zoom." },
                new { name = "Size",     type = "Size",            description = "Width and height in pixels." },
                new { name = "Location", type = "Point",           description = "Position relative to parent." },
                new { name = "Dock",     type = "DockStyle",       description = "Docking behavior." }
            },
            commonEvents = new[]
            {
                new { name = "Click", description = "Fires when the picture box is clicked." }
            }
        }
    };
}
