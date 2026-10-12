namespace Aw.Judge.Core.Secrets;

/// <summary>A secret the judge reads. Where each one is kept is the secret source's business.</summary>
public enum SecretName
{
    /// <summary>The TypeSafe API key Jev calls with (TYPESAFE_API_KEY).</summary>
    TypeSafeApiKey = 1,
}
