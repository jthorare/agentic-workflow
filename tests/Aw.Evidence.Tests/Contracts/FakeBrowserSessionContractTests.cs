using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeBrowserSessionContractTests : BrowserSessionContract
{
    protected override Task<IBrowserSession> OpenContractPageAsync()
    {
        var page = new FakePage { Path = "/" };
        _ = page.Texts.Add("Hospitals");
        _ = page.Texts.Add("Open");
        foreach (var id in (string[])["open", "search", "next"])
        {
            _ = page.TestIds.Add(id);
        }

        page.Candidates.Add(new Candidate(0, "textbox", "Search", TestId.From("search"), Text: null));
        page.Candidates.Add(new Candidate(1, "button", "Open", TestId.From("open"), "Open"));
        page.Candidates.Add(new Candidate(2, "a", "Next", TestId.From("next"), "Next"));
        page.OnClick["testId:open"] = static p => p.Texts.Add("Panel open");
        page.OnClick["candidate:1"] = static p => p.Texts.Add("Panel open");
        page.OnClick["testId:next"] = static p => p.Path = "/next";
        page.Boxes["testId:open"] = new Box(8, 40, 52, 21);
        var options = new BrowserSessionOptions(new ResolvedHost("http://localhost:3000", HostKind.Local), new ViewportSize(1280, 800), RecordDirectory: null, UserAgentOverride: null);
        return Task.FromResult<IBrowserSession>(new FakeBrowserSession(page, options));
    }

    protected override Task<IBrowserSession> OpenFeaturePageAsync(IReadOnlyDictionary<string, string>? localStorage = null)
    {
        var page = new FakePage { Path = "/features" };
        foreach (var id in (string[])["strip", "card", "lane", "note", "night", "show-ghost", "row"])
        {
            _ = page.TestIds.Add(id);
        }

        page.Counts["testId:row"] = 3;
        _ = page.Roles.Add("button|Plain");
        _ = page.Roles.Add("button|Dup");
        page.Counts["role:button|Dup"] = 2;
        _ = page.Texts.Add("Plain");
        _ = page.Texts.Add(localStorage is not null && localStorage.TryGetValue("theme", out var theme) ? $"Theme {theme}" : "Theme none");
        page.OnScroll["testId:strip"] = static p => p.Texts.Add("Strip scrolled");
        page.OnDrag["testId:card>testId:lane"] = static (p, gesture) => p.Texts.Add(gesture.HoldAlt ? "Dropped with Alt" : "Dropped");
        page.OnKey = static (p, key) => p.Texts.Add($"Key {key}");
        page.OnClick["testId:night"] = static p => p.Query["timeFilter"] = "Night";
        page.OnClick["testId:show-ghost"] = static p => p.TestIds.Add("ghost");
        page.OnClick["testId:row[1]"] = static p => p.Texts.Add("Row 2 clicked");
        page.OnClick["role:button|Dup#2"] = static p => p.Texts.Add("Dup 2 clicked");
        var options = new BrowserSessionOptions(new ResolvedHost("http://localhost:3000", HostKind.Local), new ViewportSize(1280, 800), RecordDirectory: null, UserAgentOverride: null, localStorage);
        return Task.FromResult<IBrowserSession>(new FakeBrowserSession(page, options));
    }
}
