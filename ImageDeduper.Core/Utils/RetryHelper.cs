namespace ImageDeduper.Core.Utils;

public static class RetryHelper
{
    public static async Task<T?> SafeExecuteAsync<T>(
        Func<Task<T>> action,
        Func<int, Task>? onRetry = null,
        int retries = 0,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt <= retries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action().ConfigureAwait(false);
            }
            catch when (attempt < retries)
            {
                if (onRetry is not null)
                {
                    await onRetry(attempt + 1).ConfigureAwait(false);
                }

                await Task.Delay(GetDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return default;
    }

    public static async Task<bool> SafeExecuteAsync(
        Func<Task> action,
        Func<int, Task>? onRetry = null,
        int retries = 0,
        CancellationToken cancellationToken = default)
    {
        var result = await SafeExecuteAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        }, onRetry, retries, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static TimeSpan GetDelay(int attempt)
    {
        var jitter = Random.Shared.NextDouble();
        return TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt) + jitter));
    }
}
