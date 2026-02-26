namespace WinFormsDesignerMcp.Models;

/// <summary>
/// Represents a single control in the WinForms designer visual tree.
/// </summary>
public class ControlNode
{
    /// <summary>
    /// The variable name of the control (e.g., "button1").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The fully-qualified type name (e.g., "System.Windows.Forms.Button").
    /// </summary>
    public required string ControlType { get; set; }

    /// <summary>
    /// For custom (non-standard) controls, the resolved base type that is a known
    /// System.Windows.Forms type (e.g., "System.Windows.Forms.LinkLabel" for a custom
    /// "Entergy.ControlsLibrary.LinkLabelExtended"). Null for standard controls or
    /// when the base type cannot be resolved.
    /// </summary>
    public string? BaseType { get; set; }

    /// <summary>
    /// Property assignments for this control (property name → raw value expression).
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();

    /// <summary>
    /// Child controls (i.e., controls added via <c>Controls.Add</c>).
    /// </summary>
    public List<ControlNode> Children { get; set; } = [];

    /// <summary>
    /// Events wired up in the designer for this control.
    /// </summary>
    public List<EventWiring> Events { get; set; } = [];
}
