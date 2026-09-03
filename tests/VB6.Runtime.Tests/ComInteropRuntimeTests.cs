using System.Runtime.Versioning;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

/// <summary>
/// Measures the IDispatch path against a COM server this project did not build. A controlled
/// component built from IDL would be the better ground truth, but it needs MIDL and a Windows
/// SDK; <c>Scripting.Dictionary</c> ships with Windows, has a real type library, dual interfaces,
/// a default property and documented error numbers, and is therefore an honest stand-in for
/// everything except the exotic type-library shapes.
/// </summary>
[TestClass]
public sealed class ComInteropRuntimeTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ComDispatch_CallsAForeignServerWithSeveralArguments()
    {
        var dictionary = CreateDictionary();
        if (dictionary is null)
        {
            return;
        }

        // Two arguments is the case that used to fail: a VARIANT is 24 bytes on x64, not 16, so
        // the second argument overlapped the first and never reached the server intact.
        Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Add", new object?[] { "a", 1 }, false, out _));
        Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Add", new object?[] { "b", 2 }, false, out _));

        Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Count", Array.Empty<object?>(), false, out var count));
        Assert.AreEqual(2, VBConversions.CLng(count));

        Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Item", new object?[] { "a" }, false, out var item));
        Assert.AreEqual(1, VBConversions.CLng(item));

        Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Exists", new object?[] { "b" }, false, out var exists));
        Assert.IsTrue(VBConversions.CBool(exists));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ComDispatch_KeepsTheServersOwnErrorNumber()
    {
        var dictionary = CreateDictionary();
        if (dictionary is null)
        {
            return;
        }

        try
        {
            Assert.IsTrue(VBComDispatch.TryInvoke(dictionary, "Add", new object?[] { "a", 1 }, false, out _));

            // Scripting.Dictionary answers a duplicate key with 0x800A01C9 straight from Invoke
            // and never fills EXCEPINFO. VB6 shows 457 with its own text for it.
            var duplicate = Assert.ThrowsExactly<VB6RaisedError>(() =>
                VBComDispatch.TryInvoke(dictionary, "Add", new object?[] { "a", 2 }, false, out _));
            Assert.AreEqual(457, duplicate.Number);
            Assert.AreEqual(
                "This key is already associated with an element of this collection",
                duplicate.Description);

            // A number outside VB6's own table keeps the server's value rather than becoming 5.
            var missing = Assert.ThrowsExactly<VB6RaisedError>(() =>
                VBComDispatch.TryInvoke(dictionary, "Remove", new object?[] { "absent" }, false, out _));
            Assert.AreEqual(32811, missing.Number);
        }
        finally
        {
            VBErrors.Clear();
        }
    }

    [SupportedOSPlatform("windows")]
    private static object? CreateDictionary()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM interop measurement requires Windows.");
            return null;
        }

        // scrrun.dll is a Windows component. A missing ProgID here is a broken machine, not a
        // reason to pass quietly.
        var type = Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false);
        Assert.IsNotNull(type, "Scripting.Dictionary is not registered on this machine.");
        return Activator.CreateInstance(type!);
    }
}
