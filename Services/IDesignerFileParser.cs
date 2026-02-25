using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services;

/// <summary>
/// Parses a WinForms designer file into a <see cref="FormModel"/>.
/// </summary>
public interface IDesignerFileParser
{
    /// <summary>
    /// The language this parser handles.
    /// </summary>
    DesignerLanguage Language { get; }

    /// <summary>
    /// Parse the designer file at the given path into a <see cref="FormModel"/>.
    /// </summary>
    Task<FormModel> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
