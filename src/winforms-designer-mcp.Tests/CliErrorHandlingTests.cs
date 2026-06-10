using System.Diagnostics;

namespace WinFormsDesignerMcp.Tests;

/// <summary>
/// Integration tests that run the CLI executable as a subprocess to verify error
/// handling behaviour introduced in Program.cs (the try/catch around InvokeAsync).
/// </summary>
public class CliErrorHandlingTests
{
    private static readonly string CliDll = Path.Combine(
        AppContext.BaseDirectory,
        "winforms-designer-mcp.dll"
    );

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCliAsync(
        params string[] args
    )
    {
        var psi = new ProcessStartInfo("dotnet", [CliDll, .. args])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout, stderr);
    }

    [Theory]
    [InlineData("list-controls", "--file", "form.txt")]
    [InlineData("list-controls", "--file", "form.py")]
    [InlineData("parse", "--file", "readme.md")]
    [InlineData("get-control-properties", "--file", "form.json", "--control", "button1")]
    public async Task UnsupportedFileExtension_ExitsOne_WritesErrorToStderr(params string[] args)
    {
        var (exitCode, _, stderr) = await RunCliAsync(args);

        Assert.Equal(1, exitCode);
        Assert.StartsWith("Error:", stderr.Trim());
        Assert.DoesNotContain("Unhandled exception", stderr);
        Assert.DoesNotContain("   at ", stderr);
    }

    [Fact]
    public async Task UnsupportedFileExtension_ErrorMessageContainsFilePath()
    {
        var (_, _, stderr) = await RunCliAsync("list-controls", "--file", "form.txt");

        Assert.Contains("form.txt", stderr);
    }

    [Fact]
    public async Task ValidFile_ExitsZero()
    {
        var (exitCode, _, _) = await RunCliAsync(
            "list-controls",
            "--file",
            TestHelpers.CSharpSamplePath
        );

        Assert.Equal(0, exitCode);
    }
}
