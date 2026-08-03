namespace NexusKit.GameData.ObjectTables;

/// <summary>
/// Surfaces another character's in-game <b>search comment</b> (the free text set
/// under Search Info — not the Lodestone biography) as it arrives from the server.
/// <para>The search comment is not part of the object table: the game's
/// <c>Character</c> struct carries no such field, so unlike class, level, race or
/// the FC tag it cannot be read from a scan tick. It is request/response data,
/// delivered only after an Examine. This watcher therefore does not poll — it
/// listens in on the inbound handler and reports whatever the game was already
/// told, so no extra request ever leaves the client.</para>
/// <para>Consequence for consumers: coverage is limited to characters the user
/// actually examined. Anyone else simply has no value on file, which is a normal
/// state and not an error.</para>
/// </summary>
public interface IInspectSearchCommentWatcher
{
    /// <summary>Raised when the game hands the client a search comment for a
    /// character, with the resolved ContentId and the text (<c>null</c> when the
    /// character has no search comment set).
    /// <para>Fires on the framework thread — handlers must not block. Anything
    /// touching the database or the network belongs on a <c>Task.Run</c>.</para></summary>
    event Action<ulong, string?>? SearchCommentReceived;
}
