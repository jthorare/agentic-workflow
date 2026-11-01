using System.Globalization;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>
/// A Slack workspace of one channel, three days of ten threads each (a root and a reply) plus two loose messages a day, behind <see cref="FakeSlackReader"/>, read by the real Slack connector through Sindri's <see cref="ConnectorFeed"/>.
/// It answers like Slack's rate limit (an empty page with a reset time) once <see cref="ResumeUpstream.InterruptAfter"/> has run out, for history pages, thread reads and the probe alike.
/// </summary>
internal sealed class SlackResumeUpstream : ResumeUpstream, ISlackReader
{
    private const string Channel = "C0001GEN";
    private static readonly DateTimeOffset _firstDay = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _resetsAt = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeSlackReader _inner = new(Channel);
    private readonly List<string> _keys = [];

    public SlackResumeUpstream()
    {
        for (var day = 0; day < 3; day++)
        {
            var start = _firstDay.AddDays(day);
            _keys.Add("slack:" + Channel + "/day/" + start.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            for (var t = 0; t < 10; t++)
            {
                var root = SlackTs.Format(start.AddMinutes(t * 10));
                _ = _inner
                    .Add(Channel, A.SlackMessage(root).WithText("root " + root))
                    .Add(Channel, A.SlackMessage(SlackTs.Format(start.AddMinutes(t * 10).AddSeconds(30))).InThread(root).WithText("reply"));
                _keys.Add("slack:" + Channel + "/" + root);
            }

            _ = _inner
                .Add(Channel, A.SlackMessage(SlackTs.Format(start.AddMinutes(101))).WithText("loose one"))
                .Add(Channel, A.SlackMessage(SlackTs.Format(start.AddMinutes(102))).WithText("loose two"));
        }

        using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero))).AddSlackConnectorFactory().BuildServiceProvider();
        var settings = SlackSettings.Create([new SlackChannelRef(Channel, "general")], token: null, "example").Value;
        var connector = services.GetRequiredService<ISlackConnectorFactory>().Create(settings, this, TimeSpan.FromDays(30));
        Feed = new ConnectorFeed(SourceId.From("slack"), connector, new SlackPlacement([Channel]).Place);
    }

    public override RecordFeed Feed { get; }

    public override IReadOnlyCollection<string> Keys => _keys;

    public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken) =>
        Admit($"history {channel} {oldest} {latest}") ? _inner.History(channel, oldest, latest, cancellationToken) : Limited();

    public ValueTask<Result<SlackMessagePage>> HistoryPage(string channel, string? oldest, string? latest, int limit, CancellationToken cancellationToken) =>
        Admit($"history {channel} {oldest} {latest}") ? _inner.HistoryPage(channel, oldest, latest, limit, cancellationToken) : Limited();

    public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken) =>
        Admit($"replies {channel} {threadTs}") ? _inner.Replies(channel, threadTs, cancellationToken) : Limited();

    public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken) =>
        Admit("probe " + channel) ? _inner.Probe(channel, cancellationToken) : ValueTask.FromResult(Results.Success<DateTimeOffset?>(_resetsAt));

    private static ValueTask<Result<SlackMessagePage>> Limited() => ValueTask.FromResult<Result<SlackMessagePage>>(new SlackMessagePage([], _resetsAt));
}
