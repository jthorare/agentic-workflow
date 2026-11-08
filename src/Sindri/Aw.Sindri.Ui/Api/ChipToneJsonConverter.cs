namespace Aw.Sindri.Ui.Api;

/// <summary>Reads and writes a <see cref="ChipTone"/> as its wire word; a tone this build doesn't know (or a non-string) reads as neutral.</summary>
internal sealed class ChipToneJsonConverter() : WordEnumJsonConverter<ChipTone>(
    [(ChipTone.Attn, "attn"), (ChipTone.Fail, "fail"), (ChipTone.Ok, "ok"), (ChipTone.Neutral, "neutral")],
    ChipTone.Neutral);
