using System.ComponentModel;

namespace AgentLab;

public sealed class WeatherService
{
    public int Runs { get; private set; }

    [Description("Gets the current weather for a city.")]
    public string GetWeather([Description("The city name.")] string city)
    {
        Runs++;
        return $"{city}: 18 C, light rain";
    }
}

public sealed class BankService
{
    public List<(string To, int Amount)> Transfers { get; } = [];

    [Description("Transfers money to an account.")]
    public string Transfer(string to, int amount)
    {
        Transfers.Add((to, amount));
        return $"Transferred {amount} to {to}.";
    }
}

public sealed class AccountService
{
    public int DeleteRuns { get; private set; }

    [Description("Permanently deletes an account.")]
    public string DeleteAccount(string id)
    {
        DeleteRuns++;
        return $"Deleted {id}.";
    }
}
