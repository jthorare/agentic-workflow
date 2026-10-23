using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Workflow.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>A report retried with its nonce after a timeout (engine spec rev 8 §5.2, review run 1): the claim stays and the same Update id is resent.</summary>
[Lane(TestLane.Unit)]
public sealed class CompleteStepRetryTests
{
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('d', 32));

    private readonly TemporalLikeRuntime _instance = new();
    private readonly InMemoryCommandNonces _nonces = new(new FakeTimeProvider());

    [Fact(Timeout = 30_000)]
    public async Task A_report_retried_after_a_timeout_that_did_apply_is_applied_and_keeps_its_claim()
    {
        var service = new CompleteStepService(_instance, _instance, _instance, new InMemoryFactStore(), _nonces, _instance, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_instance, new FakeLauncher())));
        var request = Report();
        _instance.LoseNextResponse();

        var first = await service.Handle(request, TestContext.Current.CancellationToken);
        var retry = await service.Handle(request, TestContext.Current.CancellationToken);

        Assert.Equal("SND-API-002", first.IsError ? first.Error.Code : "accepted");
        Assert.False(retry.IsError, retry.IsError ? retry.Error.Message : null);
        Assert.Equal(1, _instance.Applied);
        Assert.Equal([_nonce, _nonce], _instance.Sent);
        Assert.Equal(NonceClaim.Applied, (await _nonces.Claim(_nonce, _instance.Address.Value + "\n" + ReportSha(), TestContext.Current.CancellationToken)).Value);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_retry_with_a_pending_claim_is_sent_again_while_the_instance_cannot_be_read()
    {
        var service = new CompleteStepService(_instance, _instance, _instance, new InMemoryFactStore(), _nonces, _instance, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_instance, new FakeLauncher())));
        var request = Report();
        _instance.LoseNextResponse();
        _ = await service.Handle(request, TestContext.Current.CancellationToken);
        _instance.GetFails = Core.SindriErrors.ApprovalsUnavailable.With("no worker (test)");

        var retry = await service.Handle(request, TestContext.Current.CancellationToken);

        Assert.False(retry.IsError, retry.IsError ? retry.Error.Message : null);
        Assert.Equal([_nonce, _nonce], _instance.Sent);
        Assert.Equal(1, _instance.Applied);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_refused_report_gives_its_fresh_nonce_back()
    {
        var service = new CompleteStepService(_instance, _instance, _instance, new InMemoryFactStore(), _nonces, _instance, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_instance, new FakeLauncher())));
        _instance.RefuseNext();

        var refused = await service.Handle(Report(), TestContext.Current.CancellationToken);
        var other = await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken);

        Assert.Equal("SND-WF-003", refused.IsError ? refused.Error.Code : "accepted");
        Assert.Equal(NonceClaim.Fresh, other.Value);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(ApprovalCheck.Revoked, "SND-RECIPE-005")]
    [InlineData(ApprovalCheck.Drifted, "SND-RECIPE-006")]
    public async Task A_report_on_an_instance_whose_recipe_is_no_longer_approved_is_refused_and_gives_its_nonce_back(ApprovalCheck approval, string code)
    {
        var facts = new InMemoryFactStore();
        var service = new CompleteStepService(_instance, _instance, _instance, facts, _nonces, _instance, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_instance, new FakeLauncher())));
        _instance.Approval = approval;

        var refused = await service.Handle(Report(), TestContext.Current.CancellationToken);
        var other = await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken);

        Assert.Equal(code, refused.IsError ? refused.Error.Code : "accepted");
        Assert.Empty(_instance.Sent);
        Assert.Empty(facts.Stored);   // nothing was stored for a report that was refused
        Assert.Equal(NonceClaim.Fresh, other.Value);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_report_whose_approval_check_cannot_be_made_gives_the_error_and_its_nonce_back_and_stores_nothing(bool instanceQueryFails)
    {
        var facts = new InMemoryFactStore();
        var service = new CompleteStepService(_instance, _instance, _instance, facts, _nonces, _instance, new EngineFilledReport(EvalComposition.Catalog, EvalComposition.Options, new FakeLaunchFiles(), new ReportOutputTypes(_instance, new FakeLauncher())));
        var failure = Core.SindriErrors.ApprovalsUnavailable.With("approved/latest can't be read (test)");
        _instance.GetFails = instanceQueryFails ? failure : null;
        _instance.CheckFails = instanceQueryFails ? null : failure;

        var refused = await service.Handle(Report(), TestContext.Current.CancellationToken);
        var other = await _nonces.Claim(_nonce, "another command", TestContext.Current.CancellationToken);

        Assert.Equal(failure.Code, refused.IsError ? refused.Error.Code : "accepted");
        Assert.Empty(_instance.Sent);
        Assert.Empty(facts.Stored);
        Assert.Equal(NonceClaim.Fresh, other.Value);
    }

    private static CompleteStepRequest Report() =>
        new("T-1/step/1", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);

    private static string ReportSha() => CompleteStepService.ReportSha(Report(), []);
}
