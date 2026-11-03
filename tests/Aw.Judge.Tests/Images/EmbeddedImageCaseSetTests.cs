using Aw.Judge.Core.Images;
using Aw.Judge.Infrastructure.Images;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Images;

/// <summary>
/// The 20 visual-critique cases embedded from tests/fixtures/judge/images: ten pages per label, each a PNG at one of the two rendered sizes, so a
/// renamed or missing fixture fails here and not inside a live 'judge check images' run.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class EmbeddedImageCaseSetTests
{
    // ui-evidence's default viewport, and the same at device pixel ratio 2 (the README's four retina cases).
    private static readonly (ImageFormat, int, int)?[] _sizes = [(ImageFormat.Png, 1512, 982), (ImageFormat.Png, 3024, 1964)];

    [Fact]
    public void The_binary_embeds_ten_cases_per_label_each_a_readable_png()
    {
        var cases = new EmbeddedImageCaseSet().Cases;

        Assert.Equal(20, cases.Count);
        Assert.Equal(10, cases.Count(c => c.Expected == "looks-right"));
        Assert.Equal(10, cases.Count(c => c.Expected == "looks-off"));
        Assert.Equal(20, cases.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, c => Assert.Contains(ImageHeader.Read(c.Bytes.Span), _sizes));
    }
}
