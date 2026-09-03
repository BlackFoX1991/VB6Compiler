using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 Instancing decides who may see and create a class. It is written into the .cls as
/// VB_Exposed and VB_Creatable, and it has to reach the emitted COM metadata -- otherwise a
/// Private helper class ends up in the type library and the registration.
/// </summary>
[TestClass]
public sealed class ComInstancingExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_KeepsAPrivateClassOutOfTheComSurface()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Instancing", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Instancing.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Instancing
                Class=Offen; Offen.cls
                Class=Intern; Intern.cls
                Class=NurLesbar; NurLesbar.cls
                """);
            WriteClass(directory, "Offen", exposed: true, creatable: true);
            WriteClass(directory, "Intern", exposed: false, creatable: false);
            WriteClass(directory, "NurLesbar", exposed: true, creatable: false);

            var assemblyPath = Path.Combine(directory, "Instancing.dll");
            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new VB6.Emit.Managed.ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));

            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();

            Assert.IsTrue(IsComVisible(metadata, "__vb6_class_Offen"));
            Assert.IsTrue(HasProgId(metadata, "__vb6_class_Offen"));

            // Private: unsichtbar für COM, also auch nicht im Manifest und nicht registriert.
            Assert.IsFalse(IsComVisible(metadata, "__vb6_class_Intern"));

            // PublicNotCreatable: sichtbar, aber ohne ProgID -- nicht über den Namen erzeugbar.
            Assert.IsTrue(IsComVisible(metadata, "__vb6_class_NurLesbar"));
            Assert.IsFalse(HasProgId(metadata, "__vb6_class_NurLesbar"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_DerivesOnlyComVisibleClassesFromTheEventSource()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6EventSource", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Events.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Events
                Class=Offen; Offen.cls
                Class=Intern; Intern.cls
                """);
            WriteClass(directory, "Offen", exposed: true, creatable: true);
            WriteClass(directory, "Intern", exposed: false, creatable: false);

            var assemblyPath = Path.Combine(directory, "Events.dll");
            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new VB6.Emit.Managed.ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));

            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();

            // Die Basis trägt den Connection Point. Sie gehört nur an eine Klasse, die COM
            // überhaupt sieht -- sonst schleppt jede private Hilfsklasse COM-Ballast mit.
            Assert.AreEqual("VBComEventSource", GetBaseTypeName(metadata, "__vb6_class_Offen"));
            Assert.AreEqual("Object", GetBaseTypeName(metadata, "__vb6_class_Intern"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string GetBaseTypeName(MetadataReader metadata, string typeName)
    {
        var baseType = FindType(metadata, typeName).BaseType;
        return baseType.Kind == HandleKind.TypeReference
            ? metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)baseType).Name)
            : baseType.Kind.ToString();
    }

    private static void WriteClass(string directory, string name, bool exposed, bool creatable) =>
        File.WriteAllText(
            Path.Combine(directory, name + ".cls"),
            string.Join(
                Environment.NewLine,
                "VERSION 1.0 CLASS",
                "BEGIN",
                "  MultiUse = -1  'True",
                "END",
                "Attribute VB_Name = \"" + name + "\"",
                "Attribute VB_Creatable = " + (creatable ? "True" : "False"),
                "Attribute VB_PredeclaredId = False",
                "Attribute VB_Exposed = " + (exposed ? "True" : "False"),
                "Option Explicit",
                "",
                "Public Function Wert() As Long",
                "    Wert = 1",
                "End Function",
                ""));

    private static TypeDefinition FindType(MetadataReader metadata, string name) =>
        metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Single(type => metadata.GetString(type.Name) == name);

    private static bool IsComVisible(MetadataReader metadata, string typeName)
    {
        var type = FindType(metadata, typeName);
        foreach (var handle in type.GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (GetAttributeTypeName(metadata, attribute) != "ComVisibleAttribute")
            {
                continue;
            }

            var value = metadata.GetBlobReader(attribute.Value);
            _ = value.ReadUInt16();
            return value.ReadBoolean();
        }

        return false;
    }

    private static bool HasProgId(MetadataReader metadata, string typeName) =>
        FindType(metadata, typeName).GetCustomAttributes()
            .Select(metadata.GetCustomAttribute)
            .Any(attribute => GetAttributeTypeName(metadata, attribute) == "ProgIdAttribute");

    private static string GetAttributeTypeName(MetadataReader metadata, CustomAttribute attribute)
    {
        if (attribute.Constructor.Kind != HandleKind.MemberReference)
        {
            return string.Empty;
        }

        var member = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
        return member.Parent.Kind == HandleKind.TypeReference
            ? metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name)
            : string.Empty;
    }
}
