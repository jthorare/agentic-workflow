using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.notify.desk' (engine spec §6.3, §12; README X5, X50; engine P6 unit (c)): a recipe's notice is 'info' and
/// carries the entered state's rendered 'next' and the instance address, and replies 'notify.delivered' with
/// 'delivered'; an engine notice carries its own class and text and replies nothing. A repeated identity is found
/// already sent.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NotifyDeskEffectTests
{
    private static readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemoryDeskNotices _notices = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_recipe_notice_is_info_with_the_rendered_next_and_replies_delivered()
    {
        var effect = new NotifyDeskEffect(_notices, _clock);

        var done = await effect.Dispatch(Recipe("WFI-a:2:0", "Waiting for the desk notification to be delivered."), Token);

        Assert.Equal(new EffectDone(OutcomeName.From("delivered"), Existed: false), done.Value);
        var notice = Assert.Single(_notices.Sent);
        Assert.Equal(new DeskNotice("WFI-a:2:0", InstanceAddress.From("T-4/dev-notify/1"), ItemKey.From("T-4"), NoticeClass.Info,
            "Waiting for the desk notification to be delivered.", KnownInstants.Epoch), notice);
    }

    [Fact]
    public async Task An_engine_notice_carries_its_class_and_text_and_replies_nothing()
    {
        var effect = new NotifyDeskEffect(_notices, _clock);
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["class"] = Literal("interrupt"),
            ["text"] = Literal("T-4/dev-notify/1 parked (timeout): ask ran past 1h"),
        };

        var done = await effect.Dispatch(Recipe("WFI-a:1:n3", text: null) with { FromEngine = true, Arguments = arguments }, Token);

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        var notice = Assert.Single(_notices.Sent);
        Assert.Equal((NoticeClass.Interrupt, "T-4/dev-notify/1 parked (timeout): ask ran past 1h"), (notice.Class, notice.Text));
    }

    [Fact]
    public async Task A_repeated_identity_was_already_sent()
    {
        var effect = new NotifyDeskEffect(_notices, _clock);

        _ = await effect.Dispatch(Recipe("WFI-a:2:0", "first"), Token);
        var again = await effect.Dispatch(Recipe("WFI-a:2:0", "first") with { Command = "c-2" }, Token);

        Assert.Equal(new EffectDone(OutcomeName.From("delivered"), Existed: true), again.Value);
        _ = Assert.Single(_notices.Sent);
    }

    private static EffectContext Recipe(string identity, string? text) => new(
        BlockId.From("effect.notify.desk"), identity, "c-1", FromEngine: false, "T-4/dev-notify/1", "T-4", Attempt: 1,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), text);

    private static JsonElement Literal(string text)
    {
        using var document = JsonDocument.Parse("\"" + JsonEncodedText.Encode(text).Value + "\"");
        return document.RootElement.Clone();
    }
}
