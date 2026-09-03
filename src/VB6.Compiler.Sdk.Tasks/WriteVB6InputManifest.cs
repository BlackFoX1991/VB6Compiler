using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using VB6.ProjectSystem;

namespace VB6.Compiler.Sdk.Tasks;

/// <summary>
/// Writes the exact input manifest of a .vbp or .vbg inside the MSBuild process.
///
/// The same work is available through <c>vb6c --write-input-manifest</c>, and that path stays: a
/// build that cannot load this task -- an older MSBuild, a different runtime -- keeps working by
/// shelling out. What the task adds is the process start it saves on every build, which for an
/// incremental build is most of the cost.
/// </summary>
public sealed class WriteVB6InputManifest : Microsoft.Build.Utilities.Task
{
    [Required]
    public string ProjectPath { get; set; } = string.Empty;

    [Required]
    public string ManifestPath { get; set; } = string.Empty;

    [Output]
    public int InputCount { get; private set; }

    public override bool Execute()
    {
        VBInputManifestResult result;
        try
        {
            result = VBInputManifest.Write(ProjectPath, ManifestPath);
        }
        catch (IOException exception)
        {
            Log.LogError("Failed to write the VB6 input manifest: {0}", exception.Message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            Log.LogError("Failed to write the VB6 input manifest: {0}", exception.Message);
            return false;
        }

        foreach (var diagnostic in result.Diagnostics)
        {
            // A diagnostic from a successful load is a warning: the project still builds, and
            // failing the build over it would be a change in behaviour against the CLI path.
            if (result.Success)
            {
                Log.LogWarning("{0}", diagnostic);
            }
            else
            {
                Log.LogError("{0}", diagnostic);
            }
        }

        if (!result.Success)
        {
            return false;
        }

        InputCount = result.InputCount;
        Log.LogMessage(
            MessageImportance.Low,
            "Generated exact VB6 input manifest: {0} ({1} inputs)",
            result.OutputPath,
            result.InputCount);
        return true;
    }
}
