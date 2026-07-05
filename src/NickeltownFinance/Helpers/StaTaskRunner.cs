using System.Runtime.Versioning;

namespace NickeltownFinance.Helpers;

/// <summary>
/// Runs work on a dedicated STA thread for WinRT/WPF APIs that require it.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class StaTaskRunner
{
    public static Task<T> RunAsync<T>(Func<T> work, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                tcs.TrySetResult(work());
            }
            catch (OperationCanceledException)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "NickeltownFinance-STA"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    public static Task RunAsync(Action work, CancellationToken cancellationToken = default) =>
        RunAsync(() =>
        {
            work();
            return true;
        }, cancellationToken);

    public static Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        RunAsync(() => work(cancellationToken).GetAwaiter().GetResult(), cancellationToken);
}
