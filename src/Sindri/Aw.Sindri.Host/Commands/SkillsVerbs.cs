using Aw.Sindri.Core;
using Aw.Sindri.Features.GetSkills;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri skills [--json]' (skill flows spec §7): a client of GET /sindri/skills. It reads the route and never starts the control plane. Exit 0 ok (an empty list included), 1 when Sindri isn't running
/// (SND-API-001), 2 on usage or any other error.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class SkillsVerbs(ControlPlaneClient client, Output output)
{
    internal const string Usage = "skills [--json]";

    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not ([] or ["--json"]))
        {
            return await output.Error(SindriErrors.InvalidInput.With($"'{string.Join(' ', args)}' isn't an argument of this command: {Usage}")).ConfigureAwait(false);
        }

        var answer = await client.Get<GetSkillsResponse>("/sindri/skills", ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        return args is ["--json"]
            ? await output.Raw(answer.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(SkillsText.Render(answer.Value.Value), 0).ConfigureAwait(false);
    }
}
