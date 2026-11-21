# MFA Factors: HOTP, TOTP, SMS, Push and Recovery

## Goal

Build HOTP (RFC 4226) and TOTP (RFC 6238) from scratch, check them against the official test vectors for SHA-1,
SHA-256 and SHA-512, then write the part of an authenticator verifier that the RFC math does not give you: the
clock window, replay protection, constant-time comparison, an attempt limit and single-use recovery codes.

## Prerequisites

- HMAC and hashing in `System.Security.Cryptography`
- `TimeProvider` (the tests drive it with `FakeTimeProvider`)
- .NET 10 SDK

## Task

There is no network, no server and no OTP library: `src/MfaLab` is plain C# and the tests use a fake clock. The
seeds and the expected codes are the ones printed in RFC 4226 Appendix D and RFC 6238 Appendix B. SMS and push are
docs only in this lab: they cannot be tested without a phone, and the concept explains why they are weaker than
the factors you build here. The secrets in the tests are the public RFC test seeds, never a real credential.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `Hotp.Compute`: HMAC of the 8-byte big-endian counter, dynamic truncation, last N digits, for SHA-1, SHA-256 and SHA-512.
- TODO-01: `Totp.Step`: `floor(unix_time / period)`, safe for the year 2603.
- TODO-02: `Totp.Compute`: TOTP is HOTP over the time step.
- TODO-03: `CodeComparer.Equal`: constant-time comparison.
- TODO-04: `TotpVerifier.Verify`: accept the codes of the steps inside the window around the current one; anything that is not exactly `digits` digits is just invalid.
- TODO-05: `TotpVerifier.Verify`: refuse a step that was already used (and every older one) as a replay.
- TODO-06: `TotpVerifier.Verify`: count failures per account, lock after the limit even for the right code, reset on success.
- TODO-07: `RecoveryCodes.Generate`: 80-bit random codes as `XXXX-XXXX-XXXX-XXXX`, only their hashes stored.
- TODO-08: `RecoveryCodes.Redeem`: normalize, hash, look up, burn on success.

Read the window test: it is the table of phone offsets (0, 10, 20, 40, -20, -50, 80 seconds) against window 0, +-1 and +-2.
Then read the "phone runs ahead" test and notice why accepting the next step makes the current code a replay.

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Bonus (Optional)

The tests of the optional TODOs carry `Trait("Optional", "true")`. Skip them with `dotnet test --filter "Optional!=true"`
or run only them with `dotnet test --filter "Optional=true"`.

- TODO-09 (optional): `OtpAuthUri.Build`: the `otpauth://` URI behind the enrollment QR code (base32 secret, issuer, algorithm, digits, period).
- TODO-10 (optional): `HotpVerifier.Verify`: a look-ahead window and resynchronization for counter-based tokens.

## Notes on the solution

- `Hotp.Compute` is the 10 lines of the RFC: `HMACSHA1/256/512.HashData` over the counter written with
  `BinaryPrimitives.WriteUInt64BigEndian`, offset = low nibble of the last byte, `& 0x7FFFFFFF`, modulo 10^digits.
  The same 31-bit number gives 8 digits or 6 digits, so there is no separate 6-digit algorithm.
- `TotpVerifier.Verify` checks the lock first, then walks `current - window .. current + window`. A match at a step
  `<= _lastStep` is a Replay and does not count as a failure; a match moves `_lastStep` forward and clears the
  failure counter. Accepting a code from step n+1 moves `_lastStep` past n, so the current code is then a replay:
  correct, and one more reason to keep the window small (a random guess is accepted with probability `(2w+1)/10^6`).
- Constant-time comparison uses `CryptographicOperations.FixedTimeEquals`. The length of an OTP is public, the
  digits are not.
- Recovery codes are 80 bits from `RandomNumberGenerator`, so a plain SHA-256 is enough (speed does not matter at
  that size); shorter codes would need scrypt or Argon2 and a lockout. Regenerating replaces the whole set.
- The TOTP secret itself cannot be hashed (the server needs it to compute the expected code): encrypt it at rest with
  a key held outside the database. That part is explained in the concept and not exercised here.
