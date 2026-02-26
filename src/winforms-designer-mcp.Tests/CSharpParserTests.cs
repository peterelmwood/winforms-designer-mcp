using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Parser tests for the C# designer file parser.
/// Uses the shared SampleForm.Designer.cs test data.
/// </summary>
public class CSharpParserTests
{
    private static async Task<FormModel> ParseSampleAsync()
    {
        var service = TestHelpers.CreateService();
        return await service.ParseAsync(TestHelpers.CSharpSamplePath);
    }

    [Fact]
    public async Task Parse_FormName_IsForm1()
    {
        var model = await ParseSampleAsync();
        Assert.Equal("Form1", model.FormName);
    }

    [Fact]
    public async Task Parse_Language_IsCSharp()
    {
        var model = await ParseSampleAsync();
        Assert.Equal(DesignerLanguage.CSharp, model.Language);
    }

    [Fact]
    public async Task Parse_Namespace_IsSampleApp()
    {
        var model = await ParseSampleAsync();
        Assert.Equal("SampleApp", model.Namespace);
    }

    [Fact]
    public async Task Parse_ControlCount_Is5()
    {
        var model = await ParseSampleAsync();
        // panel1, button1, textBox1, label1, dataGridView1
        Assert.Equal(5, model.Controls.Count);
    }

    [Fact]
    public async Task Parse_RootControlCount_Is3()
    {
        var model = await ParseSampleAsync();
        // dataGridView1, label1, panel1 are added directly to the form
        Assert.Equal(3, model.RootControls.Count);
    }

    [Fact]
    public async Task Parse_Panel1HasTwoChildren()
    {
        var model = await ParseSampleAsync();
        var panel = model.Controls.First(c => c.Name == "panel1");
        Assert.Equal(2, panel.Children.Count);
        Assert.Contains(panel.Children, c => c.Name == "button1");
        Assert.Contains(panel.Children, c => c.Name == "textBox1");
    }

    [Fact]
    public async Task Parse_Button1Properties_ContainTextAndSize()
    {
        var model = await ParseSampleAsync();
        var button = model.Controls.First(c => c.Name == "button1");

        Assert.True(button.Properties.ContainsKey("Text"));
        Assert.Equal("\"Submit\"", button.Properties["Text"]);

        Assert.True(button.Properties.ContainsKey("Size"));
        Assert.Contains("75", button.Properties["Size"]);
    }

    [Fact]
    public async Task Parse_Button1_HasClickEvent()
    {
        var model = await ParseSampleAsync();
        var button = model.Controls.First(c => c.Name == "button1");

        Assert.Single(button.Events);
        Assert.Equal("Click", button.Events[0].EventName);
        Assert.Equal("button1_Click", button.Events[0].HandlerMethodName);
    }

    [Fact]
    public async Task Parse_FormProperties_ContainClientSize()
    {
        var model = await ParseSampleAsync();
        Assert.True(model.FormProperties.ContainsKey("ClientSize"));
        Assert.Contains("284", model.FormProperties["ClientSize"]);
        Assert.Contains("261", model.FormProperties["ClientSize"]);
    }

    [Fact]
    public async Task Parse_FormProperties_ContainText()
    {
        var model = await ParseSampleAsync();
        Assert.True(model.FormProperties.ContainsKey("Text"));
        Assert.Equal("\"Sample Form\"", model.FormProperties["Text"]);
    }

    [Fact]
    public async Task Parse_FormEvents_ContainLoad()
    {
        var model = await ParseSampleAsync();
        Assert.Single(model.FormEvents);
        Assert.Equal("Load", model.FormEvents[0].EventName);
        Assert.Equal("Form1_Load", model.FormEvents[0].HandlerMethodName);
    }

    [Fact]
    public async Task Parse_TextBox1_HasAccessibleName()
    {
        var model = await ParseSampleAsync();
        var textBox = model.Controls.First(c => c.Name == "textBox1");
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

    [Fact]
    public async Task Parse_DataGridView_HasColumnHeaderProperty()
    {
        var model = await ParseSampleAsync();
        var dgv = model.Controls.First(c => c.Name == "dataGridView1");
        Assert.True(dgv.Properties.ContainsKey("ColumnHeadersHeightSizeMode"));
    }

    [Fact]
    public async Task Parse_LocationProperties_ContainPointValues()
    {
        var model = await ParseSampleAsync();
        var button = model.Controls.First(c => c.Name == "button1");
        Assert.True(button.Properties.ContainsKey("Location"));
        Assert.Contains("Point", button.Properties["Location"]);
    }
}
