using Aw.Context.Core.Code;

namespace Aw.Context.Features.GetCode;

/// <summary>The two rules that tie a test file to an implementing file without a map edge: the sibling rule (names only) and the imports rule (the test's text names the file's path).</summary>
internal sealed class LexicalTestsRule(LexicalCandidates candidates)
{
    internal async ValueTask Find(LexicalRun run, IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        var pass = run.Pass;
        var implementing = files.ToHashSet(StringComparer.Ordinal);
        var testPaths = run.Tracked.Where(p => !implementing.Contains(p) && TestPaths.IsTest(p, pass.Settings.TestGlobs)).ToList();
        pass.Tests.AddRange(testPaths.SelectMany(test => files.Where(f => SiblingRule.Ties(test, f)).Select(f => new LexicalTest(test, CodeBasis.Sibling, [f]))));
        var forms = files.ToDictionary(f => f, NameMatcher.PathTerms, StringComparer.Ordinal);
        var terms = forms.Values.SelectMany(t => t).Distinct(StringComparer.Ordinal).ToList();
        if (terms.Count == 0 || testPaths.Count == 0)
        {
            pass.Searched.Add(new CodeLexical(run.Repo.Name.Value, run.Commit[..7], files.Count, Candidates: 0, Read: 0));
            return;
        }

        var found = await candidates.Find(run, new CandidateQuery(terms, testPaths, CodeRules.MaxCandidates, LexicalPass.TestsId), cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return;
        }

        var read = await Imports(run, forms, found.Value.Paths, cancellationToken).ConfigureAwait(false);
        pass.TestsCapped |= found.Value.Cut;
        pass.Searched.Add(new CodeLexical(run.Repo.Name.Value, run.Commit[..7], files.Count, found.Value.Paths.Count, read));
    }

    // Reads each candidate (in parallel, taken in candidate order) and ties it to the implementing files whose path forms its text names; returns how many it could read.
    private async ValueTask<int> Imports(LexicalRun run, Dictionary<string, IReadOnlyList<string>> forms, List<string> paths, CancellationToken cancellationToken)
    {
        var read = 0;
        bool Take(string candidate, string? text)
        {
            if (text is null)
            {
                return true;
            }

            read++;
            var named = forms.Where(f => f.Value.Count > 0 && NameMatcher.FindPath(text, f.Value) is not null).Select(f => f.Key).ToList();
            if (named.Count > 0)
            {
                run.Pass.Tests.Add(new LexicalTest(candidate, CodeBasis.Imports, named));
            }

            return true;
        }

        await Fanout.ForEach(paths, (candidate, token) => candidates.Text(run, candidate, token), Take, cancellationToken).ConfigureAwait(false);
        return read;
    }
}
