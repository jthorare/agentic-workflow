using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The added lines read out of a zero-context unified diff.</summary>
public sealed class DiffAddedLinesTests
{
    private const string Diff = """
        diff --git a/src/A.cs b/src/A.cs
        index 111..222 100644
        --- a/src/A.cs
        +++ b/src/A.cs
        @@ -3,0 +4,2 @@ class A
        +first
        +second
        @@ -10 +12 @@ class A
        -old
        +replaced
        diff --git a/gone.txt b/gone.txt
        deleted file mode 100644
        --- a/gone.txt
        +++ /dev/null
        @@ -1 +0,0 @@
        -bye
        diff --git a/new.txt b/new.txt
        new file mode 100644
        --- /dev/null
        +++ b/new.txt
        @@ -0,0 +1 @@
        +hello
        """;

    [Fact, Trait("Lane", "unit")]
    public void Each_added_line_carries_its_file_and_its_line_number_in_the_new_file()
    {
        var added = DiffAddedLines.Parse(Diff);

        Assert.Equal([("src/A.cs", 4, "first"), ("src/A.cs", 5, "second"), ("src/A.cs", 12, "replaced"), ("new.txt", 1, "hello")], added);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_diff_adds_nothing() => Assert.Empty(DiffAddedLines.Parse(string.Empty));
}
