using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The Add several sheet's row checks (concept pins spec §3.6 "Add several" table), run on every edit of a row.</summary>
#pragma warning disable MA0182 // Q7b: the shell sheet mounts it
internal sealed partial class PinSeveralViewModel
{
    private static string NameKey(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();

    // Re-judges every row against the ones before it and the pins of the profile: its name (an existing pin, a repeat), its match (the same concept twice), and the buttons.
    private void Evaluate()
    {
        var names = new Dictionary<string, PinSeveralRowViewModel>(StringComparer.Ordinal);
        var matches = new Dictionary<string, PinSeveralRowViewModel>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            PinView? updates = null;
            int? repeats = null;
            if (row.Readable && names.TryGetValue(NameKey(row.Name), out var earlier))
            {
                repeats = earlier.Number;
            }
            else if (row.Readable)
            {
                names[NameKey(row.Name)] = row;
                updates = Existing(row.Name);
            }

            if (row.Judge(updates, repeats))
            {
                Fill(row);
            }

            row.Matching(_matching && row.Readable, MatchNote(row, matches));
        }

        Changed();
    }

    // The pin a name is the name or the id of: an update, not a new pin.
    private PinView? Existing(string name)
    {
        var key = NameKey(name);
        var id = PinSeveralIds.Slug(name);
        return _open.Pins.FirstOrDefault(p => string.Equals(NameKey(p.Name), key, StringComparison.Ordinal) || string.Equals(NameKey(p.Id), key, StringComparison.Ordinal) || (id.Length > 0 && string.Equals(p.Id, id, StringComparison.Ordinal)));
    }

    private string MatchNote(PinSeveralRowViewModel row, Dictionary<string, PinSeveralRowViewModel> matches)
    {
        if (!row.Readable)
        {
            return string.Empty;
        }

        if (_matching)
        {
            return Labels.PinSeveralMatching;
        }

        if (_matchFailed)
        {
            return Labels.PinSeveralMatchFailed;
        }

        if (row.SelectedChoice.Key is not { } key)
        {
            return string.Empty;
        }

        if (matches.TryGetValue(key, out var earlier))
        {
            return Labels.PinSeveralAlsoMatched(earlier.SelectedChoice.Name);
        }

        matches[key] = row;
        return string.Empty;
    }

    // Fills a row's words and features from what its matched concept read gave. A row that updates a pin takes only the words its line gives: it never clears that pin's features.
    private void Fill(PinSeveralRowViewModel row)
    {
        var key = row.SelectedChoice.Key;
        row.FilledFrom = key;
        PinSuggestView? read = null;
        var asked = key is not null && _concepts.TryGetValue(key, out read);
        if (row.Updates is not null)
        {
            row.Fill(Words(row, []), [], string.Empty);
            return;
        }

        var features = read?.Features ?? [];
        row.Fill(Words(row, read?.Terms ?? []), [.. features.Select(x => new PinFeatureRow(x.Key, x.Label, ticked: true))], asked && read is null ? Labels.PinSheetTermsUnreadable : string.Empty);
    }

    // The line's own words first, then the concept's terms to make 20; each word is checked as it is on the single sheet.
    private IReadOnlyList<PinWordRow> Words(PinSeveralRowViewModel row, IReadOnlyList<PinSuggestTerm> terms)
    {
        var words = row.AlsoWords.Select(w => (Word: w, Records: terms.FirstOrDefault(t => string.Equals(t.Word, w, StringComparison.OrdinalIgnoreCase))?.NewRecords)).ToList();
        var room = Math.Max(0, PinSheetViewModel.WordsShown - words.Count);
        var own = words.Select(w => w.Word).ToList();
        words.AddRange(terms.Where(t => !own.Contains(t.Word, StringComparer.OrdinalIgnoreCase)).Take(room).Select(t => (t.Word, (int?)t.NewRecords)));
        return [.. words.Select((w, index) => Word(w.Word, w.Records, index, row.Updates?.Id))];
    }

    private PinWordRow Word(string word, int? records, int index, string? self)
    {
        if (index >= PinSheetViewModel.WordsShown)
        {
            return new PinWordRow(word, records, ticked: false, Labels.PinSeveralOverWords) { IsRefused = true };
        }

        if (PinWordRules.Refusal(word, _open.Pins, self) is { } why)
        {
            return new PinWordRow(word, newRecords: null, ticked: false, why) { IsRefused = true };
        }

        return records is { } count && count > PinSheetViewModel.TooBroad
            ? new PinWordRow(word, records, ticked: false, Labels.PinSheetTooBroad(count))
            : new PinWordRow(word, records, ticked: true);
    }
}
#pragma warning restore MA0182
