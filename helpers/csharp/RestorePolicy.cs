using System.Diagnostics;

namespace ScipCsharp;

/// <summary>
/// Restore policy for the design-time workspace load: MSBuildWorkspace does
/// not restore, but running <c>dotnet restore</c> on every reindex is far too
/// heavy for multi-repo hubs. Restore only when the evidence says assets are
/// missing or stale. Compiled into both scip-csharp and the test project via
/// a linked Compile item (same pattern as WorkspaceLoad.cs).
/// </summary>
internal static class RestorePolicy
{
    /// <summary>Env override for the restore kill timeout, in seconds.</summary>
    public const string TimeoutEnvName = "CODESEARCH_RESTORE_TIMEOUT_SECS";
    private const string RestoreTimeoutEnv = TimeoutEnvName;
    private const int DefaultRestoreTimeoutSeconds = 300;

    /// <summary>
    /// True when at least one project lacks <c>obj/project.assets.json</c> or
    /// has one older than its .csproj (a package reference or target framework
    /// was edited since the last restore). False means every project has fresh
    /// assets and restore can be skipped entirely — the steady state of an
    /// actively developed repo.
    /// </summary>
    public static bool NeedsRestore(IEnumerable<string> projectRelativePaths, string solutionDir)
    {
        foreach (var relative in projectRelativePaths)
        {
            var projectPath = Path.GetFullPath(Path.Combine(solutionDir, relative));
            var projectDir = Path.GetDirectoryName(projectPath) ?? solutionDir;
            var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
            if (!File.Exists(assetsPath))
            {
                return true;
            }
            if (File.GetLastWriteTimeUtc(projectPath) > File.GetLastWriteTimeUtc(assetsPath))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Restore kill timeout in seconds: <c>CODESEARCH_RESTORE_TIMEOUT_SECS</c>
    /// when set to a positive integer, otherwise 300. Unparseable values fall
    /// back to the default rather than failing the load.
    /// </summary>
    public static int RestoreTimeoutSeconds()
    {
        var raw = Environment.GetEnvironmentVariable(RestoreTimeoutEnv);
        if (int.TryParse(raw, out var seconds) && seconds > 0)
        {
            return seconds;
        }
        return DefaultRestoreTimeoutSeconds;
    }
}
