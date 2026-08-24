using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ComDispatchRuntimeTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void DefaultMember_UsesDispatchValueWhenComObjectHasNoItemMember()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM default-member test requires Windows.");
            return;
        }

        var comType = Type.GetTypeFromProgID("htmlfile", throwOnError: false);
        if (comType is null)
        {
            Assert.Inconclusive("The htmlfile COM class is not available.");
            return;
        }

        var document = VBInteraction.CreateObject("htmlfile", string.Empty);
        var value = VBDynamicDispatch.GetDefaultMember(document, Array.Empty<object?>());

        Assert.AreEqual("[object]", value);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AutomationDispatch_InvokesMethodsPropertiesAndDefaultItem()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM automation test requires Windows.");
            return;
        }

        var comType = Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false);
        if (comType is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var dictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        var addArguments = Arguments("answer", 41);
        VBDynamicDispatch.InvokeMember(dictionary, "aDd", addArguments);

        Assert.AreEqual(1, Convert.ToInt32(VBDynamicDispatch.GetMember(dictionary, "COUNT")));

        VBDynamicDispatch.SetDefaultMember(dictionary, new object?[] { "answer" }, 42);
        Assert.AreEqual(42, VBDynamicDispatch.GetDefaultMember(dictionary, new object?[] { "answer" }));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AutomationDispatch_UnwrapsManagedComObjectProviders()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM provider test requires Windows.");
            return;
        }

        var comType = Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false);
        if (comType is null)
        {
            Assert.Inconclusive("The Scripting.Dictionary COM class is not available.");
            return;
        }

        var dictionary = VBInteraction.CreateObject("Scripting.Dictionary", string.Empty);
        try
        {
            var provider = new ComObjectProvider(dictionary);
            VBDynamicDispatch.InvokeMember(provider, "Add", Arguments("answer", 41));

            Assert.AreEqual(1, Convert.ToInt32(VBDynamicDispatch.GetMember(provider, "Count")));
            VBDynamicDispatch.SetDefaultMember(provider, new object?[] { "answer" }, 42);
            Assert.AreEqual(42, VBDynamicDispatch.GetDefaultMember(provider, new object?[] { "answer" }));
        }
        finally
        {
            if (Marshal.IsComObject(dictionary))
            {
                Marshal.FinalReleaseComObject(dictionary);
            }
        }
    }

    private static VBArray<object> Arguments(params object?[] values)
    {
        var arguments = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            arguments[index] = values[index]!;
        }

        return arguments;
    }

    private sealed class ComObjectProvider : IVBComObjectProvider
    {
        public ComObjectProvider(object comObject)
        {
            ComObject = comObject;
        }

        public object? ComObject { get; }
    }
}
