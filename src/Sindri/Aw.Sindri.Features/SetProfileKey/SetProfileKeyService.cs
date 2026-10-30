using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetProfileKey;

/// <summary>Plans the edit, checks the edited text the way 'profile validate' checks the file (the profile's rules, the context section's, the model keys) and only then writes it. The profile then waits for the owner's approval; this approves nothing.</summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="context">Checks the context section.</param>
/// <param name="models">Checks the model keys against the catalog; null: not checked.</param>
internal sealed class SetProfileKeyService(IProfileKeyWriter writer, IProfileTextParser parser, IContextEngine context, ModelKeyChecks? models = null) : IService<SetProfileKeyRequest, SetProfileKeyResponse>
{
    public async ValueTask<Result<SetProfileKeyResponse>> Handle(SetProfileKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = writer.Plan(request.Key, request.Value);
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed)
        {
            if (await Problem(plan.Value, cancellationToken).ConfigureAwait(false) is { } problem)
            {
                return problem;   // SND-PROFILE-002 / SND-CONTEXT-001 / SND-MODEL-001, with the key path; nothing was written
            }
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : new SetProfileKeyResponse(saved.Value, request.Key, request.Value, plan.Value.Changed);
    }

    private async ValueTask<Error?> Problem(ProfileKeyEdit edit, CancellationToken cancellationToken)
    {
        var loaded = parser.Parse(edit.Text, edit.Path);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var profile = loaded.Value.Profile;
        var checkedContext = context.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))]);
        return checkedContext.IsError
            ? checkedContext.Error
            : models is null ? null : await models.Models(profile, cancellationToken).ConfigureAwait(false);
    }
}
