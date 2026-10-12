using System.Text.Json.Nodes;
using Aw.Judge.Core.Briefs;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Questions;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SaveBrief;

// The goal, criteria and proof command are stored like a question's input: redacted (a proof command is where a token
// most often sits), then capped at UntrustedText.BriefCap, so decisions.sqlite holds no secret and no unbounded text.
internal sealed class SaveBriefService(IBriefStore briefs, TimeProvider clock, IInputRedactor redactor) : IService<SaveBriefRequest, SaveBriefResponse>
{
    public async ValueTask<Result<SaveBriefResponse>> Handle(SaveBriefRequest request, CancellationToken cancellationToken)
    {
        var brief = new Brief
        {
            ToolUseId = request.ToolUseId,
            SessionId = request.SessionId,
            PromptId = request.PromptId,
            DispatchName = request.DispatchName,
            SubagentType = request.SubagentType,
            Goal = Stored(request.Goal),
            AcceptanceCriteria = Stored(request.AcceptanceCriteria),
            ProofCommand = Stored(request.ProofCommand),
            SavedAt = clock.GetUtcNow(),
        };
        var saved = await briefs.Save(brief, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new SaveBriefResponse(brief);
    }

    // Redact first: a secret cut at the cap would no longer match its pattern.
    private string Stored(string text) =>
        InputCap.Truncate(redactor.Redact(JsonValue.Create(text))?.GetValue<string>() ?? "", UntrustedText.BriefCap);
}
