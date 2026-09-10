using System.Reflection;
using System.Runtime.Versioning;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class EmittedShapeProbeTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void Probe()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Shape", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Recs.vbp"), """
            Type=OleDll
            Name=Recs
            Module=Typen; Typen.bas
            Class=Geber; Geber.cls
            """);
        File.WriteAllText(Path.Combine(directory, "Typen.bas"), """
            Attribute VB_Name = "Typen"
            Option Explicit

            Public Type TPunkt
                X As Long
                Y As Long
            End Type
            """);
        File.WriteAllText(Path.Combine(directory, "Geber.cls"), """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Geber"
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = False
            Attribute VB_Exposed = True
            Option Explicit

            Public Function Ort() As TPunkt
                Ort.X = 3
            End Function

            Public Function Zahl() As Long
                Zahl = 7
            End Function
            """);

        var assemblyPath = Path.Combine(directory, "Recs.dll");
        var emit = VBProjectCompilation.Create(Path.Combine(directory, "Recs.vbp")).EmitManagedApplication(
            assemblyPath,
            new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
        Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

        var context = new System.Runtime.Loader.AssemblyLoadContext("shape", isCollectible: true);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);
        foreach (var type in assembly.GetTypes().Where(type => type.Name.Contains("Geber", StringComparison.Ordinal)))
        {
            Console.WriteLine($"TYPE {type.FullName} base={type.BaseType?.FullName}");
            foreach (var contract in type.GetInterfaces())
            {
                Console.WriteLine("  IFACE " + contract.FullName);
            }

            foreach (var attribute in CustomAttributeData.GetCustomAttributes(type))
            {
                Console.WriteLine("  ATTR " + attribute);
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Console.WriteLine($"  METHOD {method.Name} dispid={CustomAttributeData.GetCustomAttributes(method).Count}");
            }
        }

        context.Unload();
    }
}
