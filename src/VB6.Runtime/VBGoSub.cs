namespace VB6.Runtime;

/// <summary>Thread-local return-index storage for VB6 GoSub/Return control flow.</summary>
public static class VBGoSub
{
    [ThreadStatic]
    private static Stack<Stack<int>>? _frames;

    public static void Enter() =>
        (_frames ??= new Stack<Stack<int>>()).Push(new Stack<int>());

    public static void Leave()
    {
        if (_frames is null || !_frames.TryPop(out _))
        {
            throw new InvalidOperationException("VB6 GoSub procedure frame is unbalanced.");
        }
    }

    public static void Push(int returnIndex)
    {
        if (_frames is null || _frames.Count == 0)
        {
            throw new InvalidOperationException("VB6 GoSub was used outside a procedure frame.");
        }

        _frames.Peek().Push(returnIndex);
    }

    public static int Pop()
    {
        if (_frames is null || _frames.Count == 0 || !_frames.Peek().TryPop(out var returnIndex))
        {
            throw new VB6RuntimeErrorException(3, "VB6 Return executed without an active GoSub.");
        }

        return returnIndex;
    }

    /// <summary>Builds the exception for a return index no jump table can serve.</summary>
    /// <remarks>
    /// It returns the exception instead of throwing it so the emitter can end the block with a
    /// real <c>throw</c>. A call to a method that happens to throw is not a terminator as far as
    /// the CLR is concerned: the block would fall off its end, and with the index still on the
    /// evaluation stack the whole method is rejected before it ever runs.
    /// </remarks>
    public static Exception InvalidReturn() =>
        new InvalidOperationException("VB6 GoSub return index is invalid.");
}
