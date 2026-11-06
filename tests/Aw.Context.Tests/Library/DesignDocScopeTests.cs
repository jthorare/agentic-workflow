using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocScopeTests
{
    private const string Aw = "aw/acme-harbor";

    private static ScopedDesignDoc Doc(string path, string scope, string kind, string id, string body, string extra = "") =>
        new(path, scope, DesignDocParser.Parse(path, ADoc.Front(kind, id, extra) + body));

    private static ScopedDesignDoc Product(string path = "/aw/product.md", string scope = Aw) =>
        Doc(path, scope, "product", "harbor-booking-product", "## Goals\n- **REQ-U1** rider books\n");

    [Fact, Trait("Lane", "unit")]
    public void The_doc_at_the_lowest_path_keeps_a_shared_id_and_the_others_are_012_but_keep_their_items()
    {
        var b = Doc("/aw/b.md", Aw, "note", "harbor-notes", "## Goals\n");
        var a = Doc("/aw/a.md", Aw, "note", "harbor-notes", "## Goals\n");
        var other = Doc("/aw/c.md", "aw/other-repo", "note", "harbor-notes", "## Goals\n");

        var findings = DesignDocScope.Check([b, a, other]);

        var finding = Assert.Single(findings);
        Assert.Equal("/aw/b.md", finding.Path);
        Assert.Equal(DesignDocCodes.SharedDocId, finding.Error.Code);
        Assert.Equal(3, finding.Error.Line);
        Assert.Contains("/aw/a.md", finding.Error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(b.Doc.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_removed_doc_hands_its_id_to_the_next_one_with_no_rewrite()
    {
        var a = Doc("/aw/a.md", Aw, "note", "harbor-notes", "## Goals\n");
        var b = Doc("/aw/b.md", Aw, "note", "harbor-notes", "## Goals\n");

        _ = Assert.Single(DesignDocScope.Check([a, b]));
        Assert.Empty(DesignDocScope.Check([b]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ref_to_an_item_the_scope_lacks_is_015_at_its_line()
    {
        var tasks = Doc("/aw/tasks.md", Aw, "tasks", "harbor-booking-tasks", "## TASK-1 Form\n- reqs: REQ-12\n");

        var finding = Assert.Single(DesignDocScope.Check([tasks]));

        Assert.Equal(DesignDocCodes.BrokenRef, finding.Error.Code);
        Assert.Equal(ADoc.Body + 2, finding.Error.Line);
        Assert.Contains("REQ-12", finding.Error.Message, StringComparison.Ordinal);
        Assert.NotEmpty(tasks.Doc.Items);
        Assert.NotEmpty(tasks.Doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_id_resolves_in_its_own_doc_or_any_doc_of_the_scope()
    {
        var tasks = Doc("/aw/tasks.md", Aw, "tasks", "harbor-booking-tasks", "## TASK-1 Form\n- reqs: REQ-U1\n## TASK-2 Pay\n- depends: TASK-1\n");

        _ = Assert.Single(DesignDocScope.Check([tasks]));
        Assert.Empty(DesignDocScope.Check([tasks, Product()]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("harbor-booking-product#REQ-U1", false)]
    [InlineData("harbor-booking-product#REQ-U9", true)]
    [InlineData("harbor-missing#REQ-U1", true)]
    [InlineData("doc:aw/acme-harbor/harbor-booking-product#REQ-U1", false)]
    [InlineData("doc:aw/acme-harbor/harbor-booking-product#REQ-U9", true)]
    [InlineData("doc:aw/acme-harbor/harbor-missing#REQ-U1", true)]
    public void A_doc_qualified_ref_names_the_doc_and_then_the_item(string to, bool broken)
    {
        var tasks = Doc("/aw/tasks.md", Aw, "tasks", "harbor-booking-tasks", $"## TASK-1 Form\n- reqs: {to}\n");

        var findings = DesignDocScope.Check([tasks, Product()]);

        string[] expected = broken ? [DesignDocCodes.BrokenRef] : [];
        string[] actual = [.. findings.Select(static f => f.Error.Code)];
        Assert.Equal(expected, actual);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_ref_into_a_scope_outside_the_set_is_not_checked_and_a_ref_into_another_scope_in_it_is()
    {
        var tasks = Doc("/aw/tasks.md", Aw, "tasks", "harbor-booking-tasks", "## TASK-1 Form\n- reqs: doc:repo:harbor-web/harbor-booking-spec#REQ-1\n");
        var spec = Doc("/repo/spec.md", "repo:harbor-web", "spec", "harbor-booking-spec", "## Goals\n- **REQ-1** x\n");

        Assert.Empty(DesignDocScope.Check([tasks]));
        Assert.Empty(DesignDocScope.Check([tasks, spec]));
        var noReq = Doc("/repo/spec.md", "repo:harbor-web", "spec", "harbor-booking-spec", "## Goals\n");
        string[] codes = [.. DesignDocScope.Check([tasks, noReq]).Select(static f => f.Error.Code)];
        Assert.Equal([DesignDocCodes.BrokenRef], codes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_id_is_not_checked_when_the_doc_links_to_a_parent_in_another_scope()
    {
        var stages = Doc("/aw/stages.md", Aw, "tasks", "harbor-booking-tasks", "## TASK-1 Form\n- reqs: REQ-1\n", "parent: doc:repo:harbor-web/harbor-booking-spec\n");

        Assert.Empty(DesignDocScope.Check([stages]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("parent: harbor-booking-product", false)]
    [InlineData("parent: harbor-nope", true)]
    [InlineData("supersedes: harbor-nope", true)]
    [InlineData("supersedes: doc:repo:elsewhere/harbor-nope", false)]
    [InlineData("supersedes: doc:aw/acme-harbor/harbor-nope", true)]
    [InlineData("supersedes: doc:aw/acme-harbor/harbor-booking-product", false)]
    public void An_unknown_parent_or_supersedes_is_016_at_its_key_line(string key, bool unknown)
    {
        var note = Doc("/aw/note.md", Aw, "note", "harbor-note", "## Goals\n", key + "\n");

        var findings = DesignDocScope.Check([note, Product()]);

        if (!unknown)
        {
            Assert.Empty(findings);
            return;
        }

        var finding = Assert.Single(findings);
        Assert.Equal(DesignDocCodes.UnknownDocLink, finding.Error.Code);
        Assert.Equal("/aw/note.md", finding.Path);
        Assert.Equal(6 + 1, finding.Error.Line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_doc_with_no_front_matter_or_a_parse_error_takes_no_part()
    {
        var skipped = new ScopedDesignDoc("/aw/plain.md", Aw, DesignDocParser.Parse("/aw/plain.md", "# Plain\n"));
        var broken = Doc("/aw/broken.md", Aw, "spec", "harbor-broken", "- **REQ-** x\n");

        Assert.Empty(DesignDocScope.Check([skipped, broken]));
    }

    private static ScopedDesignDoc Stored(ScopedDesignDoc doc) =>
        doc with { Doc = doc.Doc with { FrontMatter = doc.Doc.FrontMatter! with { KeyLines = new Dictionary<string, int>(StringComparer.Ordinal) } } };

    [Fact, Trait("Lane", "unit")]
    public void A_doc_read_back_from_storage_has_no_key_lines_and_its_findings_have_no_line()
    {
        var a = Stored(Doc("/aw/a.md", Aw, "note", "harbor-notes", "## Goals\n"));
        var b = Stored(Doc("/aw/b.md", Aw, "note", "harbor-notes", "## Goals\n", "parent: harbor-missing\n"));

        var findings = DesignDocScope.Check([a, b]);

        Assert.Equal([DesignDocCodes.SharedDocId, DesignDocCodes.UnknownDocLink], [.. findings.Select(static f => f.Error.Code).Order(StringComparer.Ordinal)]);
        Assert.All(findings, static f => Assert.Null(f.Error.Line));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_doc_with_a_bad_status_keeps_its_id_so_a_parent_naming_it_and_refs_to_it_resolve()
    {
        var a = Doc("/aw/a.md", Aw, "tasks", "harbor-parent", "## TASK-5 Thing\n", "status: nope\n");
        var b = Doc("/aw/b.md", Aw, "tasks", "harbor-child", "## TASK-1 Form\n- reqs: harbor-parent#TASK-5\n", "parent: harbor-parent\n");

        var findings = DesignDocScope.Check([a, b]);

        Assert.DoesNotContain(findings, static f => f.Error.Code == DesignDocCodes.UnknownDocLink);
        Assert.DoesNotContain(findings, static f => f.Error.Code == DesignDocCodes.BrokenRef);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_ref_through_a_parent_in_another_scope_is_checked_against_that_scope_when_it_is_loaded()
    {
        const string Other = "aw/other-repo";
        var product = Doc("/aw/other.md", Other, "product", "harbor-booking-product", "## Goals\n- **REQ-U1** rider books\n");
        var tasks = Doc("/aw/tasks.md", Aw, "tasks", "harbor-booking-tasks", "## TASK-1 Form\n- reqs: REQ-U1, REQ-99\n", $"parent: doc:{Other}/harbor-booking-product\n");

        var loaded = Assert.Single(DesignDocScope.Check([tasks, product]));
        var notLoaded = DesignDocScope.Check([tasks]);

        Assert.Equal(DesignDocCodes.BrokenRef, loaded.Error.Code);
        Assert.Contains("REQ-99", loaded.Error.Message, StringComparison.Ordinal);
        Assert.Empty(notLoaded);
    }
}
