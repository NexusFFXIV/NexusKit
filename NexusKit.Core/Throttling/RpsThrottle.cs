namespace NexusKit.Core.Throttling;

/// <summary>
/// Sliding-window rate limiter. <see cref="AcquireAsync"/> returns immediately
/// when fewer than <c>maxPerSecond</c> calls have happened in the last second;
/// otherwise it waits until the oldest call is older than a second.
/// </summary>
public sealed class RpsThrottle
{
    private readonly int mMaxPerSecond;
    private readonly Queue<DateTime> mTimestamps = new();
    private readonly SemaphoreSlim mGate = new(1, 1);

    public RpsThrottle(int maxPerSecond)
    {
        if (maxPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(maxPerSecond));
        mMaxPerSecond = maxPerSecond;
    }

    public async Task AcquireAsync(CancellationToken ct = default)
    {
        await mGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddSeconds(-1);

            while (mTimestamps.Count > 0 && mTimestamps.Peek() < cutoff)
                mTimestamps.Dequeue();

            if (mTimestamps.Count >= mMaxPerSecond)
            {
                var oldest = mTimestamps.Peek();
                var waitMs = (int)Math.Ceiling((oldest.AddSeconds(1) - now).TotalMilliseconds);
                if (waitMs > 0)
                    await Task.Delay(waitMs, ct).ConfigureAwait(false);
                mTimestamps.Dequeue();
            }

            mTimestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            mGate.Release();
        }
    }
}
