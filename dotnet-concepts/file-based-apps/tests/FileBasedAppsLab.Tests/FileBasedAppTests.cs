using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FileBasedAppsLab.Tests;

// Runs only when the "dotnet" on PATH is SDK 11 or later (TODO-06 needs #:include).
// On SDK 10 the test is reported as skipped with a clear reason instead of failing.
public sealed class Sdk11FactAttribute : FactAttribute
{
    private static readonly string Version = ReadSdkVersion();

    public Sdk11FactAttribute()
    {
        if (!int.TryParse(Version.Split('.')[0], out var major) || major < 11)
            Skip = $"TODO-06 needs the .NET 11 SDK (dotnet on PATH reports '{Version}').";
    }

    private static string ReadSdkVersion()
    {
        // Run from the temp folder so no global.json of the lab changes the answer.
        var psi = new ProcessStartInfo("dotnet", "--version")
        {
            WorkingDirectory = Path.GetTempPath(),
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return output;
    }
}

public sealed class FileBasedAppTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public FileBasedAppTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private record Result(int ExitCode, string Output);

    // Runs "dotnet <dotnetArgs>" inside the temp directory, with the outer test host state removed.
    private Result Dotnet(params string[] dotnetArgs)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in dotnetArgs) psi.ArgumentList.Add(a);
        foreach (var key in psi.Environment.Keys.ToList())
        {
            if (key.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("DOTNET_HOST_PATH", StringComparison.OrdinalIgnoreCase))
                psi.Environment.Remove(key);
        }
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        // Safety net only: a normal run takes a few seconds.
        if (!process.WaitForExit(TimeSpan.FromSeconds(180)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet did not finish in 180 seconds.");
        }
        process.WaitForExit();
        return new Result(process.ExitCode, stdout.Result + stderr.Result);
    }

    private Result Run(string source, params string[] appArgs)
    {
        File.WriteAllText(Path.Combine(_dir, "app.cs"), source);
        var args = new List<string> { "run", "app.cs" };
        if (appArgs.Length > 0) { args.Add("--"); args.AddRange(appArgs); }
        return Dotnet(args.ToArray());
    }

    // Writes every file (creating subfolders) and runs app.cs.
    private Result RunFiles(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(_dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text);
        }
        return Dotnet("run", "app.cs");
    }

    // A fixed program (not part of the TODOs) that reports whether dynamic code is available.
    private const string DynamicCodeProbe =
        "Console.WriteLine(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);";

    [Fact]
    public void ShouldRunASingleFileWithoutAProjectFile()
    {
        var result = Run(Apps.HelloApp());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("hello from a file-based app", result.Output.Trim());
    }

    [Fact]
    public void ShouldPassArgumentsAfterTheDoubleDashAndReturnTheExitCode()
    {
        var result = Run(Apps.AppWithArgs(), "one", "two", "three");

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("one,two,three", result.Output.Trim());
    }

    [Fact]
    public void ShouldTurnOffTheAotDefaultWithAPropertyDirective()
    {
        var result = Run(Apps.AppWithProperty());

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("True", result.Output.Trim());
    }

    [Fact]
    public void ShouldKeepTheAotDefaultWhenNoPropertyIsSet()
    {
        // The same probe without the directive: the generated project targets Native AOT.
        var result = Run(DynamicCodeProbe);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("False", result.Output.Trim());
    }

    [Fact]
    public void ShouldAcceptAShebangAsTheFirstLine()
    {
        if (OperatingSystem.IsWindows()) return; // shebangs are a Unix feature

        var source = Apps.ShebangApp();
        Assert.StartsWith("#!/usr/bin/env dotnet", source);

        var result = Run(source);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("shebang ok", result.Output.Trim());
    }

    [Fact]
    public void ShouldReportACompileErrorWithAnIdAndANonZeroExitCode()
    {
        var result = Run(Apps.AppWithCompileError());

        Assert.NotEqual(0, result.ExitCode);
        Assert.Matches(new Regex(@"error CS\d{4}"), result.Output);
    }

    // Bonus
    [Fact]
    public void ShouldShowThePropertyInTheConvertedProject()
    {
        File.WriteAllText(Path.Combine(_dir, "app.cs"), Apps.AppWithProperty());

        var result = Dotnet("project", "convert", "app.cs");

        Assert.Equal(0, result.ExitCode);
        var csproj = File.ReadAllText(Path.Combine(_dir, "app", "app.csproj"));
        Assert.Contains("<PublishAot>false</PublishAot>", csproj);
        Assert.DoesNotContain("#:property", File.ReadAllText(Path.Combine(_dir, "app", "app.cs")));
    }

    // TODO-06 (optional): needs the .NET 11 SDK, skipped otherwise.
    [Sdk11Fact]
    public void ShouldSplitAnAppAcrossFilesWithIncludeAndAQuotedProperty()
    {
        var files = Apps.AppWithInclude();
        Assert.Contains("app.cs", files.Keys);
        Assert.True(files.Count >= 3, "app.cs plus at least two included files");

        var result = RunFiles(files);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "hi ana", "Contoso Tools" }, result.Output.Trim().Split('\n').Select(l => l.Trim()));
        Assert.DoesNotContain("CA2266", result.Output); // the shebang is there
    }

    [Sdk11Fact]
    public void ShouldFailWhenTheIncludeDirectivesAreRemoved()
    {
        var files = Apps.AppWithInclude().ToDictionary(
            f => f.Key,
            f => f.Key == "app.cs" ? DropLines(f.Value, "#:include") : f.Value);

        var result = RunFiles(files);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Matches(new Regex(@"error CS\d{4}"), result.Output); // the other files are not part of the build
    }

    [Sdk11Fact]
    public void ShouldWarnAboutTheEntryPointWhenTheShebangIsRemoved()
    {
        var files = Apps.AppWithInclude().ToDictionary(
            f => f.Key,
            f => f.Key == "app.cs" ? DropLines(f.Value, "#!") : f.Value);

        var result = RunFiles(files);

        Assert.Equal(0, result.ExitCode); // only a warning
        Assert.Contains("CA2266", result.Output);
    }

    private static string DropLines(string source, string prefix) =>
        string.Join('\n', source.Split('\n').Where(l => !l.TrimStart().StartsWith(prefix)));
}
