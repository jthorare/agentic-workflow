using Aw.Kernel.Testing;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

[Lane(TestLane.Unit)]
public sealed class ContextInputSectionsTests
{
    // A code Step reads the typed result, not the text: a big map's lenses view is far over 256 KiB as JSON, and neither the text nor the result is cut.
    [Fact]
    public void A_text_and_a_typed_result_of_any_size_reach_the_Step_whole()
    {
        var view = new List<string> { new('x', 3 * 1024 * 1024) };
        var text = string.Concat(Enumerable.Repeat("a line of the readable text\n", 40_000));

        var filled = ContextInputSections.Filled("context.lenses", text, [], view, new System.Text.Json.JsonSerializerOptions { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() });

        Assert.Equal(text, filled.Sections["context.lenses"]);
        Assert.Equal(3 * 1024 * 1024, filled.Result!.Value[0].GetString()!.Length);
    }
}
