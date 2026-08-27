using System.Reflection;

namespace VB6.Runtime;

/// <summary>
/// Selects the observable compatibility contract used by generated VB6 programs.
/// </summary>
public enum VBCompatibilityProfile
{
    /// <summary>Deterministic cross-machine behavior used by existing callers.</summary>
    Deterministic,

    /// <summary>Documented classic VB6 SP6 behavior for x86 Windows targets.</summary>
    VB6Sp6
}

/// <summary>Records the compatibility profile selected when an assembly was emitted.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class VBCompatibilityProfileAttribute : Attribute
{
    public VBCompatibilityProfileAttribute(string profile)
    {
        Profile = profile;
    }

    public string Profile { get; }

    /// <summary>
    /// Reads the profile recorded on a generated assembly. Assemblies without the additive
    /// metadata, or with an unknown value, retain the deterministic host behavior.
    /// </summary>
    public static VBCompatibilityProfile FromAssembly(Assembly? assembly)
    {
        try
        {
            var value = assembly?.GetCustomAttribute<VBCompatibilityProfileAttribute>()?.Profile;
            return Enum.TryParse(value, ignoreCase: true, out VBCompatibilityProfile profile) &&
                   Enum.IsDefined(profile)
                ? profile
                : VBCompatibilityProfile.Deterministic;
        }
        catch (Exception) when (assembly is not null)
        {
            return VBCompatibilityProfile.Deterministic;
        }
    }
}
