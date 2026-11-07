namespace Aw.Context.Core.Linear;

/// <summary>Where the write key stands (Linear hub spec 2026-11-04 D3): what doctor's 'write key:' line says. One of <see cref="LinearWriteKeyResolves"/>, <see cref="LinearWriteKeyNotSet"/>, <see cref="LinearWriteKeyRefused"/> or <see cref="LinearWriteKeySameAsReadKey"/>. Nothing here calls Linear.</summary>
public abstract record LinearWriteKeyStatus
{
    private protected LinearWriteKeyStatus()
    {
    }
}
