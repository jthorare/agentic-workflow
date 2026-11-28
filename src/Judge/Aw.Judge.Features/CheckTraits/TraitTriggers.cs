using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// A trait's 'trigger': the excerpt's text (its +, - and context lines) must match it before the trait is asked about the
/// excerpt. A trait with none always applies. An expression that doesn't compile, or a match that runs past a second, is
/// logged once per trait and run ('trait-trigger-invalid') and the trait applies as if it had none. What was skipped is
/// one 'trigger-skipped' line per run, with the count per trait.
/// </summary>
/// <param name="log">The trait-check log.</param>
internal sealed class TraitTriggers(ITraitCheckLog log)
{
    private static readonly TimeSpan _matchTimeout = TimeSpan.FromSeconds(1);

    // Null for a trait whose trigger is absent or unusable.
    private readonly Dictionary<Trait, Regex?> _compiled = [];
    private readonly Dictionary<string, int> _skipped = new(StringComparer.Ordinal);

    /// <summary>Whether the trait is asked about the source; a skip is counted.</summary>
    /// <param name="trait">The trait.</param>
    /// <param name="source">The excerpt.</param>
    /// <returns>False when the trait has a usable trigger the source doesn't match.</returns>
    internal bool Applies(Trait trait, DiffSource source)
    {
        if (Compiled(trait) is not { } trigger)
        {
            return true;
        }

        try
        {
            if (trigger.IsMatch(source.Text))
            {
                return true;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            Invalid(trait, "the match ran past 1 second");
            return true;
        }

        _skipped[trait.Name] = _skipped.GetValueOrDefault(trait.Name) + 1;
        return false;
    }

    /// <summary>Logs the run's skips as one line, when there were any.</summary>
    /// <param name="mode">The check's mode.</param>
    internal void LogSkipped(TraitCheckMode mode)
    {
        if (_skipped.Count == 0)
        {
            return;
        }

        var traits = new JsonObject();
        foreach (var (name, count) in _skipped.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            traits[name] = count;
        }

        log.Append(new JsonObject { ["event"] = "trigger-skipped", ["mode"] = mode.ToString().ToLowerInvariant(), ["count"] = _skipped.Values.Sum(), ["traits"] = traits });
    }

    private Regex? Compiled(Trait trait)
    {
        if (_compiled.TryGetValue(trait, out var known))
        {
            return known;
        }

        Regex? compiled = null;
        if (trait.Trigger is { } pattern)
        {
            try
            {
                compiled = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Multiline, _matchTimeout);
            }
            catch (ArgumentException e)
            {
                Invalid(trait, e.Message);
            }
        }

        return _compiled[trait] = compiled;
    }

    // Logged once; the trait is judged without its trigger from here on.
    private void Invalid(Trait trait, string error)
    {
        _compiled[trait] = null;
        log.Append(new JsonObject { ["event"] = "trait-trigger-invalid", ["trait"] = trait.Name, ["file"] = trait.File, ["error"] = error });
    }
}
