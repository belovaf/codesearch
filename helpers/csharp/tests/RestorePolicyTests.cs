using ScipCsharp;
using Xunit;

namespace ScipCsharp.Tests;

/// <summary>
/// Unit tests for the restore gating policy: a repo whose projects all carry
/// fresh obj/project.assets.json must skip restore entirely (the steady state
/// of an actively developed repo), while a missing or stale assets file
/// triggers it.
/// </summary>
public class RestorePolicyTests : IDisposable
{
    private readonly string _solutionDir;

    public RestorePolicyTests()
    {
        _solutionDir = Path.Combine(Path.GetTempPath(), "cs-restore-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_solutionDir);
    }

    public void Dispose()
    {
        Directory.Delete(_solutionDir, recursive: true);
    }

    private string AddProject(string relativePath, bool withAssets, bool staleAssets = false)
    {
        var projectPath = Path.Combine(_solutionDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        if (withAssets)
        {
            var objDir = Path.Combine(Path.GetDirectoryName(projectPath)!, "obj");
            Directory.CreateDirectory(objDir);
            var assetsPath = Path.Combine(objDir, "project.assets.json");
            File.WriteAllText(assetsPath, "{}");
            if (staleAssets)
            {
                // Make the assets look OLDER than the project file.
                File.SetLastWriteTimeUtc(assetsPath, File.GetLastWriteTimeUtc(projectPath).AddMinutes(-5));
            }
        }
        return projectPath;
    }

    [Fact]
    public void FreshAssetsOnEveryProjectSkipsRestore()
    {
        AddProject(@"src\Acme.Core\Acme.Core.csproj", withAssets: true);
        AddProject(@"src\Acme.Web\Acme.Web.csproj", withAssets: true);

        Assert.False(RestorePolicy.NeedsRestore(
            new[] { @"src\Acme.Core\Acme.Core.csproj", @"src\Acme.Web\Acme.Web.csproj" }, _solutionDir));
    }

    [Fact]
    public void MissingAssetsTriggersRestore()
    {
        AddProject(@"src\Acme.Core\Acme.Core.csproj", withAssets: true);
        AddProject(@"src\Acme.Web\Acme.Web.csproj", withAssets: false);

        Assert.True(RestorePolicy.NeedsRestore(
            new[] { @"src\Acme.Core\Acme.Core.csproj", @"src\Acme.Web\Acme.Web.csproj" }, _solutionDir));
    }

    [Fact]
    public void StaleAssetsOlderThanProjectTriggerRestore()
    {
        AddProject(@"src\Acme.Core\Acme.Core.csproj", withAssets: true, staleAssets: true);

        Assert.True(RestorePolicy.NeedsRestore(new[] { @"src\Acme.Core\Acme.Core.csproj" }, _solutionDir));
    }

    [Fact]
    public void EmptyProjectListSkipsRestore()
    {
        Assert.False(RestorePolicy.NeedsRestore(Array.Empty<string>(), _solutionDir));
    }

    [Fact]
    public void RestoreTimeoutDefaultsTo300AndParsesEnvOverride()
    {
        Assert.Equal(300, RestorePolicy.RestoreTimeoutSeconds());

        Environment.SetEnvironmentVariable(RestorePolicy.TimeoutEnvName, "600");
        try
        {
            Assert.Equal(600, RestorePolicy.RestoreTimeoutSeconds());
        }
        finally
        {
            Environment.SetEnvironmentVariable(RestorePolicy.TimeoutEnvName, null);
        }

        Environment.SetEnvironmentVariable(RestorePolicy.TimeoutEnvName, "not-a-number");
        try
        {
            Assert.Equal(300, RestorePolicy.RestoreTimeoutSeconds());
        }
        finally
        {
            Environment.SetEnvironmentVariable(RestorePolicy.TimeoutEnvName, null);
        }
    }
}
