using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the plan and the words for a refusal.</summary>
internal sealed partial class ApproveAllViewModel
{
    private const string SessionRefusal = "SND-AUTH-002";
    private const string RunningRefusal = "SND-JOB-409";
    private const string PlanChangedRefusal = "SND-RECIPE-009";

    /// <summary>The workflows the approval covers, in the order it happens (children first).</summary>
    public IReadOnlyList<PlanRowViewModel> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>The workflows that need their own approval, each with the sentence the server gave.</summary>
    public IReadOnlyList<ApproveAllLine> OwnApproval { get; private set => Set(ref field, value); } = [];

    /// <summary>True when something needs its own approval.</summary>
    public bool HasOwnApproval => OwnApproval.Count > 0;

    /// <summary>"3 workflows will be approved".</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The server's sentence when nothing waits; empty otherwise.</summary>
    public string EmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The plan's digest, id and hashes, for Details (the digest and one line each).</summary>
    public IReadOnlyList<string> DetailLines { get; private set => Set(ref field, value); } = [];

    /// <summary>The refusal or failure in words; empty for none.</summary>
    public string Error
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
            if (value.Length > 0)
            {
                Live.Say(value);
            }
        }
    } = string.Empty;

    /// <summary>The refusal's code, shown after the words in muted text; empty for none.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when <see cref="Error"/> shows.</summary>
    public bool HasError => Error.Length > 0;

    /// <summary>A line that is not a refusal (the plan changed); empty for none.</summary>
    public string Notice
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNotice));
            if (value.Length > 0)
            {
                Live.Say(value);
            }
        }
    } = string.Empty;

    /// <summary>True when <see cref="Notice"/> shows.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>True when there is a plan to approve and nothing on its way: the Approve button is live.</summary>
    public bool CanApprove => Phase == ApproveAllPhase.Plan && !IsSending && Rows.Count > 0;

    // Reads the plan's answer: it fills the lists.
    private void ShowPlan(Result<ApproveAllPlan> read)
    {
        if (read.IsError)
        {
            Fail(read.Error);
            return;
        }

        _plan = read.Value;
        Fill(read.Value);
        if (read.Value.Recipes.Count == 0)
        {
            if (EmptyText.Length == 0)
            {
                EmptyText = HasOwnApproval ? Labels.ApproveOnlyOwn : Labels.ApproveNothingWaiting;   // never a blank, nameless line
            }

            Phase = ApproveAllPhase.Empty;
            return;
        }

        Phase = ApproveAllPhase.Plan;
        Live.Say(Labels.ApproveReady);
    }

    private void Fill(ApproveAllPlan plan)
    {
        Rows = [.. plan.Recipes.Select(r => new PlanRowViewModel(r))];
        OwnApproval = [.. plan.OwnApproval.Select(OwnLine)];
        EmptyText = plan.Empty ?? string.Empty;
        Heading = Labels.ApprovePlanHeading(plan.Recipes.Count);
        DetailLines = [.. Rows.Select(r => r.Detail).Prepend("plan " + plan.Digest)];
        OnChanged(nameof(HasOwnApproval));
    }

    private static ApproveAllLine OwnLine(OwnApprovalRecipe own) =>
        new(WorkflowNames.Of(own.Id, own.DisplayName), own.Sentence, Extra: own.Added.Count > 0 ? Labels.ApproveNewlyAllowed(string.Join("; ", own.Added.Select(a => a.Sentence))) : string.Empty);

    // A refusal in words: the sentence first, its code after. An agent session is told in plain words that the app is the person's.
    private void Fail(Error error, bool keepPlan = false)
    {
        ErrorCode = error.Code;
        Error = error.Code switch
        {
            SessionRefusal => Labels.ApproveSessionRefused,
            RunningRefusal => Labels.ApproveAlreadyRunning,
            _ => error.Message,
        };
        Phase = keepPlan ? Phase : ApproveAllPhase.Failed;
        OnChanged(nameof(CanApprove));
    }

    private void ResetPlanState()
    {
        (Rows, OwnApproval, DetailLines) = ([], [], []);
        (Heading, EmptyText, Error, ErrorCode, Notice) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        OnChanged(nameof(HasOwnApproval));
        OnChanged(nameof(HasNotice));
    }
}
