using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The Add several sheet's reads: Read the list (the split, then ONE suggest call for the matches, then one read per matched concept) and a changed match.</summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralViewModel
{
    private const int NameAndDefinition = 80;

    /// <summary>Turns the box into rows, matches each line to a derived concept and fills the matched rows' words and features.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that ends when the rows are matched (or the match failed).</returns>
    internal async Task ReadListAsync(CancellationToken cancellationToken)
    {
        if (!CanRead)
        {
            return;
        }

        var read = PinListParser.Read(Text);
        TooManyNote = read.Truncated ? Labels.PinSeveralTooMany : string.Empty;
        OnChanged(nameof(TooManyNote));
        OnChanged(nameof(HasTooManyNote));
        SetMessage(string.Empty);
        _concepts.Clear();
        (_matching, _matchFailed) = (true, false);
        Rows = [.. read.Lines.Select(l => new PinSeveralRowViewModel(l, Evaluate))];
        Evaluate();
        try
        {
            await MatchAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _matching = false;
        }

        foreach (var row in Rows)
        {
            Fill(row);
        }

        Evaluate();
        RowsRead?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the concept a row's picker was changed to and fills its words and features from it.</summary>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the row is filled.</returns>
    internal async Task RepickAsync(PinSeveralRowViewModel row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        var key = row.SelectedChoice.Key;
        if (string.Equals(row.FilledFrom, key, StringComparison.Ordinal))
        {
            return;
        }

        if (key is not null && !_concepts.ContainsKey(key) && _apiUrl() is { } url)
        {
            row.Matching(matching: true, Labels.PinSeveralMatching);
            await LoadAsync(url, [key], cancellationToken).ConfigureAwait(true);
        }

        Fill(row);
        Evaluate();
    }

    // One suggest call for the whole list; each line's answer pre-picks its row. A failed call leaves every row on "New, no match yet".
    private async Task MatchAsync(CancellationToken cancellationToken)
    {
        var readable = Rows.Where(r => r.Readable).ToList();
        if (readable.Count == 0)
        {
            return;
        }

        IReadOnlyList<PinSuggestMatch>? matches = null;
        var url = _apiUrl();
        if (url is not null)
        {
            var asked = await _api.ContextPinSuggest(url, new PinSuggestQuery(_open.Repo, Lines: [.. readable.Select(Line)]), cancellationToken).ConfigureAwait(true);
            matches = asked.IsSuccess ? asked.Value.Matches : null;
        }

        _matchFailed = matches is null;
        Pick(readable, matches);
        if (url is not null && matches is not null)
        {
            await LoadAsync(url, [.. readable.Select(r => r.SelectedChoice.Key).OfType<string>().Distinct(StringComparer.Ordinal)], cancellationToken).ConfigureAwait(true);
        }
    }

    // Each readable row takes the concept its line matched, or "New, no match yet".
    private void Pick(List<PinSeveralRowViewModel> readable, IReadOnlyList<PinSuggestMatch>? matches)
    {
        var choices = Choices(matches);
        for (var i = 0; i < readable.Count; i++)
        {
            var key = matches?.FirstOrDefault(m => m.Line == i)?.Concept;
            readable[i].Choose(choices, choices.FirstOrDefault(c => key is not null && string.Equals(c.Key, key, StringComparison.Ordinal)) ?? PinSeveralChoice.New);
        }
    }

    // The definition is clipped so name and definition stay at 80 characters, as the suggest read takes them.
    private static PinSuggestLine Line(PinSeveralRowViewModel row)
    {
        var name = row.Name.Trim();
        var room = NameAndDefinition - name.Length;
        var definition = row.Definition.Trim();
        return new PinSuggestLine(name, definition.Length == 0 || room <= 0 ? null : definition[..Math.Min(definition.Length, room)]);
    }

    private List<PinSeveralChoice> Choices(IReadOnlyList<PinSuggestMatch>? matches)
    {
        List<PinSeveralChoice> all = [PinSeveralChoice.New, .. _derived];
        foreach (var match in matches ?? [])
        {
            if (match.Concept is { } key && !all.Exists(c => string.Equals(c.Key, key, StringComparison.Ordinal)))
            {
                all.Add(new PinSeveralChoice(key, match.Name ?? key));
            }
        }

        return all;
    }

    // One read per concept (a few at a time): its terms with their counts and its member features.
    private async Task LoadAsync(Uri url, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(ConceptReads);
        await Task.WhenAll(keys.Select(async key =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                var read = await _api.ContextPinSuggest(url, new PinSuggestQuery(_open.Repo, Concept: key), cancellationToken).ConfigureAwait(true);
                _concepts[key] = read.IsSuccess ? read.Value : null;
            }
            finally
            {
                _ = gate.Release();
            }
        })).ConfigureAwait(true);
    }
}
#pragma warning restore MA0182
