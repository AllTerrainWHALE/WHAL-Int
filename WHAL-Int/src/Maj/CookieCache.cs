using System.Runtime.Serialization;
using Database;
using EggIncApi;
using Ei;
using JsonCompilers;
using Newtonsoft.Json;

/**
 * TODO:
 *   - Handle duplicates gracefully (e.g., ignore or update existing ccEntries)
 *   
 *   - Process and log LB progress entries
 *   - Process and log LB progress ccEntries
 *     - Don't piss about with auto-detecting when to update.
 *       Instead, just have a Y/N prompt for it.
 *     - Log past LBs in JSON file.
 *       - MAKE SURE NOT TO REPLACE THE DATA WHEN AN ERROR OCCURSES DURING PROCESSING!!!
 */

namespace Maj;
public class CookieCache : Majeggstics
{
    private static readonly string leaderboardsDir = "WHAL-Int\\data\\leaderboards";

    private List<Tables.CookieCache.Entry> ccEntries = new();

    private SQLiteConnection dbConnection = SQLiteConnection.Instance();

    private SeasonLB season;
    private Tables.Seasons.Entry _seasonEntry;
    private Tables.Seasons.Entry seasonEntry
    {
        get
        {
            if (_seasonEntry.Id == null)
            {
                _seasonEntry = new Tables.Seasons.Entry
                {
                    Id = season.Scope!,
                    Name = season.Name
                };
            }
            return _seasonEntry;
        }
    }

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

            // Validate player IGNs
            Tables.Users.Entry[] userEntriesToValidate = activeContract.Coops
                .SelectMany(c => c.Contributors)
                .Where(p => !p.IsExternal)
                .Select(p => new Tables.Users.Entry
                {
                    DiscordId = p.DiscordId!,
                    Username = "",
                    IGN = p.IGN
                })
                .Distinct()
                .ToArray();
            Tables.Users.ValidateIGN(userEntriesToValidate);

            // Process Fastest Speedrun Coop
            Coop? fastestSrCoop = activeContract.Coops
                .Where(c => c.CoopFlags.SpeedRun == true)
                .FirstOrDefault();
            if (fastestSrCoop != null)
            {
                List<Player> fastestSrCoopContributors = fastestSrCoop.Contributors
                .Where(p => !p.IsExternal)
                .ToList();

                Tables.Coops.Entry coopEntry = new() { ContractId = contractId, CoopId = fastestSrCoop.CoopId };

                fastestSrCoopContributors.ForEach(p =>
                {
                    Tables.Users.Entry userEntry = new() { DiscordId = p.DiscordId!, Username = "", IGN = p.IGN };

                    Tables.CookieCache.Entry ccEntry = new()
                    {
                        Season = seasonEntry,
                        Coop = coopEntry,
                        User = userEntry,
                        RuleId = Tables.Rules.Indexes.FastestCoop
                    };
                    ccEntries.Add(ccEntry);
                });
            }

            // Process Fastest Fastrun Coop
            Coop? fastestFrCoop = activeContract.Coops
                .Where(c => c.CoopFlags.FastRun == true)
                .FirstOrDefault();
            if (fastestFrCoop != null)
            {
                List<Player> fastestFrCoopContributors = fastestFrCoop.Contributors
                .Where(p => !p.IsExternal)
                .ToList();

                Tables.Coops.Entry coopEntry = new() { ContractId = contractId, CoopId = fastestFrCoop.CoopId };

                fastestFrCoopContributors.ForEach(p =>
                {
                    Tables.Users.Entry userEntry = new() { DiscordId = p.DiscordId!, Username = "", IGN = p.IGN };

                    Tables.CookieCache.Entry ccEntry = new()
                    {
                        Season = seasonEntry,
                        Coop = coopEntry,
                        User = userEntry,
                        RuleId = Tables.Rules.Indexes.FastestCoop
                    };
                    ccEntries.Add(ccEntry);
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

        // Sort the leaderboard ccEntries, filter out unknown players, and add to queued ccEntries
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

            Tables.Users.Entry userEntry = new() {DiscordId = discordId, Username = "", IGN = playerEntry.IGN! };

            Tables.CookieCache.Entry entry = new()
            {
                Season = seasonEntry,
                User = userEntry,
                RuleId = differenceLbResponse.Scope! == "ALL_TIME"
                    ? Tables.Rules.Indexes.AlltimeLbProgress
                    : Tables.Rules.Indexes.SeasonalLbProgress,
            };
            ccEntries.Add(entry);

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
        Tables.Users.Validate([.. ccEntries.Select(e => e.User)]);

        // Validate coops
        Tables.Coops.Entry[] coopEntriesToValidate = ccEntries
            .Where(e =>
                e.Coop.HasValue &&
                e.Coop.Value.Id == null &&
                e.Coop.Value.ContractId != null &&
                e.Coop.Value.CoopId != null)
            .Select(e => e.Coop!.Value)
            .Distinct()
            .ToArray();
        Tables.Coops.Validate(coopEntriesToValidate);

        // Insert ccEntries into CookieCache table
        Tables.CookieCache.Insert(ccEntries.ToArray());
    }
}

internal static class Tables
{
    private static SQLiteConnection dbConnection = SQLiteConnection.Instance();

    public static class CookieCache
    {
        internal struct Entry
        {
            public Tables.Seasons.Entry Season;
            public Tables.Coops.Entry? Coop;
            public Tables.Users.Entry User;
            public Tables.Rules.Indexes RuleId;
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

                cmd.Parameters.AddWithValue($"$season_id_{i}", entry.Season.Id);
                cmd.Parameters.AddWithValue($"$coop_id_{i}", entry.Coop.HasValue ? Tables.Coops.GetId(entry.Coop.Value) : DBNull.Value);
                cmd.Parameters.AddWithValue($"$user_id_{i}", entry.User.DiscordId);
                cmd.Parameters.AddWithValue($"$rule_id_{i}", entry.RuleId);
                cmd.Parameters.AddWithValue($"$additional_cookies_{i}", entry.AdditionalCookies.HasValue ? entry.AdditionalCookies!.Value : DBNull.Value);

            }
            cmd.ExecuteNonQuery();
        }
        public static void Insert(Entry entry) => Insert([entry]);
    }

    public static class Seasons
    {
        public struct Entry {
            public string Id;
            public string? Name;
        }

        public static string Name = "Seasons";
        public static bool Exists() => TableExists(Name);

        public static void Insert(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] queryValues = entries
                .Select((e, i) =>
                    $"($season_id_{i}, $season_name_{i})")
                .ToArray();

            query = $@"
                INSERT INTO {Name} (id, name)
                VALUES {string.Join(", ", queryValues)};";
            cmd = dbConnection.CreateCommand(query);

            for (int i = 0; i < entries.Length; i++)
            {
                cmd.Parameters.AddWithValue($"$season_id_{i}", entries[i].Id);
                cmd.Parameters.AddWithValue($"$season_name_{i}", entries[i].Name);
            }

            cmd.ExecuteNonQuery();
        }
        public static void Insert(Entry entry) => Insert([entry]);
        public static void Insert(SeasonLB season) =>
            Insert([new Entry { Id = season.Scope!, Name = season.Name }]);

        public static bool Validate(Entry entry)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Check if season exists
            query = $@"SELECT count(*) FROM {Name} WHERE id=$season_id";

            cmd = dbConnection.CreateCommand(query);
            cmd.Parameters.AddWithValue("$season_id", entry.Id);

            long count = Convert.ToInt32(cmd.ExecuteScalar()!);
            if (count == 0)
            {
                // Insert season into database
                Insert(entry);

                return false; // Season was not previously present
            }
            return true; // Season already exists
        }
        public static bool Validate(SeasonLB season) =>
            Validate(new Entry { Id = season.Scope!, Name = season.Name });
    }

    public static class Coops
    {
        internal struct Entry
        {
            public int? Id;
            public string? CoopId;
            public string? ContractId;
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
        public static int GetId(Entry entry) {
            if (entry.Id.HasValue)
                return entry.Id.Value;
            return GetId(entry.ContractId!, entry.CoopId!);
        }

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
                Entry entry = entries[i];
                cmd.Parameters.AddWithValue($"$coop_id_{i}", entry.CoopId);
                cmd.Parameters.AddWithValue($"$contract_id_{i}", entry.ContractId);
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

        public static void Insert(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] queryValues = entries
                .Select((e, i) =>
                    $"($discord_id_{i}, $username_{i}, $ign_{i}, $nickname_{i})")
                .ToArray();

            query = $@"
                INSERT INTO {Name} (discord_id, username, ign, nickname)
                VALUES {string.Join(", ", queryValues)};";
            cmd = dbConnection.CreateCommand(query);

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];

                cmd.Parameters.AddWithValue($"$discord_id_{i}", entry.DiscordId);
                cmd.Parameters.AddWithValue($"$username_{i}", entry.Username);
                cmd.Parameters.AddWithValue($"$ign_{i}", entry.IGN);
                cmd.Parameters.AddWithValue($"$nickname_{i}", entry.Nickname);
            }
        }

        public static bool Validate(Entry[] entries)
        {

            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Get existing list of users
            List<Entry> existingEntries = new();

            query = $"SELECT discord_id, ign FROM {Name};";

            cmd = dbConnection.CreateCommand(query);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string discordId = reader.GetString(0);
                string ign = reader.GetString(1);
                existingEntries.Add(new Entry() { DiscordId = discordId, Username = "", IGN = ign });
            }

            // Find missing users
            string[] existingDiscordIds = existingEntries
                .Select(e => e.DiscordId)
                .ToArray();
            Entry[] missingEntries = entries
                .Where(e => !existingDiscordIds.Contains(e.DiscordId))
                .ToArray();

            // Insert missing users into the database
            MajUser majUser;
            Entry missingEntry;
            for (int i = 0; i < missingEntries.Count(); i++)
            {
                cmd.Dispose();
                missingEntry = missingEntries[i];

                try
                {
                    majUser = Player.DiscordIdToMajPlayer(missingEntry.DiscordId);
                    missingEntry.Username = majUser.DiscordUsername!;
                }
                catch (KeyNotFoundException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not find a username for `{missingEntry.IGN}` ({missingEntry.DiscordId}). Please enter manually below:");
                    Console.ResetColor();

                    // Username ccEntry
                    Console.Write("> ");
                    missingEntry.Username = Console.ReadLine() ?? "unknown_user";
                    if (string.IsNullOrWhiteSpace(missingEntry.Username))
                        missingEntry.Username = "unknown_user";
                }

                query = $@"
                INSERT INTO {Name}  (discord_id, username, ign)
                VALUES ($discord_id, $username, $ign);";
                cmd = dbConnection.CreateCommand(query);

                cmd.Parameters.AddWithValue("$discord_id", missingEntry.DiscordId);
                cmd.Parameters.AddWithValue("$username", missingEntry.Username);
                cmd.Parameters.AddWithValue("$ign", missingEntry.IGN);

                cmd.ExecuteNonQuery();
            }

            return missingEntries.Count() == 0; // Return true if no users were missing
        }
        public static bool Validate(string discordId, string username = "", string ign = "") =>
            Validate([new Entry { DiscordId = discordId, Username = username, IGN = ign }]);

        public static void ValidateIGN(Entry[] entries, Entry[] existingEntries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            foreach (Entry entry in entries)
            {
                Entry existingEntry = existingEntries
                    .FirstOrDefault(e => e.DiscordId == entry.DiscordId);

                if (existingEntry.DiscordId == null || existingEntry.IGN == entry.IGN)
                    continue; // IGN matches or is unknown, no update needed

                // IGN mismatch detected, prompt for update
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: IGN mismatch for Discord ID `{entry.DiscordId}`. Database has `{existingEntry.IGN}`, but new entry has `{entry.IGN}`.");
                Console.Write("\tWould you like to update the IGN in the database? ([Y]/N): ");
                Console.ResetColor();

                string? input = Console.ReadLine()?.ToLower();
                if (input == "n" || input == "no")
                    continue; // Skip update

                // Update IGN in database
                query = $@"
                    UPDATE {Name}
                    SET ign=$ign
                    WHERE discord_id=$discord_id;";
                cmd = dbConnection.CreateCommand(query);
                cmd.Parameters.AddWithValue("$ign", entry.IGN);
                cmd.Parameters.AddWithValue("$discord_id", entry.DiscordId);
                cmd.ExecuteNonQuery();
            }
        }
        public static void ValidateIGN(Entry entry, Entry[] existingEntries) => ValidateIGN([entry], existingEntries);
        public static void ValidateIGN(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            // Get existing list of users
            List<Entry> existingEntries = new();

            query = $"SELECT discord_id, ign FROM {Name};";

            cmd = dbConnection.CreateCommand(query);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string discordId = reader.GetString(0);
                string ign = reader.GetString(1);
                existingEntries.Add(new Entry() { DiscordId = discordId, Username = "", IGN = ign });
            }

            ValidateIGN(entries, [.. existingEntries]);
        }
        public static void ValidateIGN(Entry entry) =>
            ValidateIGN([entry]);
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

    public static object ValueOrDBNull(object? value) =>
        value ?? DBNull.Value;
}
