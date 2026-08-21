namespace VB6.Emit.Managed;

internal static class AssemblyHashAlgorithm
{
    public const System.Configuration.Assemblies.AssemblyHashAlgorithm Sha256 =
        System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA256;
}

/// <summary>
/// Strongly typed zero value for ECMA-335 assembly flags. System.Reflection.AssemblyFlags has no
/// named None member in .NET 10, but metadata APIs still require the enum value rather than an int.
/// </summary>
internal static class AssemblyFlags
{
    public const System.Reflection.AssemblyFlags None = (System.Reflection.AssemblyFlags)0;
}
