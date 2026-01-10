using Database;
using EggIncApi;
using Ei;
using JsonCompilers;
using Newtonsoft.Json;
using static JsonCompilers.LeaderboardInfo.Types;
using static Maj.Tables;

namespace Maj;
public class CookieCache : Majeggstics
{
    private Dictionary<string, ContractStats> stats = new();

    private SQLiteConnection dbConnection = SQLiteConnection.Instance();

    private SeasonLB season;

    private string dbName = "majeggstics.db";

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

    public void ProcessContracts()
    {
        if (!Tables.CookieCache.Exists())
            throw new InvalidOperationException($"\"{Tables.CookieCache.Name}\" table does not exist in the database.");
        if (!Tables.Seasons.Exists())
            throw new InvalidOperationException($"\"{Tables.Seasons.Name}\" table does not exist in the database.");
        if (!Tables.Coops.Exists())
            throw new InvalidOperationException($"\"{Tables.Coops.Name}\" table does not exist in the database.");
        if (!Tables.Users.Exists())
            throw new InvalidOperationException($"\"{Tables.Users.Name}\" table does not exist in the database.");
        if (!Tables.Rules.Exists())
            throw new InvalidOperationException($"\"{Tables.Rules.Name}\" table does not exist in the database.");

        // Validate season
        Tables.Seasons.Validate(season);

        foreach (var contract in ActiveContracts)
        {
            string contractId = contract.Key;
            ActiveContract activeContract = contract.Value;
            ContractStats contractStats = stats[contractId];

            // Validate players
            Tables.Users.Validate(contractStats.FastestCoopPlayers);

            // Validate coops
            Tables.Coops.Validate([.. activeContract.Coops]);

            // Insert fastest coop cookies
            Tables.CookieCache.Insert(
                season_id: season.Scope!,
                contract_id: contractId,
                coop_code: contractStats.FastestCoop.CoopId,
                user_ids: [.. contractStats.FastestCoopPlayers],
                rule_id: 1, // Fastest coop rule
                additional_cookies: 0
            );
        }
    }
}

internal class ContractStats
{
    private ActiveContract contract;

    public Coop FastestCoop => contract.OrderCoopsBy(x => x).First();
    
    public List<string> FastestCoopPlayers =>
        FastestCoop.Contributors
            .Where(p => !p.IsExternal)
            .Select(p => p.DiscordId!)
            .ToList();

    private Dictionary<string, object> fastestCoopProp => new Dictionary<string, object>
    {
        { "code", FastestCoop.CoopId },
        { "players", FastestCoopPlayers }
    };

    private List<string> sinkPlayers = null!;
    public List<string> SinkPlayers
    {
        get
        {
            if (this.sinkPlayers != null)
                return this.sinkPlayers;

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
                        $"{string.Join(", ", coopSinkPlayers.Select(p => p.IGN))}");
                }
                Console.ResetColor();

                coopSinkPlayers = [.. coopSinkPlayers.Where(p => !p.IsExternal)];

                sinkPlayers.AddRange(coopSinkPlayers);
            }

            this.sinkPlayers = sinkPlayers
                .Select(p => p.DiscordId!)
                .ToList();

            return this.sinkPlayers;
        }
    }

    public List<string> AllPlayers =>
        [.. FastestCoopPlayers, .. SinkPlayers];

    public ContractStats(ActiveContract contract)
    {
        this.contract = contract;
    }
}

internal static class Tables
{
    private static SQLiteConnection dbConnection = SQLiteConnection.Instance();

    public static class CookieCache
    {
        public static string Name = "CookieCache";
        public static bool Exists() => TableExists(Name);

        public static void Insert(string season_id, int? coop_id, string[] user_ids, int rule_id, int additional_cookies = 0)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] userIdParams = user_ids
                .Select((id, idx) => $"$user_id_{idx}")
                .ToArray();

            query = $@"
                INSERT INTO {Name} (season_id, coop_id, user_id, rule_id, additional_cookies)
                VALUES {string.Join(", ", userIdParams.Select(p => $"($season_id, $coop_id, {p}, $rule_id, $additional_cookies)"))};";

            Console.WriteLine($"""
                {season_id}
                {coop_id}
                {string.Join(", ", user_ids)}
                {rule_id}
                {additional_cookies}
                """);

            cmd = dbConnection.CreateCommand(query);

            cmd.Parameters.AddWithValue("$season_id", season_id);

            if (coop_id.HasValue)
                cmd.Parameters.AddWithValue("$coop_id", coop_id.Value);
            else
                cmd.Parameters.AddWithValue("$coop_id", DBNull.Value);

            for (int i = 0; i < user_ids.Length; i++)
                cmd.Parameters.AddWithValue($"user_id_{i}", user_ids[i]);

            cmd.Parameters.AddWithValue("$rule_id", rule_id);
            cmd.Parameters.AddWithValue("$additional_cookies", additional_cookies);

            cmd.ExecuteNonQuery();
        }
        public static void Insert(string season_id, int coop_id, string user_id, int rule_id, int additional_cookies = 0)
            => Insert(season_id, coop_id, [user_id], rule_id, additional_cookies);
        public static void Insert(string season_id, string contract_id, string coop_code, string[] user_ids, int rule_id, int additional_cookies = 0)
            => Insert(season_id, Coops.GetId(contract_id, coop_code), user_ids, rule_id, additional_cookies);
        public static void Insert(string season_id, string contract_id, string coop_code, string user_id, int rule_id, int additional_cookies = 0)
            => Insert(season_id, Coops.GetId(contract_id, coop_code),[user_id], rule_id, additional_cookies);
    }

    public static class Seasons
    {
        public static string Name = "Seasons";
        public static bool Exists() => TableExists(Name);

        public static bool Validate(SeasonLB season)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Check if season exists
            query = $@"SELECT count(*) FROM {Name} WHERE id=$season_id";

            cmd = dbConnection.CreateCommand(query);
            cmd.Parameters.AddWithValue("$season_id", season.Scope!);

            long count = Convert.ToInt32(cmd.ExecuteScalar()!);
            if (count == 0)
            {
                // Insert season into database
                cmd.Dispose();
                query = $@"
                INSERT INTO {Name} (id, name)
                VALUES ($season_id, $season_name);";
                cmd = dbConnection.CreateCommand(query);
                cmd.Parameters.AddWithValue("$season_id", season.Scope!);
                cmd.Parameters.AddWithValue("$season_name", season.Name!);
                cmd.ExecuteNonQuery();

                return false; // Season was not previously present
            }
            return true; // Season already exists
        }
    }

    public static class Coops
    {
        public static string Name = "Coops";
        public static bool Exists() => TableExists(Name);

        public static int GetId(string contract_id, string coop_id)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            query = $@"
                SELECT id FROM {Name}
                WHERE coop_id=$coop_id AND contract_id=$contract_id
            ";

            cmd = dbConnection.CreateCommand(query);
            cmd.Parameters.AddWithValue("$coop_id", coop_id);
            cmd.Parameters.AddWithValue("$contract_id", contract_id);

            object? result = cmd.ExecuteScalar() ?? throw new KeyNotFoundException($"Coop with Contract ID `{contract_id}` and Coop ID `{coop_id}` not found in database.");
            int id = Convert.ToInt32(result);

            return id;
        }
        public static int GetId(Coop coop) => GetId(coop.ContractId, coop.CoopId);

        public static bool Validate(Coop coop)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Check if coop exists
            query = $@"
                SELECT count(*) FROM {Name}
                WHERE coop_id=$coop_id AND contract_id=$contract_id
            ";

            cmd = dbConnection.CreateCommand(query);
            cmd.Parameters.AddWithValue("$coop_id", coop.CoopId!);
            cmd.Parameters.AddWithValue("$contract_id", coop.ContractId!);

            long count = Convert.ToInt32(cmd.ExecuteScalar()!);
            if (count == 0)
            {
                // Insert coop into database
                cmd.Dispose();
                query = $@"
                INSERT INTO {Name} (coop_id, contract_id)
                VALUES ($coop_id, $contract_id);";
                cmd = dbConnection.CreateCommand(query);
                cmd.Parameters.AddWithValue("$coop_id", coop.CoopId!);
                cmd.Parameters.AddWithValue("$contract_id", coop.ContractId!);
                cmd.ExecuteNonQuery();
                return false; // Coop was not previously present
            }

            return true; // Coop already exists
        }
        public static bool Validate(List<Coop> coopList)
        {
            bool allExist = true;
            foreach (Coop coop in coopList)
            {
                bool exists = Validate(coop);
                if (!exists)
                    allExist = false;
            }
            return allExist;
        }
    }

    public static class Users
    {
        public static string Name = "Users";
        public static bool Exists() => TableExists(Name);

        public static bool Validate(List<string> discordIdsList)
        {

            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Get existing list of users
            HashSet<string> existingUsers = new();

            query = $"SELECT discord_id FROM {Name};";

            cmd = dbConnection.CreateCommand(query);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string discordId = reader.GetString(0);
                existingUsers.Add(discordId);
            }

            // Find missing users
            List<string> missingUsers = discordIdsList
                .Where(id => !existingUsers.Contains(id))
                .ToList();

            // Insert missing users into the database
            MajUser user;
            string discordUsername;
            foreach (string discordId in missingUsers)
            {
                cmd.Dispose();

                try
                {
                    user = Player.DiscordIdToMajPlayer(discordId);
                    discordUsername = user.DiscordUsername!;
                }
                catch (KeyNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not find a username for `{discordId}` from. Please enter one below:");
                    Console.ResetColor();
                    Console.Write("> ");

                    discordUsername = Console.ReadLine() ?? "unknown_user";
                }

                query = $@"
                INSERT INTO {Name}  (discord_id, username)
                VALUES ($discord_id, $username);";
                cmd = dbConnection.CreateCommand(query);

                cmd.Parameters.AddWithValue("$discord_id", discordId);
                cmd.Parameters.AddWithValue("$username", discordUsername);

                cmd.ExecuteNonQuery();
            }

            return missingUsers.Count == 0; // Return true if no users were missing
        }
    }

    public static class Rules
    {
        public static string Name = "Rules";
        public static bool Exists() => TableExists(Name);
    }

    public static bool TableExists(string tableName)
    {
        string query = @"
            SELECT 1
            FROM sqlite_master
            WHERE type='table' AND name=$tableName;
            LIMIT 1
        ";
        using var cmd = dbConnection.CreateCommand(query);
        cmd.Parameters.AddWithValue("$tableName", tableName);

        object? result = cmd.ExecuteScalar();
        return result != null;
    }
}
