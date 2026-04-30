using Adaptive.ObservabilityClient.Internal;
using Xunit;

namespace Adaptive.ObservabilityClient.Tests;

public class BackgroundJobDeduperTests
{
    [Fact]
    public void Suppresses_repeats_within_window()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var dedup = new BackgroundJobDeduper(TimeSpan.FromMinutes(15), clock);

        Assert.True(dedup.ShouldEmit("nightly-import", "TimeoutException"));
        Assert.False(dedup.ShouldEmit("nightly-import", "TimeoutException"));
        Assert.False(dedup.ShouldEmit("nightly-import", "TimeoutException"));
    }

    [Fact]
    public void Allows_after_window()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var dedup = new BackgroundJobDeduper(TimeSpan.FromMinutes(15), clock);

        Assert.True(dedup.ShouldEmit("nightly-import", "TimeoutException"));
        clock.Advance(TimeSpan.FromMinutes(16));
        Assert.True(dedup.ShouldEmit("nightly-import", "TimeoutException"));
    }

    [Fact]
    public void Different_jobs_track_separately()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var dedup = new BackgroundJobDeduper(TimeSpan.FromMinutes(15), clock);

        Assert.True(dedup.ShouldEmit("job-a", "TimeoutException"));
        Assert.True(dedup.ShouldEmit("job-b", "TimeoutException"));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset start) => _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
