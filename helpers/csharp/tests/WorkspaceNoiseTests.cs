using ScipCsharp;
using Xunit;

namespace ScipCsharp.Tests;

/// <summary>
/// Unit tests for the workspace-noise policy: NuGet audit advisories must not
/// surface as index warnings (they are security notes, not load failures),
/// while genuine Msbuild load failures still do.
/// </summary>
public class WorkspaceNoiseTests
{
    [Fact]
    public void NuGetAuditAdvisoryIsFiltered()
    {
        var message =
            "Msbuild failed when processing the file 'Acme.Core.csproj' with message: " +
            "Package 'Microsoft.Extensions.Caching.Memory' 8.0.0 has a known high severity " +
            "vulnerability, https://github.com/advisories/GHSA-qj66-m88j-hmgj";
        Assert.True(WorkspaceNoise.IsNuGetAuditNoise(message));
    }

    [Fact]
    public void GenuineLoadFailureIsKept()
    {
        var message =
            "Msbuild failed when processing the file 'Acme.Core.csproj' with message: " +
            "The type or namespace name 'Catalog' does not exist in the namespace 'Acme'";
        Assert.False(WorkspaceNoise.IsNuGetAuditNoise(message));
    }

    [Fact]
    public void NullAndUnrelatedMessagesAreKept()
    {
        Assert.False(WorkspaceNoise.IsNuGetAuditNoise(null));
        Assert.False(WorkspaceNoise.IsNuGetAuditNoise(string.Empty));
        Assert.False(WorkspaceNoise.IsNuGetAuditNoise("Invalid project file, unsupported project type"));
    }

    [Theory]
    [InlineData("has a KNOWN vulnerability")]
    [InlineData("HAS A KNOWN VULNERABILITY")]
    public void MatchingIsCaseInsensitive(string message)
    {
        Assert.True(WorkspaceNoise.IsNuGetAuditNoise(message));
    }
}
