using VB6.Compiler;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ManagedComRegistrationTests
{
    [TestMethod]
    public void GetRegsvr32Path_SelectsTheRequestedProcessArchitecture()
    {
        var x86 = ManagedComRegistration.GetRegsvr32Path(ManagedPlatform.X86);
        var x64 = ManagedComRegistration.GetRegsvr32Path(ManagedPlatform.X64);
        var anyCpu = ManagedComRegistration.GetRegsvr32Path(ManagedPlatform.AnyCpu);

        StringAssert.EndsWith(x86, "regsvr32.exe");
        StringAssert.EndsWith(x64, "regsvr32.exe");
        StringAssert.EndsWith(anyCpu, "regsvr32.exe");
        if (OperatingSystem.IsWindows() && Environment.Is64BitOperatingSystem)
        {
            StringAssert.Contains(x86, "SysWOW64");
            StringAssert.Contains(x64, "System32");
            StringAssert.Contains(anyCpu, "System32");
        }
    }

    [TestMethod]
    public void Execute_RejectsMissingAndNonComHostInputsWithoutStartingAProcess()
    {
        var missing = ManagedComRegistration.Execute(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".comhost.dll"));
        Assert.IsFalse(missing.Success);
        StringAssert.Contains(missing.StandardError, "was not found");

        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerComRegistrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var dllPath = Path.Combine(directory, "not-a-comhost.dll");
            File.WriteAllBytes(dllPath, Array.Empty<byte>());
            var invalid = ManagedComRegistration.Execute(dllPath);

            Assert.IsFalse(invalid.Success);
            StringAssert.Contains(invalid.StandardError, ".comhost.dll");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
