# File-Based Apps

## Goal

Write C# programs that live in a single `.cs` file and run them with `dotnet run app.cs`,
using top-level statements, arguments, exit codes, `#:property` directives and a shebang line.
See what the SDK does when the file has an error, and what it hides from you by default.

## Prerequisites

- C# top-level statements and raw string literals
- .NET 10 SDK (10.0.100 or later) on your `PATH` (the optional TODO-06 needs the .NET 11 SDK)

## Task

The methods in `Apps` return the text of a single-file program. The tests save it as
`app.cs` in a unique temporary directory, run it with `dotnet run app.cs` (a real child
process, with no project file), and check the output and the exit code. Only features that
work offline are used: the lab never uses `#:package`, which would need NuGet. In a real
script you would add a package with `#:package Name@Version`.

The tests start the real SDK, so each of them takes a few seconds. The whole run takes
about 5 to 60 seconds (7 CLI runs, plus 3 more when TODO-06 runs on SDK 11; the first run on a machine is the slowest).
The temporary directories are deleted when each test ends.

## Instructions

Complete the following TODOs in `src/FileBasedAppsLab/Apps.cs`:

- TODO-00: Write `HelloApp`, a program of top-level statements with no project file.
- TODO-01: Write `AppWithArgs`, which prints the arguments and returns their count as the exit code.
- TODO-02: Write `AppWithProperty`, which sets `PublishAot=false` with a `#:property` directive.
- TODO-03: Write `ShebangApp`, which starts with `#!/usr/bin/env dotnet`.
- TODO-04: Write `AppWithCompileError`, a program that fails to compile with a `CSxxxx` error.

Run the tests until they all pass. Notice the fourth test: it runs the same probe without
the directive and shows that the generated project targets Native AOT by default.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Bonus (Optional)

- TODO-05 (optional): The last test runs `dotnet project convert app.cs` on your
  `AppWithProperty` program. Open the generated `app.csproj` (set a breakpoint, or copy the
  folder out of the test) and find `PackAsTool`, `UserSecretsId` and `PublishAot`.
  Which of them did you write, and which did the SDK add?
- TODO-06 (optional, needs the .NET 11 SDK): Write `AppWithInclude`, a program that spans three
  files. It returns a map of relative path to file text, with `app.cs` as the entry point. `app.cs`
  starts with the shebang line, pulls in `greeter.cs` (a `Greeter` class) and `models/customer.cs`
  (a `Customer` record) with the include directive, and sets the assembly company to
  `Contoso Tools` with a quoted `#:property` value. It prints the greeting for `ana`, then the
  `AssemblyCompany` of the running assembly. Three tests cover it: the run itself, the build
  failing once the include lines are removed, and the `CA2266` warning once the shebang is
  removed. On SDK 10.0.100 the include directive is rejected, so these tests are reported as
  skipped (the `dotnet` on your `PATH` is asked for its version); with the .NET 11 SDK they run.
