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

            // T008 — Panel nesting: button1 must appear inside panel1, not as a root sibling.
            var panelIdx = content.IndexOf("data-name=\"panel1\"", StringComparison.Ordinal);
            var button1Idx = content.IndexOf("data-name=\"button1\"", StringComparison.Ordinal);
            var label1Idx = content.IndexOf("data-name=\"label1\"", StringComparison.Ordinal);
            Assert.True(panelIdx >= 0, "panel1 should appear in the HTML");
            Assert.True(button1Idx >= 0, "button1 should appear in the HTML");
            Assert.True(
                button1Idx > panelIdx,
                "button1 must appear after panel1 opens (nesting check)"
            );
            Assert.True(
                button1Idx < label1Idx,
                "button1 must appear before label1 (root-level sibling), confirming it is not promoted to the form root"
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RenderHtml_GroupBox_ChildrenNestedInParentDiv()
    {
        // T002 — GroupBox children must appear nested inside the groupBox1 div.
        // This is the primary regression test for FR-1 and FR-2.
        var tmpFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.html");
        try
        {
            var json = await RenderFormHtmlTools.RenderFormHtml(
                TestHelpers.CreateService(),
                TestHelpers.MultipleControlsPath,
                tmpFile,
                CancellationToken.None
            );

            // T002 / SC-6: verify control count in JSON response.
            // 21 System.Windows.Forms controls + 1 System.ComponentModel.BackgroundWorker = 22.
            using var doc = JsonDocument.Parse(json);
            var controlCount = doc.RootElement.GetProperty("controlCount").GetInt32();
            Assert.Equal(22, controlCount);

            var html = await File.ReadAllTextAsync(tmpFile);

            // Child controls must exist in the HTML at all.
            Assert.Contains("data-name=\"groupBox1\"", html);
            Assert.Contains("data-name=\"radioButton1\"", html);
            Assert.Contains("data-name=\"radioButton2\"", html);

            // radioButton1/2 must appear AFTER groupBox1 opens (nesting).
            var gbIdx = html.IndexOf("data-name=\"groupBox1\"", StringComparison.Ordinal);
            var rb1Idx = html.IndexOf("data-name=\"radioButton1\"", StringComparison.Ordinal);
            var rb2Idx = html.IndexOf("data-name=\"radioButton2\"", StringComparison.Ordinal);
            Assert.True(
                rb1Idx > gbIdx,
                "radioButton1 must appear after groupBox1 opens — it should be nested inside it"
            );
            Assert.True(
                rb2Idx > gbIdx,
                "radioButton2 must appear after groupBox1 opens — it should be nested inside it"
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RenderHtml_TabControl_LeafControlsNestedInTabPage()
    {
        // T003 — 3-level nesting: comboBox1 must appear inside tabPage1 which is inside tabControl1.
        var tmpFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.html");
        try
        {
            var html = await RenderAndReadHtml(TestHelpers.MultipleControlsPath, tmpFile);

            // All elements must exist.
            Assert.Contains("data-name=\"tabControl1\"", html);
            Assert.Contains("data-name=\"tabPage1\"", html);
            Assert.Contains("data-name=\"comboBox1\"", html);
            Assert.Contains("data-name=\"checkBox1\"", html);

            // comboBox1 must appear after tabControl1 AND after tabPage1 (3-level nesting).
            var tcIdx = html.IndexOf("data-name=\"tabControl1\"", StringComparison.Ordinal);
            var tp1Idx = html.IndexOf("data-name=\"tabPage1\"", StringComparison.Ordinal);
            var cb1Idx = html.IndexOf("data-name=\"comboBox1\"", StringComparison.Ordinal);
            Assert.True(tp1Idx > tcIdx, "tabPage1 must appear after tabControl1 opens");
            Assert.True(
                cb1Idx > tp1Idx,
                "comboBox1 must appear after tabPage1 opens — 3-level nesting"
            );

            // checkBox1 must appear after tabPage2.
            var tp2Idx = html.IndexOf("data-name=\"tabPage2\"", StringComparison.Ordinal);
            var chk1Idx = html.IndexOf("data-name=\"checkBox1\"", StringComparison.Ordinal);
            Assert.True(
                chk1Idx > tp2Idx,
                "checkBox1 must appear after tabPage2 opens — nested inside it"
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RenderHtml_VbSample_ChildControlsNestedInPanel()
    {
        // T014 — VB.NET parity: Button1 must be nested inside Panel1 for the VB sample.
        // VB sample uses PascalCase names: Panel1, Button1, TextBox1.
        var tmpFile = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid():N}.html");
        try
        {
            var html = await RenderAndReadHtml(TestHelpers.VbSamplePath, tmpFile);

            Assert.Contains("data-name=\"Panel1\"", html);
            Assert.Contains("data-name=\"Button1\"", html);

            var panelIdx = html.IndexOf("data-name=\"Panel1\"", StringComparison.Ordinal);
            var buttonIdx = html.IndexOf("data-name=\"Button1\"", StringComparison.Ordinal);
            Assert.True(
                buttonIdx > panelIdx,
                "Button1 must appear after Panel1 opens in the VB.NET HTML output"
            );
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    private static async Task<string> RenderAndReadHtml(string designerPath, string outputPath)
    {
        await RenderFormHtmlTools.RenderFormHtml(
            TestHelpers.CreateService(),
            designerPath,
            outputPath,
            CancellationToken.None
        );
        return await File.ReadAllTextAsync(outputPath);
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
