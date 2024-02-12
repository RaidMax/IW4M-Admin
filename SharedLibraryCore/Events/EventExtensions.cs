using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Events;

public static class EventExtensions
{
    public static Task InvokeAsync<TEventType>(this Func<TEventType, CancellationToken, Task> function,
        TEventType eventArgType, CancellationToken token)
    {
        if (function is null)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(function.GetInvocationList().Cast<Func<TEventType, CancellationToken, Task>>()
            .Select(x => RunHandler(x, eventArgType, token)));
    }

    private static async Task RunHandler<TEventType>(Func<TEventType, CancellationToken, Task> handler,
        TEventType eventArgType, CancellationToken token)
    {
        if (token == CancellationToken.None)
        {
            try
            {
                // special case to allow tasks like request after delay to run longer
                await handler(eventArgType, token);
            }
            catch (Exception ex)
            {
                // todo: static logger
                Console.WriteLine("InvokeAsync: " + ex);
            }
        }

        using var timeoutToken = new CancellationTokenSource(Utilities.DefaultCommandTimeout);
        using var tokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken.Token);

        try
        {
            await handler(eventArgType, tokenSource.Token);
        }
        catch (Exception ex)
        {
            // todo: static logger
            Console.WriteLine("InvokeAsync: " + ex);
        }
    }
}
