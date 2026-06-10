namespace WinFormsDesignerMcp.Models;

/// <summary>
/// Language-agnostic representation of a WinForms designer file.
/// Produced by parsing a .Designer.cs or .Designer.vb file.
/// </summary>
public class FormModel
{
    /// <summary>
    /// Absolute path to the designer file that was parsed.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The language of the designer file.
    /// </summary>
    public required DesignerLanguage Language { get; set; }

    /// <summary>
    /// The name of the form class (e.g., "Form1").
    /// </summary>
    public required string FormName { get; set; }

    /// <summary>
    /// The namespace containing the form class, if any.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Properties assigned directly on the form (this/Me) inside InitializeComponent().
    /// </summary>
    public Dictionary<string, string> FormProperties { get; set; } = new();

    /// <summary>
    /// Events wired on the form itself.
    /// </summary>
    public List<EventWiring> FormEvents { get; set; } = [];

    /// <summary>
    /// All controls declared on the form (flat list — use parent-child info to build hierarchy).
    /// </summary>
    public List<ControlNode> Controls { get; set; } = [];

    /// <summary>
    /// Top-level controls added directly to the form's Controls collection.
    /// This is a subset of <see cref="Controls"/> organized hierarchically.
    /// </summary>
    public List<ControlNode> RootControls { get; set; } = [];

    /// <summary>
    /// Raw local variable declaration statements from the beginning of InitializeComponent(),
    /// e.g. a ComponentResourceManager declaration. Preserved verbatim for round-trip fidelity.
    /// </summary>
    public List<string> LocalDeclarations { get; set; } = [];

    /// <summary>
    /// Raw invocation statements at the form level in InitializeComponent() that are not standard
    /// property assignments, Controls.Add, or event wiring — e.g. resources.ApplyResources(this, ...).
    /// Preserved verbatim for round-trip fidelity.
    /// </summary>
    public List<string> FormRawStatements { get; set; } = [];
}
