using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-HOME-002: no Home row has that id (home spec §10); answered with HTTP 404.</summary>
    public static readonly ErrorDefinition HomeNotFound = new("SND-HOME-002", ErrorKind.NotFound, "No such Home row.");

    /// <summary>The Home codes (home spec §10), for <see cref="All"/>.</summary>
    private static ErrorDefinition[] EveryHome() => [HomeNotFound];
}
