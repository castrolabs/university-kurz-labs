namespace MfaLab;

public enum VerifyResult
{
    Ok,
    Invalid,
    Replay,
    Locked,
}

/// <summary>
/// What a server keeps for one user's authenticator app: the secret, the last accepted step and a failure count.
/// The RFC math is not enough; the window, the replay rule and the attempt limit are the verifier's job.
/// </summary>
public sealed class TotpVerifier(byte[] secret, TimeProvider time, int window = 1, int maxFailures = 5, int digits = 6)
{
    private long _lastStep = -1;
    private int _failures;

    public long LastStep => _lastStep;
    public int Failures => _failures;

    public VerifyResult Verify(string? code)
    {
        if (_failures >= maxFailures) return VerifyResult.Locked;

        var current = (long)Totp.Step(time.GetUtcNow().ToUnixTimeSeconds());
        if (code is { Length: var length } && length == digits && code.All(char.IsAsciiDigit))
        {
            for (var step = Math.Max(0, current - window); step <= current + window; step++)
            {
                if (!CodeComparer.Equal(Hotp.Compute(secret, (ulong)step, digits), code)) continue;
                if (step <= _lastStep) return VerifyResult.Replay;
                _lastStep = step;
                _failures = 0;
                return VerifyResult.Ok;
            }
        }

        _failures++;
        return VerifyResult.Invalid;
    }
}
