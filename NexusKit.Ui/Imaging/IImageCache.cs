using Dalamud.Interface.Textures.TextureWraps;

namespace NexusKit.Ui.Imaging;

/// <summary>
/// Caching loader for HTTP(S)-hosted images (Lodestone avatars/portraits, FFXIVCollect
/// catalog icons, etc.). Lookups are synchronous and non-blocking: the cache returns
/// the texture wrap when ready and <c>null</c> while a fetch is in flight or after a
/// permanent error.
/// <para>Implementations must own the lifecycle of the wraps they return — callers
/// must not dispose the returned <see cref="IDalamudTextureWrap"/>. The cache is a
/// plugin-scoped singleton; everything is released when the plugin unloads.</para>
/// </summary>
public interface IImageCache
{
    /// <summary>Get the texture for <paramref name="url"/>. Returns null until the
    /// underlying HTTP+decode completes (typically a frame or three) or if loading
    /// failed permanently. The first call for a URL triggers the fetch.</summary>
    IDalamudTextureWrap? GetTexture(string url);
}
