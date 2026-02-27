using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services.CSharp;

/// <summary>
/// Parses a C# WinForms .Designer.cs file into a <see cref="FormModel"/>
/// using Roslyn syntax analysis.
/// </summary>
public class CSharpDesignerFileParser : IDesignerFileParser
{
    public DesignerLanguage Language => DesignerLanguage.CSharp;

    /// <summary>
    /// Parses a C# Windows Forms designer file asynchronously and extracts the form structure, including controls,
    /// properties, and event wiring.
    /// </summary>
    /// <remarks>The returned FormModel includes the form's name, namespace, properties, events, and a
    /// hierarchy of controls as defined in the designer file. Only controls and properties explicitly declared in the
    /// file are included. This method does not validate the correctness of the designer file beyond the required
    /// structure.</remarks>
    /// <param name="filePath">The full path to the C# designer file to parse. The file must exist and be accessible.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the parse operation.</param>
    /// <returns>A task that represents the asynchronous parse operation. The task result contains a FormModel describing the
    /// form's structure and metadata.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the file does not contain a class declaration or an InitializeComponent method, or if
    /// InitializeComponent has no body.</exception>
    public async Task<FormModel> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var sourceText = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: cancellationToken);
        var root = await tree.GetRootAsync(cancellationToken);

        // Find the class declaration (the partial form class).
        var classDecl =
            root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No class declaration found in {filePath}");

        // Find namespace.
        string? ns = classDecl
            .Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault()
            ?.Name.ToString();

        // Find InitializeComponent method.
        var initMethod =
            classDecl
                .Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent")
            ?? throw new InvalidOperationException(
                $"No InitializeComponent method found in {filePath}"
            );

        var statements =
            initMethod.Body?.Statements
            ?? throw new InvalidOperationException("InitializeComponent has no body");

        // Collect field names declared in the class so we can distinguish real control
        // declarations (this.button1 = new Button()) from form-level property assignments
        // that also use 'new' (this.ClientSize = new Size(...)).
        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                fieldNames.Add(variable.Identifier.Text);
            }
        }

        // Phase 1: Extract control declarations (this.x = new Type()).
        var controlsByName = new Dictionary<string, ControlNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var stmt in statements)
        {
            if (
                TryParseControlDeclaration(stmt, out var name, out var typeName)
                && fieldNames.Contains(name)
            )
            {
                controlsByName[name] = new ControlNode { Name = name, ControlType = typeName };
            }
        }

        // Phase 2: Extract property assignments, Controls.Add calls, and event wiring.
        var formProperties = new Dictionary<string, string>();
        var formEvents = new List<EventWiring>();
        var parentChildMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        // "form" as the key for controls added to this.Controls
        parentChildMap["$form"] = [];

        foreach (var stmt in statements)
        {
            // Property assignment: this.controlName.Property = value
            if (TryParsePropertyAssignment(stmt, out var target, out var propName, out var value))
            {
                if (target is null || target.Equals("this", StringComparison.OrdinalIgnoreCase))
                {
                    // Form-level property.
                    formProperties[propName] = value;
                }
                else if (controlsByName.TryGetValue(target, out var control))
                {
                    control.Properties[propName] = value;
                }
            }

            // Controls.Add: this.panel1.Controls.Add(this.button1) or this.Controls.Add(...)
            if (TryParseControlsAdd(stmt, out var parentName, out var childName))
            {
                var key = parentName ?? "$form";
                if (!parentChildMap.TryGetValue(key, out var children))
                {
                    children = [];
                    parentChildMap[key] = children;
                }

                children.Add(childName);
            }

            // Event wiring: this.button1.Click += new EventHandler(this.button1_Click)
            if (TryParseEventWiring(stmt, out var evtTarget, out var evtName, out var handler))
            {
                var wiring = new EventWiring { EventName = evtName, HandlerMethodName = handler };
                if (
                    evtTarget is null
                    || evtTarget.Equals("this", StringComparison.OrdinalIgnoreCase)
                )
                {
                    formEvents.Add(wiring);
                }
                else if (controlsByName.TryGetValue(evtTarget, out var ctrl))
                {
                    ctrl.Events.Add(wiring);
                }
            }
        }

        // Phase 3: Build parent-child hierarchy.
        BuildHierarchy(controlsByName, parentChildMap);

        // Top-level controls are those added to $form.
        var rootControlNames = parentChildMap.GetValueOrDefault("$form") ?? [];
        var rootControls = rootControlNames
            .Where(n => controlsByName.ContainsKey(n))
            .Select(n => controlsByName[n])
            .ToList();

        return new FormModel
        {
            FilePath = filePath,
            Language = DesignerLanguage.CSharp,
            FormName = classDecl.Identifier.Text,
            Namespace = ns,
            FormProperties = formProperties,
            FormEvents = formEvents,
            Controls = controlsByName.Values.ToList(),
            RootControls = rootControls,
        };
    }

    /// <summary>
    /// Try to parse: this.button1 = new System.Windows.Forms.Button();
    /// </summary>
    private static bool TryParseControlDeclaration(
        StatementSyntax statement,
        out string controlName,
        out string typeName
    )
    {
        controlName = typeName = "";

        if (
            statement
            is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
        )
        {
            return false;
        }

        // Left side: this.controlName
        if (assignment.Left is not MemberAccessExpressionSyntax left)
        {
            return false;
        }

        if (left.Expression is not ThisExpressionSyntax)
        {
            return false;
        }

        // Right side: new Type()
        if (assignment.Right is not ObjectCreationExpressionSyntax creation)
        {
            return false;
        }

        controlName = left.Name.Identifier.Text;
        typeName = creation.Type.ToString();
        return true;
    }

    /// <summary>
    /// Try to parse: this.controlName.Property = value;
    /// or: this.Property = value; (form-level)
    /// </summary>
    private static bool TryParsePropertyAssignment(
        StatementSyntax statement,
        out string? targetControl,
        out string propertyName,
        out string valueExpression
    )
    {
        targetControl = null;
        propertyName = valueExpression = "";

        if (
            statement
            is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
        )
        {
            return false;
        }

        // Event wiring uses += (AddAssignment) and -= (SubtractAssignment); skip those here.
        // They are handled by TryParseEventWiring instead.
        if (
            assignment.IsKind(SyntaxKind.AddAssignmentExpression)
            || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression)
        )
        {
            return false;
        }

        if (assignment.Left is not MemberAccessExpressionSyntax propAccess)
        {
            return false;
        }

        // Note: we intentionally do NOT skip ObjectCreationExpressionSyntax on the right side.
        // Control declarations (this.button1 = new Button()) are handled separately by
        // TryParseControlDeclaration. Allowing them here adds harmless entries to formProperties,
        // but is necessary to capture form-level properties like:
        //   this.ClientSize = new Size(284, 261)
        //   this.Font = new Font("Segoe UI", 9F)

        propertyName = propAccess.Name.Identifier.Text;
        valueExpression = assignment.Right.ToString();

        // this.controlName.Property = value
        if (
            propAccess.Expression is MemberAccessExpressionSyntax innerAccess
            && innerAccess.Expression is ThisExpressionSyntax
        )
        {
            targetControl = innerAccess.Name.Identifier.Text;
            return true;
        }

        // this.Property = value (form-level)
        if (propAccess.Expression is ThisExpressionSyntax)
        {
            targetControl = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to parse: this.Controls.Add(this.button1)
    /// or: this.panel1.Controls.Add(this.button1)
    /// </summary>
    private static bool TryParseControlsAdd(
        StatementSyntax statement,
        out string? parentControl,
        out string childControl
    )
    {
        parentControl = null;
        childControl = "";

        if (
            statement
            is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
        )
        {
            return false;
        }

        // The method must be ".Controls.Add"
        if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess)
        {
            return false;
        }

        if (methodAccess.Name.Identifier.Text != "Add")
        {
            return false;
        }

        // methodAccess.Expression should be X.Controls
        if (methodAccess.Expression is not MemberAccessExpressionSyntax controlsAccess)
        {
            return false;
        }

        if (controlsAccess.Name.Identifier.Text != "Controls")
        {
            return false;
        }

        // Extract parent: this.panel1.Controls or this.Controls
        if (
            controlsAccess.Expression is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Expression is ThisExpressionSyntax
        )
        {
            parentControl = parentAccess.Name.Identifier.Text;
        }
        else if (controlsAccess.Expression is ThisExpressionSyntax)
        {
            parentControl = null; // Form-level
        }
        else
        {
            return false;
        }

        // Extract child from first argument: this.button1
        // Supports both Controls.Add(child) and Controls.Add(child, col, row) for TableLayoutPanel.
        if (invocation.ArgumentList.Arguments.Count < 1)
        {
            return false;
        }

        var arg = invocation.ArgumentList.Arguments[0].Expression;
        if (
            arg is MemberAccessExpressionSyntax childAccess
            && childAccess.Expression is ThisExpressionSyntax
        )
        {
            childControl = childAccess.Name.Identifier.Text;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to parse: this.button1.Click += new EventHandler(this.button1_Click)
    /// or shorthand: this.button1.Click += this.button1_Click
    /// </summary>
    private static bool TryParseEventWiring(
        StatementSyntax statement,
        out string? targetControl,
        out string eventName,
        out string handlerName
    )
    {
        targetControl = null;
        eventName = handlerName = "";

        if (
            statement
            is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
        )
        {
            return false;
        }

        if (!assignment.IsKind(SyntaxKind.AddAssignmentExpression))
        {
            return false;
        }

        // Left side: this.button1.Click or this.Click
        if (assignment.Left is not MemberAccessExpressionSyntax eventAccess)
        {
            return false;
        }

        eventName = eventAccess.Name.Identifier.Text;

        if (
            eventAccess.Expression is MemberAccessExpressionSyntax innerAccess
            && innerAccess.Expression is ThisExpressionSyntax
        )
        {
            targetControl = innerAccess.Name.Identifier.Text;
        }
        else if (eventAccess.Expression is ThisExpressionSyntax)
        {
            targetControl = null;
        }
        else
        {
            return false;
        }

        // Right side: new EventHandler(this.handler) or this.handler
        var rightExpr = assignment.Right;

        // Unwrap: new EventHandler(this.handler_Click)
        if (
            rightExpr is ObjectCreationExpressionSyntax delegateCreation
            && delegateCreation.ArgumentList?.Arguments.Count == 1
        )
        {
            rightExpr = delegateCreation.ArgumentList.Arguments[0].Expression;
        }

        // Now rightExpr should be this.handler_Click
        if (rightExpr is MemberAccessExpressionSyntax handlerAccess)
        {
            handlerName = handlerAccess.Name.Identifier.Text;
            return true;
        }

        // Or just a simple identifier
        if (rightExpr is IdentifierNameSyntax handlerIdent)
        {
            handlerName = handlerIdent.Identifier.Text;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Wire up parent-child relationships on the ControlNode tree.
    /// </summary>
    private static void BuildHierarchy(
        Dictionary<string, ControlNode> controls,
        Dictionary<string, List<string>> parentChildMap
    )
    {
        foreach (var (parentKey, childNames) in parentChildMap)
        {
            if (parentKey == "$form")
            {
                continue; // Root controls are handled by caller.
            }

            if (!controls.TryGetValue(parentKey, out var parentNode))
            {
                continue;
            }

            foreach (var childName in childNames)
            {
                if (controls.TryGetValue(childName, out var childNode))
                {
                    parentNode.Children.Add(childNode);
                }
            }
        }
    }
}
