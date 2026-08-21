using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using VB6.Compiler;

namespace VB6.Emit.Managed.Tests;

[TestClass]
public sealed class ModuleTypeInitializerTests
{
    [TestMethod]
    public void Emit_ModuleStorageInitializerHasClrTypeConstructorMetadata()
    {
        var lowering = VBCompilation.Create("""
            Private Caption As String

            Sub Main()
                Debug.Print Caption
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsTrue(lowering.Success, string.Join(Environment.NewLine, lowering.Diagnostics));
        var result = new ManagedEmitter().Emit(
            lowering.Program!,
            new ManagedEmitOptions("ModuleInitializer", EmitPortablePdb: false));
        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));

        using var stream = new MemoryStream(result.PeImage!, writable: false);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();

        var moduleTypeHandle = reader.TypeDefinitions.Single(handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Name) == "__vb6_module_Module1");
        var moduleType = reader.GetTypeDefinition(moduleTypeHandle);
        Assert.AreEqual(TypeAttributes.NotPublic, moduleType.Attributes & TypeAttributes.BeforeFieldInit);

        var cctorHandle = moduleType.GetMethods().Single(handle =>
            reader.GetString(reader.GetMethodDefinition(handle).Name) == ".cctor");
        var cctor = reader.GetMethodDefinition(cctorHandle);
        Assert.AreEqual(MethodAttributes.Private, cctor.Attributes & MethodAttributes.MemberAccessMask);
        Assert.AreNotEqual(MethodAttributes.Private, cctor.Attributes & MethodAttributes.Static);
        Assert.AreNotEqual(MethodAttributes.Private, cctor.Attributes & MethodAttributes.SpecialName);
        Assert.AreNotEqual(MethodAttributes.Private, cctor.Attributes & MethodAttributes.RTSpecialName);
    }
}
