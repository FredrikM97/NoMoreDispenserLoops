using System.Diagnostics;
using Xunit;

public static class KleiVersion
{
    public const uint ChangeList = 123456;
}

public class OniBuildTests
{
    [Fact]
    public async Task BuildInfoUsesTheSelectedAssemblyAndRejectsMissingVersion()
    {
        string directory = Path.Combine(Path.GetTempPath(), "oni-build-" + Guid.NewGuid());
        string managed = Path.Combine(directory, "game managed");
        string output = Path.Combine(directory, "mod output");
        Directory.CreateDirectory(managed);
        string assembly = Path.Combine(managed, "Assembly-CSharp.dll");

        try
        {
            File.Copy(typeof(KleiVersion).Assembly.Location, assembly);
            var result = await WriteBuildInfo(managed, output);
            Assert.True(result.ExitCode == 0, result.Output);
            Assert.Equal("123456", File.ReadAllText(Path.Combine(output, "oni-build.txt")).Trim());

            File.Delete(Path.Combine(output, "oni-build.txt"));
            File.Copy(typeof(Assert).Assembly.Location, assembly, true);
            result = await WriteBuildInfo(managed, output);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Could not read KleiVersion.ChangeList", result.Output);
            Assert.False(File.Exists(Path.Combine(output, "oni-build.txt")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task<(int ExitCode, string Output)> WriteBuildInfo(string managed, string output)
    {
        string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../scripts/OniBuildInfo/OniBuildInfo.csproj"));
        var start = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(Path.Combine(managed, "Assembly-CSharp.dll"));
        Directory.CreateDirectory(output);
        start.ArgumentList.Add(Path.Combine(output, "oni-build.txt"));
        using var process = Process.Start(start);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout + await stderr);
    }
}
