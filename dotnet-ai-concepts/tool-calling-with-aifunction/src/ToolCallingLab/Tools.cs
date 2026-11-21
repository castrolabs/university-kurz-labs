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
    public static AIFunction CreateWeather()
    {
        // TODO-00: Turn the call WeatherTools.Weather(city) into an AIFunction FROM A LAMBDA. A lambda gets a
        //          generated name and no description, so pass a name ("GetWeather") and a description of your own.
        throw new NotImplementedException("TODO-00");
    }

    public static AIFunction CreateForecast(JsonSerializerOptions? serializerOptions = null)
    {
        // TODO-01: Create the AIFunction from WeatherTools.GetForecast. Pass AIFunctionFactoryOptions with the
        //          SerializerOptions given by the caller (AIJsonUtilities.DefaultOptions when it is null): the
        //          same options build the schema and bind the arguments. Print f.JsonSchema and read it:
        //          which parameters are "required"? Is "1 to 7" enforced anywhere?
        throw new NotImplementedException("TODO-01");
    }
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
        // TODO-07: The arguments come from the model: validate them like an HTTP body, BEFORE any side effect.
        //          Reject `to` that does not match Account with "Rejected: 'to' must match ACC-<digits>." and an
        //          amount outside 0.01 to 1000 with "Rejected: amount must be between 0.01 and 1000 euros."
        //          (returned as text, so the model can relay it). Otherwise count the transfer and return
        //          "Transferred {amount} to {to}."
        _ = Account;
        throw new NotImplementedException("TODO-07");
    }

    [Description("Deletes an account.")]
    public string DeleteAccount(string id)
    {
        Deletions++;
        return $"Deleted {id}.";
    }

    private readonly HashSet<string> _seenOrders = [];

    [Description("Transfers money once per order id.")]
    public string TransferOnce(string orderId, string to, decimal amount)
    {
        // TODO-10 (optional): The CallId changes on every call, so it cannot deduplicate a side effect.
        //                     Use the business key: transfer only the first time an orderId is seen
        //                     (_seenOrders) and answer "Order {orderId} was already processed." afterwards.
        _ = _seenOrders;
        throw new NotImplementedException("TODO-10");
    }
}
