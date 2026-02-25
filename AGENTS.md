# AGENTS.md - WinForms Designer MCP Server

Instructions for AI coding agents working on this repository.

## Project Overview

This is an **MCP (Model Context Protocol) server** written in C# / .NET 10 that parses and manipulates WinForms
`.Designer.cs` and `.Designer.vb` files. It uses the **stdio** transport and is consumed by MCP clients like
Claude Desktop, VS Code Copilot, or MCP Inspector.

It does **not** start a WinForms application or designer surface - it performs all work by parsing and generating
`InitializeComponent()` code via Roslyn syntax trees.

## Build & Run

```bash
dotnet build                     # Build (debug)
dotnet build -c Release          # Build (release)
dotnet build -warnaserror        # Build with warnings-as-errors (use this to validate changes)
dotnet pack -c Release -o ./artifacts   # Pack as NuGet .NET tool
dotnet run                       # Run the MCP server (stdio - expects a client on stdin/stdout)
```

There are no unit tests yet. Validate changes by ensuring `dotnet build -warnaserror` passes with 0 errors and
0 warnings.

## Architecture

### Core Flow

```
MCP Client ←(stdio)→ Program.cs (Host) → MCP Tool methods → DesignerFileService → Parser/Writer
```

1. **Program.cs** - Configures the .NET Generic Host with DI, registers parser/writer services as singletons,
   adds the MCP server with stdio transport, and auto-discovers tool classes from the assembly.
2. **DesignerFileService** - Facade that detects language from file extension (`.cs` → C#, `.vb` → VB.NET)
   and delegates to the correct `IDesignerFileParser` / `IDesignerFileWriter`.
3. **Parsers** (`CSharpDesignerFileParser`, `VbDesignerFileParser`) - Use Roslyn to parse `InitializeComponent()`
   into a language-agnostic `FormModel`. Each statement is tried against several patterns: control declaration,
   property assignment, `Controls.Add`, and event wiring. A final pass builds the parent-child hierarchy.
4. **Writers** (`CSharpDesignerFileWriter`, `VbDesignerFileWriter`) - Regenerate `InitializeComponent()` and
   field declarations from a `FormModel`, preserving other code in the file (Dispose, etc.).
5. **Tools** - Static methods annotated with `[McpServerTool]` inside `[McpServerToolType]` classes.
   `DesignerFileService` is injected as a method parameter by the MCP SDK.

### Key Models

- **`FormModel`** - The shared currency. Contains `FormProperties` (dict), `FormEvents`, `Controls` (flat list),
  and `RootControls` (hierarchical tree of `ControlNode`).
- **`ControlNode`** - Name, fully-qualified ControlType, Properties (dict string→string of raw value expressions),
  Children (list), Events (list of `EventWiring`).
- **`EventWiring`** - `EventName` + `HandlerMethodName`.

### Property Values

Property values in `ControlNode.Properties` and `FormModel.FormProperties` are stored as **raw source code
expressions** exactly as they appear in the designer file. Examples:

- `"Submit"` (including the quotes)
- `new System.Drawing.Size(75, 23)`
- `new System.Drawing.Point(12, 80)`
- `true`
- `System.Windows.Forms.DockStyle.Fill`

When extracting numeric values (e.g., for rendering), use regex on the parenthesized arguments. Do not attempt
to evaluate the expressions.

## Adding a New MCP Tool

1. Create a new class (or add to an existing one) in `Tools/`.
2. Annotate the class with `[McpServerToolType]`.
3. Annotate each tool method with `[McpServerTool(Name = "tool_name")]` and `[Description("...")]`.
4. Tool methods must be `public static` (or `public static async Task<string>`).
5. Add `DesignerFileService designerService` as a parameter for DI injection.
6. Add `[Description("...")]` on each parameter.
7. Return JSON (use `JsonSerializer.Serialize` with `JsonNamingPolicy.CamelCase`).

The MCP SDK auto-discovers tools from the assembly - no manual registration needed.

## Adding a New Language

1. Implement `IDesignerFileParser` and `IDesignerFileWriter` for the language.
2. Add a new value to the `DesignerLanguage` enum.
3. Register the implementations as singletons in `Program.cs`.
4. Update `DesignerFileService.DetectLanguage` with the file extension mapping.

## Conventions

- **Namespace**: `WinFormsDesignerMcp` (root), with `.Models`, `.Services`, `.Services.CSharp`,
  `.Services.VisualBasic`, `.Tools` sub-namespaces.
- **Nullable**: enabled project-wide.
- **Implicit usings**: enabled.
- **Target**: `net10.0`.
- **No `this.` or `Me.` in C# code** - the project uses implicit member access.
- **C# and VB.NET parity**: any parsing/writing feature should work for both languages. If you change one
  parser, apply the equivalent change to the other.
- **TestData**: files in `TestData/` are excluded from compilation (`<Compile Remove="TestData\**" />`) and
  copied to the output directory. These are sample `.Designer.cs` / `.Designer.vb` files for manual testing.

## Known Pitfalls

- **Parser symmetry**: The C# and VB.NET parsers have parallel structure but different Roslyn syntax types.
  `ThisExpressionSyntax` (C#) maps to `MeExpressionSyntax` (VB). `ExpressionStatementSyntax` with
  `AssignmentExpressionSyntax` (C#) maps to `AssignmentStatementSyntax` (VB). Always change both parsers together.
- **ObjectCreationExpressionSyntax**: Property values using `new` (Size, Location, Font, etc.) must NOT be
  filtered out by the parsers. An earlier bug blanket-skipped all `ObjectCreationExpressionSyntax` right-hand
  values - this was fixed but could regress.
- **SVG rendering Z-order**: WinForms `Controls[0]` has the highest Z-order (painted on top). SVG paints later
  elements on top. The renderer iterates controls in reverse to compensate.
- **Container rendering**: Containers with children get only a subtle name label to avoid overlapping child controls.
- **stdio protocol**: MCP uses stdout for protocol messages. All logging goes to stderr
  (`LogToStandardErrorThreshold = LogLevel.Trace`). Never write non-protocol output to stdout.

## Packaging

The project is packaged as a .NET global tool (`<PackAsTool>true</PackAsTool>`). The version is in the `<Version>`
property in the `.csproj`. When tagging a release (e.g., `git tag v1.2.3`), the CI publish workflow derives the
version from the tag automatically.

## File Quick Reference

| File | What it does |
|---|---|
| `Program.cs` | Entry point: DI, MCP server config, stdio transport |
| `Models/FormModel.cs` | Core model: form properties, events, controls |
| `Models/ControlNode.cs` | Single control: name, type, properties, children, events |
| `Services/DesignerFileService.cs` | Facade: language detection + parser/writer delegation |
| `Services/CSharp/CSharpDesignerFileParser.cs` | Roslyn C# parser for InitializeComponent() |
| `Services/CSharp/CSharpDesignerFileWriter.cs` | C# code generator |
| `Services/VisualBasic/VbDesignerFileParser.cs` | Roslyn VB.NET parser for InitializeComponent() |
| `Services/VisualBasic/VbDesignerFileWriter.cs` | VB.NET code generator |
| `Tools/VisualTreeTools.cs` | list_controls, get_control_properties, parse_designer_file |
| `Tools/LayoutTools.cs` | place_control, modify_control_property, remove_control |
| `Tools/MetadataTools.cs` | get_available_control_types, get_control_type_info |
| `Tools/RenderFormTools.cs` | render_form_image (SVG wireframe) |
| `Tools/RenderFormHtmlTools.cs` | render_form_html (interactive HTML preview) |
| `Tools/ValidationTools.cs` | check_accessibility_compliance |
| `.github/workflows/ci.yml` | CI: build + pack on push/PR to main |
| `.github/workflows/publish.yml` | Publish: NuGet + binaries on version tags |
