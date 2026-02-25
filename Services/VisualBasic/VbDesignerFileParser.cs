using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Services.VisualBasic;

/// <summary>
/// Parses a VB.NET WinForms .Designer.vb file into a <see cref="FormModel"/>
/// using Roslyn syntax analysis.
/// </summary>
public class VbDesignerFileParser : IDesignerFileParser
{
    public DesignerLanguage Language => DesignerLanguage.VisualBasic;

    public async Task<FormModel> ParseAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var sourceText = await File.ReadAllTextAsync(filePath, cancellationToken);
        var tree = VisualBasicSyntaxTree.ParseText(
            sourceText,
            cancellationToken: cancellationToken
        );
        var root = await tree.GetRootAsync(cancellationToken);

        // Find the class declaration.
        var classBlock =
            root.DescendantNodes().OfType<ClassBlockSyntax>().FirstOrDefault()
            ?? throw new InvalidOperationException($"No class declaration found in {filePath}");

        var classDecl = classBlock.ClassStatement;

        // Find namespace.
        string? ns = classBlock
            .Ancestors()
            .OfType<NamespaceBlockSyntax>()
            .FirstOrDefault()
            ?.NamespaceStatement.Name.ToString();

        // Find InitializeComponent method.
        var initMethod =
            classBlock
                .Members.OfType<MethodBlockSyntax>()
                .FirstOrDefault(m =>
                    m.SubOrFunctionStatement.Identifier.Text == "InitializeComponent"
                )
            ?? throw new InvalidOperationException(
                $"No InitializeComponent method found in {filePath}"
            );

        var statements = initMethod.Statements;

        // Phase 1: Extract control declarations (Me.X = New Type()).
        var controlsByName = new Dictionary<string, ControlNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var stmt in statements)
        {
            if (TryParseControlDeclaration(stmt, out var name, out var typeName))
            {
                controlsByName[name] = new ControlNode { Name = name, ControlType = typeName };
            }
        }

        // Phase 2: Extract property assignments, Controls.Add calls, and event wiring.
        var formProperties = new Dictionary<string, string>();
        var formEvents = new List<EventWiring>();
        var parentChildMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        parentChildMap["$form"] = [];

        foreach (var stmt in statements)
        {
            // Property assignment
            if (TryParsePropertyAssignment(stmt, out var target, out var propName, out var value))
            {
                if (target is null)
                {
                    formProperties[propName] = value;
                }
                else if (controlsByName.TryGetValue(target, out var control))
                {
                    control.Properties[propName] = value;
                }
            }

            // Controls.Add
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

            // AddHandler event wiring
            if (TryParseEventWiring(stmt, out var evtTarget, out var evtName, out var handler))
            {
                var wiring = new EventWiring { EventName = evtName, HandlerMethodName = handler };
                if (evtTarget is null)
                {
                    formEvents.Add(wiring);
                }
                else if (controlsByName.TryGetValue(evtTarget, out var ctrl))
                {
                    ctrl.Events.Add(wiring);
                }
            }
        }

        // Phase 3: Build hierarchy.
        BuildHierarchy(controlsByName, parentChildMap);

        var rootControlNames = parentChildMap.GetValueOrDefault("$form") ?? [];
        var rootControls = rootControlNames
            .Where(n => controlsByName.ContainsKey(n))
            .Select(n => controlsByName[n])
            .ToList();

        return new FormModel
        {
            FilePath = filePath,
            Language = DesignerLanguage.VisualBasic,
            FormName = classDecl.Identifier.Text,
            Namespace = ns,
            FormProperties = formProperties,
            FormEvents = formEvents,
            Controls = controlsByName.Values.ToList(),
            RootControls = rootControls,
        };
    }

    /// <summary>
    /// Try to parse: Me.Button1 = New System.Windows.Forms.Button()
    /// </summary>
    private static bool TryParseControlDeclaration(
        StatementSyntax statement,
        out string controlName,
        out string typeName
    )
    {
        controlName = typeName = "";

        if (statement is not AssignmentStatementSyntax assignment)
            return false;

        // Left side: Me.Button1
        if (assignment.Left is not MemberAccessExpressionSyntax left)
            return false;

        if (left.Expression is not MeExpressionSyntax)
            return false;

        // Right side: New Type()
        if (assignment.Right is not ObjectCreationExpressionSyntax creation)
            return false;

        controlName = left.Name.Identifier.Text;
        typeName = creation.Type.ToString();
        return true;
    }

    /// <summary>
    /// Try to parse: Me.Button1.Text = "Click Me"
    /// or: Me.Text = "Form1" (form-level)
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

        if (statement is not AssignmentStatementSyntax assignment)
            return false;

        if (assignment.Left is not MemberAccessExpressionSyntax propAccess)
            return false;

        // Note: we intentionally do NOT skip ObjectCreationExpressionSyntax on the right side.
        // Control declarations (Me.Button1 = New Button()) are handled separately by
        // TryParseControlDeclaration. Allowing them here adds harmless entries to formProperties,
        // but is necessary to capture form-level properties like:
        //   Me.ClientSize = New Size(284, 261)
        //   Me.Font = New Font("Segoe UI", 9F)

        propertyName = propAccess.Name.Identifier.Text;
        valueExpression = assignment.Right.ToString();

        // Me.Button1.Property = value
        if (
            propAccess.Expression is MemberAccessExpressionSyntax innerAccess
            && innerAccess.Expression is MeExpressionSyntax
        )
        {
            targetControl = innerAccess.Name.Identifier.Text;
            return true;
        }

        // Me.Property = value (form-level)
        if (propAccess.Expression is MeExpressionSyntax)
        {
            targetControl = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to parse: Me.Controls.Add(Me.Button1)
    /// or: Me.Panel1.Controls.Add(Me.Button1)
    /// </summary>
    private static bool TryParseControlsAdd(
        StatementSyntax statement,
        out string? parentControl,
        out string childControl
    )
    {
        parentControl = null;
        childControl = "";

        // In VB, method calls can be ExpressionStatementSyntax or CallStatementSyntax.
        InvocationExpressionSyntax? invocation = null;

        if (
            statement is ExpressionStatementSyntax exprStmt
            && exprStmt.Expression is InvocationExpressionSyntax inv1
        )
        {
            invocation = inv1;
        }
        else if (
            statement is CallStatementSyntax callStmt
            && callStmt.Invocation is InvocationExpressionSyntax inv2
        )
        {
            invocation = inv2;
        }

        if (invocation is null)
            return false;

        if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess)
            return false;

        if (methodAccess.Name.Identifier.Text != "Add")
            return false;

        if (methodAccess.Expression is not MemberAccessExpressionSyntax controlsAccess)
            return false;

        if (controlsAccess.Name.Identifier.Text != "Controls")
            return false;

        // Parent: Me.Panel1.Controls or Me.Controls
        if (
            controlsAccess.Expression is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Expression is MeExpressionSyntax
        )
        {
            parentControl = parentAccess.Name.Identifier.Text;
        }
        else if (controlsAccess.Expression is MeExpressionSyntax)
        {
            parentControl = null;
        }
        else
        {
            return false;
        }

        // Child arg: Me.Button1
        if (invocation.ArgumentList?.Arguments.Count != 1)
            return false;

        var arg = invocation.ArgumentList.Arguments[0].GetExpression();
        if (
            arg is MemberAccessExpressionSyntax childAccess
            && childAccess.Expression is MeExpressionSyntax
        )
        {
            childControl = childAccess.Name.Identifier.Text;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Try to parse: AddHandler Me.Button1.Click, AddressOf Me.Button1_Click
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

        if (statement is not AddRemoveHandlerStatementSyntax addHandler)
            return false;

        if (!addHandler.IsKind(SyntaxKind.AddHandlerStatement))
            return false;

        // Event expression: Me.Button1.Click or Me.Click
        if (addHandler.EventExpression is not MemberAccessExpressionSyntax eventAccess)
            return false;

        eventName = eventAccess.Name.Identifier.Text;

        if (
            eventAccess.Expression is MemberAccessExpressionSyntax innerAccess
            && innerAccess.Expression is MeExpressionSyntax
        )
        {
            targetControl = innerAccess.Name.Identifier.Text;
        }
        else if (eventAccess.Expression is MeExpressionSyntax)
        {
            targetControl = null;
        }
        else
        {
            return false;
        }

        // Handler: AddressOf Me.Button1_Click
        var delegateExpr = addHandler.DelegateExpression;
        if (
            delegateExpr is UnaryExpressionSyntax
            {
                Operand: MemberAccessExpressionSyntax handlerAccess
            }
        )
        {
            handlerName = handlerAccess.Name.Identifier.Text;
            return true;
        }

        if (delegateExpr is MemberAccessExpressionSyntax directHandler)
        {
            handlerName = directHandler.Name.Identifier.Text;
            return true;
        }

        return false;
    }

    private static void BuildHierarchy(
        Dictionary<string, ControlNode> controls,
        Dictionary<string, List<string>> parentChildMap
    )
    {
        foreach (var (parentKey, childNames) in parentChildMap)
        {
            if (parentKey == "$form")
                continue;

            if (!controls.TryGetValue(parentKey, out var parentNode))
                continue;

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
