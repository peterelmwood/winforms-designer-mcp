using WinFormsDesignerMcp.Models;
using WinFormsDesignerMcp.Services;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Tests for <see cref="DesignerFileService"/> — language detection and delegation.
/// </summary>
public class DesignerFileServiceTests
{
    [Theory]
    [InlineData("Form1.Designer.cs", DesignerLanguage.CSharp)]
    [InlineData("Form1.Designer.vb", DesignerLanguage.VisualBasic)]
    [InlineData("SomeFile.cs", DesignerLanguage.CSharp)]
    [InlineData("SomeFile.vb", DesignerLanguage.VisualBasic)]
    [InlineData("path/to/MyForm.Designer.CS", DesignerLanguage.CSharp)]
    [InlineData("path/to/MyForm.Designer.VB", DesignerLanguage.VisualBasic)]
    public void DetectLanguage_ReturnsCorrectLanguage(string filePath, DesignerLanguage expected)
    {
        var result = DesignerFileService.DetectLanguage(filePath);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Form1.Designer.txt")]
    [InlineData("Form1.py")]
    [InlineData("readme.md")]
    public void DetectLanguage_ThrowsForUnknownExtension(string filePath)
    {
        Assert.Throws<ArgumentException>(() => DesignerFileService.DetectLanguage(filePath));
    }

    [Fact]
    public async Task ParseAsync_CSharpFile_ReturnsModel()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.CSharpSamplePath);

        Assert.Equal("Form1", model.FormName);
        Assert.Equal(DesignerLanguage.CSharp, model.Language);
    }

    [Fact]
    public async Task ParseAsync_VbFile_ReturnsModel()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.VbSamplePath);

        Assert.Equal("Form1", model.FormName);
        Assert.Equal(DesignerLanguage.VisualBasic, model.Language);
    }
}
