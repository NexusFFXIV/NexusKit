using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Microsoft.Extensions.Logging;

namespace NexusKit.GameData.ObjectTables;

/// <summary>
/// Hooks <c>AgentInspect.ReceiveSearchComment</c> and observes the
/// <c>AgentCharaCard</c> storage — the two game paths that expose a character's
/// search comment — and republishes it keyed on ContentId.
/// <para>Read-only by construction: the detour calls the original first and then
/// only reads its arguments. Nothing here sends a request, so the watcher can
/// never produce traffic the user didn't trigger by examining someone.</para>
/// <para>The entity id arrives as an argument, so there is no need to read
/// <c>AgentInspect.CurrentEntityId</c> and race a second Examine that may already
/// have overwritten it.</para>
/// </summary>
internal sealed unsafe class InspectSearchCommentWatcher : IInspectSearchCommentWatcher, IDisposable
{
    private readonly IObjectTable mObjectTable;
    private readonly IFramework mFramework;
    private readonly ILogger<InspectSearchCommentWatcher> mLog;
    private readonly Hook<AgentInspect.Delegates.ReceiveSearchComment>? mHook;
    private bool mDisposed;
    private ulong mLastPlateContentId;
    private string? mLastPlateComment;

    public event Action<ulong, string?>? SearchCommentReceived;

    public InspectSearchCommentWatcher(
        IGameInteropProvider interop,
        IObjectTable objectTable,
        IFramework framework,
        ILogger<InspectSearchCommentWatcher> log)
    {
        mObjectTable = objectTable;
        mFramework = framework;
        mLog = log;

        // A failed hook must not take the plugin down with it — the rest of the
        // observation pipeline is independent of this one signature, and a
        // missing search comment is a state the UI already renders.
        try
        {
            mHook = interop.HookFromAddress<AgentInspect.Delegates.ReceiveSearchComment>(
                AgentInspect.Addresses.ReceiveSearchComment.Value, Detour);
            mHook.Enable();
        }
        catch (Exception ex)
        {
            mLog.LogWarning(ex, "Search-comment hook could not be installed; search comments stay unavailable.");
        }

        // Adventurer Plates expose the same search comment through AgentCharaCard
        // instead of AgentInspect. Polling the already-populated agent avoids a
        // second request and catches plates opened from any in-game window.
        mFramework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        if (mDisposed) return;
        mDisposed = true;
        mFramework.Update -= OnFrameworkUpdate;
        mHook?.Dispose();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        try
        {
            var module = AgentModule.Instance();
            var agent = module == null ? null : module->GetAgentByInternalId(AgentId.CharaCard);
            var data = agent == null ? null : ((AgentCharaCard*)agent)->Data;
            if (agent == null || !agent->IsAgentActive() || data == null)
            {
                mLastPlateContentId = 0;
                mLastPlateComment = null;
                return;
            }

            var contentId = data->ContentId;
            if (contentId == 0) return;
            var comment = ReadUtf8(data->SearchComment.StringPtr);
            if (contentId == mLastPlateContentId && string.Equals(comment, mLastPlateComment, StringComparison.Ordinal)) return;

            mLastPlateContentId = contentId;
            mLastPlateComment = comment;
            SearchCommentReceived?.Invoke(contentId, comment);
        }
        catch (Exception ex)
        {
            mLog.LogDebug(ex, "Search-comment adventurer plate read failed");
        }
    }

    private void Detour(AgentInspect* thisPtr, uint entityId, byte* searchComment)
    {
        // Original first, unconditionally: the game's own state must land even
        // if everything below throws.
        mHook!.Original(thisPtr, entityId, searchComment);

        try
        {
            var contentId = ResolveContentId(entityId);
            if (contentId == 0) return;

            var text = ReadUtf8(searchComment);
            SearchCommentReceived?.Invoke(contentId, text);
        }
        catch (Exception ex)
        {
            // An exception escaping a detour crashes the game, not just us.
            mLog.LogWarning(ex, "Search-comment detour failed for entity {EntityId}", entityId);
        }
    }

    /// <summary>Entity id → stable ContentId via the object table. Examine only
    /// works on a character the client can see, so the target is in the table by
    /// the time the answer arrives; a miss means they left in the meantime and
    /// the comment has nothing to attach to.</summary>
    private ulong ResolveContentId(uint entityId)
    {
        foreach (var obj in mObjectTable)
        {
            if (obj.EntityId != entityId) continue;
            if (obj is not IPlayerCharacter pc) continue;
            return pc.GetContentId();
        }
        return 0;
    }

    /// <summary>Null-terminated UTF-8 from the game's buffer. Empty is normalised
    /// to <c>null</c> — "no search comment set" and "empty search comment" are the
    /// same thing to every consumer.</summary>
    private static string? ReadUtf8(byte* ptr)
    {
        if (ptr is null) return null;
        var text = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)ptr);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
