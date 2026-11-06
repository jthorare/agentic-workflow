using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

/// <summary>Builds small made-up design docs: a front matter of 7 lines, so a body's line n is file line <see cref="Body"/> + n.</summary>
internal static class ADoc
{
    internal const string Path = "/vault/harbor.md";

    /// <summary>The number of lines before a body's first line.</summary>
    internal const int Body = 7;

    internal static string Front(string kind = "spec", string id = "harbor-booking", string extra = "") =>
        $"---\naw-doc: 1\nid: {id}\nkind: {kind}\ntitle: \"Harbor ferry booking\"\nstatus: draft\n{extra}---\n";

    internal static ParsedDesignDoc Parse(string kind, string body, string id = "harbor-booking") =>
        DesignDocParser.Parse(Path, Front(kind, id) + body);

    internal static string[] Ids(ParsedDesignDoc doc) => [.. doc.Items.Select(static i => i.Key)];

    internal static string[] Codes(ParsedDesignDoc doc) => [.. doc.Errors.Select(static e => e.Code)];
}
