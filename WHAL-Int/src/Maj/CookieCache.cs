using System.Runtime.Serialization;
using Database;
using EggIncApi;
using Ei;
using JsonCompilers;
using Newtonsoft.Json;

/**
 * TODO:
 *   - Handle duplicates gracefully (e.g., ignore or update existing entries)
 *   
 *   - Process and log LB progress entries
 *     - Don't piss about with auto-detecting when to update.
 *       Instead, just have a Y/N prompt for it.
 *     - Log past LBs in JSON file.
 *       - MAKE SURE NOT TO REPLACE THE DATA WHEN AN ERROR OCCURSES DURING PROCESSING!!!
 *     - Need to include tracking of IGNs, otherwise players will be missed if they aren't in the Users table.
 *       - Include check in Users.Validate(), where DiscordID is found but IGN is different to the one in the DB.
 *       - Prompt for auto replacement of IGN in such cases.
 */

namespace Maj;
public class CookieCache : Majeggstics
{
    private static readonly string leaderboardsDir = "WHAL-Int\\data\\leaderboards";

    private List<Tables.CookieCache.Entry> entries = new();

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
    }

    public void ProcessContracts()
    {
        foreach (var contract in ActiveContracts)
        {
            string contractId = contract.Key;
            ActiveContract activeContract = contract.Value;

            activeContract.OrderCoopsBy(x => x);

            // Process Fastest Speedrun Coop
            Coop? fastestSrCoop = activeContract.Coops
                .Where(c => c.CoopFlags.SpeedRun == true)
                .FirstOrDefault();
            if (fastestSrCoop != null)
            {
                List<string> fastestSrCoopDiscordIds = fastestSrCoop.Contributors
                .Where(p => !p.IsExternal)
                .Select(p => p.DiscordId!)
                .ToList();

                fastestSrCoopDiscordIds.ForEach(id =>
                {
                    Tables.CookieCache.Entry entry = new()
                    {
                        SeasonId = season.Scope!,
                        ContractId = contractId,
                        CoopCode = fastestSrCoop.CoopId,
                        UserId = id,
                        RuleId = (int)Tables.Rules.Indexes.FastestCoop
                    };
                    entries.Add(entry);
                });
            }

            // Process Fastest Fastrun Coop
            Coop? fastestFrCoop = activeContract.Coops
                .Where(c => c.CoopFlags.FastRun == true)
                .FirstOrDefault();
            if (fastestFrCoop != null)
            {
                List<string> fastestFrCoopDiscordIds = fastestFrCoop.Contributors
                .Where(p => !p.IsExternal)
                .Select(p => p.DiscordId!)
                .ToList();

                fastestFrCoopDiscordIds.ForEach(id =>
                {
                    Tables.CookieCache.Entry entry = new()
                    {
                        SeasonId = season.Scope!,
                        ContractId = contractId,
                        CoopCode = fastestFrCoop.CoopId,
                        UserId = id,
                        RuleId = (int)Tables.Rules.Indexes.FastestCoop
                    };
                    entries.Add(entry);
                });
            }
        }
    }

    public void ProcessLeaderboard(string scope)
    {
        // Fetch leaderboard data
        Task<LBResponse> lbTask = Request.GetLeaderboard(scope);
        lbTask.Wait();
        LBResponse lbResponse = lbTask.Result
            ?? throw new Exception("Failed to retrieve leaderboard data.");

        // Fetch known users igns and ids from the database
        Dictionary<string,string> knownIGNsAndIds = new();
        string query = $"SELECT ign, discord_id FROM {Tables.Users.Name};";
        using var cmd = dbConnection.CreateCommand(query);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string ign = reader.GetString(0);
            string discordId = reader.GetString(1);
            knownIGNsAndIds[ign] = discordId;
        }

        // Compare against previous LB state
        List<LBResponse> storedLbResponses;
        LBResponse differenceLbResponse;
        string leaderboardScopeDir = $"{leaderboardsDir}\\{scope}.json";
        if (File.Exists(leaderboardScopeDir))
        {
            // Load previous leaderboard data
            storedLbResponses = JsonConvert.DeserializeObject<List<LBResponse>>(
                File.ReadAllText(leaderboardScopeDir)
            ) ?? throw new SerializationException("Failed to deserialize previous leaderboard data.");

            LBResponse previousLbResponse = storedLbResponses.Last();

            // Get the differences between the current and previous leaderboard states
            differenceLbResponse = CompareLeaderboards(lbResponse, previousLbResponse);

            // Append current leaderboard to stored data
            storedLbResponses.Add(lbResponse);
        }
        else
        {
            Directory.CreateDirectory(leaderboardsDir);
            // No previous data, so the difference is the entire current leaderboard
            differenceLbResponse = lbResponse;
            storedLbResponses = [lbResponse];
        }

        // Save new leaderboard data
        File.WriteAllText(
            leaderboardScopeDir,
            JsonConvert.SerializeObject(
                storedLbResponses,
                Formatting.Indented
        ));

        // Sort the leaderboard entries, filter out unknown players, and add to queued entries
        foreach (LBEntry playerEntry in differenceLbResponse.Entries!)
            {
                string discordId;
                try
                {
                    MajUser majUser = Player.IGNToMajPlayer(playerEntry.IGN!);
                    discordId = majUser.DiscordId!;
                }
                catch (KeyNotFoundException)
                {
                    if (!knownIGNsAndIds.ContainsKey(playerEntry.IGN!))
                        continue; // Skip players that can't map to a Discord ID
                    discordId = knownIGNsAndIds[playerEntry.IGN!];
                }

                Tables.CookieCache.Entry entry = new()
                {
                    SeasonId = differenceLbResponse.Scope!,
                    UserId = discordId,
                    RuleId = differenceLbResponse.Scope! == "ALL_TIME"
                        ? (int)Tables.Rules.Indexes.AlltimeLbProgress
                        : (int)Tables.Rules.Indexes.SeasonalLbProgress,
                };
                entries.Add(entry);

                Console.WriteLine($"Queued entry for {playerEntry.IGN} ({discordId})");
            }
    }

    private LBResponse CompareLeaderboards(LBResponse lb1, LBResponse lb2)
    {
        // Deep clone lb1 to avoid modifying the original
        LBResponse lbResult = JsonConvert.DeserializeObject<LBResponse>(
            JsonConvert.SerializeObject(lb1)
        ) ?? throw new SerializationException("Failed to clone leaderboard data.");
        lbResult.Entries!.Clear();

        foreach (LBEntry e1 in lb1.Entries!)
        {
            LBEntry? e2 = lb2.Entries!
                .FirstOrDefault(e2 => e2.IGN == e1.IGN);

            if (e2 == null || e1.Rank! < e2!.Rank!)
            {
                lbResult.Entries!.Add(e1);
            }

        }
        return lbResult;
    }

    public void UpdateCache()
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

        // Validate users
        Tables.Users.Validate([.. entries.Select(e =>
            new Tables.Users.Entry(){
                DiscordId = e.UserId,
                Username = "",
                IGN = ""
        })]);

        // Validate coops
        Tables.Coops.Entry[] coopEntriesToValidate = entries
            .Where(e =>
                e.CoopId == null &&
                e.ContractId != null &&
                e.CoopCode != null)
            .Select(e => new Tables.Coops.Entry
            {
                ContractId = e.ContractId!,
                CoopId = e.CoopCode!
            })
            .Distinct()
            .ToArray();
        Tables.Coops.Validate(coopEntriesToValidate);

        // Insert entries into CookieCache table
        Tables.CookieCache.Insert(entries.ToArray());
    }
}

internal static class Tables
{
    private static SQLiteConnection dbConnection = SQLiteConnection.Instance();

    public static class CookieCache
    {
        internal struct Entry
        {
            public string SeasonId;
            public string? ContractId;
            public string? CoopCode;
            public int? CoopId;
            public string UserId;
            public int RuleId;
            public int? AdditionalCookies;
        }

        public static string Name = "CookieCache";
        public static bool Exists() => TableExists(Name);

        public static void Insert(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] queryValues = entries
                .Select((e, i) =>
                    $"($season_id_{i}, $coop_id_{i}, $user_id_{i}, $rule_id_{i}, $additional_cookies_{i})")
                .ToArray();

            query = $@"
                INSERT INTO {Name} (season_id, coop_id, user_id, rule_id, additional_cookies)
                VALUES {string.Join(", ", queryValues)};";

            cmd = dbConnection.CreateCommand(query);

            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                if (
                    entry.CoopId == null &&
                    entry.ContractId != null &&
                    entry.CoopCode != null)
                { entry.CoopId = Coops.GetId(entry.ContractId!, entry.CoopCode!); }

                cmd.Parameters.AddWithValue($"$season_id_{i}", entry.SeasonId);
                cmd.Parameters.AddWithValue($"$coop_id_{i}", entry.CoopId != null ? entry.CoopId! : DBNull.Value);
                cmd.Parameters.AddWithValue($"$user_id_{i}", entry.UserId);
                cmd.Parameters.AddWithValue($"$rule_id_{i}", entry.RuleId);
                cmd.Parameters.AddWithValue($"$additional_cookies_{i}", entry.AdditionalCookies.HasValue ? entry.AdditionalCookies!.Value : DBNull.Value);

            }
            cmd.ExecuteNonQuery();
        }
        public static void Insert(Entry entry) => Insert([entry]);
        public static void Insert(string seasonId, int coopId, string userId, int ruleId, int? additionalCookies = null) =>
            Insert(new Entry
            {
                SeasonId = seasonId,
                CoopId = coopId,
                UserId = userId,
                RuleId = ruleId,
                AdditionalCookies = additionalCookies
            });
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
        internal struct Entry
        {
            public string CoopId;
            public string ContractId;
        }

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
        public static int GetId(Entry entry) => GetId(entry.ContractId, entry.CoopId);

        public static void Insert(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] queryValues = entries
                .Select((e, i) =>
                    $"($coop_id_{i}, $contract_id_{i})")
                .ToArray();

            query = $@"
                INSERT INTO {Name} (coop_id, contract_id)
                VALUES {string.Join(", ", queryValues)};";
            cmd = dbConnection.CreateCommand(query);
            for (int i = 0; i < entries.Length; i++)
            {
                cmd.Parameters.AddWithValue($"$coop_id_{i}", entries[i].CoopId);
                cmd.Parameters.AddWithValue($"$contract_id_{i}", entries[i].ContractId);
            }
            cmd.ExecuteNonQuery();
        }
        public static void Insert(Entry entry) => Insert([entry]);
        public static void Insert(string contractId, string coopId) =>
            Insert([new Entry { ContractId = contractId, CoopId = coopId }]);

        public static bool Validate(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Get existing coops
            Dictionary<string, List<string>> existingCoops = new();

            query = $@"
                SELECT contract_id, coop_id FROM {Name}
                WHERE
                    contract_id IN ({string.Join(", ", entries.Select(c => $"\"{c.ContractId}\"").Distinct())})
                    AND
                    coop_id IN ({string.Join(", ", entries.Select(c => $"\"{c.CoopId}\"").Distinct())});
            ";

            cmd = dbConnection.CreateCommand(query);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string contractId = reader.GetString(0);
                string coopId = reader.GetString(1);

                if (existingCoops.Keys.Contains(contractId))
                    existingCoops[contractId].Add(coopId);
                else
                    existingCoops[contractId] = new() { coopId };
            }

            List<Entry> missingCoops = entries
                .Where(c =>
                    !existingCoops.ContainsKey(c.ContractId) ||
                    !existingCoops[c.ContractId].Contains(c.CoopId))
                .ToList();

            // Return false if no coops are missing
            if (missingCoops.Count == 0)
                return false;

            cmd.Dispose();

            // Insert missing coops into the database
            Insert(missingCoops.ToArray());

            return true; // Some coops were missing and have been added
        }
        public static bool Validate(string contractId, string coopCode) =>
            Validate([new Entry { ContractId = contractId, CoopId = coopCode }]);
        public static bool Validate(Coop coop) =>
            Validate([new Entry { ContractId = coop.ContractId, CoopId = coop.CoopId }]);
    }

    public static class Users
    {
        public struct Entry
        {
            public string DiscordId;
            public string Username;
            public string IGN;
            public string? Nickname;
        }

        public static string Name = "Users";
        public static bool Exists() => TableExists(Name);

        public static bool Validate(List<Entry> entries)
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
            List<Entry> missingUsers = entries
                .Where(e => !existingUsers.Contains(e.DiscordId))
                .ToList();

            // Insert missing users into the database
            MajUser user;
            Entry entry;
            for (int i = 0; i < missingUsers.Count(); i++)
            {
                cmd.Dispose();
                entry = missingUsers[i];

                try
                {
                    user = Player.DiscordIdToMajPlayer(entry.DiscordId);
                    entry.Username = user.DiscordUsername!;
                    entry.IGN = user.IGN!;
                }
                catch (KeyNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not find a username and IGN for `{entry.DiscordId}`. Please enter manually below:");
                    Console.ResetColor();

                    // Username entry
                    Console.Write("> ");
                    entry.Username = Console.ReadLine() ?? "unknown_user";
                    if (string.IsNullOrWhiteSpace(entry.Username))
                        entry.Username = "unknown_user";

                    // IGN entry
                    Console.Write("> ");
                    entry.IGN = Console.ReadLine() ?? "unknown_ign";
                    if (string.IsNullOrWhiteSpace(entry.IGN))
                        entry.IGN = "unknown_ign";
                }

                query = $@"
                INSERT INTO {Name}  (discord_id, username, ign)
                VALUES ($discord_id, $username, $ign);";
                cmd = dbConnection.CreateCommand(query);

                cmd.Parameters.AddWithValue("$discord_id", entry.DiscordId);
                cmd.Parameters.AddWithValue("$username", entry.Username);
                cmd.Parameters.AddWithValue("$ign", entry.IGN);

                cmd.ExecuteNonQuery();
            }

            return missingUsers.Count == 0; // Return true if no users were missing
        }
        public static bool Validate(string discordId, string username = "", string ign = "") =>
            Validate([new Entry { DiscordId = discordId, Username = username, IGN = ign }]);
    }

    public static class Rules
    {
        public static string Name = "Rules";
        public static bool Exists() => TableExists(Name);

        public enum Indexes
        {
            FastestCoop = 1,

            SeasonalLbProgress = 50,
            AlltimeLbProgress = 60
        }
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
