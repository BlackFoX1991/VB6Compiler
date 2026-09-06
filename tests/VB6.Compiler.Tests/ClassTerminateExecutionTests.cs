using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Class_Terminate</c> follows generated storage ownership synchronously. The emitted
/// finalizer and shutdown register are a fallback for an uninstrumented escape or a reference
/// cycle at normal process end, so a class declaring the event still needs its finalizer.
/// </summary>
[TestClass]
public sealed class ClassTerminateExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_GivesAClassWithTerminateAFinalizer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Terminate", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Leben.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Leben"
                Class=MitTerminate; MitTerminate.cls
                Class=OhneTerminate; OhneTerminate.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MitTerminate.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "term"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "OhneTerminate.cls"), """
                Option Explicit

                Public Sub Tue()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim r As MitTerminate
                    Set r = New MitTerminate
                    Set r = Nothing
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "Leben.dll");
            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(assemblyPath);
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));

            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            var metadata = peReader.GetMetadataReader();

            // Nur eine Klasse mit Class_Terminate bekommt einen Finalizer -- die andere trägt
            // keinen Aufräumaufwand, den sie nicht braucht.
            Assert.IsTrue(HasFinalizer(metadata, "__vb6_class_MitTerminate"));
            Assert.IsFalse(HasFinalizer(metadata, "__vb6_class_OhneTerminate"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool HasFinalizer(MetadataReader metadata, string typeName)
    {
        var type = metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Single(candidate => metadata.GetString(candidate.Name) == typeName);

        return type.GetMethods()
            .Select(metadata.GetMethodDefinition)
            .Any(method => metadata.GetString(method.Name) == "Finalize");
    }
}
