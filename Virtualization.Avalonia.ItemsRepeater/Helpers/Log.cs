#if DEBUG && REPEATER_TRACE

namespace Virtualization.Avalonia;

internal class Log
{
#if NET9_0_OR_GREATER
    public static void Debug(string format, params ReadOnlySpan<object?> args)
    {
        Debugger.Log(0, "ItemRepeater", string.Format(format, args));
    }
#else
    public static void Debug(string format, params object[] args)
    {
        Debugger.Log(0, "ItemRepeater", string.Format(format, args));
    }
#endif
}
#endif
