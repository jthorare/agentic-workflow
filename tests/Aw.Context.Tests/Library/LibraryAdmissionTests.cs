using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

/// <summary>Which files the library feed reads, decided from their first 4 KiB (PR review page spec 2026-11-04 §4.1).</summary>
public sealed class LibraryAdmissionTests
{
    private const string Rest = "id: ferry-booking\nkind: plan\ntitle: \"Harbor ferry booking\"\nstatus: draft\n---\n\n# Harbor ferry booking\n";

    [Theory]
    [InlineData("---\naw-doc: 1\n" + Rest, LibraryAdmissionKind.Read)]
    [InlineData("---\naw-doc: 2\n" + Rest, LibraryAdmissionKind.Read)]   // a later convention version is stored with SND-LIB-010 and no items, not a near miss
    [InlineData("---\naw-doc: 1\nid: cut-off-by-the-4-KiB-head\n", LibraryAdmissionKind.Read)]   // the front matter never closes inside the head
    [InlineData("# Harbor ferry booking\n\nNo front matter.\n", LibraryAdmissionKind.Skip)]
    [InlineData("---\ntitle: \"Harbor ferry booking\"\n---\n\n# No aw-doc key\n", LibraryAdmissionKind.Skip)]
    [InlineData("", LibraryAdmissionKind.Skip)]
    [InlineData("---\naw-doc: \"1\"\n" + Rest, LibraryAdmissionKind.NearMiss)]
    [InlineData("---\naw-doc: 1.0\n" + Rest, LibraryAdmissionKind.NearMiss)]
    [InlineData("---\naw-doc: yes\n" + Rest, LibraryAdmissionKind.NearMiss)]
    [InlineData("\n---\naw-doc: 1\n" + Rest, LibraryAdmissionKind.NearMiss)]   // aw-doc: 1 after a blank first line
    public void A_file_is_read_skipped_or_counted_as_a_near_miss_by_its_head(string head, LibraryAdmissionKind expected) =>
        Assert.Equal(expected, LibraryAdmission.Classify("plans/ferry-booking.md", head));

    [Fact]
    public void A_near_miss_gives_its_019_with_the_line_what_is_wrong_and_the_fix()
    {
        var miss = LibraryAdmission.NearMiss("plans/ferry-booking.md", "---\naw-doc: \"1\"\n" + Rest);

        Assert.NotNull(miss);
        Assert.Equal((DesignDocCodes.NearMiss, 2), (miss.Code, miss.Line));
        Assert.Equal("plans/ferry-booking.md:2: aw-doc is \"1\", not the number 1, so the file isn't read.", miss.Message);
        Assert.Equal("Write aw-doc: 1 with no quotes, on the line after the first ---.", miss.Fix);
    }

    [Theory]
    [InlineData("---\naw-doc: 1\n" + Rest)]
    [InlineData("---\naw-doc: 2\n" + Rest)]
    [InlineData("# No front matter\n")]
    public void A_read_or_skipped_file_has_no_near_miss(string head) =>
        Assert.Null(LibraryAdmission.NearMiss("plans/ferry-booking.md", head));
}
