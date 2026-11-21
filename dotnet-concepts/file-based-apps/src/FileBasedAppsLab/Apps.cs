namespace FileBasedAppsLab;

// Each method returns the full text of a single-file C# program. The tests save it as app.cs
// and run it with the .NET 10 SDK. Raw string literals (""" ... """) keep the code readable.
public static class Apps
{
    public static string HelloApp()
    {
        // TODO-00: Return a program made only of top-level statements that prints exactly
        //   "hello from a file-based app" to standard output. No project file, no Main method.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static string AppWithArgs()
    {
        // TODO-01: Return a program that prints its arguments joined by "," on one line
        //   (use the implicit "args" variable) and returns args.Length as the exit code.
        //   The test passes the arguments after "--".
        throw new NotImplementedException("Not implemented yet.");
    }

    public static string AppWithProperty()
    {
        // TODO-02: Return a program that starts with "#:property PublishAot=false" and prints
        //   System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported.
        //   The test expects "True". Without the directive the SDK's Native AOT default
        //   makes the same line print "False" (see the fourth test).
        throw new NotImplementedException("Not implemented yet.");
    }

    public static string ShebangApp()
    {
        // TODO-03: Return a program whose very first line is "#!/usr/bin/env dotnet" and that
        //   prints "shebang ok". The compiler must accept the shebang as a first line.
        throw new NotImplementedException("Not implemented yet.");
    }

    public static string AppWithCompileError()
    {
        // TODO-04: Return a program that does NOT compile (for example, it uses a name that
        //   does not exist). The run must exit with a non-zero code and print a CSxxxx error.
        throw new NotImplementedException("Not implemented yet.");
    }

    // TODO-06 (optional, needs the .NET 11 SDK): A program that spans several files.
    // Return a map of relative path to file text; it must contain "app.cs", which the test runs.
    //   - app.cs starts with the shebang line, pulls in two other files with the include
    //     directive and sets the assembly company with a QUOTED property value:
    //     "Contoso Tools" (the value has a space, so it needs quotes).
    //   - one included file, greeter.cs, holds a Greeter class with a static Hi(string name)
    //     that returns "hi " + name; the other, models/customer.cs, holds a Customer record
    //     with a Name. Both files live next to app.cs (the second one in a subfolder).
    //   - app.cs prints two lines: Greeter.Hi applied to the name of new Customer("ana"),
    //     then the AssemblyCompanyAttribute value of the running assembly.
    // On SDK 10.0.100 the include directive is rejected, which is why the test is skipped there.
    public static IReadOnlyDictionary<string, string> AppWithInclude()
    {
        throw new NotImplementedException("Not implemented yet.");
    }
}
