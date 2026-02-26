using System.Text.Json;
using WinFormsDesignerMcp.Tools;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Tests for the MCP tool methods (VisualTreeTools, LayoutTools, MetadataTools, etc.)
/// called directly, as the CLI does.
/// </summary>
public class ToolTests
{
    // ─── VisualTreeTools ─────────────────────────────────────────────────

    [Fact]
    public async Task ListControls_ReturnsValidJson_WithExpectedFields()
    {
        var json = await VisualTreeTools.ListControls(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Form1", root.GetProperty("formName").GetString());
        Assert.Equal("CSharp", root.GetProperty("language").GetString());
        Assert.Equal("SampleApp", root.GetProperty("namespace").GetString());
        Assert.Equal(3, root.GetProperty("controls").GetArrayLength());
    }

    [Fact]
    public async Task GetControlProperties_Button1_ReturnsProperties()
    {
        var json = await VisualTreeTools.GetControlProperties(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            "button1",
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("button1", root.GetProperty("name").GetString());
        Assert.Contains("Submit", root.GetProperty("properties").GetProperty("Text").GetString()!);
    }

    [Fact]
    public async Task GetControlProperties_FormLevel_ReturnsFormProperties()
    {
        var json = await VisualTreeTools.GetControlProperties(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            "Form1",
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Form1", root.GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("properties", out _));
    }

    [Fact]
    public async Task GetControlProperties_NotFound_ReturnsError()
    {
        var json = await VisualTreeTools.GetControlProperties(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            "nonExistent",
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task ParseDesignerFile_ReturnsCompleteModel()
    {
        var json = await VisualTreeTools.ParseDesignerFile(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("formName", out _));
        Assert.True(root.TryGetProperty("controls", out _));
        Assert.True(root.TryGetProperty("rootControls", out _));
        Assert.True(root.TryGetProperty("formProperties", out _));
    }

    // ─── MetadataTools ──────────────────────────────────────────────────

    [Fact]
    public void GetAvailableControlTypes_ReturnsNonEmptyJsonArray()
    {
        var json = MetadataTools.GetAvailableControlTypes();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("DataGridView")]
    [InlineData("Panel")]
    public void GetControlTypeInfo_KnownType_ReturnsDetails(string typeName)
    {
        var json = MetadataTools.GetControlTypeInfo(typeName);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("name", out var nameEl));
        Assert.Equal(typeName, nameEl.GetString());
        Assert.True(root.TryGetProperty("commonProperties", out _));
    }

    [Fact]
    public void GetControlTypeInfo_UnknownType_ReturnsError()
    {
        var json = MetadataTools.GetControlTypeInfo("FakeWidget");
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    // ─── LayoutTools ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceControl_AddsButtonToForm()
    {
        // Work on a copy so we don't mutate the shared test data.
        var tmpFile = CopyToTemp(TestHelpers.CSharpSamplePath, ".cs");
        try
        {
            var json = await LayoutTools.PlaceControl(
                TestHelpers.CreateService(),
                tmpFile,
                "Button",
                name: "btnTest",
                cancellationToken: CancellationToken.None
            );

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("btnTest", doc.RootElement.GetProperty("controlName").GetString());

            // Re-parse and verify the control exists.
            var model = await TestHelpers.CreateService().ParseAsync(tmpFile);
            Assert.Contains(model.Controls, c => c.Name == "btnTest");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task ModifyControlProperty_ChangesText()
    {
        var tmpFile = CopyToTemp(TestHelpers.CSharpSamplePath, ".cs");
        try
        {
            var json = await LayoutTools.ModifyControlProperty(
                TestHelpers.CreateService(),
                tmpFile,
                "button1",
                "Text",
                "\"OK\"",
                CancellationToken.None
            );

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            // Re-parse and verify.
            var model = await TestHelpers.CreateService().ParseAsync(tmpFile);
            var button = model.Controls.First(c => c.Name == "button1");
            Assert.Equal("\"OK\"", button.Properties["Text"]);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RemoveControl_RemovesControlAndChildren()
    {
        var tmpFile = CopyToTemp(TestHelpers.CSharpSamplePath, ".cs");
        try
        {
            var json = await LayoutTools.RemoveControl(
                TestHelpers.CreateService(),
                tmpFile,
                "panel1",
                CancellationToken.None
            );

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            // panel1, button1, textBox1 should all be gone.
            var removedNames = doc
                .RootElement.GetProperty("removedControls")
                .EnumerateArray()
                .Select(e => e.GetString())
                .ToList();
            Assert.Contains("panel1", removedNames);
            Assert.Contains("button1", removedNames);
            Assert.Contains("textBox1", removedNames);

            // Re-parse and verify.
            var model = await TestHelpers.CreateService().ParseAsync(tmpFile);
            Assert.DoesNotContain(model.Controls, c => c.Name == "panel1");
            Assert.DoesNotContain(model.Controls, c => c.Name == "button1");
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RemoveControl_NotFound_ReturnsFalse()
    {
        var json = await LayoutTools.RemoveControl(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            "nope",
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
    }

    // ─── RenderFormTools ─────────────────────────────────────────────────

    [Fact]
    public async Task RenderSvg_WithoutOutput_ReturnsBase64()
    {
        var json = await RenderFormTools.RenderFormImage(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            outputPath: null,
            padding: 20,
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("svg", doc.RootElement.GetProperty("format").GetString());
        // base64Content should be present.
        Assert.True(doc.RootElement.TryGetProperty("base64Content", out _));
    }

    [Fact]
    public async Task RenderSvg_WithOutput_SavesFile()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.svg");
        try
        {
            var json = await RenderFormTools.RenderFormImage(
                TestHelpers.CreateService(),
                TestHelpers.CSharpSamplePath,
                outputPath: tmpFile,
                padding: 20,
                CancellationToken.None
            );

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.True(File.Exists(tmpFile));
            var content = await File.ReadAllTextAsync(tmpFile);
            Assert.StartsWith("<svg", content);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ─── RenderFormHtmlTools ─────────────────────────────────────────────

    [Fact]
    public async Task RenderHtml_SavesFile()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.html");
        try
        {
            var json = await RenderFormHtmlTools.RenderFormHtml(
                TestHelpers.CreateService(),
                TestHelpers.CSharpSamplePath,
                tmpFile,
                CancellationToken.None
            );

            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.True(File.Exists(tmpFile));
            var content = await File.ReadAllTextAsync(tmpFile);
            Assert.Contains("<!DOCTYPE html>", content);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // ─── ValidationTools ─────────────────────────────────────────────────

    [Fact]
    public async Task CheckAccessibility_ReturnsReport()
    {
        var json = await ValidationTools.CheckAccessibilityCompliance(
            TestHelpers.CreateService(),
            TestHelpers.CSharpSamplePath,
            CancellationToken.None
        );

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Form1", root.GetProperty("formName").GetString());
        Assert.True(root.TryGetProperty("issueCount", out _));
        Assert.True(root.TryGetProperty("issues", out _));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static string CopyToTemp(string source, string extension)
    {
        // Use .Designer.cs extension so the service detects the language correctly.
        var tmp = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.Designer{extension}");
        File.Copy(source, tmp);
        return tmp;
    }
}
