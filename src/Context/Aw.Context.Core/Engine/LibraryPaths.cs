using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The library's path rules (review page spec §5.1), which need the machine's home folders: '~/' expansion, the refused roots and roots inside one another. A path that does not exist is not refused here.</summary>
public static class LibraryPaths
{
    private const string Prefix = "context.sources.library";

    /// <summary>Expands and checks the roots of <paramref name="source"/>. The 'aw' root may hold $AW_SINDRI_HOME (it opens only its plan, proof and system-design folders); a vault may not.</summary>
    /// <param name="source">The checked settings.</param>
    /// <param name="host">The machine's home folders.</param>
    /// <returns>The roots (the 'aw' root first, unless turned off), or SND-LIB-001 naming the key path.</returns>
    public static Result<IReadOnlyList<LibraryRoot>> Resolve(LibrarySource source, LibraryHost host)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(host);
        var roster = new Roster(host);
        if (source.AwRootOn)
        {
            var aw = new LibraryRoot { Name = "aw", Path = roster.Expand(source.StateDir ?? host.StateDefault), Models = source.Models, IsAw = true };
            if (roster.Admit(aw, Prefix + ".stateDir", awRoot: true) is { } refused)
            {
                return refused;
            }
        }

        for (var i = 0; i < source.Vaults.Count; i++)
        {
            var vault = source.Vaults[i];
            var root = new LibraryRoot { Name = vault.Name, Path = roster.Expand(vault.Path), Deny = vault.Deny, Models = vault.Models };
            if (roster.Admit(root, string.Create(CultureInfo.InvariantCulture, $"{Prefix}.vaults[{i}].path"), awRoot: false) is { } refused)
            {
                return refused;
            }
        }

        return Kernel.Results.Success<IReadOnlyList<LibraryRoot>>(roster.Roots);
    }

    /// <summary>The roots admitted so far, with the machine's folders they are compared against.</summary>
    private sealed class Roster
    {
        private readonly LibraryHost _host;
        private readonly string _home;
        private readonly string? _sindri;
        private readonly List<(LibraryRoot Root, string Real)> _admitted = [];

        internal Roster(LibraryHost host)
        {
            _host = host;
            _home = Normalize(host.Home);
            _sindri = host.SindriHome is { Length: > 0 } sindri ? host.RealPath(Normalize(sindri)) : null;
        }

        internal IReadOnlyList<LibraryRoot> Roots => [.. _admitted.Select(static r => r.Root)];

        internal string Expand(string written) =>
            Normalize(written.StartsWith("~/", StringComparison.Ordinal) ? _home + "/" + written[2..] : written);

        internal Error? Admit(LibraryRoot root, string key, bool awRoot)
        {
            var real = _host.RealPath(root.Path);
            var problem = LibraryRules.PathProblem(root.Path) ?? Refusal(root, real, awRoot);
            if (problem is not null)
            {
                return LibraryRules.Invalid(key, $"'{root.Path}' {problem}");
            }

            _admitted.Add((root, real));
            return null;
        }

        private string? Refusal(LibraryRoot root, string real, bool awRoot)
        {
            if (root.Path == "/" || real == "/")
            {
                return "can't be the root folder";
            }

            if (real == _host.RealPath(_home))
            {
                return "can't be your home folder";
            }

            if (!awRoot && _sindri is not null && Holds(real, _sindri))
            {
                return "can't hold the Sindri home ($AW_SINDRI_HOME): its databases and tokens would be read as notes";
            }

            return _admitted.FirstOrDefault(r => Holds(r.Real, real) || Holds(real, r.Real)) is { Root: { } other }
                ? $"overlaps the {other.Name} root ({other.Path}); roots can't sit inside one another"
                : null;
        }
    }

    /// <summary>Whether <paramref name="outer"/> is <paramref name="inner"/> or a folder above it.</summary>
    private static bool Holds(string outer, string inner) =>
        string.Equals(outer, inner, StringComparison.Ordinal) || inner.StartsWith(outer == "/" ? "/" : outer + "/", StringComparison.Ordinal);

    /// <summary>The path without repeated or trailing '/' and without '.' segments ('..' was refused before).</summary>
    private static string Normalize(string path)
    {
        if (!path.StartsWith('/'))
        {
            return path;
        }

        var kept = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(static s => s != ".");
        return "/" + string.Join('/', kept);
    }
}
