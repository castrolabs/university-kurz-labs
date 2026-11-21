# SAML 2.0 Assertions, Bindings and SSO Flows

## Goal

Write the service provider (SP) side of SAML browser SSO in .NET: encode and decode the Redirect and POST
bindings, verify an XML signature with `SignedXml` against a pinned certificate, and apply the mandatory
checks (audience, validity window, destination, recipient, `InResponseTo`, replay). Then break a naive
verifier with an XML Signature Wrapping attack and fix it by reading only the element the signature covers.

## Prerequisites

- The lesson "SAML 2.0: Assertions, Bindings and SSO Flows" (identity track)
- Basic XML and `XmlDocument`
- .NET 10 SDK

## Task

There is no Keycloak and no network. The tests contain `TestIdp`, a throwaway identity provider: an RSA key and a
self-signed certificate that exist only in memory, and a builder that produces a signed `Response` (enveloped
XMLDSig, exclusive C14N, RSA-SHA256, the same shape as the lesson's). Time comes from a `FakeTimeProvider`, so
the validity-window tests are deterministic. `Wrapping` builds two wrapping attacks. `NaiveVerifier` (provided)
is the lesson's toy verifier: it checks that a signature is valid and then reads the first `NameID` in the
document. The tests show it returning `admin@example.org` for a document whose signature is perfectly valid.

This is defensive and local: the attacks target the lab's own naive verifier, with disposable keys.

## Instructions

Complete the following TODOs:

- TODO-00: `SamlBindings.EncodeRedirect` and `DecodeRedirect`: the HTTP-Redirect binding (raw DEFLATE, then base64).
- TODO-01: `SamlXml.Load`: parse untrusted XML with DTDs forbidden and no resolver, keeping whitespace.
- TODO-02: `AssertionVerifier.GetSignedAssertion`: return the assertion the pinned certificate really signed, checking position and identity as well as the cryptography.
- TODO-03: `Conditions.CheckTimeWindow`: `NotBefore` and `NotOnOrAfter` with the injected `TimeProvider` and the clock skew.
- TODO-04: `Conditions.CheckAudience`: every `AudienceRestriction` must name this SP.
- TODO-05: `SubjectChecks.CheckBearerConfirmation`: destination, recipient and `InResponseTo`, single use.
- TODO-06: `ReplayCache.TryRegister`: reject a repeated assertion ID and forget expired ones.
- TODO-07: `SamlServiceProvider.ProcessPostResponse`: put it together, in a safe order, reading only from the signed assertion.

Read the wrapping tests carefully: the attacker never touches a signed byte. Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Bonus (Optional)

- TODO-08 (optional): `AttributeReader.ReadMultiValued`: merge repeated `Attribute` elements and multiple `AttributeValue` elements (the `ReadFirstValues` helper loses values), then call it from `ProcessPostResponse`.
