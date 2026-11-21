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
        // TODO-06: Count failures per account. When `_failures` has reached `maxFailures`, answer Locked BEFORE looking at the
        //          code (even the right code is refused). A wrong or malformed code increments `_failures`; an accepted one resets it.
        // TODO-04: Take the current step from `time`. A code is only a candidate when it has exactly `digits` ASCII digits
        //          (anything else is Invalid, never an exception). Compare it with the code of every step from
        //          current - window to current + window, using CodeComparer.Equal.
        // TODO-05: A matching step <= `_lastStep` was already used: answer Replay (a code seen by a shoulder surfer or a
        //          proxy must not work twice). Otherwise remember the step in `_lastStep` and answer Ok.
        throw new NotImplementedException();
    }
}
