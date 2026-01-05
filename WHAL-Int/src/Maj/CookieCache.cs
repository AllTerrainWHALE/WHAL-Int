using Newtonsoft.Json;
using Ei;
using JsonCompilers;
using Database;
using EggIncApi;

namespace Maj;
public class CookieCache : Majeggstics
{
    private Dictionary<string, ContractStats> stats = new();

    private SQLiteConnection dbConnection = SQLiteConnection.Instance();

    private SeasonLB season;

    private string dbName = "majeggstics.db";
    private string tableName => $"cc_{season.Scope}";

    public CookieCache(SeasonLB season)
    {
        dbConnection.DataSource = dbName;
        dbConnection.Connect();

        if (!dbConnection.IsConnected())
            throw new InvalidOperationException($"Failed to connect to the `{dbConnection.DataSource}` database.");

        this.season = season;
    }
    public CookieCache(string seasonId)
    {
        dbConnection.DataSource = dbName;
        dbConnection.Connect();

        if (!dbConnection.IsConnected())
            throw new InvalidOperationException($"Failed to connect to the `{dbConnection.DataSource}` database.");

        Task<LBInfoResponse> lbInfoTask = Request.GetLBInfo();
        lbInfoTask.Wait();
        LBInfoResponse lbInfo = lbInfoTask.Result
            ?? throw new Exception("Failed to retrieve leaderboard info.");
        season = lbInfo.SeasonsList!
            .First(s => s.Scope == seasonId);
        if (season == null)
            throw new InvalidDataException($"Season ID invalid: {seasonId}");
    }

    public new void AddContract(string contractId)
    {
        base.AddContract(contractId);
        stats[contractId] = new ContractStats(ActiveContracts[contractId]);
    }



    public void CreateTable()
    {
        string query = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                contractId TEXT PRIMARY KEY,
                coopId TEXT NOT NULL,
                discordId INTEGER NOT NULL CHECK (discordId >= 0),
                ruleId INTEGER NOT NULL,
                gainedCookies INTEGER NOT NULL,
                additionalCookies INTEGER
            );";
        dbConnection.ExecuteNonQuery(query);
    }

    public bool TableExists()
    {
        string query = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}';";
        var result = dbConnection.ExecuteReaderQuery(query);
        return result.Count > 0;
    }

    public void LogData(string contractId, string coopId, long discordId, int ruleId, long gainedCookies, long? additionalCookies = null)
    {
        string query = $@"
            INSERT OR REPLACE INTO {tableName} 
            (contractId, coopId, discordId, ruleId, gainedCookies, additionalCookies) 
            VALUES 
            ($contractId, $coopId, $discordId, $ruleId, $gainedCookies, $additionalCookies);";
        var parameters = new Dictionary<string, object?>
        {
            { "$contractId", contractId },
            { "$coopId", coopId },
            { "$discordId", discordId },
            { "$ruleId", ruleId },
            { "$gainedCookies", gainedCookies },
            { "$additionalCookies", additionalCookies }
        };
        dbConnection.ExecuteNonQuery(query, parameters);
    }

}

internal class ContractStats
{
    private ActiveContract contract;

    [JsonIgnore]
    public Coop FastestCoop => contract.OrderCoopsBy(x => x).First();

    [JsonIgnore]
    public List<MajUser> FastestCoopPlayers =>
        FastestCoop.Contributors
            .Select(p => Player.IGNToMajPlayer(p.UserName))
            .ToList();

    [JsonProperty(PropertyName = "fastestCoop")]
    private Dictionary<string, object> fastestCoopProp => new Dictionary<string, object>
    {
        { "code", FastestCoop.CoopId },
        { "players", FastestCoopPlayers }
    };

    [JsonProperty(PropertyName = "sinkPlayers")]
    public List<MajUser> SinkPlayers
    {
        get
        {
            List<Player> sinkPlayers = new();
            foreach (Coop coop in contract.Coops)
            {
                List<Player> coopSinkPlayers = coop.Contributors
                    .Where(p => p.Sink)
                    .ToList();

                Console.ForegroundColor = ConsoleColor.Yellow;
                if (coopSinkPlayers.Count == 0)
                {
                    Console.WriteLine($"No sinks found in {coop.CoopId} for contract {contract.ContractId}");
                }
                else if (coopSinkPlayers.Count > 1)
                {
                    Console.WriteLine($"Multiple sinks found in {coop.CoopId} for contract {contract.ContractId}: " +
                        $"{string.Join(", ", coopSinkPlayers.Select(p => p.UserName))}");
                }
                Console.ResetColor();

                sinkPlayers.AddRange(coopSinkPlayers);
            }

            List<MajUser> majSinkPlayers = sinkPlayers
                .Select(p => Player.IGNToMajPlayer(p.UserName))
                .ToList();

            return majSinkPlayers;
        }
    }

    public ContractStats(ActiveContract contract)
    {
        this.contract = contract;
    }
}

internal static class Tables
{
    public static string CookieCache = "CookieCache";
    public static string Coops = "Coops";
    public static string Users = "Users";
    public static string Rules = "Rules";
    public static string Seasons = "Seasons";
}
