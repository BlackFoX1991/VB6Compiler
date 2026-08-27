using VB6.Compiler;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ManagedDiagnosticTests
{
    [TestMethod]
    public void EmitManaged_ReportsPortablePdbFailureAsE0002()
    {
        var previous = DirectManagedCompilation.PortablePdbEmitterOverride;
        DirectManagedCompilation.PortablePdbEmitterOverride = static (_, _, _, _) =>
            throw new InvalidOperationException("injected PDB failure");

        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerManagedDiagnostics", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var result = VBCompilation.Create("Sub Main()\nEnd Sub", "Injected.bas")
                    .EmitManagedApplication(Path.Combine(directory, "Injected.dll"));

                Assert.IsFalse(result.Success);
                Assert.IsNotNull(result.BackendResult);
                Assert.IsTrue(result.BackendResult!.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6E0002"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        finally
        {
            DirectManagedCompilation.PortablePdbEmitterOverride = previous;
        }
    }
}
