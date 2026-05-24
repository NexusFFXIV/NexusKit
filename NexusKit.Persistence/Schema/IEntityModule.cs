using Microsoft.EntityFrameworkCore;

namespace NexusKit.Persistence.Schema;

public interface IEntityModule
{
    /// <summary>
    /// Short identifier prepended to every table this module configures. The framework
    /// rewrites raw table names declared in <see cref="ConfigureEntities"/> to
    /// <c>nexus_{TablePrefix}_{rawName}</c>, so two modules can independently declare a
    /// table named e.g. <c>player</c> without colliding.
    /// <para>Format: lowercase letters / digits / underscores. Example: <c>"ffxivcollect"</c>.</para>
    /// <para>An empty string is reserved for shared framework tables that intentionally
    /// have no per-module namespace (e.g. the cross-module <c>settings</c> key/value
    /// store). Module authors should always return a non-empty value.</para>
    /// </summary>
    string TablePrefix { get; }

    void ConfigureEntities(ModelBuilder modelBuilder);
}
