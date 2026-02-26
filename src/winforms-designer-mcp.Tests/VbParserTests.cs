using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Parser tests for the VB.NET designer file parser.
/// Uses the shared SampleForm.Designer.vb test data.
/// </summary>
public class VbParserTests
{
    private static async Task<FormModel> ParseSampleAsync()
    {
        var service = TestHelpers.CreateService();
        return await service.ParseAsync(TestHelpers.VbSamplePath);
    }

    [Fact]
    public async Task Parse_FormName_IsForm1()
    {
        var model = await ParseSampleAsync();
        Assert.Equal("Form1", model.FormName);
    }

    [Fact]
    public async Task Parse_Language_IsVisualBasic()
    {
        var model = await ParseSampleAsync();
        Assert.Equal(DesignerLanguage.VisualBasic, model.Language);
    }

    [Fact]
    public async Task Parse_ControlCount_Is5()
    {
        var model = await ParseSampleAsync();
        Assert.Equal(5, model.Controls.Count);
    }

    [Fact]
    public async Task Parse_RootControlCount_Is3()
    {
        var model = await ParseSampleAsync();
        Assert.Equal(3, model.RootControls.Count);
    }

    [Fact]
    public async Task Parse_Panel1HasTwoChildren()
    {
        var model = await ParseSampleAsync();
        var panel = model.Controls.First(c =>
            c.Name.Equals("Panel1", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal(2, panel.Children.Count);
    }

    [Fact]
    public async Task Parse_Button1_HasClickEvent()
    {
        var model = await ParseSampleAsync();
        var button = model.Controls.First(c =>
            c.Name.Equals("Button1", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Single(button.Events);
        Assert.Equal("Click", button.Events[0].EventName);
        Assert.Equal("Button1_Click", button.Events[0].HandlerMethodName);
    }

    [Fact]
    public async Task Parse_Button1Properties_ContainTextAndSize()
    {
        var model = await ParseSampleAsync();
        var button = model.Controls.First(c =>
            c.Name.Equals("Button1", StringComparison.OrdinalIgnoreCase)
        );

        Assert.True(button.Properties.ContainsKey("Text"));
        Assert.Equal("\"Submit\"", button.Properties["Text"]);

        Assert.True(button.Properties.ContainsKey("Size"));
        Assert.Contains("75", button.Properties["Size"]);
    }

    [Fact]
    public async Task Parse_FormProperties_ContainClientSize()
    {
        var model = await ParseSampleAsync();
        Assert.True(model.FormProperties.ContainsKey("ClientSize"));
        Assert.Contains("284", model.FormProperties["ClientSize"]);
    }

    [Fact]
    public async Task Parse_FormEvents_ContainLoad()
    {
        var model = await ParseSampleAsync();
        Assert.Single(model.FormEvents);
        Assert.Equal("Load", model.FormEvents[0].EventName);
    }

    [Fact]
    public async Task Parse_TextBox1_HasAccessibleName()
    {
        var model = await ParseSampleAsync();
        var textBox = model.Controls.First(c =>
            c.Name.Equals("TextBox1", StringComparison.OrdinalIgnoreCase)
        );
        Assert.True(textBox.Properties.ContainsKey("AccessibleName"));
        Assert.Equal("\"Search input\"", textBox.Properties["AccessibleName"]);
    }

    [Fact]
    public async Task Parse_ControlTypes_AreFullyQualified()
    {
        var model = await ParseSampleAsync();
        foreach (var control in model.Controls)
        {
            Assert.StartsWith("System.Windows.Forms.", control.ControlType);
        }
    }
}
