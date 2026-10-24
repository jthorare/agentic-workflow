using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Requests;

/// <summary>The pure rules of filing a request (API spec §3.2 'FileRequest', §4.2, §9).</summary>
public static class RequestRules
{
    /// <summary>The longest text, in Unicode scalars of the raw text (P4 plan G2).</summary>
    public const int TextMax = 64 * 1024;

    /// <summary>The most open requests in one repo (a session caller; P4 plan G4).</summary>
    public const int MaxOpenPerRepo = 200;

    /// <summary>The most open requests overall (a session caller).</summary>
    public const int MaxOpenTotal = 500;

    /// <summary>Checks a request's kind and text.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="text">The text.</param>
    /// <returns>Success, or SND-REQ-001 for an unknown kind, empty text or text over <see cref="TextMax"/> scalars.</returns>
    public static Result<Unit> Validate(RequestKind kind, string? text)
    {
        if (!Enum.IsDefined(kind))
        {
            return SindriErrors.RequestInvalid.With("That isn't a request kind (fix: rescope, add-task, note or handback)");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return SindriErrors.RequestInvalid.With("The request has no text (fix: say what you ask for)");
        }

        return Scalars.Count(text) > TextMax
            ? SindriErrors.RequestInvalid.With(string.Create(CultureInfo.InvariantCulture, $"The text is longer than {TextMax} characters (fix: shorten it)"))
            : Unit.Value;
    }

    /// <summary>Checks the caps against the open requests already filed.</summary>
    /// <param name="openForRepo">Open requests in the new request's repo (0 without a repo).</param>
    /// <param name="openTotal">Open requests overall.</param>
    /// <returns>Success, or SND-REQ-002 when the new one would be the 201st in the repo or the 501st overall.</returns>
    public static Result<Unit> Cap(int openForRepo, int openTotal)
    {
        if (openForRepo >= MaxOpenPerRepo)
        {
            return SindriErrors.RequestsRateLimited.With(string.Create(CultureInfo.InvariantCulture,
                $"{openForRepo} requests are already open in this repo; the limit is {MaxOpenPerRepo} (fix: wait for a human to decide some)"));
        }

        return openTotal >= MaxOpenTotal
            ? SindriErrors.RequestsRateLimited.With(string.Create(CultureInfo.InvariantCulture,
                $"{openTotal} requests are already open; the limit is {MaxOpenTotal} (fix: wait for a human to decide some)"))
            : Unit.Value;
    }

    /// <summary>Whether the answer to a request is recorded and not yet acted on (API spec §3.4): true in P4, until the workflow engine acts on answers (P11).</summary>
    /// <returns>True.</returns>
    public static bool RecordedOnly() => true;
}
