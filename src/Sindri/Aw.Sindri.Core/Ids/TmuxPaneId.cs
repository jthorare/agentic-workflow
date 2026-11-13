using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>A tmux pane id, '%N' as 'list-panes -F #{pane_id}' prints it. A target such as 'session:window.pane' is not a pane id: the host only ever returns the '%N' form.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<TmuxPaneId>))]
public readonly partial struct TmuxPaneId : ITypedId<TmuxPaneId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A tmux pane id is '%' and digits, like '%3'");

    [GeneratedRegex("^%[0-9]{1,9}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
