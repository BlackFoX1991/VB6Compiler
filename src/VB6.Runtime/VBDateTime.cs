namespace VB6.Runtime;

/// <summary>Portable Date/Time intrinsics represented by OLE Automation doubles.</summary>
public static class VBDateTime
{
    public static double Now() => DateTime.Now.ToOADate();
}
