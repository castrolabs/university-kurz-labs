namespace FileBasedAppsLab;

public static class Apps
{
    public static string HelloApp() =>
        """
        Console.WriteLine("hello from a file-based app");
        """;

    public static string AppWithArgs() =>
        """
        Console.WriteLine(string.Join(",", args));
        return args.Length;
        """;

    public static string AppWithProperty() =>
        """
        #:property PublishAot=false
        Console.WriteLine(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
        """;

    public static string ShebangApp() =>
        """
        #!/usr/bin/env dotnet
        Console.WriteLine("shebang ok");
        """;

    public static string AppWithCompileError() =>
        """
        Console.WriteLine(ThisNameDoesNotExist);
        """;

    // TODO-06 (optional, needs the .NET 11 SDK)
    public static IReadOnlyDictionary<string, string> AppWithInclude() =>
        new Dictionary<string, string>
        {
            ["app.cs"] =
                """
                #!/usr/bin/env dotnet
                #:include greeter.cs
                #:include models/customer.cs
                #:property Company="Contoso Tools"
                using System.Reflection;

                var customer = new Customer("ana");
                Console.WriteLine(Greeter.Hi(customer.Name));
                Console.WriteLine(Assembly.GetEntryAssembly()!.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company);
                """,
            ["greeter.cs"] =
                """
                public static class Greeter
                {
                    public static string Hi(string name) => "hi " + name;
                }
                """,
            ["models/customer.cs"] =
                """
                public record Customer(string Name);
                """,
        };
}
