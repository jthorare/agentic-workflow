using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IDeskNotices"/> honours (engine spec §6.3, §5.7; engine P6 unit (c)), run against the
/// fake and the notifications file: a notice is sent once per effect identity, so an activity retry or a re-emission
/// sends nothing twice.
/// </summary>
public abstract class DeskNoticesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Notices that have sent nothing yet.</summary>
    /// <returns>The notices.</returns>
    protected abstract IDeskNotices Create();

    /// <summary>A notice for <paramref name="identity"/>.</summary>
    /// <param name="identity">The effect identity.</param>
    /// <param name="kind">Its class.</param>
    /// <returns>The notice.</returns>
    public static DeskNotice Notice(string identity, NoticeClass kind = NoticeClass.Interrupt) => new(
        identity, InstanceAddress.From("T-4/dev-notify/1"), ItemKey.From("T-4"), kind, "Waiting for the desk notification to be delivered.",
        new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The first send of an identity sends it; the second sends nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_notice_is_sent_once_per_identity()
    {
        var notices = Create();

        var first = await notices.Send(Notice("WFI-a:2:0"), Token);
        var again = await notices.Send(Notice("WFI-a:2:0") with { Text = "a retry" }, Token);

        Expect.True(first.IsSuccess && first.Value);
        Expect.True(again.IsSuccess && !again.Value);
    }

    /// <summary>Each identity is its own notice, interrupt or info.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Distinct_identities_are_each_sent()
    {
        var notices = Create();

        var park = await notices.Send(Notice("WFI-a:1:n3"), Token);
        var task = await notices.Send(Notice("WFI-a:2:n4", NoticeClass.Info), Token);

        Expect.True(park.IsSuccess && park.Value);
        Expect.True(task.IsSuccess && task.Value);
    }
}
