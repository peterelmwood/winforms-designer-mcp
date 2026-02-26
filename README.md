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

## Installation

### As a .NET global tool (from NuGet)

```bash
dotnet tool install -g WinFormsDesignerMcp
```

Then configure your MCP client to use the tool command directly:

```json
{ "command": "winforms-designer-mcp" }
```

### As a self-contained binary (from GitHub Releases)

Download the archive for your platform from the
[Releases](https://github.com/<owner>/winforms-designer-mcp/releases) page:

| Platform | Archive |
|---|---|
| Windows x64 | `winforms-designer-mcp-win-x64.tar.gz` |
| Linux x64 | `winforms-designer-mcp-linux-x64.tar.gz` |
| macOS ARM64 | `winforms-designer-mcp-osx-arm64.tar.gz` |

Extract, then point your MCP client at the binary path. No .NET SDK required.

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

## CLI Usage

The same tool capabilities are also available as a direct command-line interface. When arguments are passed,
the process runs as a CLI tool instead of an MCP server.

```bash
# Show help and all available subcommands
winforms-designer-mcp --help

# List all controls in a designer file
winforms-designer-mcp list-controls --file MyForm.Designer.cs

# Get properties for a specific control
winforms-designer-mcp get-control-properties --file MyForm.Designer.cs --control button1

# Parse a designer file into full JSON
winforms-designer-mcp parse --file MyForm.Designer.vb

# Render an interactive HTML preview
winforms-designer-mcp render-html --file MyForm.Designer.cs --output preview.html

# Render an SVG wireframe
winforms-designer-mcp render-svg --file MyForm.Designer.cs --output wireframe.svg

# Pipe rendered output directly to stdout with --output -
winforms-designer-mcp render-svg --file MyForm.Designer.cs --output - > wireframe.svg
winforms-designer-mcp render-html --file MyForm.Designer.cs --output - | less

# Add a new control
winforms-designer-mcp place-control --file MyForm.Designer.cs --type Button --name btnSave --parent panel1

# Modify a control property
winforms-designer-mcp modify-property --file MyForm.Designer.cs --control btnSave --property Text --value '"Save"'

# Remove a control
winforms-designer-mcp remove-control --file MyForm.Designer.cs --control btnSave

# List available control types
winforms-designer-mcp control-types

# Get metadata for a specific control type
winforms-designer-mcp control-type-info --type DataGridView

# Check accessibility compliance
winforms-designer-mcp check-accessibility --file MyForm.Designer.cs
```

> **Running from source:** Replace `winforms-designer-mcp` with `dotnet run --` when working from the repo:
> `dotnet run -- list-controls --file TestData/SampleForm.Designer.cs`

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
- **`render_form_html`** - Renders the designer file as an interactive HTML page that maps WinForms controls
  to native HTML elements (buttons, inputs, selects, tables, etc.) positioned at their actual designer
  coordinates. Includes a collapsible control tree sidebar and a click-to-inspect property panel. Output is
  a self-contained `.html` file that opens directly in a browser.

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

## Planned Enhancements (Phase 3)

The following enhancements to the HTML preview renderer are planned for future implementation.
They require extending the Roslyn parsers to handle additional WinForms designer patterns.

- **MenuStrip / ToolStripMenuItem interactions** — Extend C# and VB parsers to handle `Items.AddRange`
  and `DropDownItems.AddRange` calls, wiring `ToolStripMenuItem` hierarchy. Render real menu item names
  with click-to-open dropdown behavior and nested submenu flyouts in the HTML preview.
- **ToolStrip / StatusStrip real items** — Render actual parsed child `ToolStripButton`,
  `ToolStripLabel`, and `ToolStripStatusLabel` items instead of placeholder icons and text.
- **ContextMenuStrip support** — Parse context menu items and display them on right-click of
  associated controls in the HTML preview.

## Packaging & Releasing

The project is configured as a [.NET tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) and
includes GitHub Actions workflows for CI and publishing.

### Local packaging

```bash
# Pack a NuGet tool package
dotnet pack -c Release -o ./artifacts

# Install locally for testing
dotnet tool install -g --add-source ./artifacts WinFormsDesignerMcp

# Build a self-contained binary for a specific platform
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

### Versioning

The version is set in the `<Version>` property in the `.csproj`. During CI, the publish workflow derives the
version automatically from the git tag (e.g., tag `v1.2.3` → version `1.2.3`).

### Version tag script

Use `scripts/Bump-VersionTag.ps1` to compute and create the next `v<major>.<minor>.<build>` tag.

```powershell
# Create the next local tag
.\scripts\Bump-VersionTag.ps1

# Create an annotated tag with a message
.\scripts\Bump-VersionTag.ps1 -Message "Release v1.2.3"

# Create and push the next tag
.\scripts\Bump-VersionTag.ps1 -Push

# Dry run (no git mutation): prints only the computed tag value
.\scripts\Bump-VersionTag.ps1 -WhatIf

# Show additional script diagnostics
.\scripts\Bump-VersionTag.ps1 -Verbose
```

When `-WhatIf` is used, the script does not create or push any tag and writes only the computed tag (for example,
`v1.2.3`).
When `-Message` is provided, the script creates an annotated tag (`git tag -a ... -m ...`); otherwise it creates a
lightweight tag.
Use PowerShell's built-in `-Verbose` common parameter for additional diagnostics.

### Publishing a release

1. Update the `<Version>` in `winforms-designer-mcp.csproj` if desired (CI overrides it from the tag).
2. Commit and push to `main`.
3. Tag the commit: `git tag v1.2.3 && git push origin v1.2.3`
4. The **Publish** workflow will:
   - Build and push the NuGet package to nuget.org (requires `NUGET_API_KEY` secret).
   - Build self-contained binaries for Windows x64, Linux x64, and macOS ARM64.
   - Create a GitHub Release with the binaries attached.

### CI

The **CI** workflow runs on every push/PR to `main`: build, pack, and upload the `.nupkg` as an artifact.

## Project Structure

```text
Program.cs                              # MCP server entry point (stdio transport)
AGENTS.md                              # Agent instructions for AI coding assistants
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
  RenderFormHtmlTools.cs                # render_form_html (interactive HTML preview)
  ValidationTools.cs                    # check_accessibility_compliance
TestData/
  SampleForm.Designer.cs               # C# sample form (5 controls with nesting and events)
  SampleForm.Designer.vb               # Equivalent VB.NET sample form
.github/
  workflows/
    ci.yml                              # Build + pack on push/PR to main
    publish.yml                         # NuGet + self-contained binaries on version tags
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
