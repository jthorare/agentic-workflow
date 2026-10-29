using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

public sealed class FlowPassTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private const string ShareDialog =
        "export function ShareDialog() {\n  return <form><button data-testid=\"create-link\" onClick={handleCreateLink}>Create</button><button data-testid=\"revoke-link\" onClick={handleRevokeLink}>Revoke</button></form>;\n}\n";

    private const string LinkRoutes = "app.get(\"/links\", list);\n";

    private const string ShareSpec =
        "import { test } from \"@playwright/test\";\n"
        + "test(\"share a link\", async ({ page }) => {\n"
        + "  await page.goto(\"/links\");\n"
        + "  await page.getByTestId(\"create-link\").click();\n"
        + "  await page.getByTestId(\"revoke-link\").click();\n"
        + "});\n";

    private static RepoFile[] App(params RepoFile[] journeys) =>
        [new("src/ui/ShareDialog.tsx", ShareDialog), new("src/api/links.ts", LinkRoutes), .. journeys];

    private static GraphNode[] Flows(ExtractedMap map) => [.. map.Nodes.Where(n => n.Key.Kind == NodeKind.Flow)];

    private static string[] Steps(ExtractedMap map) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.StepOf).OrderBy(e => e.Order).Select(e => string.Create(CultureInfo.InvariantCulture, $"{e.From.Value}|{e.Order}"))];

    private static string[] AllText(ExtractedMap map) => [.. map.Nodes.SelectMany(n => new[] { n.Key.Value, n.Label })];

    [Fact, Trait("Lane", "unit")]
    public void A_playwright_spec_yields_one_flow_named_by_its_test_title_with_ordered_steps()
    {
        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/share.spec.ts", ShareSpec)));

        var flow = Assert.Single(Flows(map));
        Assert.Equal("flow:share-a-link", flow.Key.Value);
        Assert.Equal("share a link", flow.Label);
        Assert.Equal(["surface:route/GET:/links|1", "surface:pane/create-link|2", "surface:pane/revoke-link|3"], Steps(map), StringComparer.Ordinal);
        Assert.All(map.Edges.Where(e => e.Type == EdgeType.StepOf), e => Assert.Equal("flow:share-a-link", e.To.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ui_evidence_plan_yields_a_flow_and_ignores_its_base_url_storage_state_and_login()
    {
        const string plan = "{\n"
            + "  \"title\": \"share a link\",\n"
            + "  \"baseUrl\": \"https://staging.internal.example\",\n"
            + "  \"storageState\": \"auth/state.json\",\n"
            + "  \"steps\": [\n"
            + "    { \"action\": \"fill\", \"target\": \"login-email\", \"value\": \"owner@internal.example\" },\n"
            + "    { \"action\": \"goto\", \"target\": \"/links\" },\n"
            + "    { \"action\": \"click\", \"target\": \"create-link\" },\n"
            + "    { \"action\": \"click\", \"target\": \"revoke-link\" }\n"
            + "  ]\n"
            + "}\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile(".ui-evidence/share.json", plan)));

        var flow = Assert.Single(Flows(map));
        Assert.Equal("flow:share-a-link", flow.Key.Value);
        Assert.Equal(["surface:route/GET:/links|1", "surface:pane/create-link|2", "surface:pane/revoke-link|3"], Steps(map), StringComparer.Ordinal);
        var text = string.Join('\n', AllText(map));
        Assert.DoesNotContain("staging.internal.example", text, StringComparison.Ordinal);
        Assert.DoesNotContain("auth/state.json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("owner@internal.example", text, StringComparison.Ordinal);
        Assert.DoesNotContain("login-email", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_journey_with_one_matched_step_yields_no_flow()
    {
        var spec = "test(\"open links\", async ({ page }) => {\n  await page.goto(\"/links\");\n  await page.getByTestId(\"nothing-here\").click();\n});\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/open.spec.ts", spec)));

        Assert.Empty(Flows(map));
        Assert.Empty(Steps(map));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_route_step_matches_on_the_url_path_only()
    {
        var spec = "test(\"tabbed links\", async ({ page }) => {\n  await page.goto(\"/links?tab=2#top\");\n  await page.getByTestId(\"create-link\").click();\n});\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/tabs.spec.ts", spec)));

        Assert.Equal(["surface:route/GET:/links|1", "surface:pane/create-link|2"], Steps(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_test_id_match_wins_over_a_role_name_match()
    {
        var spec = "test(\"revoke\", async ({ page }) => {\n"
            + "  await page.goto(\"/links\");\n"
            + "  await page.getByTestId(\"create-link\").getByRole(\"button\", { name: \"Revoke link\" }).click();\n"
            + "});\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/order.spec.ts", spec)));

        Assert.Equal(["surface:route/GET:/links|1", "surface:pane/create-link|2"], Steps(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_filled_value_never_reaches_a_label_or_key()
    {
        var spec = "test(\"invite\", async ({ page }) => {\n"
            + "  await page.goto(\"/links\");\n"
            + "  await page.getByLabel(\"Email\").fill(\"joi@example.com\");\n"
            + "  await page.getByTestId(\"create-link\").click();\n"
            + "});\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/invite.spec.ts", spec)));

        _ = Assert.Single(Flows(map));
        Assert.DoesNotContain(AllText(map), t => t.Contains("joi@example.com", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Unsupported_forms_add_no_steps()
    {
        var spec = "test(\"mixed\", async ({ page }) => {\n"
            + "  await page.goto(\"/links\");\n"
            + "  await page.locator(\".css\").click();\n"
            + "  for (const id of ids) { await page.getByTestId(id).click(); }\n"
            + "  await clickEverything(page);\n"
            + "  await page.getByTestId(\"create-link\").click();\n"
            + "});\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/mixed.spec.ts", spec)));

        Assert.Equal(["surface:route/GET:/links|1", "surface:pane/create-link|2"], Steps(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_journey_file_over_the_size_cap_yields_no_flow()
    {
        var padded = ShareSpec + "// " + new string('x', 512_001) + "\n";

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/huge.spec.ts", padded)));

        Assert.Empty(Flows(map));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_unit_test_file_yields_no_flow()
    {
        var map = MapExtractor.Extract(_repo, App(new RepoFile("src/ui/ShareDialog.test.ts", ShareSpec)));

        Assert.Empty(Flows(map));
        Assert.Empty(Steps(map));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_skip_counts_name_their_reasons()
    {
        var spec = "test(\"counted\", async ({ page }) => {\n"
            + "  await page.goto(\"/links\");\n"
            + "  await page.locator(\".css\").click();\n"
            + "  await page.getByTestId(\"nothing-here\").click();\n"
            + "  await page.getByTestId(\"create-link\").click();\n"
            + "});\n";
        var huge = ShareSpec + "// " + new string('x', 512_001) + "\n";

        var skips = MapExtractor.Extract(_repo, App(new RepoFile("e2e/counted.spec.ts", spec), new RepoFile("e2e/huge.spec.ts", huge)), RecognizerSet.None).Skips;

        Assert.Equal(new JourneySkips(UnsupportedForm: 1, NoMatch: 1, Denied: 0, OverSizeCap: 1, OverFileCap: 0), skips);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_title_with_an_email_or_a_token_is_replaced_by_the_file_name()
    {
        var spec = ShareSpec.Replace("share a link", "invite owner@internal.example", StringComparison.Ordinal);

        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/invite-flow.spec.ts", spec)));

        var flow = Assert.Single(Flows(map));
        Assert.Equal("flow:invite-flow", flow.Key.Value);
        Assert.Equal("invite-flow", flow.Label);
        Assert.DoesNotContain(AllText(map), t => t.Contains("internal.example", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_journeys_with_one_title_get_numbered_keys_in_path_order()
    {
        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/b.spec.ts", ShareSpec), new RepoFile("e2e/a.spec.ts", ShareSpec)));

        Assert.Equal(["flow:share-a-link", "flow:share-a-link-2"], Flows(map).Select(f => f.Key.Value), StringComparer.Ordinal);
        Assert.Equal(["e2e/a.spec.ts"], Flows(map)[0].EvidenceOrEmpty.Select(e => e.Key.Value[(e.Key.Value.IndexOf('/', StringComparison.Ordinal) + 1)..]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_journey_file_adds_no_code_node()
    {
        var map = MapExtractor.Extract(_repo, App(new RepoFile("e2e/share.spec.ts", ShareSpec)));

        Assert.DoesNotContain(map.Nodes, n => n.Key.Value.Contains("e2e/", StringComparison.Ordinal));
    }
}
