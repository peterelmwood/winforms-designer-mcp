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
    /// Create a <see cref="DesignerFileService"/> without DI, just like the CLI does.
    /// </summary>
    public static DesignerFileService CreateService() =>
        new(
            parsers: [new CSharpDesignerFileParser(), new VbDesignerFileParser()],
            writers: [new CSharpDesignerFileWriter(), new VbDesignerFileWriter()]
        );
}
