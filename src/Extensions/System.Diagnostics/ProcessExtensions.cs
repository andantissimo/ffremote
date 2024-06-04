namespace System.Diagnostics;

internal static class ProcessExtensions
{
    public static async Task WaitForExitOrKillAsync(this Process process, bool entireProcessTree, CancellationToken cancellationToken = default)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree);
            throw;
        }
    }
}
