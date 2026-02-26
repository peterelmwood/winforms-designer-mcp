using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services;

/// <summary>
/// Writes changes back to a WinForms designer file from a <see cref="FormModel"/>.
/// </summary>
public interface IDesignerFileWriter
{
    /// <summary>
    /// The language this writer handles.
    /// </summary>
    DesignerLanguage Language { get; }

    /// <summary>
    /// Write the <see cref="FormModel"/> back to the designer file, updating
    /// InitializeComponent() and field declarations while preserving other code.
    /// </summary>
    Task WriteAsync(string filePath, FormModel model, CancellationToken cancellationToken = default);
}
