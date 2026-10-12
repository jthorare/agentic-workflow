namespace Aw.Sindri.Host.Commands;

/// <summary>The banner bare 'sindri' prints (ASCII only, so it reads the same in any terminal and in a log).</summary>
internal static class Banner
{
    internal static readonly string Text = string.Join('\n',
        @"      _           _      _ ",
        @"  ___(_)_ __   __| |_ __(_)",
        @" / __| | '_ \ / _` | '__| |",
        @" \__ \ | | | | (_| | |  | |",
        @" |___/_|_| |_|\__,_|_|  |_|");
}
