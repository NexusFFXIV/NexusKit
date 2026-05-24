using System.Net.Http;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Logging;

namespace NexusKit.Ui.Imaging;

internal sealed class ImageCache : IImageCache, IDisposable
{
    private readonly ITextureProvider mTextures;
    private readonly ILogger<ImageCache> mLog;
    private readonly HttpClient mHttp;
    private readonly Dictionary<string, CacheEntry> mEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly object mLock = new();
    private bool mDisposed;

    public ImageCache(ITextureProvider textures, ILogger<ImageCache> log)
    {
        mTextures = textures;
        mLog = log;
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
            if (mEntries.TryGetValue(url, out var existing)) return existing.Texture;

            // Mark in-flight so concurrent GetTexture calls don't re-start the fetch.
            mEntries[url] = new CacheEntry();
        }

        _ = LoadAsync(url);
        return null;
    }

    private async Task LoadAsync(string url)
    {
        try
        {
            var bytes = await mHttp.GetByteArrayAsync(url).ConfigureAwait(false);
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
        catch (Exception ex)
        {
            // Permanent failure for this URL — leave the entry without a texture so we
            // don't re-spam the network. A future enhancement could add retry with backoff.
            mLog.LogWarning(ex, "ImageCache: load failed for {Url}", url);
        }
    }

    public void Dispose()
    {
        lock (mLock)
        {
            if (mDisposed) return;
            mDisposed = true;
            foreach (var entry in mEntries.Values)
                entry.Texture?.Dispose();
            mEntries.Clear();
        }
        mHttp.Dispose();
    }

    private sealed class CacheEntry
    {
        public IDalamudTextureWrap? Texture;
    }
}
