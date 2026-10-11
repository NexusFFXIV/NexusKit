using Xunit;

namespace NexusKit.ChatNotifications.Tests;

public sealed class NotificationPolicyTests
{
    [Fact]
    public void DisabledQuietHours_StaysImmediate()
    {
        var decision = NotificationPolicy.Decide(new NotificationPolicySettings
        {
            QuietHoursEnabled = false,
            DigestMode = NotificationDigestMode.DuringQuietHours,
        }, new TimeOnly(23, 0));

        Assert.False(decision.IsQuietHours);
        Assert.Equal(NotificationDelivery.Immediate, decision.Delivery);
    }

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(12, 0, false)]
    public void OvernightQuietHours_UsesDigestOnlyInsideWindow(int hour, int minute, bool expectedQuiet)
    {
        var decision = NotificationPolicy.Decide(new NotificationPolicySettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(22, 0),
            QuietHoursEnd = new TimeOnly(7, 0),
            DigestMode = NotificationDigestMode.DuringQuietHours,
        }, new TimeOnly(hour, minute));

        Assert.Equal(expectedQuiet, decision.IsQuietHours);
        Assert.Equal(expectedQuiet ? NotificationDelivery.Digest : NotificationDelivery.Immediate,
            decision.Delivery);
    }

    [Fact]
    public void SessionEnd_DigestsOutsideQuietHoursToo()
    {
        var decision = NotificationPolicy.Decide(new NotificationPolicySettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(22, 0),
            QuietHoursEnd = new TimeOnly(7, 0),
            DigestMode = NotificationDigestMode.SessionEnd,
        }, new TimeOnly(12, 0));

        Assert.False(decision.IsQuietHours);
        Assert.Equal(NotificationDelivery.Digest, decision.Delivery);
    }

    [Fact]
    public void EqualBounds_DisableQuietInterval()
    {
        var decision = NotificationPolicy.Decide(new NotificationPolicySettings
        {
            QuietHoursEnabled = true,
            QuietHoursStart = new TimeOnly(8, 0),
            QuietHoursEnd = new TimeOnly(8, 0),
            DigestMode = NotificationDigestMode.DuringQuietHours,
        }, new TimeOnly(8, 0));

        Assert.False(decision.IsQuietHours);
        Assert.Equal(NotificationDelivery.Immediate, decision.Delivery);
    }

    [Fact]
    public void DigestBuffer_FlushesInFifoOrder()
    {
        var buffer = new NotificationDigestBuffer();
        buffer.Add(new("first", 0, DateTime.UtcNow, "one"));
        buffer.Add(new("second", 0, DateTime.UtcNow, "two"));

        var flushed = buffer.Flush();

        Assert.Equal(new[] { "one", "two" }, flushed.Select(x => x.Message));
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void DigestBuffer_DropsOldestAtBound()
    {
        var buffer = new NotificationDigestBuffer();
        for (var i = 0; i < 101; i++)
            buffer.Add(new("kind", 0, DateTime.UtcNow, i.ToString()));

        var flushed = buffer.Flush();

        Assert.Equal(100, flushed.Count);
        Assert.Equal("1", flushed[0].Message);
        Assert.Equal("100", flushed[^1].Message);
    }
}
