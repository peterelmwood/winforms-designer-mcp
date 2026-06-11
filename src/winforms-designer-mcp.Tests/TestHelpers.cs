using WinFormsDesignerMcp.Services;
using WinFormsDesignerMcp.Services.CSharp;
using WinFormsDesignerMcp.Services.VisualBasic;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Shared helpers for test classes.
/// </summary>
internal static class TestHelpers
{
    /// <summary>Path to the C# sample designer file, relative to the test output directory.</summary>
    public static string CSharpSamplePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "SampleForm.Designer.cs");

    /// <summary>Path to the VB.NET sample designer file, relative to the test output directory.</summary>
    public static string VbSamplePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "SampleForm.Designer.vb");

    /// <summary>
    /// Path to the complex multi-control test fixture sourced from the dotnet/winforms
    /// integration test suite (MIT). Contains 3-level nesting: TabControl→TabPage→leaf,
    /// and GroupBox→RadioButton nesting.
    /// </summary>
    public static string MultipleControlsPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "MultipleControls.Designer.cs");

    /// <summary>
    /// Path to the test fixture with ComponentResourceManager and typed event-handler delegates.
    /// </summary>
    public static string ResourcesFormPath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "ResourcesForm.Designer.cs");

    /// <summary>
    /// Create a <see cref="DesignerFileService"/> without DI, just like the CLI does.
    /// </summary>
    public static DesignerFileService CreateService() =>
        new(
            parsers: [new CSharpDesignerFileParser(), new VbDesignerFileParser()],
            writers: [new CSharpDesignerFileWriter(), new VbDesignerFileWriter()]
        );
}
