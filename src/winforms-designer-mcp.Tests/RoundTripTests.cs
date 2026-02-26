using WinFormsDesignerMcp.Models;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Round-trip tests: parse → write → re-parse and verify equivalence.
/// </summary>
public class RoundTripTests
{
    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public async Task RoundTrip_PreservesControlCount(string lang)
    {
        var sourcePath = lang == "cs" ? TestHelpers.CSharpSamplePath : TestHelpers.VbSamplePath;
        var ext = lang == "cs" ? ".Designer.cs" : ".Designer.vb";
        var tmpFile = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}{ext}");

        try
        {
            var service = TestHelpers.CreateService();

            // Parse original.
            var original = await service.ParseAsync(sourcePath);

            // Write to temp file.
            File.Copy(sourcePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            // Re-parse the written file.
            var reparsed = await service.ParseAsync(tmpFile);

            Assert.Equal(original.Controls.Count, reparsed.Controls.Count);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public async Task RoundTrip_PreservesControlNames(string lang)
    {
        var sourcePath = lang == "cs" ? TestHelpers.CSharpSamplePath : TestHelpers.VbSamplePath;
        var ext = lang == "cs" ? ".Designer.cs" : ".Designer.vb";
        var tmpFile = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}{ext}");

        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(sourcePath);

            File.Copy(sourcePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var reparsed = await service.ParseAsync(tmpFile);

            var originalNames = original
                .Controls.Select(c => c.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var reparsedNames = reparsed
                .Controls.Select(c => c.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.Equal(originalNames, reparsedNames, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public async Task RoundTrip_PreservesHierarchy(string lang)
    {
        var sourcePath = lang == "cs" ? TestHelpers.CSharpSamplePath : TestHelpers.VbSamplePath;
        var ext = lang == "cs" ? ".Designer.cs" : ".Designer.vb";
        var tmpFile = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}{ext}");

        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(sourcePath);

            File.Copy(sourcePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var reparsed = await service.ParseAsync(tmpFile);

            Assert.Equal(original.RootControls.Count, reparsed.RootControls.Count);

            // Verify panel1 still has children after round-trip.
            var originalPanel = original.Controls.First(c =>
                c.Name.Equals("panel1", StringComparison.OrdinalIgnoreCase)
            );
            var reparsedPanel = reparsed.Controls.First(c =>
                c.Name.Equals("panel1", StringComparison.OrdinalIgnoreCase)
            );
            Assert.Equal(originalPanel.Children.Count, reparsedPanel.Children.Count);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public async Task RoundTrip_PreservesFormProperties(string lang)
    {
        var sourcePath = lang == "cs" ? TestHelpers.CSharpSamplePath : TestHelpers.VbSamplePath;
        var ext = lang == "cs" ? ".Designer.cs" : ".Designer.vb";
        var tmpFile = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}{ext}");

        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(sourcePath);

            File.Copy(sourcePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var reparsed = await service.ParseAsync(tmpFile);

            var originalKeys = original.FormProperties.Keys.OrderBy(k => k).ToList();
            var reparsedKeys = reparsed.FormProperties.Keys.OrderBy(k => k).ToList();
            Assert.Equal(originalKeys, reparsedKeys);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public async Task RoundTrip_PreservesEvents(string lang)
    {
        var sourcePath = lang == "cs" ? TestHelpers.CSharpSamplePath : TestHelpers.VbSamplePath;
        var ext = lang == "cs" ? ".Designer.cs" : ".Designer.vb";
        var tmpFile = Path.Combine(Path.GetTempPath(), $"roundtrip-{Guid.NewGuid():N}{ext}");

        try
        {
            var service = TestHelpers.CreateService();
            var original = await service.ParseAsync(sourcePath);

            File.Copy(sourcePath, tmpFile);
            await service.WriteAsync(tmpFile, original);

            var reparsed = await service.ParseAsync(tmpFile);

            // Form-level events.
            Assert.Equal(original.FormEvents.Count, reparsed.FormEvents.Count);

            // Control-level events.
            foreach (var oCtrl in original.Controls)
            {
                var rCtrl = reparsed.Controls.First(c =>
                    c.Name.Equals(oCtrl.Name, StringComparison.OrdinalIgnoreCase)
                );
                Assert.Equal(oCtrl.Events.Count, rCtrl.Events.Count);
            }
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
