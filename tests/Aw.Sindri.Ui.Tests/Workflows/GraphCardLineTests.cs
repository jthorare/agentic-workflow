using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The words on a workflow card (#3684): line 1 is the title or the block's name, never the id; line 2 is one settings line per block kind.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphCardLineTests
{
    private static JsonElement Config(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Theory]
    [InlineData("manual", "{}", "From Run now or the API")]
    [InlineData("code", "{\"code\":\"const a = 1;\\n\\nreturn a;\\n\"}", "JavaScript · 2 lines")]
    [InlineData("code", "{\"code\":\"return 1;\"}", "JavaScript · 1 line")]
    [InlineData("condition", "{\"when\":\"failing checks > 0\"}", "If failing checks > 0")]
    [InlineData("http", "{\"url\":\"https://status.example.com/api\"}", "GET status.example.com/api")]
    [InlineData("http", "{\"method\":\"post\",\"url\":\"http://a.test/hook\"}", "POST a.test/hook")]
    [InlineData("notify", "{\"title\":\"#release, with a ping\"}", "Notify: #release, with a ping")]
    [InlineData("wait", "{\"duration\":\"10m\"}", "Wait 10 minutes")]
    [InlineData("wait", "{\"duration\":\"1h\"}", "Wait 1 hour")]
    [InlineData("wait", "{\"until\":\"07:00\",\"timezone\":\"America/Los_Angeles\"}", "Wait until 07:00 America/Los_Angeles")]
    [InlineData("collect", "{\"items\":\"{{ steps }}\"}", "Collect {{ steps }}")]
    [InlineData("prompt", "{\"prompt\":\"Summarize\"}", "Prompt, answer in text")]
    [InlineData("prompt", "{\"prompt\":\"Summarize\",\"output\":{\"type\":\"object\"}}", "Prompt, answer as fields")]
    [InlineData("create_item", "{}", "Create item in the queue")]
    [InlineData("create_item", "{\"repo\":\"me/app\"}", "Create item in me/app")]
    [InlineData("run_agent", "{\"agent\":\"claude-code\",\"repo\":\"me/app\"}", "Run claude-code in me/app")]
    [InlineData("run_workflow", "{\"workflow\":\"nightly\"}", "Run workflow nightly")]
    [InlineData("for_each", "{\"items\":\"{{ prs }}\"}", "For each of {{ prs }}")]
    [InlineData("call_tool", "{\"toolkit\":\"notes\",\"tool\":\"add_note\"}", "notes · add_note")]
    [InlineData("ask_owner", "{\"question\":\"Ship it?\"}", "Ask: Ship it?")]
    public void Each_block_kind_gets_one_settings_line_from_its_config(string type, string config, string expected) =>
        Assert.Equal(expected, GraphCardLine.Settings(type, Config(config), summary: null, kindTitle: null), StringComparer.Ordinal);

    [Fact]
    public void The_schedule_line_is_the_one_the_server_serves() =>
        Assert.Equal("Every weekday 7:00 America/Los_Angeles", GraphCardLine.Settings("schedule", Config("{\"cron\":\"0 7 * * 1-5\"}"), "Every weekday 7:00 America/Los_Angeles", "Schedule"), StringComparer.Ordinal);

    [Theory]
    [InlineData("code", "{}", "Code")]
    [InlineData("http", "{\"method\":\"GET\"}", "HTTP request")]
    [InlineData("schedule", "{}", "Schedule")]
    [InlineData("memory_write", "{}", "Remember")]
    public void A_kind_whose_setting_is_missing_shows_the_blocks_name(string type, string config, string expected) =>
        Assert.Equal(expected, GraphCardLine.Settings(type, Config(config), summary: null, kindTitle: null), StringComparer.Ordinal);

    [Fact]
    public void A_missing_config_reads_like_an_empty_one() =>
        Assert.Equal("If", GraphCardLine.Settings("condition", default, summary: null, kindTitle: null), StringComparer.Ordinal);

    [Theory]
    [InlineData("Fetch upstream status", "code", null, "Fetch upstream status")]
    [InlineData("", "code", null, "Code")]
    [InlineData("  ", "condition", null, "If")]
    [InlineData("", "code", "Run code", "Run code")]
    [InlineData("", "made_up_kind", null, "made up kind")]
    public void Line_1_is_the_title_or_the_blocks_name_never_the_id(string title, string type, string? kindTitle, string expected) =>
        Assert.Equal(expected, GraphCardLine.Title(title, type, kindTitle), StringComparer.Ordinal);

    [Theory]
    [InlineData("api.example.com/x?key=abc", "GET api.example.com/x")]                    // no scheme
    [InlineData("https://api.example.com?key=abc", "GET api.example.com")]                // only a query after the host
    [InlineData("https://api.example.com/docs#part", "GET api.example.com/docs")]         // a fragment
    [InlineData("deploy@hooks.example.com/run", "GET hooks.example.com/run")]             // a user with no scheme
    public void An_http_cards_line_shows_the_host_and_path_only(string url, string expected) =>
        Assert.Equal(expected, GraphCardLine.Settings("http", Config("{\"url\":\"" + url + "\"}"), summary: null, kindTitle: null), StringComparer.Ordinal);

    // A credential and a query never reach the card (#4032). The url is built in parts so the commit's secret scan does not read it as a leaked credential.
    [Fact]
    public void An_http_cards_line_drops_the_urls_user_and_password() =>
        Assert.Equal("GET api.example.com/x", GraphCardLine.Settings("http", Config("{\"url\":\"https://" + string.Join(":", "user", "token") + "@api.example.com/x?key=abc\"}"), summary: null, kindTitle: null), StringComparer.Ordinal);

    [Fact]
    public void An_untitled_step_with_no_type_still_has_a_line_1() =>
        Assert.False(string.IsNullOrWhiteSpace(GraphCardLine.Title(string.Empty, string.Empty, kindTitle: null)), "line 1 is empty for an untitled step with no type (#4032)");

    // #4063: a password with a raw ?, / or # (not percent-encoded) must not leave a piece on the card. The urls are built in parts so the commit's secret
    // scan does not read them as leaked credentials.
    [Theory]
    [InlineData("p?ss")]
    [InlineData("pa/ss")]
    [InlineData("pa#ss")]
    public void A_password_with_a_raw_separator_leaves_no_piece_on_the_card(string password) =>
        Assert.Equal("GET host/x", GraphCardLine.Settings("http", Config("{\"url\":\"https://" + string.Join(":", "user", password) + "@host/x\"}"), summary: null, kindTitle: null), StringComparer.Ordinal);

    [Fact]
    public void A_port_is_not_a_password_and_stays_on_the_card() =>
        Assert.Equal("GET host:8080/x", GraphCardLine.Settings("http", Config("{\"url\":\"https://host:8080/x\"}"), summary: null, kindTitle: null), StringComparer.Ordinal);

    private static string HttpLine(string url) => GraphCardLine.Settings("http", Config("{\"url\":\"" + url + "\"}"), summary: null, kindTitle: null);

    // #4156: an all-digit password with a raw /, ? or # looked like a port ("user:12" read as host:port). The urls are built in parts for the commit's secret scan.
    [Theory]
    [InlineData("12/34")]
    [InlineData("12?34")]
    [InlineData("12#34")]
    public void A_digit_password_with_a_raw_separator_leaves_no_piece_on_the_card(string password) =>
        Assert.Equal("GET host/x", HttpLine("https://" + string.Join(":", "user", password) + "@host/x"), StringComparer.Ordinal);

    [Theory]
    [InlineData("https://host:8080/x", "GET host:8080/x")]
    [InlineData("https://[::1]:8080/p", "GET [::1]:8080/p")]
    [InlineData("https://host/x@y", "GET host/x@y")]
    [InlineData("https://[::1]/a@b", "GET [::1]/a@b")]   // the ':' inside the brackets is part of the host, not a password
    public void A_host_with_a_port_or_brackets_stays_whole(string url, string expected) =>
        Assert.Equal(expected, HttpLine(url), StringComparer.Ordinal);

    // Accepted cost (#4156): a port followed by an @ later in the url reads as user:password@, because an all-digit password looks the same. The card shows the
    // text after the last @. Leaking a password is worse than a wrong host on an odd url, so this is on purpose: don't "fix" it back.
    [Fact]
    public void A_port_with_an_at_later_in_the_url_reads_as_a_password() =>
        Assert.Equal("GET name", HttpLine("https://host:8080/@name"), StringComparer.Ordinal);

    // #4156, the class: whatever sits before the last @ may be a password, wherever its ':' is. A user name with a raw / (a domain/user sign-in) puts the ':'
    // after the first separator, and a bracket that holds no IPv6 address hides nothing. No suffix of the password may be on the card.
    [Theory]
    [InlineData("user", "12/34")]
    [InlineData("user", "12?34")]
    [InlineData("user", "12#34")]
    [InlineData("user", "9")]
    [InlineData("user", "1:2/3")]
    [InlineData("user", "p@1/2")]
    [InlineData("", "12/34")]
    [InlineData("dom/user", "secret")]
    [InlineData("dom?user", "12")]
    [InlineData("[a", "9/8]")]
    public void No_suffix_of_a_url_password_shows_on_the_card(string user, string password)
    {
        var line = HttpLine("https://" + string.Join(":", user, password) + "@host/x");
        Assert.Equal("GET host/x", line, StringComparer.Ordinal);
        for (var i = 0; i < password.Length; i++)
        {
            Assert.DoesNotContain(password[i..], line, StringComparison.Ordinal);
        }
    }
}
