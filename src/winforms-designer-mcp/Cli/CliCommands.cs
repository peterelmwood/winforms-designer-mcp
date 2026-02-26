using System.CommandLine;
using WinFormsDesignerMcp.Services;
using WinFormsDesignerMcp.Services.CSharp;
using WinFormsDesignerMcp.Services.VisualBasic;
using WinFormsDesignerMcp.Tools;

namespace WinFormsDesignerMcp.Cli;

/// <summary>
/// Builds System.CommandLine subcommands that map 1:1 to the MCP tools.
/// Each subcommand constructs a <see cref="DesignerFileService"/> directly
/// (no DI host) and calls the existing static tool methods.
/// </summary>
public static class CliCommands
{
    /// <summary>
    /// Create a <see cref="DesignerFileService"/> without DI.
    /// </summary>
    private static DesignerFileService CreateService() =>
        new(
            parsers: [new CSharpDesignerFileParser(), new VbDesignerFileParser()],
            writers: [new CSharpDesignerFileWriter(), new VbDesignerFileWriter()]
        );

    /// <summary>
    /// Sentinel value: when <c>--output</c> is <c>"-"</c>, write the rendered
    /// content to stdout instead of a file.
    /// </summary>
    private const string StdoutMarker = "-";

    /// <summary>
    /// If <paramref name="requestedOutput"/> is <c>"-"</c>, returns a temp file path
    /// so the tool can write there; otherwise returns the original path.
    /// </summary>
    /// <remarks>
    /// TODO: Avoid temp files for stdout. For SVG, call with outputPath=null and decode
    /// the base64Content from the JSON response. For HTML, refactor the tool to optionally
    /// return content in the response body instead of always requiring a file path.
    /// </remarks>
    private static string ResolveOutputPath(string requestedOutput, string extension) =>
        requestedOutput == StdoutMarker
            ? Path.Combine(Path.GetTempPath(), $"winforms-cli-{Guid.NewGuid():N}{extension}")
            : requestedOutput;

    /// <summary>
    /// If the user asked for stdout (<c>-</c>), reads the temp file, writes its
    /// content to stdout, and deletes the temp file. Otherwise prints the JSON
    /// result as before.
    /// </summary>
    private static async Task EmitOutput(
        string requestedOutput,
        string actualPath,
        string jsonResult
    )
    {
        if (requestedOutput == StdoutMarker)
        {
            try
            {
                var content = await File.ReadAllTextAsync(actualPath);
                Console.Write(content);
            }
            finally
            {
                File.Delete(actualPath);
            }
        }
        else
        {
            Console.WriteLine(jsonResult);
        }
    }

    // ─── Shared options ──────────────────────────────────────────────────

    private static Option<string> FileOption(bool required = true)
    {
        var opt = new Option<string>("--file")
        {
            Description = "Path to the .Designer.cs or .Designer.vb file",
        };
        if (required)
            opt.Required = true;
        return opt;
    }

    private static Option<string> ControlOption() =>
        new("--control") { Description = "The control name (e.g., 'button1')", Required = true };

    // ─── Root command ────────────────────────────────────────────────────

    /// <summary>
    /// Build the root command with all subcommands.
    /// </summary>
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "WinForms Designer CLI — parse, inspect, render, and modify .Designer.cs / .Designer.vb files."
        );

        root.Add(BuildListControls());
        root.Add(BuildGetControlProperties());
        root.Add(BuildParse());
        root.Add(BuildPlaceControl());
        root.Add(BuildModifyProperty());
        root.Add(BuildRemoveControl());
        root.Add(BuildControlTypes());
        root.Add(BuildControlTypeInfo());
        root.Add(BuildRenderSvg());
        root.Add(BuildRenderHtml());
        root.Add(BuildCheckAccessibility());

        return root;
    }

    // ─── Subcommands ─────────────────────────────────────────────────────

    private static Command BuildListControls()
    {
        var file = FileOption();
        var cmd = new Command("list-controls", "List all controls with their hierarchy") { file };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await VisualTreeTools.ListControls(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildGetControlProperties()
    {
        var file = FileOption();
        var control = ControlOption();
        var cmd = new Command(
            "get-control-properties",
            "Get all properties and events for a specific control"
        )
        {
            file,
            control,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await VisualTreeTools.GetControlProperties(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    parseResult.GetValue(control)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildParse()
    {
        var file = FileOption();
        var cmd = new Command(
            "parse",
            "Parse a designer file into a complete structured JSON representation"
        )
        {
            file,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await VisualTreeTools.ParseDesignerFile(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildPlaceControl()
    {
        var file = FileOption();
        var type = new Option<string>("--type")
        {
            Description = "Control type (e.g., 'Button', 'TextBox')",
            Required = true,
        };
        var name = new Option<string?>("--name") { Description = "Optional name for the control" };
        var parent = new Option<string?>("--parent")
        {
            Description = "Optional parent control name",
        };
        var properties = new Option<string?>("--properties")
        {
            Description = "Optional JSON property overrides",
        };

        var cmd = new Command("place-control", "Add a new control to the designer file")
        {
            file,
            type,
            name,
            parent,
            properties,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await LayoutTools.PlaceControl(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    parseResult.GetValue(type)!,
                    parseResult.GetValue(name),
                    parseResult.GetValue(parent),
                    parseResult.GetValue(properties),
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildModifyProperty()
    {
        var file = FileOption();
        var control = ControlOption();
        var property = new Option<string>("--property")
        {
            Description = "The property name (e.g., 'Text', 'Size')",
            Required = true,
        };
        var value = new Option<string>("--value")
        {
            Description =
                "The value expression (e.g., '\"OK\"', 'new System.Drawing.Size(100, 50)')",
            Required = true,
        };

        var cmd = new Command("modify-property", "Change a property on an existing control")
        {
            file,
            control,
            property,
            value,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await LayoutTools.ModifyControlProperty(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    parseResult.GetValue(control)!,
                    parseResult.GetValue(property)!,
                    parseResult.GetValue(value)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildRemoveControl()
    {
        var file = FileOption();
        var control = ControlOption();
        var cmd = new Command(
            "remove-control",
            "Remove a control and all of its children from the designer file"
        )
        {
            file,
            control,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await LayoutTools.RemoveControl(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    parseResult.GetValue(control)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildControlTypes()
    {
        var cmd = new Command(
            "control-types",
            "List available WinForms control types with descriptions"
        );
        cmd.SetAction(_ =>
        {
            Console.WriteLine(MetadataTools.GetAvailableControlTypes());
        });
        return cmd;
    }

    private static Command BuildControlTypeInfo()
    {
        var type = new Option<string>("--type")
        {
            Description = "Control type name (e.g., 'Button', 'DataGridView')",
            Required = true,
        };
        var cmd = new Command(
            "control-type-info",
            "Get detailed metadata for a specific control type"
        )
        {
            type,
        };
        cmd.SetAction(parseResult =>
        {
            Console.WriteLine(MetadataTools.GetControlTypeInfo(parseResult.GetValue(type)!));
        });
        return cmd;
    }

    private static Command BuildRenderSvg()
    {
        var file = FileOption();
        var output = new Option<string?>("--output")
        {
            Description =
                "Output file path for the SVG, or '-' for stdout. If omitted, returns base64-encoded SVG.",
        };
        var padding = new Option<int>("--padding")
        {
            Description = "Padding in pixels around the form",
            DefaultValueFactory = _ => 20,
        };

        var cmd = new Command("render-svg", "Render the form as an SVG wireframe diagram")
        {
            file,
            output,
            padding,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var requested = parseResult.GetValue(output);
                var actualPath = requested is not null
                    ? ResolveOutputPath(requested, ".svg")
                    : null;
                var result = await RenderFormTools.RenderFormImage(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    actualPath,
                    parseResult.GetValue(padding),
                    ct
                );
                if (requested is not null && actualPath is not null)
                    await EmitOutput(requested, actualPath, result);
                else
                    Console.WriteLine(result);
            }
        );
        return cmd;
    }

    private static Command BuildRenderHtml()
    {
        var file = FileOption();
        var output = new Option<string>("--output")
        {
            Description = "Output file path for the HTML file, or '-' for stdout",
            Required = true,
        };

        var cmd = new Command(
            "render-html",
            "Render the form as an interactive HTML page with control tree and property inspector"
        )
        {
            file,
            output,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var requested = parseResult.GetValue(output)!;
                var actualPath = ResolveOutputPath(requested, ".html");
                var result = await RenderFormHtmlTools.RenderFormHtml(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    actualPath,
                    ct
                );
                await EmitOutput(requested, actualPath, result);
            }
        );
        return cmd;
    }

    private static Command BuildCheckAccessibility()
    {
        var file = FileOption();
        var cmd = new Command(
            "check-accessibility",
            "Scan the form for accessibility issues (missing names, tab order, etc.)"
        )
        {
            file,
        };
        cmd.SetAction(
            async (parseResult, ct) =>
            {
                var result = await ValidationTools.CheckAccessibilityCompliance(
                    CreateService(),
                    parseResult.GetValue(file)!,
                    ct
                );
                Console.WriteLine(result);
            }
        );
        return cmd;
    }
}
