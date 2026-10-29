namespace Aw.Sindri.Features.GetStatus;

/// <summary>Whether Jev can be asked on this box (a TypeSafe API key is set, or the keychain holds one), for the status's label-choice verdict. The Host answers; where nothing is registered the key counts as present.</summary>
public interface IJevKeyProbe
{
    /// <summary>Whether a Jev key is available.</summary>
    /// <returns>True when it is.</returns>
    public bool Present();
}
