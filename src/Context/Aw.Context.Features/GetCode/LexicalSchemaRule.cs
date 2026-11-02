using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetCode;

/// <summary>The schema rule: the repo's files that match the profile's schema globs and name a data thing (any form of its key name, label or aliases).</summary>
internal sealed class LexicalSchemaRule(LexicalCandidates candidates)
{
    internal async ValueTask Find(LexicalRun run, CancellationToken cancellationToken)
    {
        var pass = run.Pass;
        if (pass.Settings.SchemaGlobs.Count == 0)
        {
            return;
        }

        var schemaPaths = run.Tracked.Where(p => PathDenylist.Matches(p, pass.Settings.SchemaGlobs)).ToList();
        var searchable = pass.Things.Where(t => t.Searchable).ToList();
        pass.SchemaCapped |= searchable.Count > CodeRules.MaxThings;   // the rest are reported, not dropped silently
        var forms = searchable.Take(CodeRules.MaxThings).Select(t => (Thing: t, Terms: CodeLensBuilder.ThingTerms(pass.Graph, t.Key, pass.Clean))).Where(f => f.Terms.Count > 0).ToList();
        if (forms.Count == 0 || schemaPaths.Count == 0)
        {
            return;
        }

        // One search for every thing's terms together (one git call per chunk of paths); the files are then tied to their things in process.
        var union = forms.SelectMany(f => f.Terms).Distinct(StringComparer.Ordinal).ToList();
        var query = new CandidateQuery(union, schemaPaths, CodeRules.MaxCandidates, LexicalPass.SchemaId);
        if (await candidates.Find(run, query, cancellationToken).ConfigureAwait(false) is not { } found)
        {
            return;
        }

        pass.SchemaCapped |= found.Cut;
        await Name(run, forms, found.Paths, cancellationToken).ConfigureAwait(false);
    }

    // Ties each candidate file (read in parallel, taken in path order) to the things whose terms its text names, at most one more than the listed files per thing (so the merge sees the cut).
    private async ValueTask Name(LexicalRun run, List<(CodeThing Thing, IReadOnlyList<string> Terms)> forms, List<string> paths, CancellationToken cancellationToken)
    {
        var tied = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Take(string path, string? text)
        {
            if (text is not null)
            {
                Tie(run, forms, tied, (path, text));
            }

            return true;
        }

        await Fanout.ForEach(paths, (path, token) => candidates.Text(run, path, token), Take, cancellationToken).ConfigureAwait(false);
    }

    private static void Tie(LexicalRun run, List<(CodeThing Thing, IReadOnlyList<string> Terms)> forms, Dictionary<string, int> tied, (string Path, string Text) file)
    {
        foreach (var (thing, terms) in forms)
        {
            var count = tied.GetValueOrDefault(thing.Key.Value);
            if (count > CodeRules.MaxFilesPerThing || NameMatcher.FindTerm(file.Text, terms) is not { } matched)
            {
                continue;
            }

            tied[thing.Key.Value] = count + 1;
            run.Pass.SchemaCapped |= count == CodeRules.MaxFilesPerThing;
            run.Pass.Schema.Add(new LexicalSchemaFile(thing.Key, file.Path, matched));
        }
    }
}
