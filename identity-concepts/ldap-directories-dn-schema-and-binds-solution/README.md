# LDAP Directories: DNs, Filters and Binds

## Goal

Rebuild, in plain C#, the parts of LDAP that the lesson measured against a real OpenLDAP: parse and
escape distinguished names (RFC 4514), parse and escape search filters (RFC 4515), build a filter
that user input cannot rewrite, and implement the bind semantics of an in-memory directory,
including the trap where a DN with an empty password turns into a successful anonymous bind.

## Prerequisites

- The lesson "LDAP Directories: DN, Schema and Binds" (identity track)
- Basic C# (records, pattern matching, LINQ)
- .NET 10 SDK

## Task

There is no LDAP server and no network. `InMemoryDirectory` is a small tree with LDAP semantics:
`Search` takes the filter as the string a client would send, and `Bind` answers like a server
(`Success`, `InvalidCredentials` (49), `UnwillingToPerform` (53)). Passwords are stored as `{SSHA}`
hashes (`Ssha`, provided). The filter tree, its evaluation (case-insensitive, an undefined attribute
matches nothing) and `NaiveFilters` / `NaiveLoginService`, the two deliberately broken classes the
tests attack, are provided.

## Instructions

Complete the following TODOs (already done in this solution):

- TODO-00: `DistinguishedName.EscapeValue`: the RFC 4514 escaping of a value inside a DN.
- TODO-01: `DistinguishedName.Parse`: unescaped `,` and `+` split RDNs and AVAs, spaces around components are ignored, both `\,` and `\2C` are decoded, a component without `type=value` throws `FormatException`.
- TODO-02: `Rdn.IsEquivalentTo`: case-insensitive, order-insensitive inside a multi-valued RDN.
- TODO-03: `LdapFilter.Escape`: the RFC 4515 escaping of a value inside a filter.
- TODO-04: `LdapFilter.Parse`: strict, with `\2a` as a literal star and `FilterException` for anything malformed, including text left after the first complete filter.
- TODO-05: `SafeFilters.UserLookup`: the same lookup as `NaiveFilters.UserLookup`, but injection-proof.
- TODO-06: `InMemoryDirectory.Search`: base, one-level and subtree scopes plus the filter, and an error for a missing base.
- TODO-07: `InMemoryDirectory.Bind`: anonymous, unauthenticated, wrong password and unknown DN, each with the right result and identity.
- TODO-08: `LoginService.Login`: search-then-bind that cannot be talked into a login by an empty password, a wildcard or a bind that "succeeded" as anonymous.

Read the tests that attack `NaiveFilters` and `NaiveLoginService`: they are the lesson's injection oracle and its
`bind_anon_dn` bug, run against your code. Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Bonus (Optional)

- TODO-09 (optional): `InMemoryDirectory.Compare`: literal comparison of an attribute value. Then compare `userPassword` with the clear text password and notice the answer.
