using System.Text;
using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services.CSharp;

/// <summary>
/// Writes a <see cref="FormModel"/> back to a C# .Designer.cs file,
/// regenerating InitializeComponent() and field declarations.
/// </summary>
public class CSharpDesignerFileWriter : IDesignerFileWriter
{
    public DesignerLanguage Language => DesignerLanguage.CSharp;

    public async Task WriteAsync(string filePath, FormModel model, CancellationToken cancellationToken = default)
    {
        string existingContent = File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath, cancellationToken)
            : "";

        string output;
        if (!string.IsNullOrWhiteSpace(existingContent))
        {
            output = ReplaceInExistingFile(existingContent, model);
        }
        else
        {
            output = GenerateFullFile(model);
        }

        await File.WriteAllTextAsync(filePath, output, cancellationToken);
    }

    /// <summary>
    /// Replace InitializeComponent() body and field declarations in an existing file.
    /// </summary>
    private static string ReplaceInExistingFile(string source, FormModel model)
    {
        // Strategy: find the InitializeComponent method body and replace it.
        // Also find/update field declarations at the end of the class.
        // We use simple text replacement anchored on known markers.

        var result = new StringBuilder(source);

        // Replace InitializeComponent body.
        var initStart = FindMethodBodyStart(source, "InitializeComponent");
        var initEnd = FindMatchingBrace(source, initStart);

        if (initStart >= 0 && initEnd > initStart)
        {
            var newBody = GenerateInitializeComponentBody(model);
            result.Remove(initStart + 1, initEnd - initStart - 1);
            result.Insert(initStart + 1, newBody);
        }

        // Update field declarations — replace from #endregion to end of class.
        var updatedSource = result.ToString();
        var endRegionIdx = updatedSource.IndexOf("#endregion", StringComparison.Ordinal);
        if (endRegionIdx >= 0)
        {
            // Find the line after #endregion.
            var afterEndRegion = updatedSource.IndexOf('\n', endRegionIdx);
            if (afterEndRegion >= 0)
            {
                // Find the closing brace of the class (second-to-last '}').
                var classEnd = updatedSource.LastIndexOf('}');
                if (classEnd > 0)
                {
                    classEnd = updatedSource.LastIndexOf('}', classEnd - 1);
                }

                if (classEnd > afterEndRegion)
                {
                    var newFields = GenerateFieldDeclarations(model);
                    result = new StringBuilder(updatedSource);
                    result.Remove(afterEndRegion + 1, classEnd - afterEndRegion - 1);
                    result.Insert(afterEndRegion + 1, newFields);
                }
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Generate a complete .Designer.cs file from scratch.
    /// </summary>
    private static string GenerateFullFile(FormModel model)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace}");
            sb.AppendLine("{");
        }

        var indent = string.IsNullOrEmpty(model.Namespace) ? "" : "    ";

        sb.AppendLine($"{indent}partial class {model.FormName}");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Required designer variable.");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    private System.ComponentModel.IContainer components = null;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Clean up any resources being used.");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    protected override void Dispose(bool disposing)");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        if (disposing && (components != null))");
        sb.AppendLine($"{indent}        {{");
        sb.AppendLine($"{indent}            components.Dispose();");
        sb.AppendLine($"{indent}        }}");
        sb.AppendLine($"{indent}        base.Dispose(disposing);");
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    #region Windows Form Designer generated code");
        sb.AppendLine();
        sb.AppendLine($"{indent}    /// <summary>");
        sb.AppendLine($"{indent}    /// Required method for Designer support - do not modify");
        sb.AppendLine($"{indent}    /// the contents of this method with the code editor.");
        sb.AppendLine($"{indent}    /// </summary>");
        sb.AppendLine($"{indent}    private void InitializeComponent()");
        sb.AppendLine($"{indent}    {{");
        sb.Append(GenerateInitializeComponentBody(model, indent + "        "));
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}    #endregion");
        sb.Append(GenerateFieldDeclarations(model, indent + "    "));
        sb.AppendLine($"{indent}}}");

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static string GenerateInitializeComponentBody(FormModel model, string indent = "            ")
    {
        var sb = new StringBuilder();
        sb.AppendLine();

        // Control instantiations.
        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}this.{control.Name} = new {control.ControlType}();");
        }

        // SuspendLayout calls for container controls.
        var containers = model.Controls.Where(c => c.Children.Count > 0).ToList();
        foreach (var container in containers)
        {
            sb.AppendLine($"{indent}this.{container.Name}.SuspendLayout();");
        }
        sb.AppendLine($"{indent}this.SuspendLayout();");

        // Per-control property blocks.
        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}// ");
            sb.AppendLine($"{indent}// {control.Name}");
            sb.AppendLine($"{indent}// ");
            foreach (var (propName, value) in control.Properties)
            {
                sb.AppendLine($"{indent}this.{control.Name}.{propName} = {value};");
            }
            foreach (var child in control.Children)
            {
                sb.AppendLine($"{indent}this.{control.Name}.Controls.Add(this.{child.Name});");
            }
            foreach (var evt in control.Events)
            {
                sb.AppendLine($"{indent}this.{control.Name}.{evt.EventName} += new System.EventHandler(this.{evt.HandlerMethodName});");
            }
        }

        // Form-level properties.
        sb.AppendLine($"{indent}// ");
        sb.AppendLine($"{indent}// {model.FormName}");
        sb.AppendLine($"{indent}// ");
        foreach (var (propName, value) in model.FormProperties)
        {
            sb.AppendLine($"{indent}this.{propName} = {value};");
        }

        // Form Controls.Add for root controls.
        foreach (var root in model.RootControls)
        {
            sb.AppendLine($"{indent}this.Controls.Add(this.{root.Name});");
        }

        // Form event wiring.
        foreach (var evt in model.FormEvents)
        {
            sb.AppendLine($"{indent}this.{evt.EventName} += new System.EventHandler(this.{evt.HandlerMethodName});");
        }

        // ResumeLayout calls.
        foreach (var container in containers)
        {
            sb.AppendLine($"{indent}this.{container.Name}.ResumeLayout(false);");
        }
        sb.AppendLine($"{indent}this.ResumeLayout(false);");
        sb.AppendLine($"{indent}this.PerformLayout();");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string GenerateFieldDeclarations(FormModel model, string indent = "        ")
    {
        var sb = new StringBuilder();
        sb.AppendLine();

        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}private {control.ControlType} {control.Name};");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Find the opening brace of a method body by name.
    /// </summary>
    private static int FindMethodBodyStart(string source, string methodName)
    {
        var idx = source.IndexOf(methodName, StringComparison.Ordinal);
        if (idx < 0) return -1;

        // Find the opening '{' after the method signature.
        return source.IndexOf('{', idx);
    }

    /// <summary>
    /// Find the matching closing brace for an opening brace.
    /// </summary>
    private static int FindMatchingBrace(string source, int openBraceIndex)
    {
        if (openBraceIndex < 0 || openBraceIndex >= source.Length) return -1;

        int depth = 0;
        for (int i = openBraceIndex; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }
}
