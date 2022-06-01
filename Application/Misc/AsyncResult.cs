using System;
using System.Threading;

namespace IW4MAdmin.Application.Misc;

public class AsyncResult : IAsyncResult
{
    public object AsyncState { get; set; }
    public WaitHandle AsyncWaitHandle { get; set; }
    public bool CompletedSynchronously { get; set; }
    public bool IsCompleted { get; set; }
}
