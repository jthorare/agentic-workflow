using System.Text;

namespace Aw.Spike.E15;

/// <summary>The N1b fixture notes, written as raw bytes at run time (a BOM and mixed endings do not survive every editor or git setting).</summary>
internal static class Fixtures
{
    public const string Demo =
        "# E15 spike\n\nSome **bold**, *italic* and `code` text with a [link](https://example.com).\n\n"
        + "- item one\n- [ ] open task\n- [x] done task\n\n> a quote\n\n"
        + "| a | b |\n|---|---|\n| 1 | 2 |\n\n```\nfenced code\n```\n\n"
        + "Fixes [[issue 123]] and [[PR 9]] today.\nNext line.\n";

    public static (string Name, byte[] Bytes)[] All { get; } =
    [
        ("crlf.md", Encoding.UTF8.GetBytes("# Title\r\n\r\n- item one\r\n- [ ] task\r\n\r\nDone [[issue 123]] end\r\n")),
        ("mixed.md", Encoding.UTF8.GetBytes("# Mixed\r\nline two\nline three\rline four\r\nlast\n")),
        ("bom-tabs.md", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Tabs\n\t- indented\twith tab\n\n```\n\tcode\n```\n")]),
        ("no-final-newline.md", Encoding.UTF8.GetBytes("# No final\nlast line without newline")),
        ("lf.md", Encoding.UTF8.GetBytes("# Plain LF\n\ntext\n")),
    ];

    public static string BigText(int lines)
    {
        var sb = new StringBuilder(lines * 40);
        for (var i = 0; i < lines; i++)
        {
            sb.Append(
                (i % 10) switch
                {
                    0 => $"## Heading {i}",
                    1 or 2 => $"- item {i} with **bold** and `code`",
                    3 => $"- [ ] task {i} for [[issue {i}]]",
                    4 => $"| col {i} | value |",
                    5 => "```",
                    6 => $"code line {i}",
                    7 => "```",
                    _ => $"plain text line {i} with some words in it",
                }).Append('\n');
        }
        return sb.ToString();
    }
}
