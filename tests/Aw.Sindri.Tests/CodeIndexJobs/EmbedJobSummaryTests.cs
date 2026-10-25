using Aw.Sindri.Features;
using Aw.Sindri.Features.CodeIndexJobs;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The embed job's detail line names what the run made and what it carried over, so a full re-embed (issue 990) is visible from the job.</summary>
public sealed class EmbedJobSummaryTests
{
    [Fact]
    public void A_pending_run_names_the_vectors_it_made_and_the_ones_it_carried()
    {
        var result = new EmbedCodeIndexResult("demo", EmbedCodeIndexResult.Pending, Embedded: 41_540, Embeddable: 41_540, Carried: 41_228, Made: 312, Milliseconds: 5);

        Assert.Equal("demo: embedded 312, carried 41,228 of 41,540", CodeIndexJobServiceCollectionExtensions.EmbedSummary(result));
    }

    [Fact]
    public void A_busy_index_embeds_nothing()
    {
        var result = new EmbedCodeIndexResult("demo", EmbedCodeIndexResult.Later, Embedded: 0, Embeddable: 0, Carried: 0, Made: 0, Milliseconds: 5);

        Assert.Equal("demo: the index was busy or not built, nothing embedded", CodeIndexJobServiceCollectionExtensions.EmbedSummary(result));
    }
}
