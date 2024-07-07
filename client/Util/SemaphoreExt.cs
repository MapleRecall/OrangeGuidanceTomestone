namespace OrangeGuidanceTomestone.Util;

internal static class SemaphoreExt {
    internal static OnDispose With(this SemaphoreSlim semaphore) {
        semaphore.Wait();
        return new OnDispose(() => semaphore.Release());
    }

    internal static async Task<OnDispose> WithAsync(this SemaphoreSlim semaphore, CancellationToken token = default) {
        await semaphore.WaitAsync(token);
        return new OnDispose(() => semaphore.Release());
    }
}
