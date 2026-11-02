using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri workflow status &lt;instance&gt; [--json]' (engine spec §6.3, §6.4; engine P4 unit (e)): the §6.4 layout (the served
/// 'current.status.text'), or with '--json' the status fields ('current.status') of the instance detail. A client of serve's routes (review run 2): the reference resolves to
/// the instance's id through 'GET /workflow/instances?address=', then 'GET /workflow/instances/{id}' answers; it never
/// starts serve. Exit 0; 1 when Sindri isn't running (SND-API-001); 2 on any other error
/// (SND-WF-001 no such instance or a reference that names none, SND-API-002 no worker answered, SND-CLI-003 a missing argument). A bare item
/// names the root of its latest attempt, as in the write verbs (spec §6.1).
/// </summary>
internal static class StatusCommand
{
    private const string Usage = "usage: sindri workflow status <instance> [--json]";

    internal static async Task<int> Run(string[] args, RecipeDesk desk, Output output, CancellationToken ct)
    {
        var json = args.Contains("--json", StringComparer.Ordinal);
        if (args.Where(static a => a != "--json").ToArray() is not [{ Length: > 0 } text] || text.StartsWith("--", StringComparison.Ordinal))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"the one argument is the instance: its address ('{{item}}/{{recipe}}/{{attempt}}', e.g. T-1/step/1) or a bare item ({Usage})")).ConfigureAwait(false);
        }

        var id = await InstanceReads.IdOf(desk, text, ct).ConfigureAwait(false);
        var got = id.IsError ? id.Error : await desk.Client.Get<InstanceDetail>($"{InstanceReads.Instances}/{InstanceRoute.Segment(id.Value.Value)}", ct).ConfigureAwait(false);
        if (got.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(got.Error)).ConfigureAwait(false);
        }

        var status = got.Value.Value.Current.Status;
        var asked = await DeskQuestions.Read(desk, id.Value, got.Value.Value, ct).ConfigureAwait(false);
        if (asked.IsError)
        {
            return await output.Error(asked.Error).ConfigureAwait(false);
        }

        if (asked.Value is not { } body)
        {
            return json
                ? await output.Json(status, 0).ConfigureAwait(false)
                : await output.Text(status.Text, 0).ConfigureAwait(false);
        }

        var questions = DeskQuestions.Questions(body, fenced: await desk.Client.ResolveSessionReason(ct).ConfigureAwait(false) is not null);
        return json
            ? await output.JsonWith(status, "questions", questions, 0).ConfigureAwait(false)
            : await output.Text(DeskQuestions.Render(status.Text, questions), 0).ConfigureAwait(false);
    }
}
