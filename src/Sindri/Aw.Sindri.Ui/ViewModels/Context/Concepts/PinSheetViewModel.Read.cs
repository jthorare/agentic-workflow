using System.Globalization;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The sheet's opening (<see cref="PinSheetViewModel"/>, continued): the fields fill at once from the card or the pin, then the button's rule, the terms and the features arrive.</summary>
internal sealed partial class PinSheetViewModel
{
    /// <summary>Fills the sheet from what opened it and reads the rest: whether anything else waits, the concept's terms and the map's features.</summary>
    /// <param name="open">What opened the sheet.</param>
    /// <param name="cancellationToken">Cancels the reads (closing the sheet).</param>
    /// <returns>A task that ends when the reads are drawn.</returns>
    internal async Task Open(PinSheetOpen open, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(open);
        Begin(open);
        if (_ports is null)
        {
            return;
        }

        try
        {
            await Task.WhenAll(ReadRule(cancellationToken), ReadTerms(cancellationToken), ReadFeatures(string.Empty, cancellationToken)).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // the sheet closed while it read
        }
    }

    // The fields from the card or the pin; the words of a card show with "Counting records…" until the terms arrive.
    private void Begin(PinSheetOpen open)
    {
        _filling = true;
        try
        {
            (_open, _pins, _repo, _ifHash) = (open, open.Pins, open.Repo, open.FileHash);
            Mode = open.Pin is not null ? PinSheetMode.EditPin : open.Card is not null ? PinSheetMode.PinThis : PinSheetMode.NewPin;
            Words.Clear();
            Features.Clear();
            if (open.Pin is { } pin)
            {
                FillPin(pin);
            }
            else if (open.Card is { } card)
            {
                FillCard(card);
            }
        }
        finally
        {
            _filling = false;
        }

        if (_ifHash is null)
        {
            SubmitError = Labels.PinSheetNoHash;   // the sheet writes nothing without the hash its fields were read under
            OnChanged(nameof(HasSubmitError));
        }

        foreach (var name in new[] { nameof(CanSubmit), nameof(Mode), nameof(Heading), nameof(IsEdit), nameof(HasPicker), nameof(CanUnpin), nameof(FolderRulesNote), nameof(HasFolderRulesNote), nameof(RenameNote), nameof(HasRenameNote), nameof(EmptyNote), nameof(HasEmptyNote), nameof(SubmitText), nameof(UnpinConfirmText) })
        {
            OnChanged(name);
        }

        NameFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FillPin(PinView pin)
    {
        (_self, _neverApproved, _folderRules) = (pin.Id, string.Equals(pin.State, PinStates.Waiting, StringComparison.Ordinal), pin.FolderRules);
        (Name, Definition) = (pin.Name, pin.Summary ?? string.Empty);
        foreach (var alias in pin.Aliases)
        {
            Words.Add(Watched(Checked(alias, typed: false)));
        }

        foreach (var key in pin.Features)
        {
            Features.Add(Watched(new PinFeatureRow(key, key, ticked: true)));
        }
    }

    private void FillCard(ConceptTileViewModel card)
    {
        _self = card.Id;
        (Name, Definition) = (card.Name, card.Summary);
        foreach (var term in card.Terms.Take(WordsShown))
        {
            Words.Add(Watched(new PinWordRow(term, newRecords: null, ticked: true, counting: true)));
        }
    }

    // Pin this: the terms arrive with their counts (20 at most, a too-broad one unticked, another pin's word refused) and the concept's member features.
    internal void FillTerms(IReadOnlyList<PinSuggestTerm> terms, IReadOnlyList<PinSuggestFeature> features)
    {
        _filling = true;
        try
        {
            var typed = Words.Where(w => w.IsTyped).ToList();
            var ticks = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var row in Words)
            {
                _ = ticks.TryAdd(PinWordRules.Normalized(row.Word), row.Ticked);   // an untick made while the counts were out survives them
            }

            Words.Clear();
            foreach (var term in terms.Take(WordsShown))
            {
                var refusal = PinWordRules.Refusal(term.Word, _pins, _self);
                var note = refusal ?? (term.NewRecords > TooBroad ? Labels.PinSheetTooBroad(term.NewRecords) : null);
                Words.Add(Watched(new PinWordRow(term.Word, term.NewRecords, ticked: note is null && ticks.GetValueOrDefault(PinWordRules.Normalized(term.Word), defaultValue: true), note) { IsRefused = refusal is not null }));
            }

            foreach (var row in typed.Where(t => Words.All(w => !string.Equals(PinWordRules.Normalized(w.Word), PinWordRules.Normalized(t.Word), StringComparison.Ordinal))))
            {
                Words.Add(row);
            }

            WordsNote = string.Empty;
            foreach (var feature in features.Where(f => Features.All(h => !string.Equals(h.Key, f.Key, StringComparison.Ordinal))))
            {
                Features.Add(Watched(new PinFeatureRow(feature.Key, feature.Label, ticked: true)));
            }
        }
        finally
        {
            _filling = false;
        }

        OnWordsChanged();
    }

    private void OnWordsChanged()
    {
        OnChanged(nameof(HasWordsNote));
        OnChanged(nameof(EmptyNote));
        OnChanged(nameof(HasEmptyNote));
    }

    // "Pin and approve" when nothing else waits for approval (only this pin, or nothing), else "Pin"; an unreadable profile reads as something waiting.
    private async Task ReadRule(CancellationToken cancellationToken)
    {
        var waiting = await Ports.Flow.Waiting(cancellationToken).ConfigureAwait(true);
        _approve = waiting is not null && waiting.OnlyMine(Mine);
        OnChanged(nameof(SubmitText));
        OnChanged(nameof(UnpinConfirmText));
    }

    private async Task ReadTerms(CancellationToken cancellationToken)
    {
        if (_open?.Card is not { } card || Ports.ApiUrl() is not { } url)
        {
            return;
        }

        var read = await Ports.Api.ContextPinSuggest(url, new PinSuggestQuery(_repo, Concept: card.Id), cancellationToken).ConfigureAwait(true);
        if (read.IsSuccess && read.Value.Terms is { } terms)
        {
            FillTerms(terms, read.Value.Features ?? []);
            return;
        }

        _filling = true;
        try
        {
            Words.Clear();
        }
        finally
        {
            _filling = false;
        }

        WordsNote = Labels.PinSheetTermsUnreadable;
        OnWordsChanged();
    }

    // The word as a row: refused unticked with its reason, or ticked.
    private PinWordRow Checked(string word, bool typed)
    {
        var refusal = PinWordRules.Refusal(word, _pins, _self);
        return new PinWordRow(word, newRecords: null, ticked: refusal is null, refusal, counting: false, typed) { IsRefused = refusal is not null };
    }

    // The pins moved under the sheet: every word is judged again, and only a row whose refusal changed is replaced (the others keep their tick).
    private void Rejudge()
    {
        _filling = true;
        try
        {
            for (var i = 0; i < Words.Count; i++)
            {
                var row = Words[i];
                var refusal = PinWordRules.Refusal(row.Word, _pins, _self);
                if (string.Equals(refusal, row.IsRefused ? row.Note : null, StringComparison.Ordinal))
                {
                    continue;
                }

                var note = refusal ?? (row.NewRecords > TooBroad ? Labels.PinSheetTooBroad(row.NewRecords.Value) : null);
                Words[i] = Watched(new PinWordRow(row.Word, row.NewRecords, ticked: false, note, row.IsCounting, row.IsTyped) { IsRefused = refusal is not null });
            }
        }
        finally
        {
            _filling = false;
        }

        OnWordsChanged();
    }

    // The write's key paths: this pin's, by the ids it may get (Edit keeps its id; a card's or a name's derived one).
    private bool Mine(string path) => Ids().Any(id => path.EndsWith(".concepts." + id, StringComparison.Ordinal));

    private IEnumerable<string> Ids()
    {
        if (_open?.Pin is { } pin)
        {
            return [pin.Id];
        }

        return _open?.Card is { } card ? [card.Id, Slug(Name)] : [Slug(Name)];
    }

    // The id the server derives from a name (lower case, runs of other characters as one dash); a different one only means the pin waits for Review and approve.
    private static string Slug(string name)
    {
        var slug = new System.Text.StringBuilder();
        foreach (var c in name.Trim().ToLower(CultureInfo.InvariantCulture))
        {
            _ = char.IsLetterOrDigit(c) ? slug.Append(c) : slug.Length > 0 && slug[^1] != '-' ? slug.Append('-') : slug;
        }

        return slug.ToString().Trim('-');
    }
}
