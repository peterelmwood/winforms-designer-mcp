using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Tests that verify C# and VB.NET parsers produce equivalent models from
/// equivalent designer files.
/// </summary>
public class ParserSymmetryTests
{
    [Fact]
    public async Task BothParsers_ReturnSameFormName()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        Assert.Equal(cs.FormName, vb.FormName);
    }

    [Fact]
    public async Task BothParsers_ReturnSameControlCount()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        Assert.Equal(cs.Controls.Count, vb.Controls.Count);
    }

    [Fact]
    public async Task BothParsers_ReturnSameRootControlCount()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        Assert.Equal(cs.RootControls.Count, vb.RootControls.Count);
    }

    [Fact]
    public async Task BothParsers_ReturnSameControlNames()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        var csNames = cs
            .Controls.Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        var vbNames = vb
            .Controls.Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(csNames, vbNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BothParsers_ReturnSameControlTypes()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        var csTypes = cs
            .Controls.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => c.ControlType)
            .ToList();
        var vbTypes = vb
            .Controls.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Select(c => c.ControlType)
            .ToList();

        Assert.Equal(csTypes, vbTypes);
    }

    [Fact]
    public async Task BothParsers_ReturnSamePropertyKeys()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        // For each control that appears in both, verify they have the same property keys.
        foreach (var csCtrl in cs.Controls)
        {
            var vbCtrl = vb.Controls.FirstOrDefault(c =>
                c.Name.Equals(csCtrl.Name, StringComparison.OrdinalIgnoreCase)
            );
            Assert.NotNull(vbCtrl);

            var csKeys = csCtrl.Properties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            var vbKeys = vbCtrl.Properties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(csKeys, vbKeys, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task BothParsers_ReturnSameFormPropertyKeys()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        var csKeys = cs.FormProperties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var vbKeys = vb.FormProperties.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(csKeys, vbKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BothParsers_ReturnSameEvents()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        // Same number of form events.
        Assert.Equal(cs.FormEvents.Count, vb.FormEvents.Count);

        // Same event names on each control.
        foreach (var csCtrl in cs.Controls)
        {
            var vbCtrl = vb.Controls.First(c =>
                c.Name.Equals(csCtrl.Name, StringComparison.OrdinalIgnoreCase)
            );
            Assert.Equal(csCtrl.Events.Count, vbCtrl.Events.Count);

            var csEventNames = csCtrl.Events.Select(e => e.EventName).OrderBy(n => n);
            var vbEventNames = vbCtrl.Events.Select(e => e.EventName).OrderBy(n => n);
            Assert.Equal(csEventNames, vbEventNames);
        }
    }

    [Fact]
    public async Task BothParsers_ReturnSameHierarchy()
    {
        var service = TestHelpers.CreateService();
        var cs = await service.ParseAsync(TestHelpers.CSharpSamplePath);
        var vb = await service.ParseAsync(TestHelpers.VbSamplePath);

        // Verify panel1 has same children in both.
        var csPanel = cs.Controls.First(c =>
            c.Name.Equals("panel1", StringComparison.OrdinalIgnoreCase)
        );
        var vbPanel = vb.Controls.First(c =>
            c.Name.Equals("Panel1", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Equal(csPanel.Children.Count, vbPanel.Children.Count);

        var csChildNames = csPanel
            .Children.Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        var vbChildNames = vbPanel
            .Children.Select(c => c.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(csChildNames, vbChildNames, StringComparer.OrdinalIgnoreCase);
    }
}
