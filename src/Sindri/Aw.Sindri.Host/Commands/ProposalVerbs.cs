using System.Globalization;
using System.Text;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.GetProposal;
using Aw.Sindri.Features.ListProposals;
using Aw.Sindri.Features.RejectProposal;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri proposals [--state S]', 'sindri proposal show &lt;id&gt;' and 'sindri proposal reject &lt;id&gt;' (issue 3787, piece 3), each with --json: clients of the proposal routes.
/// The reads never start the control plane; reject is the owner's and may start it. A proposal's text is model output over transcript text, so it is fenced as untrusted
/// when stdout isn't a terminal (an agent reading it) or with --fence.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
/// <param name="stdoutIsTerminal">Whether stdout is a terminal; the default asks the console.</param>
internal sealed class ProposalVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null, Func<bool>? stdoutIsTerminal = null)
{
    internal const string Usage = "usage: sindri proposals [--state proposed|staged|held|published|merged|rejected] [--json] [--fence|--no-fence] | proposal show <id> [--json] [--fence|--no-fence] | proposal reject <id> [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (ProposalVerbLine.Parse(args) is not { } line)
        {
            return await output.Error(SindriErrors.InvalidInput.With(Usage)).ConfigureAwait(false);
        }

        var down = ensureRunning is null ? null : await ensureRunning(line.Verb == "reject", ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var fence = line.Fence ?? !(stdoutIsTerminal?.Invoke() ?? !Console.IsOutputRedirected);
        var path = Uri.EscapeDataString(line.Id ?? string.Empty);
        return line.Verb switch
        {
            "list" => await Show(await client.Get<ListProposalsResponse>("/sindri/proposals" + UrlQuery.Of([("state", line.State)]), ct).ConfigureAwait(false), line.Json, l => ListText(l, fence)).ConfigureAwait(false),
            "show" => await Show(await client.Get<GetProposalResponse>($"/sindri/proposals/{path}", ct).ConfigureAwait(false), line.Json, p => ShowText(p, fence)).ConfigureAwait(false),
            _ => await Show(await client.Post<object?, RejectProposalResponse>($"/sindri/proposals/{path}/reject", body: null, ct).ConfigureAwait(false), line.Json, static r => $"{r.Proposal.Id.Value} rejected\n").ConfigureAwait(false),
        };
    }

    internal static string ListText(ListProposalsResponse list, bool fence)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Proposals.Count == 0)
        {
            return "no proposals\n";
        }

        var text = new StringBuilder();
        foreach (var p in list.Proposals)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{p.Id.Value}  {Word(p.State)}  {Word(p.Tier)}  {Word(p.Kind)}  {Untrusted(p.Target, fence)}  {Untrusted(p.Title, fence)}");
            _ = (p.HeldReason is { } reason ? text.Append(CultureInfo.InvariantCulture, $"  ({reason}, attempt {p.Attempts})") : text).Append('\n');
        }

        return text.ToString();
    }

    internal static string ShowText(GetProposalResponse shown, bool fence)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var p = shown.Proposal;
        var text = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{p.Id.Value}  {Word(p.State)}  {Word(p.Tier)}  {Word(p.Kind)}\n")
            .Append("target:    ").Append(Untrusted(p.Target, fence)).Append('\n')
            .Append("title:     ").Append(Untrusted(p.Title, fence)).Append('\n')
            .Append("rationale: ").Append(Untrusted(shown.Rationale, fence)).Append('\n')
            .Append("change:    ").Append(Untrusted(shown.Change, fence)).Append('\n')
            .Append("files:     ").Append(Untrusted(string.Join(", ", shown.Files), fence)).Append('\n')
            .Append("evidence:  ").AppendJoin(", ", shown.Evidence.Select(e => Untrusted(e, fence))).Append('\n')
            .Append("sessions:  ").AppendJoin(", ", shown.Sessions).Append('\n')
            .Append("origin:    ").Append(shown.Origin).Append('\n');
        _ = shown.Draft is { } draft ? text.Append("draft:     ").Append(SafeText.Line(SafeText.Escape(draft.Branch), 200)).Append(" @ ").Append(SafeText.Line(SafeText.Escape(draft.Commit), 80)).Append('\n') : text;
        return (p.HeldReason is { } reason ? text.Append(CultureInfo.InvariantCulture, $"held:      {reason}, attempt {p.Attempts}\n") : text).ToString();
    }

    private static string Word<TEnum>(TEnum value)
        where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    // why: model output over transcript text; escaped always, fenced for a reader that may be an agent
    private static string Untrusted(string text, bool fence)
    {
        var safe = SafeText.Line(SafeText.Escape(text), 2000);
        return fence ? UntrustedFence.Wrap("proposal", author: null, safe) : safe;
    }

    private async Task<int> Show<T>(Result<ApiAnswer<T>> answer, bool json, Func<T, string> render)
    {
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return json ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false) : await output.Text(render(answer.Value.Value), 0).ConfigureAwait(false);
    }
}
