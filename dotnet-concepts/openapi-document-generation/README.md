# OpenAPI Document Generation

## Goal

Generate an OpenAPI document with the built-in `Microsoft.AspNetCore.OpenApi`
package and control its content with endpoint metadata, transformers, exclusion
and several named documents.

## Prerequisites

- Minimal APIs and route groups
- Endpoint metadata (see the parameter binding lab)
- .NET 10 SDK

## Task

`Program.cs` already has the endpoints, but no document is served yet. You will
register the generator, serve `/openapi/v1.json`, then shape what it contains.
The tests parse the served JSON with `System.Text.Json.Nodes` and check paths and
properties, not the whole text (the generator output changes between versions).

## Instructions

Complete the following TODOs in `src/OpenApiLab/Program.cs`:

- TODO-00: Call `AddOpenApi` and `MapOpenApi` so `/openapi/v1.json` is served.
- TODO-01: Give `GET /games/{id}` a summary, an operation name, a tag and typed
  `200` and `404` responses using endpoint metadata.
- TODO-02: Add a document transformer that sets the title, the version and a
  server.
- TODO-03: Mark only `GET /games/legacy` as deprecated, using a transformer
  attached to that endpoint.
- TODO-04: Exclude `/internal/health` from the document without removing the
  endpoint.
- TODO-05: Register a second document named `admin` and put `/admin/stats` only
  there (endpoints without a group name appear in every document).

Run the tests until they all pass.

## Running the Lab

```bash
dotnet test
```

## Bonus (Optional)

- TODO-06 (optional): Add a schema transformer that sets the description of every
  `decimal` to `Money in euros`. This enables the last test.
- TODO-07 (optional): Set `options.OpenApiVersion` to `OpenApiSpecVersion.OpenApi3_0`
  and compare how `price` and nullable properties are written in 3.0 and 3.1.

## Moving this lab to .NET 11

This lab targets `net10.0` and pins `Microsoft.OpenApi` 2.7.5. Retargeting it to
`net11.0` with `Microsoft.AspNetCore.OpenApi` 11.0.0 needs two changes, both
measured with the .NET 11.0.100 SDK:

- Raise the pin. The 11.0.0 package depends on `Microsoft.OpenApi >= 3.11.0 && < 4.0.0`,
  so keeping 2.7.5 fails the restore with `NU1605` (a package downgrade, treated as
  an error), and so does 3.10.2. Use 3.11.0 or newer (3.11.1 passes all 7 tests).
  The same bump does not work on `net10.0`: `Microsoft.AspNetCore.OpenApi` 10.0.0
  does not compile against `Microsoft.OpenApi` 3.x (`CS0200`, `IOpenApiMediaType.Example`
  is read only), which is why this lab stays on 2.7.5.
- Mind the new default. `AddOpenApi()` without options now writes `"openapi": "3.2.0"`
  (the test only asserts the `3.` prefix). To keep the previous output, set
  `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0`, which writes `3.0.4`.
