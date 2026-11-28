using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The token-budget seams of the rig (#3805): the model the shipped 'prompt' block asks, and the profile's default budget.</summary>
internal sealed partial class GraphRig
{
    /// <summary>The model behind the rig's 'prompt' nodes; each answer reports <see cref="CountingModel.TokensPerAnswer"/> tokens.</summary>
    public CountingModel Model { get; } = new();

    /// <summary>The profile's 'workflow.budget.tokens' as the run activities read it; null sets no cap.</summary>
    public DefaultBudget Budgets { get; } = new();

    /// <summary>A model that answers "ok" with a fixed token count and counts how often it was asked.</summary>
    internal sealed class CountingModel : IGraphModelPort
    {
        private int _calls;

        /// <summary>Gets or sets the tokens each answer reports.</summary>
        public long TokensPerAnswer { get; set; }

        /// <summary>Gets how many times the model was asked.</summary>
        public int Calls => Volatile.Read(ref _calls);

        public string Untrusted(string source, string text) => text;

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            return ValueTask.FromResult<Result<string>>("ok");
        }

        ValueTask<Result<ModelAnswer>> IGraphModelPort.Answer(string prompt, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _calls);
            return ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer("ok") { Tokens = TokensPerAnswer });
        }
    }

    /// <summary>The profile's default budget, settable by the test.</summary>
    internal sealed class DefaultBudget : IRunBudgetSource
    {
        /// <summary>Gets or sets the default; null sets no cap.</summary>
        public long? Tokens { get; set; }

        public ValueTask<long?> DefaultTokens(CancellationToken cancellationToken) => ValueTask.FromResult(Tokens);
    }
}
