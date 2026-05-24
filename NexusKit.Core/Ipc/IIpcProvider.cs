namespace NexusKit.Core.Ipc;

/// <summary>
/// Marker for module/plugin classes that publish their own IPCs. Implementations
/// should register their IPCs in their constructor via <see cref="IIpcRegistry"/>,
/// keep the returned <see cref="IDisposable"/>s, and dispose them in their own
/// <see cref="IDisposable.Dispose"/>. The host eagerly resolves every registered
/// <c>IIpcProvider</c> at startup so all IPCs are live before user code runs.
/// </summary>
public interface IIpcProvider
{
}
