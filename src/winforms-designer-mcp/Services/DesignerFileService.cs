using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services
{
    /// <summary>
    /// Facade service that detects the designer language from a file path
    /// and delegates to the appropriate language-specific parser/writer.
    /// </summary>
    public class DesignerFileService(
        IEnumerable<IDesignerFileParser> parsers,
        IEnumerable<IDesignerFileWriter> writers
    )
    {
        private readonly Dictionary<DesignerLanguage, IDesignerFileParser> _parsers =
            parsers.ToDictionary(p => p.Language);

        private readonly Dictionary<DesignerLanguage, IDesignerFileWriter> _writers =
            writers.ToDictionary(w => w.Language);

        /// <summary>
        /// Detect the language from the file extension and parse the designer file.
        /// </summary>
        public Task<FormModel> ParseAsync(
            string filePath,
            CancellationToken cancellationToken = default
        )
        {
            var language = DetectLanguage(filePath);
            if (!_parsers.TryGetValue(language, out var parser))
            {
                throw new NotSupportedException($"No parser registered for {language}");
            }

            return parser.ParseAsync(filePath, cancellationToken);
        }

        /// <summary>
        /// Detect the language from the file extension and write the model back.
        /// </summary>
        public Task WriteAsync(
            string filePath,
            FormModel model,
            CancellationToken cancellationToken = default
        )
        {
            var language = DetectLanguage(filePath);
            if (!_writers.TryGetValue(language, out var writer))
            {
                throw new NotSupportedException($"No writer registered for {language}");
            }

            return writer.WriteAsync(filePath, model, cancellationToken);
        }

        /// <summary>
        /// Detect the designer language from a file path.
        /// </summary>
        public static DesignerLanguage DetectLanguage(string filePath)
        {
            if (
                filePath.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            )
            {
                return DesignerLanguage.CSharp;
            }

            if (
                filePath.EndsWith(".Designer.vb", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith(".vb", StringComparison.OrdinalIgnoreCase)
            )
            {
                return DesignerLanguage.VisualBasic;
            }

            throw new ArgumentException(
                $"Cannot determine designer language from file path: {filePath}"
            );
        }
    }
}
