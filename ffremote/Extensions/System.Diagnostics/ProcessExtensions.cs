namespace System.Diagnostics;

internal static class ProcessExtensions
{
    public static async Task<int> StartAsync(this Process process, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<int>();
        process.EnableRaisingEvents = true;
        process.Exited += OnExited;
        process.Start();
        if (process.HasExited)
            return process.ExitCode;

        using var _ = cancellationToken.Register(() =>
        {
            process.Exited -= OnExited;
            if (process.Id != default)
                process.Kill(true);
            tcs.TrySetCanceled(cancellationToken);
        });
        return await tcs.Task.ConfigureAwait(false);

        void OnExited(object? sender, EventArgs e) => tcs.TrySetResult(process.ExitCode);
    }
}
