using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBDynamicDispatchTests
{
    [TestMethod]
    public void Dispatch_ConvertsPropertyAndIndexerArguments()
    {
        var target = new HostObject();

        VBDynamicDispatch.SetMember(target, "Value", (short)7);
        Assert.AreEqual(7, target.Value);

        VBDynamicDispatch.SetIndexedMember(target, "Item", Arguments(2), (short)11);
        Assert.AreEqual(15, target[2]);
        Assert.AreEqual(15, VBDynamicDispatch.GetIndexedMember(target, "Item", Arguments(2)));
    }

    [TestMethod]
    public void Dispatch_FillsOptionalArgumentsAndPreservesByRefWriteback()
    {
        var target = new HostObject();

        Assert.AreEqual(7, VBDynamicDispatch.InvokeMember(target, "Add", Arguments(4)));

        var values = Arguments(3);
        VBDynamicDispatch.InvokeMember(target, "Increment", values);
        Assert.AreEqual(8, values[0]);
    }

    [TestMethod]
    public void Dispatch_PacksParamArrayArguments()
    {
        Assert.AreEqual(
            6,
            VBDynamicDispatch.InvokeMember(
                new HostObject(),
                "Sum",
                Arguments(1, 2, 3)));
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

    private sealed class HostObject
    {
        public int Value { get; set; }

        public int this[int index]
        {
            get => index + Value;
            set => Value = value + index;
        }

        public int Add(int value, int amount = 3) => value + amount;

        public void Increment(ref int value) => value += 5;

        public int Sum(params int[] values) => values.Sum();
    }
}
