using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace StarResonanceDpsAnalysis.WPF.Helpers;

internal static class ExceptionHelper
{
    [Conditional("DEBUG")]
    public static void ThrowIfDebug(Exception ex)
    {
        ExceptionDispatchInfo.Capture(ex).Throw();
    }
}