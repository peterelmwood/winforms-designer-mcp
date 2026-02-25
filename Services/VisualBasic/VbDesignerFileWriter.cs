using System.Text;
using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services.VisualBasic;

/// <summary>
/// Writes a <see cref="FormModel"/> back to a VB.NET .Designer.vb file,
/// regenerating InitializeComponent() and field declarations.
/// </summary>
public class VbDesignerFileWriter : IDesignerFileWriter
{
    public DesignerLanguage Language => DesignerLanguage.VisualBasic;

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

    private static string ReplaceInExistingFile(string source, FormModel model)
    {
        var result = new StringBuilder(source);

        // Replace InitializeComponent body.
        // In VB, the method body is between "Private Sub InitializeComponent()" and "End Sub".
        var initStart = FindSubBodyStart(source, "InitializeComponent");
        var initEnd = FindEndSub(source, initStart);

        if (initStart >= 0 && initEnd > initStart)
        {
            var newBody = GenerateInitializeComponentBody(model);
            result.Remove(initStart, initEnd - initStart);
            result.Insert(initStart, newBody);
        }

        // Update field declarations — replace from "End Sub" of InitializeComponent to "End Class".
        var updatedSource = result.ToString();

        // Find the end of the InitializeComponent method (after our replacement).
        var endSubIdx = updatedSource.IndexOf("End Sub", 
            updatedSource.IndexOf("InitializeComponent", StringComparison.Ordinal), 
            StringComparison.Ordinal);

        if (endSubIdx >= 0)
        {
            var afterEndSub = updatedSource.IndexOf('\n', endSubIdx);
            var endClassIdx = updatedSource.LastIndexOf("End Class", StringComparison.Ordinal);

            if (afterEndSub >= 0 && endClassIdx > afterEndSub)
            {
                var newFields = GenerateFieldDeclarations(model);
                result = new StringBuilder(updatedSource);
                result.Remove(afterEndSub + 1, endClassIdx - afterEndSub - 1);
                result.Insert(afterEndSub + 1, newFields);
            }
        }

        return result.ToString();
    }

    private static string GenerateFullFile(FormModel model)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"Namespace {model.Namespace}");
        }

        var indent = string.IsNullOrEmpty(model.Namespace) ? "" : "    ";

        sb.AppendLine($"{indent}<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>");
        sb.AppendLine($"{indent}Partial Class {model.FormName}");
        sb.AppendLine($"{indent}    Inherits System.Windows.Forms.Form");
        sb.AppendLine();
        sb.AppendLine($"{indent}    'Form overrides dispose to clean up the component list.");
        sb.AppendLine($"{indent}    <System.Diagnostics.DebuggerNonUserCode()>");
        sb.AppendLine($"{indent}    Protected Overrides Sub Dispose(ByVal disposing As Boolean)");
        sb.AppendLine($"{indent}        Try");
        sb.AppendLine($"{indent}            If disposing AndAlso components IsNot Nothing Then");
        sb.AppendLine($"{indent}                components.Dispose()");
        sb.AppendLine($"{indent}            End If");
        sb.AppendLine($"{indent}        Finally");
        sb.AppendLine($"{indent}            MyBase.Dispose(disposing)");
        sb.AppendLine($"{indent}        End Try");
        sb.AppendLine($"{indent}    End Sub");
        sb.AppendLine();
        sb.AppendLine($"{indent}    'Required by the Windows Form Designer");
        sb.AppendLine($"{indent}    Private components As System.ComponentModel.IContainer");
        sb.AppendLine();
        sb.AppendLine($"{indent}    <System.Diagnostics.DebuggerStepThrough()>");
        sb.AppendLine($"{indent}    Private Sub InitializeComponent()");
        sb.Append(GenerateInitializeComponentBody(model, indent + "        "));
        sb.AppendLine($"{indent}    End Sub");
        sb.AppendLine();
        sb.Append(GenerateFieldDeclarations(model, indent + "    "));
        sb.AppendLine($"{indent}End Class");

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine("End Namespace");
        }

        return sb.ToString();
    }

    private static string GenerateInitializeComponentBody(FormModel model, string indent = "            ")
    {
        var sb = new StringBuilder();

        // Control instantiations.
        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}Me.{control.Name} = New {control.ControlType}()");
        }

        // SuspendLayout calls.
        var containers = model.Controls.Where(c => c.Children.Count > 0).ToList();
        foreach (var container in containers)
        {
            sb.AppendLine($"{indent}Me.{container.Name}.SuspendLayout()");
        }
        sb.AppendLine($"{indent}Me.SuspendLayout()");

        // Per-control property blocks.
        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}'");
            sb.AppendLine($"{indent}'{control.Name}");
            sb.AppendLine($"{indent}'");
            foreach (var (propName, value) in control.Properties)
            {
                sb.AppendLine($"{indent}Me.{control.Name}.{propName} = {value}");
            }
            foreach (var child in control.Children)
            {
                sb.AppendLine($"{indent}Me.{control.Name}.Controls.Add(Me.{child.Name})");
            }
            foreach (var evt in control.Events)
            {
                sb.AppendLine($"{indent}AddHandler Me.{control.Name}.{evt.EventName}, AddressOf Me.{evt.HandlerMethodName}");
            }
        }

        // Form-level properties.
        sb.AppendLine($"{indent}'");
        sb.AppendLine($"{indent}'{model.FormName}");
        sb.AppendLine($"{indent}'");
        foreach (var (propName, value) in model.FormProperties)
        {
            sb.AppendLine($"{indent}Me.{propName} = {value}");
        }

        // Form Controls.Add for root controls.
        foreach (var root in model.RootControls)
        {
            sb.AppendLine($"{indent}Me.Controls.Add(Me.{root.Name})");
        }

        // Form event wiring.
        foreach (var evt in model.FormEvents)
        {
            sb.AppendLine($"{indent}AddHandler Me.{evt.EventName}, AddressOf Me.{evt.HandlerMethodName}");
        }

        // ResumeLayout calls.
        foreach (var container in containers)
        {
            sb.AppendLine($"{indent}Me.{container.Name}.ResumeLayout(False)");
        }
        sb.AppendLine($"{indent}Me.ResumeLayout(False)");
        sb.AppendLine($"{indent}Me.PerformLayout()");

        return sb.ToString();
    }

    private static string GenerateFieldDeclarations(FormModel model, string indent = "        ")
    {
        var sb = new StringBuilder();
        sb.AppendLine();

        foreach (var control in model.Controls)
        {
            sb.AppendLine($"{indent}Friend WithEvents {control.Name} As {control.ControlType}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Find the start of the InitializeComponent Sub body (after the Sub statement line).
    /// </summary>
    private static int FindSubBodyStart(string source, string methodName)
    {
        var idx = source.IndexOf(methodName, StringComparison.Ordinal);
        if (idx < 0) return -1;

        // Find the newline after the Sub declaration line.
        var lineEnd = source.IndexOf('\n', idx);
        return lineEnd >= 0 ? lineEnd + 1 : -1;
    }

    /// <summary>
    /// Find the "End Sub" that corresponds to InitializeComponent, starting from the body.
    /// </summary>
    private static int FindEndSub(string source, int startIndex)
    {
        if (startIndex < 0) return -1;

        // Simple search for "End Sub" — works because InitializeComponent doesn't contain nested Subs.
        var idx = source.IndexOf("End Sub", startIndex, StringComparison.Ordinal);
        if (idx < 0) return -1;

        // Return position just before "End Sub" (the start of the line).
        // Walk back to find the start of the line.
        var lineStart = source.LastIndexOf('\n', idx);
        return lineStart >= 0 ? lineStart + 1 : idx;
    }
}
