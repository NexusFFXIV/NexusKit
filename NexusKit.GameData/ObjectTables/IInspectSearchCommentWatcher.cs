namespace NexusKit.GameData.ObjectTables;

/// <summary>
/// Surfaces another character's in-game <b>search comment</b> (the free text set
/// under Search Info — not the Lodestone biography) as it arrives from the server.
/// <para>The search comment is not part of the object table: the game's
/// <c>Character</c> struct carries no such field. It arrives either through the
/// Examine response or through the already-populated Adventurer Plate agent.
/// Both paths report data the game was already given; this watcher sends no
/// extra request.</para>
/// <para>Coverage is therefore limited to characters the user examined or whose
/// Adventurer Plate the user opened. Anyone else simply has no value on file,
/// which is a normal state and not an error.</para>
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
