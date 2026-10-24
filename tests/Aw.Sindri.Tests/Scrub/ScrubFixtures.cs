namespace Aw.Sindri.Tests.Scrub;

/// <summary>Shared text for the scrub tests.</summary>
internal static class ScrubFixtures
{
    /// <summary>An AWS-key-shaped string, built at run time so no scanner reads the source as a leaked key.</summary>
    internal static string Secret => "AKIA" + "ABCDEFGHIJKLMNOP";

    /// <summary>A pre-commit file that is not Sindri's.</summary>
    internal const string ForeignHook = "#!/bin/sh\necho lint\n";

    /// <summary>A file that only mentions a Sindri marker: not a Sindri hook.</summary>
    internal const string MentionsMarker = "#!/bin/sh\necho '# sindri-pre-commit v3' > /dev/null\n";

    internal const string V2Hook = "#!/bin/sh\n# sindri-pre-commit v2\nSINDRI='/old/sindri'\n\"$SINDRI\" scrub --staged || exit 1\n\"$SINDRI\" shape --record --staged || true\n";

    internal const string V1Hook = "#!/bin/sh\n# sindri-scrub-pre-commit v1\nSINDRI='/old/sindri'\n\"$SINDRI\" scrub --staged || exit 1\n";
}
