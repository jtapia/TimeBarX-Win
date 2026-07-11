using System;
using TimeBarX.Core;

namespace TimeBarX.App;

internal sealed class Win32IdleProbe : IIdleProbe
{
    public TimeSpan GetIdleTime() => NativeMethods.GetIdleTime();
}
