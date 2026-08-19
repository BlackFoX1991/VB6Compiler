namespace VB6.Semantics;

/// <summary>
/// Compatibility entry point retained for callers from the initial Variant multiplication slice.
/// New Variant operator support is implemented by <see cref="VariantOperatorLowerer"/>.
/// </summary>
public static class VariantMultiplyLowerer
{
    public static SemanticModel Lower(SemanticModel model) => VariantOperatorLowerer.Lower(model);
}
