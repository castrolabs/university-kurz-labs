namespace AuthzLab;

/// <summary>
/// The scenario of the concept, defined once. Documents, folders and teams. The owner reads and edits; the owner's
/// team reads; an admin reads and edits; a confidential document needs clearance >= level for everyone, admin included.
/// </summary>
public static class Scenario
{
    public static readonly IReadOnlyDictionary<string, Entity> Users = new Dictionary<string, Entity>
    {
        ["alice"] = User("alice", ["eng"], clearance: 2, admin: false, contractor: false),
        ["bob"] = User("bob", ["eng"], clearance: 0, admin: false, contractor: false),
        ["carol"] = User("carol", [], clearance: 0, admin: true, contractor: false),
        ["dave"] = User("dave", ["sales"], clearance: 3, admin: false, contractor: false),
        ["erin"] = User("erin", ["eng"], clearance: 0, admin: false, contractor: true),
    };

    public static readonly IReadOnlyDictionary<string, Entity> Documents = new Dictionary<string, Entity>
    {
        ["d1"] = Document("d1", "Q3 plan", owner: "alice", team: "eng", confidential: false, level: 0),
        ["d2"] = Document("d2", "Salary table", owner: "alice", team: "eng", confidential: true, level: 2),
        ["d3"] = Document("d3", "Price list", owner: "dave", team: "sales", confidential: false, level: 0),
    };

    public static Request Ask(string user, string action, string document, IReadOnlyDictionary<string, object?>? context = null) =>
        new(Users[user], action, Documents[document], context);

    private static Entity User(string id, string[] teams, int clearance, bool admin, bool contractor) =>
        new("User", id, new Dictionary<string, object?>
        {
            ["teams"] = teams,
            ["clearance"] = clearance,
            ["admin"] = admin,
            ["contractor"] = contractor,
        });

    private static Entity Document(string id, string title, string owner, string team, bool confidential, int level) =>
        new("Doc", id, new Dictionary<string, object?>
        {
            ["title"] = title,
            ["owner"] = owner,
            ["team"] = team,
            ["confidential"] = confidential,
            ["level"] = level,
        });
}
