using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>Each repo's index state, for the caller's repos only; a named repo outside them is the uniform not-found (SND-CODEIDX-013).</summary>
internal sealed class GetCodeIndexStatusService(CodeIndexScopes scopes, ICodeIndex index, RepoHealths healths) : IService<GetCodeIndexStatusRequest, CodeIndexStatusView>
{
    public async ValueTask<Result<CodeIndexStatusView>> Handle(GetCodeIndexStatusRequest request, CancellationToken cancellationToken)
    {
        var repo = CodeIndexInputs.Repo(request.Repo);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (repo.IsError || scope.IsError)
        {
            return repo.IsError ? repo.Error : scope.Error;
        }

        var answer = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        var repos = repo.Value is { } wanted ? answer.Value.Repos.Where(r => r.Repo == wanted).ToList() : [.. answer.Value.Repos];
        if (repo.Value is not null && repos.Count == 0)
        {
            // The facade's own words, so a repo outside the scope reads like one never built.
            return CodeIndexErrors.IndexNotBuilt.With(CodeIndexErrors.IndexNotBuilt.Title);
        }

        return new CodeIndexStatusView(repos) { Health = await healths.Of(scope.Value, repos, cancellationToken).ConfigureAwait(false) };
    }
}
