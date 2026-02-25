# WinForms Designer MCP Server

An [MCP (Model Context Protocol)](https://modelcontextprotocol.io/) server that lets an AI read, understand, and
manipulate WinForms designer files. It parses `.Designer.cs` (C#) and `.Designer.vb` (VB.NET) files using
[Roslyn](https://github.com/dotnet/roslyn) and exposes a set of tools over the **stdio** transport so that any
MCP-compatible client (Claude Desktop, VS Code Copilot, MCP Inspector, etc.) can work with WinForms forms.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later

## Quick Start

```bash
# Clone and build
git clone <repo-url>
cd winforms-designer-mcp
dotnet build

# Run the server (stdio transport - typically launched by an MCP client)
dotnet run
```

## Connecting to an MCP Client

### Claude Desktop

Add the following to your Claude Desktop `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "winforms-designer": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/winforms-designer-mcp"]
    }
  }
}
```

### VS Code (GitHub Copilot)

Add to your `.vscode/mcp.json`:

```json
{
  "servers": {
    "winforms-designer": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/winforms-designer-mcp"]
    }
  }
}
```

### MCP Inspector (manual testing)

```bash
npx @modelcontextprotocol/inspector dotnet run --project /absolute/path/to/winforms-designer-mcp
```

## Approach

This server takes a **file-based** approach - it directly parses and generates `InitializeComponent()` code in
`.Designer.cs` and `.Designer.vb` files using Roslyn syntax trees. No live WinForms designer surface or running
Windows application is required, which means it works cross-platform for reading and writing designer files.

The architecture is language-agnostic at the tool layer: all MCP tools operate on an internal `FormModel`, and
language-specific parsing/writing is handled by pluggable `IDesignerFileParser` / `IDesignerFileWriter`
implementations.

## Implemented Tools

### Visual Tree & State Inspection

- **`list_controls`** - Returns a hierarchical list of all controls on the form, including their types, names,
  and parent-child relationships. Supports both C# and VB.NET designer files.
- **`get_control_properties`** - Fetches the full property bag (Size, Location, Anchor, Dock, etc.) for a specific
  control by name, including its events.
- **`parse_designer_file`** - Parses a designer file into a complete structured JSON representation of the entire
  form model.

### Layout & Mutation

- **`place_control`** - Adds a new control (e.g., Button, DataGridView) with smart defaults for common types.
  Supports auto-naming, parent container targeting, and property overrides.
- **`modify_control_property`** - Changes a single property on an existing control or the form itself.
- **`remove_control`** - Removes a control and all of its children from the designer file.

### Control Metadata

- **`get_available_control_types`** - Returns a curated list of ~30 common WinForms control types with descriptions
  and categories (Common, Container, Data, Display, Menu & Toolbar, Components, Dialogs).
- **`get_control_type_info`** - Returns detailed metadata for a specific control type: common properties with their
  types and descriptions, common events, and usage notes.

### Visualization

- **`render_form_image`** - Renders the designer file as an SVG wireframe diagram showing all controls at
  their actual positions and sizes, with type-specific colors, icons, and labels. Includes a window title bar
  and container nesting. Can return base64-encoded SVG in the response or save to a file.

### Validation

- **`check_accessibility_compliance`** - Scans the form for accessibility issues: missing `AccessibleName`,
  `AccessibleDescription`, `TabIndex` problems, missing `Text`, and duplicate tab indices. Returns a severity-rated
  report.

## Planned Tools (Phase 2)

The following tools from the original design are planned for future implementation:

- **`align_controls`** - High-level alignment tool (Top, Middle, DistributeVertically, etc.).
- **`apply_docking_and_anchoring`** - Set responsive resize behavior without knowing bitwise enums.
- **`create_event_handler`** - Scaffold a C#/VB.NET event handler in the code-behind and wire it in the designer.
- **`rename_control`** - Safely rename a control across `.Designer.*` and code-behind files.
- **`apply_theme_preset`** - Batch-update colors and fonts to match a style (e.g., "Modern Dark").
- **`set_control_style`** - Modify visual properties like BackColor, FlatStyle, or BackgroundImage.
- **`validate_layout_constraints`** - Detect overlapping or off-screen controls.
- **`render_form_ascii`** - Generate a text approximation of the form layout for spatial reasoning.
- **`search_component_docs`** - Provide documentation snippets for WinForms controls and libraries.

## Project Structure

```text
Program.cs                              # MCP server entry point (stdio transport)
Models/
  FormModel.cs                          # Language-agnostic form representation
  ControlNode.cs                        # Control tree node (properties, children, events)
  EventWiring.cs                        # Event -> handler mapping
  DesignerLanguage.cs                   # CSharp / VisualBasic enum
Services/
  IDesignerFileParser.cs                # Parser interface
  IDesignerFileWriter.cs                # Writer interface
  DesignerFileService.cs                # Facade - language detection + delegation
  CSharp/
    CSharpDesignerFileParser.cs         # Roslyn-based .Designer.cs parser
    CSharpDesignerFileWriter.cs         # .Designer.cs code generator
  VisualBasic/
    VbDesignerFileParser.cs             # Roslyn-based .Designer.vb parser
    VbDesignerFileWriter.cs             # .Designer.vb code generator
Tools/
  VisualTreeTools.cs                    # list_controls, get_control_properties, parse_designer_file
  LayoutTools.cs                        # place_control, modify_control_property, remove_control
  MetadataTools.cs                      # get_available_control_types, get_control_type_info
  RenderFormTools.cs                    # render_form_image (SVG wireframe)
  ValidationTools.cs                    # check_accessibility_compliance
TestData/
  SampleForm.Designer.cs               # C# sample form (5 controls with nesting and events)
  SampleForm.Designer.vb               # Equivalent VB.NET sample form
```

## Dependencies

| Package | Purpose |
|---|---|
| `ModelContextProtocol` | Official C# MCP SDK - server hosting, stdio transport, tool discovery |
| `Microsoft.Extensions.Hosting` | .NET Generic Host for DI and lifetime management |
| `Microsoft.CodeAnalysis.CSharp` | Roslyn - C# syntax tree parsing and manipulation |
| `Microsoft.CodeAnalysis.VisualBasic` | Roslyn - VB.NET syntax tree parsing and manipulation |

## License

MIT
