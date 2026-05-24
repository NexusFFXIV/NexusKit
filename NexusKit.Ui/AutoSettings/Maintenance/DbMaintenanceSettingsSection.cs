using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using NexusKit.Core.Localization;
using NexusKit.Persistence.Maintenance;
using NexusKit.Persistence.Settings;
using NexusKit.Ui.Utilities;
using NexusKit.Ui.Widgets;

namespace NexusKit.Ui.AutoSettings.Maintenance;

/// <summary>
/// Reusable settings-tab surface for the DB-maintenance subsystem: on-disk
/// + payload stats, per-contributor last-run timestamps, and a "Run now"
/// button that bypasses the per-contributor interval gates for an
/// on-demand sweep.
///
/// <para>Lives in <c>NexusKit.Ui</c> so any plugin that registered
/// <see cref="IDbStatsService"/> + <see cref="IDbMaintenanceService"/>
/// (i.e. used <c>AddNexusKitPersistence()</c>) can wire in the section
/// with a single call — see
/// <see cref="UiServiceCollectionExtensions.AddDbMaintenanceSettingsSection"/>.
/// Plugin-specific framing / styling stays out of here; the only thing
/// callers pick is the nav-order placement via the
/// <c>AddDbMaintenanceSettingsSection(int order)</c> overload.</para>
///
/// <para>Stats and last-run snapshots are gathered async on first render +
/// after each "Run now" click, then cached in this section until the user
/// hits "Refresh" or re-enters the tab. Avoids re-running the per-table
/// COUNT/SUM walk on every frame.</para>
/// </summary>
public sealed class DbMaintenanceSettingsSection : IAutoSettingsSection
{
    public int Order { get; }

    public string NavTitleKey => "nexuskit.ui.dbmaint.section.nav";

    private readonly IDbStatsService mStats;
    private readonly IDbMaintenanceService mMaintenance;
    private readonly ILocalizer mLoc;

    // Snapshot state. Both fields are mutated only on the UI thread (via
    // continuations posted from background loads); cheap reference reads
    // suffice everywhere else.
    private DbStatsSnapshot? mStatsSnapshot;
    private IReadOnlyList<MaintenanceScheduleEntry>? mSchedule;
    private Task? mLoadTask;
    private DateTime mLastRefreshAt;

    // "Run now" state: set to true when the user clicks the button, cleared
    // when the task finishes. Drives the spinner-vs-button swap.
    private Task? mRunNowTask;
    private string? mLastRunNowError;

    public DbMaintenanceSettingsSection(
        IDbStatsService stats,
        IDbMaintenanceService maintenance,
        ILocalizer localizer,
        int order = 200)
    {
        mStats = stats;
        mMaintenance = maintenance;
        mLoc = localizer;
        Order = order;
    }

    public void Render(ISettingsStore store)
    {
        // First render kicks the initial load; later renders only re-load on
        // explicit user action so the tab doesn't repeatedly walk every
        // table on every frame.
        if (mStatsSnapshot is null && mLoadTask is null) StartLoad();

        ImGui.TextWrapped(mLoc.Get("nexuskit.ui.dbmaint.section.description"));
        ImGui.Spacing();

        DrawHeader();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawLastRuns();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawTables();
    }

    private void DrawHeader()
    {
        if (mStatsSnapshot is { } snap)
        {
            var totalPayload = 0L;
            foreach (var t in snap.Tables) totalPayload += t.TextOrBlobPayloadBytes;
            // "Other" = on-disk minus what we explicitly account for. This
            // captures indexes (not in text/blob payload), integer columns,
            // page padding, and the WAL/SHM sidecars — i.e. the legitimate
            // SQLite overhead that VACUUM cannot reduce further. Clamp at 0
            // so a stale snapshot read mid-VACUUM doesn't display a
            // negative number.
            var otherBytes = Math.Max(0L, snap.OnDiskBytes - totalPayload - snap.FreelistBytes);

            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture,
                mLoc.Get("nexuskit.ui.dbmaint.header.on_disk"),
                SizeFormat.Bytes(snap.OnDiskBytes)));
            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture,
                mLoc.Get("nexuskit.ui.dbmaint.header.payload"),
                SizeFormat.Bytes(totalPayload)));
            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture,
                mLoc.Get("nexuskit.ui.dbmaint.header.other"),
                SizeFormat.Bytes(otherBytes)));
            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture,
                mLoc.Get("nexuskit.ui.dbmaint.header.freelist"),
                SizeFormat.Bytes(snap.FreelistBytes)));
            ImGui.TextColored(ImGuiColors.DalamudGrey3,
                string.Format(CultureInfo.CurrentCulture,
                    mLoc.Get("nexuskit.ui.dbmaint.header.path"),
                    snap.DbFilePath));
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, mLoc.Get("nexuskit.ui.dbmaint.loading"));
        }

        ImGui.Spacing();

        // Button order: [Run now] [Refresh] [status]. Both buttons stay on
        // the left; the status text trails on the right of the same line
        // so the user reads action → action → outcome in one pass.
        ImGui.BeginDisabled(mRunNowTask is not null);
        if (ImGui.Button(mLoc.Get("nexuskit.ui.dbmaint.button.run_now")))
            StartRunNow();
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(mLoadTask is not null);
        if (ImGui.Button(mLoc.Get("nexuskit.ui.dbmaint.button.refresh")))
            StartLoad();
        ImGui.EndDisabled();

        // Single status slot at the line's tail. Errors win over the
        // active-running indicators so the user sees the failure
        // immediately after a Run-now click without having to dismiss
        // anything. The running labels disappear automatically when the
        // matching task completes.
        var (statusText, statusColor) = ResolveStatus();
        if (statusText is not null)
        {
            ImGui.SameLine();
            ImGui.TextColored(statusColor, statusText);
        }
    }

    private (string? Text, System.Numerics.Vector4 Color) ResolveStatus()
    {
        if (mLastRunNowError is { } err)
            return (err, ImGuiColors.DalamudRed);
        if (mRunNowTask is not null)
            return (mLoc.Get("nexuskit.ui.dbmaint.run_now.running"), ImGuiColors.DalamudGrey);
        if (mLoadTask is not null)
            return (mLoc.Get("nexuskit.ui.dbmaint.loading"), ImGuiColors.DalamudGrey);
        return (null, default);
    }

    private void DrawLastRuns()
    {
        ImGui.TextDisabled(mLoc.Get("nexuskit.ui.dbmaint.heading.schedule"));
        if (mSchedule is null || mSchedule.Count == 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey3,
                mLoc.Get("nexuskit.ui.dbmaint.last_runs.empty"));
            return;
        }

        var rows = mSchedule.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
        NexusTable.Draw(
            "##nxk_dbmaint_lastruns",
            new[]
            {
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.contributor")),
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.next_run"), Width: 200f),
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.last_run"), Width: 200f),
            },
            rows,
            row =>
            {
                ImGui.TableNextColumn();
                NexusTable.CellText(row.Name);
                ImGui.TableNextColumn();
                // Next-run cell: "in 3 Std." for future, "fällig" for past-
                // due / never-run. Schedule loop ticks every 15 min so a
                // "due" entry will fire on the next wakeup.
                var nextText = row.NextRunUtc <= DateTime.UtcNow
                    ? mLoc.Get("nexuskit.ui.dbmaint.next_run.due")
                    : string.Format(mLoc.Get("nexuskit.time.in_pattern"),
                        mLoc.FormatTimeSpan(row.NextRunUtc - DateTime.UtcNow));
                NexusTable.CellText(nextText, ImGuiColors.DalamudGrey);
                ImGui.TableNextColumn();
                var lastText = row.LastRunUtc == DateTime.MinValue
                    ? mLoc.Get("nexuskit.ui.dbmaint.last_run.never")
                    : mLoc.FormatRelativeTimeAgo(row.LastRunUtc);
                NexusTable.CellText(lastText, ImGuiColors.DalamudGrey);
            });
    }

    private void DrawTables()
    {
        ImGui.TextDisabled(mLoc.Get("nexuskit.ui.dbmaint.heading.tables"));
        if (mStatsSnapshot is not { } snap)
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey3, mLoc.Get("nexuskit.ui.dbmaint.loading"));
            return;
        }

        // Append a synthetic totals row at the end of the table — first
        // column intentionally empty so it reads visually as "sum below"
        // rather than another data row. Empty Name is the sentinel the
        // drawRow callback uses to switch the row's text colour from the
        // grey table-row tint to default white, giving the totals a
        // subtle visual lift without bespoke styling.
        var totalRows = 0L;
        var totalIndexes = 0;
        var totalPayload = 0L;
        foreach (var t in snap.Tables)
        {
            totalRows += t.RowCount;
            totalIndexes += t.IndexCount;
            totalPayload += t.TextOrBlobPayloadBytes;
        }
        var withTotals = new List<DbTableStats>(snap.Tables.Count + 1);
        withTotals.AddRange(snap.Tables);
        withTotals.Add(new DbTableStats(string.Empty, totalRows, totalPayload, totalIndexes));

        NexusTable.Draw(
            "##nxk_dbmaint_tables",
            new[]
            {
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.table")),
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.rows"), Width: 90f),
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.indexes"), Width: 60f),
                new NexusTableColumn(mLoc.Get("nexuskit.ui.dbmaint.col.payload"), Width: 110f),
            },
            withTotals,
            row =>
            {
                var isTotal = string.IsNullOrEmpty(row.Name);
                // Totals row uses the default (white) text color so it
                // stands out from the grey-tinted data rows; data rows keep
                // the existing tint for the numeric columns.
                var numericTint = isTotal ? (System.Numerics.Vector4?)null : ImGuiColors.DalamudGrey;

                ImGui.TableNextColumn();
                NexusTable.CellText(row.Name);
                ImGui.TableNextColumn();
                NexusTable.CellText(row.RowCount.ToString("N0", CultureInfo.CurrentCulture),
                    numericTint);
                ImGui.TableNextColumn();
                NexusTable.CellText(row.IndexCount.ToString("N0", CultureInfo.CurrentCulture),
                    numericTint);
                ImGui.TableNextColumn();
                NexusTable.CellText(SizeFormat.Bytes(row.TextOrBlobPayloadBytes), numericTint);
            });

        ImGui.TextColored(ImGuiColors.DalamudGrey3,
            mLoc.Get("nexuskit.ui.dbmaint.tables.footnote"));
    }

    private void StartLoad()
    {
        if (mLoadTask is not null) return;
        mStatsSnapshot = null;
        mSchedule = null;
        mLoadTask = Task.Run(async () =>
        {
            try
            {
                var stats = await mStats.GatherAsync().ConfigureAwait(false);
                var schedule = await mMaintenance.GetScheduleSnapshotAsync().ConfigureAwait(false);
                mStatsSnapshot = stats;
                mSchedule = schedule;
                mLastRefreshAt = DateTime.UtcNow;
            }
            finally
            {
                mLoadTask = null;
            }
        });
    }

    private void StartRunNow()
    {
        if (mRunNowTask is not null) return;
        mLastRunNowError = null;
        mRunNowTask = Task.Run(async () =>
        {
            try
            {
                await mMaintenance.RunNowAsync().ConfigureAwait(false);
                // Refresh both the stats and last-run map so the user sees
                // the effect of the run without an extra click.
                var stats = await mStats.GatherAsync().ConfigureAwait(false);
                var schedule = await mMaintenance.GetScheduleSnapshotAsync().ConfigureAwait(false);
                mStatsSnapshot = stats;
                mSchedule = schedule;
                mLastRefreshAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                mLastRunNowError = ex.Message;
            }
            finally
            {
                mRunNowTask = null;
            }
        });
    }

}
