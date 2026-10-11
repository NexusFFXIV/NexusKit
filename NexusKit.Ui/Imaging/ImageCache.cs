using System.Net.Http;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;
using NexusKit.Core;

namespace NexusKit.Ui.Imaging;

internal sealed class ImageCache : IImageCache, IDisposable
{
    private readonly ITextureProvider mTextures;
    private readonly ILogger<ImageCache> mLog;
    private readonly HttpClient mHttp;
    private readonly CancellationTokenSource mStopCts;
    private readonly IPluginLifetime mLifetime;
    private readonly Dictionary<string, CacheEntry> mEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Task> mLoads = new();
    private readonly object mLock = new();
    private bool mDisposed;

    public ImageCache(ITextureProvider textures, ILogger<ImageCache> log, IPluginLifetime lifetime)
    {
        mTextures = textures;
        mLog = log;
        mLifetime = lifetime;
        mStopCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Stopping);
        mLifetime.StateChanged += OnLifecycleChanged;
        mHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // Lodestone's CDN serves a generic body to non-browser UAs — hint a normal client.
        mHttp.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    public IDalamudTextureWrap? GetTexture(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        lock (mLock)
        {
            if (mDisposed || mLifetime.IsStopping) return null;
            if (mEntries.TryGetValue(url, out var existing)) return existing.Texture;

            // Mark in-flight so concurrent GetTexture calls don't re-start the fetch.
            mEntries[url] = new CacheEntry();
            var load = LoadAsync(url, mStopCts.Token);
            mLoads.Add(load);
        }
        return null;
    }

    private async Task LoadAsync(string url, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var bytes = await mHttp.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            var wrap = await mTextures.CreateFromImageAsync(bytes).ConfigureAwait(false);

            lock (mLock)
            {
                if (mDisposed)
                {
                    wrap.Dispose();
                    return;
                }
                if (mEntries.TryGetValue(url, out var entry))
                    entry.Texture = wrap;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            // Permanent failure for this URL — leave the entry without a texture so we
            // don't re-spam the network. A future enhancement could add retry with backoff.
            mLog.LogWarning(ex, "ImageCache: load failed for {Url}", url);
        }
    }

    private void OnLifecycleChanged(PluginLifecycleState state)
    {
        if (state == PluginLifecycleState.Stopping)
            StopLoads();
    }

    private void StopLoads()
    {
        try { mStopCts.Cancel(); } catch (ObjectDisposedException) { }
        Task[] loads;
        lock (mLock) loads = mLoads.ToArray();
        try { Task.WhenAll(loads).GetAwaiter().GetResult(); }
        catch (Exception ex) { mLog.LogDebug(ex, "ImageCache: shutdown wait failed."); }
    }

    public void Dispose()
    {
        mLifetime.StateChanged -= OnLifecycleChanged;
        lock (mLock)
        {
            if (mDisposed) return;
            mDisposed = true;
            mStopCts.Cancel();
        }
        StopLoads();

        lock (mLock)
        {
            foreach (var entry in mEntries.Values)
                entry.Texture?.Dispose();
            mEntries.Clear();
        }
        mStopCts.Dispose();
        mHttp.Dispose();
    }

    private sealed class CacheEntry
    {
        public IDalamudTextureWrap? Texture;
    }
}
