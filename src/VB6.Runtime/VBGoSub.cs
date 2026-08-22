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
            throw new InvalidOperationException("VB6 Return executed without an active GoSub.");
        }

        return returnIndex;
    }

    public static void InvalidReturn() =>
        throw new InvalidOperationException("VB6 GoSub return index is invalid.");
}
