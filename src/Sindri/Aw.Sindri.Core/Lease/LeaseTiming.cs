namespace Aw.Sindri.Core.Lease;

/// <summary>The lease's timing (factory addendum §17.22 D2): a 60 s TTL, renewed every 20 s.</summary>
public static class LeaseTiming
{
    /// <summary>How long a grant lasts without a renewal.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    /// <summary>How often the holder renews.</summary>
    public static readonly TimeSpan RenewEvery = TimeSpan.FromSeconds(20);
}
