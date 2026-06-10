namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Tests that verify the two bugs fixed in modify_control_property:
/// (1) Typed event-handler delegate types are preserved (not downgraded to System.EventHandler).
/// (2) ComponentResourceManager local declarations and resources.ApplyResources calls are
///     preserved verbatim across parse → write round-trips.
/// </summary>
public class FidelityTests
{
    // -------------------------------------------------------------------------
    // Delegate-type preservation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Parse_ResourcesForm_CapturesDelegateTypes()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.ResourcesFormPath);

        // textBox1.KeyPress uses KeyPressEventHandler
        var textBox = model.Controls.First(c => c.Name == "textBox1");
        Assert.Single(textBox.Events);
        Assert.Equal("KeyPress", textBox.Events[0].EventName);
        Assert.Equal("textBox1_KeyPress", textBox.Events[0].HandlerMethodName);
        Assert.Equal("System.Windows.Forms.KeyPressEventHandler", textBox.Events[0].DelegateTypeName);

        // button1.Click uses the plain EventHandler
        var button = model.Controls.First(c => c.Name == "button1");
        Assert.Single(button.Events);
        Assert.Equal("Click", button.Events[0].EventName);
        Assert.Equal("button1_Click", button.Events[0].HandlerMethodName);
        Assert.Equal("System.EventHandler", button.Events[0].DelegateTypeName);
    }

    [Fact]
    public async Task RoundTrip_ResourcesForm_PreservesDelegateTypes()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.Designer.cs");
        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(TestHelpers.ResourcesFormPath);

            File.Copy(TestHelpers.ResourcesFormPath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var written = await File.ReadAllTextAsync(tmpFile);

            // The typed delegate must appear verbatim in the written file.
            Assert.Contains(
                "new System.Windows.Forms.KeyPressEventHandler(this.textBox1_KeyPress)",
                written);

            // Standard EventHandler must not be substituted for KeyPressEventHandler.
            Assert.DoesNotContain(
                "new System.EventHandler(this.textBox1_KeyPress)",
                written);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>
    /// Existing SampleForm still round-trips correctly (no regression).
    /// </summary>
    [Fact]
    public async Task RoundTrip_SampleForm_PreservesClickEventHandler()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.Designer.cs");
        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(TestHelpers.CSharpSamplePath);

            File.Copy(TestHelpers.CSharpSamplePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var written = await File.ReadAllTextAsync(tmpFile);

            Assert.Contains("new System.EventHandler(this.button1_Click)", written);
            Assert.Contains("new System.EventHandler(this.Form1_Load)", written);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // -------------------------------------------------------------------------
    // ComponentResourceManager / resources.ApplyResources preservation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Parse_ResourcesForm_CapturesLocalDeclaration()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.ResourcesFormPath);

        Assert.Single(model.LocalDeclarations);
        Assert.Contains("ComponentResourceManager", model.LocalDeclarations[0]);
        Assert.Contains("ResourcesForm", model.LocalDeclarations[0]);
    }

    [Fact]
    public async Task Parse_ResourcesForm_CapturesPerControlRawStatements()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.ResourcesFormPath);

        var textBox = model.Controls.First(c => c.Name == "textBox1");
        Assert.Single(textBox.RawStatements);
        Assert.Contains("resources.ApplyResources", textBox.RawStatements[0]);
        Assert.Contains("textBox1", textBox.RawStatements[0]);

        var button = model.Controls.First(c => c.Name == "button1");
        Assert.Single(button.RawStatements);
        Assert.Contains("resources.ApplyResources", button.RawStatements[0]);
        Assert.Contains("button1", button.RawStatements[0]);
    }

    [Fact]
    public async Task Parse_ResourcesForm_CapturesFormLevelRawStatement()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.ResourcesFormPath);

        Assert.Single(model.FormRawStatements);
        Assert.Contains("resources.ApplyResources", model.FormRawStatements[0]);
        Assert.Contains("$this", model.FormRawStatements[0]);
    }

    [Fact]
    public async Task RoundTrip_ResourcesForm_PreservesLocalDeclaration()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.Designer.cs");
        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(TestHelpers.ResourcesFormPath);

            File.Copy(TestHelpers.ResourcesFormPath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var written = await File.ReadAllTextAsync(tmpFile);

            // The ComponentResourceManager declaration must be present.
            Assert.Contains("ComponentResourceManager resources", written);
            Assert.Contains("typeof(ResourcesForm)", written);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RoundTrip_ResourcesForm_PreservesApplyResourcesCalls()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.Designer.cs");
        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(TestHelpers.ResourcesFormPath);

            File.Copy(TestHelpers.ResourcesFormPath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var written = await File.ReadAllTextAsync(tmpFile);

            Assert.Contains("resources.ApplyResources(this.textBox1", written);
            Assert.Contains("resources.ApplyResources(this.button1", written);
            Assert.Contains("resources.ApplyResources(this, ", written);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task RoundTrip_ResourcesForm_ReParsePreservesAllEvents()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.Designer.cs");
        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(TestHelpers.ResourcesFormPath);

            File.Copy(TestHelpers.ResourcesFormPath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            // Re-parse the written file and confirm events survived.
            var reparsed = await service.ParseAsync(tmpFile);

            var textBox = reparsed.Controls.First(c => c.Name == "textBox1");
            Assert.Single(textBox.Events);
            Assert.Equal("System.Windows.Forms.KeyPressEventHandler", textBox.Events[0].DelegateTypeName);

            var button = reparsed.Controls.First(c => c.Name == "button1");
            Assert.Single(button.Events);
            Assert.Equal("System.EventHandler", button.Events[0].DelegateTypeName);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    // -------------------------------------------------------------------------
    // SampleForm regression — no LocalDeclarations on a plain form
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Parse_SampleForm_HasNoLocalDeclarations()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        Assert.Empty(model.LocalDeclarations);
    }

    [Fact]
    public async Task Parse_SampleForm_HasNoRawStatements()
    {
        var service = TestHelpers.CreateService();
        var model = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        Assert.Empty(model.FormRawStatements);
        foreach (var ctrl in model.Controls)
        {
            Assert.Empty(ctrl.RawStatements);
        }
    }
}
