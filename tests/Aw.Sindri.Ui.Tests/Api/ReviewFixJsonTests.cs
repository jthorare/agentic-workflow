using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Tests.Api;


[Lane(TestLane.Unit)]
public sealed class ReviewFixJsonTests
{
    private static readonly string[] _names = ["\"kind\":\"line\"", "\"text\":\"t\"", "\"head\":\"abc\"", "\"path\":\"X.cs\"", "\"line\":11", "\"side\":\"old\"", "\"item\":\"01J9\""];

    [Fact]
    public void The_fix_request_carries_kind_text_head_sourceId_path_line_side_and_item_in_camelCase()
    {
        var json = AwJson.Serialize(new ReviewFixSend("line", "t", "abc", SourceId: null, "X.cs", 11, "old", "01J9"), SindriApiClient.Json);

        var text = System.Text.Encoding.UTF8.GetString(json);
        Assert.All(_names, name => Assert.Contains(name, text, StringComparison.Ordinal));//"\"kind\":\"line\"", "\"text\":\"t\"", "\"head\":\"abc\"", "\"path\":\"X.cs\"", "\"line\":11", "\"side\":\"old\"", "\"item\":\"01J9\"" }, name => Assert.Contains(name, text, StringComparison.Ordinal));
        Assert.DoesNotContain("\"SourceId\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fix_answer_reads_itemId_posted_postCode_postError_went_and_session()
    {
        var read = AwJson.Deserialize<ReviewFixSent>(System.Text.Encoding.UTF8.GetBytes("""{"itemId":"01J9","posted":false,"postCode":"SND-REVIEW-006","postError":"x","went":"session","session":"claude, ~/w"}"""), SindriApiClient.Json);

        Assert.Equal(new ReviewFixSent("01J9", Posted: false, "SND-REVIEW-006", "x", "session", "claude, ~/w"), read.Value);
    }
}
