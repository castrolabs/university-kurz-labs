using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace ToolCallingLab;

public enum Unit { Celsius, Fahrenheit }

public sealed record Location(string City, string? Country);

/// <summary>Plain .NET methods. Already written: the lab is about turning them into tools.</summary>
public static class WeatherTools
{
    public static string Weather(string city) => $"{city}: 18 C, light rain";

    [Description("Gets the forecast for a location.")]
    public static string GetForecast(
        [Description("Where")] Location location, DateOnly date, DateTime? notAfter, Unit unit,
        [Description("How many days, 1 to 7")] int days, int? maxResults, bool includeWind = false) =>
        $"{location.City}: {days} days in {unit}";
}

public static class ToolCatalog
{
    public static AIFunction CreateWeather() =>
        AIFunctionFactory.Create((string city) => WeatherTools.Weather(city), "GetWeather", "Gets the current weather for a city.");

    public static AIFunction CreateForecast(JsonSerializerOptions? serializerOptions = null) =>
        AIFunctionFactory.Create(
            WeatherTools.GetForecast,
            new AIFunctionFactoryOptions { SerializerOptions = serializerOptions ?? AIJsonUtilities.DefaultOptions });
}

/// <summary>A tool with a side effect. The model chooses the arguments, so they are input like an HTTP body.</summary>
public sealed class BankTools
{
    private static readonly Regex Account = new("^ACC-[0-9]{1,10}$", RegexOptions.Compiled);

    public int Transfers { get; private set; }

    public int Deletions { get; private set; }

    [Description("Transfers money to an account.")]
    public string Transfer([Description("Target account, ACC-<digits>")] string to, [Description("Euros, 0.01 to 1000")] decimal amount)
    {
        if (!Account.IsMatch(to))
        {
            return "Rejected: 'to' must match ACC-<digits>.";
        }

        if (amount < 0.01m || amount > 1000m)
        {
            return "Rejected: amount must be between 0.01 and 1000 euros.";
        }

        Transfers++;
        return $"Transferred {amount} to {to}.";
    }

    [Description("Deletes an account.")]
    public string DeleteAccount(string id)
    {
        Deletions++;
        return $"Deleted {id}.";
    }

    private readonly HashSet<string> _seenOrders = [];

    [Description("Transfers money once per order id.")]
    public string TransferOnce(string orderId, string to, decimal amount) =>
        _seenOrders.Add(orderId) ? Transfer(to, amount) : $"Order {orderId} was already processed.";
}
