using System.Runtime.Serialization;
using Database;
using EggIncApi;
using Ei;
using Formatter;
using JsonCompilers;
using Newtonsoft.Json;

/**
 * TODO:
 *   - Integrate CLI interactions
 *   - Output generation
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

    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public CookieCache(SeasonLB season)
    {
        init(season);
    }
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public CookieCache(string seasonId)
    {
        Task<LBInfoResponse> lbInfoTask = Request.GetLBInfo();
        lbInfoTask.Wait();
        LBInfoResponse lbInfo = lbInfoTask.Result
            ?? throw new Exception("Failed to retrieve leaderboard info.");
        season = lbInfo.SeasonsList!
            .First(s => s.Scope == seasonId);
        if (season == null)
            throw new InvalidDataException($"Season ID invalid: {seasonId}");

        init(season);
    }
    public CookieCache()
    {
        Task<LBInfoResponse> lbInfoTask = Request.GetLBInfo();
        lbInfoTask.Wait();
        LBInfoResponse lbInfo = lbInfoTask.Result
            ?? throw new Exception("Failed to retrieve leaderboard info.");
        season = lbInfo.SeasonsList!.Last()
            ?? throw new InvalidDataException("No current season is active.");
        init(season);
    }

    private void init(SeasonLB season)
    {
        dbConnection.DataSource = dbName;
        dbConnection.Connect();

        if (!dbConnection.IsConnected())
            throw new InvalidOperationException($"Failed to connect to the `{dbConnection.DataSource}` database.");

        this.season = season;
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
        }
    }
    public void ProcessLeaderboards()
    {
        ProcessLeaderboard(season.Scope!);
        ProcessLeaderboard("ALL_TIME");
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
        ccEntries = Tables.CookieCache.FilterOutExistingEntries(
                ccEntries.Where(e => e.Coop.HasValue).ToArray()
            ).ToList();
        Tables.CookieCache.Insert(ccEntries.ToArray());
    }

    public List<string> GenerateDiscordOutput()
    {
        string query;
        Microsoft.Data.Sqlite.SqliteCommand cmd;

        // Fetch Cookie Cache
        List<Tables.TheJar.Entry> theJar = new();
        query = $@"
            SELECT name, cookies FROM TheJar
                WHERE season_id=$season_id";
        cmd = dbConnection.CreateCommand(query);
        cmd.Parameters.AddWithValue("$season_id", season.Scope);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            string name = reader.GetString(0);
            int cookies = reader.GetInt32(1);
            theJar.Add(new Tables.TheJar.Entry { Name = name, Cookies = cookies });
        }

        // Generate output strings
        var discordTimestampNow = new DiscordTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        string title = $"# Fastlane 🍪 Cache | {season.Name}";

        string header = $"""
            *[Jump here](<https://discord.com/channels/455380663013736479/1151593648539054100/1355345560718278726>) to find out how to gain :cookie:s*
            -# ||*Apologies for those with small screens*||

            _      _ *Last Updated* :brown_square::brown_square::brown_square: *{discordTimestampNow.Format(DiscordTimestampDisplay.Relative)}*
            :brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square::brown_square:
                  :glasspane:`       Player | 🍪s          `:glasspane:
            """;

        string body = string.Join("\n",
            theJar.Select(entry => $"_      _:glasspane:` {entry.Name.Substring(0, Math.Min(entry.Name.Length, 12)),12} | {entry.Cookies,-12} `:glasspane:"));

        string footer = "_           _" + string.Concat(Enumerable.Repeat(":glasspane:", 11));

        string fullBodyOut = header + "\n" + body + "\n" + footer;

        // Split output into Discord message segments
        List<string> discordSegments = StringFormatter.SplitToCharLimitByLines(fullBodyOut, 1500);

        discordSegments.Insert(0, title);

        return discordSegments;
    }

    public void CliStart()
    {

        string? input = "y";
        while (input == "y")
        {

            Console.Write($"Would you like to add {(ActiveContracts.Count > 0 ? "another" : 'a')} contract? (Y/[N]): ");
            input = Console.ReadLine()?.ToLower() ?? "n";
            Console.WriteLine(input);

            if (input != "y" && input != "yes")
                break;

            try
            {
                Contract selectedContract = ActiveContractBuilder.CliSelectContract();
                Console.WriteLine($"Selected contract: {selectedContract.Identifier} | {selectedContract.Name}"); // print the selected contract
                AddContract(selectedContract);
            }
            catch (InvalidDataException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        Formatter.StringFormatter.ConsoleSpiner consoleSpiner = new();
        Thread thread;

        // Build Coops for selected contracts
        Console.Write("Fetching coops for selected contracts...   ");
        thread = new Thread(new ThreadStart(BuildCoops));
        thread.Start();
        while (thread.IsAlive)
        {
            consoleSpiner.Turn();
            System.Threading.Thread.Sleep(200);
        }
        consoleSpiner.Stop();
        Console.WriteLine("Done.");

        // Process contracts
        Console.Write("Processing contracts...   ");
        thread = new Thread( new ThreadStart(ProcessContracts) );
        thread.Start();
        while (thread.IsAlive)
        {
            consoleSpiner.Turn();
            System.Threading.Thread.Sleep(200);
        }
        consoleSpiner.Stop();
        Console.WriteLine("Done.");

        // Process leaderboards
        Console.Write("Would you like to process the leaderboards? (Y/[N])");
        input = Console.ReadLine()?.ToLower() ?? "n";
        if (input == "y" || input == "yes")
        {
            Console.Write("Processing leaderboards...   ");
            thread = new Thread(new ThreadStart(ProcessLeaderboards));
            thread.Start();
            while (thread.IsAlive)
            {
                consoleSpiner.Turn();
                System.Threading.Thread.Sleep(200);
            }
            consoleSpiner.Stop();
            Console.WriteLine("Done.");
        }

        // Write to database
        Console.Write("Updating cookie cache database...   ");
        thread = new Thread(new ThreadStart(UpdateCache));
        thread.Start();
        while (thread.IsAlive)
        {
            consoleSpiner.Turn();
            System.Threading.Thread.Sleep(200);
        }
        consoleSpiner.Stop();
        Console.WriteLine("Done.");

        // Generate Discord output
        List<string> outputSegments = GenerateDiscordOutput();

        Console.WriteLine($"""
            {"\x1b[92m"}========================= Output Start ========================={"\x1b[39m"}

            {string.Join("\n", outputSegments)}

            {"\x1b[92m"}=========================  Output End  ========================={"\x1b[39m"}

            """); // "\x1b[92m" is green and "\x1b[39m" is reset color


        foreach (var (segment, index) in outputSegments.Select((v, i) => (v, i))) // print each segment of the !!fuc table
        {
            Console.Write($"Press ENTER to copy segment {index + 1}/{outputSegments.Count()} ");
            Console.WriteLine(index == 0 ? "(HEADER)" : index == outputSegments.Count() - 1 ? "(FOOTER)" : "");
            Console.ReadLine();
            ClipboardHelper.CopyToClipboard(segment);
        }
    }
}

internal static class Tables
{
    private static SQLiteConnection dbConnection = SQLiteConnection.Instance();

    public static class TheJar
    {
        public struct Entry
        {
            public string Name;
            public int Cookies;
        }

        public static string Name = "TheJar";
        public static bool Exists() => TableExists(Name);
    }

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

        public static Entry[] FilterOutExistingEntries(Entry[] entries)
        {
            List<Entry> nonExistingEntries = new();
            foreach (Entry e in entries)
            {
                if (EntryExists(e))
                    nonExistingEntries.Add(e);
            }

            return nonExistingEntries.ToArray();
        }
        public static bool EntryExists(Entry entry)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            query = $@"
                SELECT count(*) FROM {Name}
                WHERE season_id=$season_id
                    AND coop_id{(entry.Coop.HasValue ? "=$coop_id" : "IS NULL")}
                    AND user_id=$user_id
                    AND rule_id=$rule_id
                    AND additional_cookies IS {(entry.AdditionalCookies.HasValue ? "= $additional_cookies" : "NULL")};";
            cmd = dbConnection.CreateCommand(query);
            cmd.Parameters.AddWithValue("$season_id", entry.Season.Id);
            if (entry.Coop.HasValue)
                cmd.Parameters.AddWithValue("$coop_id", entry.Coop.Value.Id.HasValue ? entry.Coop.Value.Id.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$user_id", entry.User.DiscordId);
            cmd.Parameters.AddWithValue("$rule_id", entry.RuleId);
            if (entry.AdditionalCookies.HasValue)
                cmd.Parameters.AddWithValue("$additional_cookies", entry.AdditionalCookies.Value);
            long count = Convert.ToInt32(cmd.ExecuteScalar()!);

            return count > 0;
        }

        public static void Insert(Entry[] entries)
        {
            string query;
            Microsoft.Data.Sqlite.SqliteCommand cmd;

            string[] queryValues = entries
                .Select((e, i) =>
                    $"($season_id_{i}, $coop_id_{i}, $user_id_{i}, $rule_id_{i}, $additional_cookies_{i})")
                .ToArray();

            if (queryValues.Length == 0)
                return; // No entries to insert

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
