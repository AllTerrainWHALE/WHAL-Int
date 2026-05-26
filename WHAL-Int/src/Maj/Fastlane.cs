using Ei;
using Formatter;
using JsonCompilers;

namespace Maj;
public class Fastlane : Majeggstics
{
    public static string cmdStr = "!!fluc";

    private static Table<Coop>? _commonCoopTable;
    private static Table<Coop> commonCoopTable
    {
        get
        {
            if (_commonCoopTable == null)
            {
                _commonCoopTable = new();
                _commonCoopTable.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
                _commonCoopTable.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
                _commonCoopTable.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");
                _commonCoopTable.SetFooter("`Primary order based off of duration`");
            }
            return _commonCoopTable;
        }
    }

    private static Table<Player>? _commonPlayerTable;
    private static Table<Player> commonPlayerTable
    {
        get
        {
            if (_commonPlayerTable == null)
            {
                int playerRow = 0;
                _commonPlayerTable = new();
                _commonPlayerTable.AddColumn(new string('#', commonPlayerIndexDigits), _ => StringFormatter.RightAligned($"{++playerRow}", commonPlayerIndexDigits, fillChar: '0'), commonPlayerIndexDigits); // auto incrementing row number column
                _commonPlayerTable.AddColumn(" Player ", player => $"{StringFormatter.LeftAligned(player.IGN.Substring(0, Math.Min(8, player.IGN.Length)), 8)}");
                _commonPlayerTable.AddColumn("  CS  ", player => StringFormatter.Centered($"{Math.Round(player.ContractScore)}", 6));
                _commonPlayerTable.AddColumn(" Rate ", player => StringFormatter.Centered($"{StringFormatter.BigNumberToString(player.ContributionRate * Duration.SECONDS_IN_AN_HOUR, strLen: 6)}", 6));
            }
            return _commonPlayerTable;
        }
    }
    private static int commonPlayerIndexDigits = 2;

    private static Table<Coop>? _speedrunCoopTable;
    private static Table<Coop> speedrunCoopTable
    {
        get
        {
            if (_speedrunCoopTable == null)
            {   // Deep copy commmon table and add speedrun specific columns
                _speedrunCoopTable = commonCoopTable.Clone();
                _speedrunCoopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7), position: 1);
                _speedrunCoopTable.AddColumn("Tokens", coop => StringFormatter.Centered($"{coop.TotalTokens}", 6), position: 2);
            }
            return _speedrunCoopTable;
        }
    }

    private static Table<Player>? _speedrunPlayerTable;
    private static Table<Player> speedrunPlayerTable
    {
        get
        {
            if (_speedrunPlayerTable == null)
            {   // Deep copy commmon table and add speedrun specific columns
                int playerRow = 0;
                _speedrunPlayerTable = commonPlayerTable.Clone();
                _speedrunPlayerTable.RemoveColumn(0);
                _speedrunPlayerTable.AddColumn(new string('#', speedrunPlayerIndexDigits), _ => StringFormatter.RightAligned($"{++playerRow}", speedrunPlayerIndexDigits, fillChar: '0'), speedrunPlayerIndexDigits, position: 0); // auto incrementing row number column
            }
            return _speedrunPlayerTable;
        }
    }
    private static int speedrunPlayerIndexDigits = 2;

    private static Table<Coop>? _fastrunCoopTable;
    private static Table<Coop> fastrunCoopTable
    {
        get
        {
            if (_fastrunCoopTable == null)
            {   // Deep copy commmon table and add fastrun specific columns
                _fastrunCoopTable = commonCoopTable.Clone();
                _fastrunCoopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7), position: 1);
                _fastrunCoopTable.AddColumn(" Ship ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippedEggs, strLen: 6)}", 6), position: 2);
            }
            return _fastrunCoopTable;
        }
    }

    private static Table<Player>? _fastrunPlayerTable;
    private static Table<Player> fastrunPlayerTable
    {
        get
        {
            if (_fastrunPlayerTable == null)
            {   // Deep copy commmon table and add fastrun specific columns
                int playerRow = 0;
                _fastrunPlayerTable = commonPlayerTable.Clone();
                _fastrunPlayerTable.RemoveColumn(0);
                _fastrunPlayerTable.AddColumn(new string('#', fastrunPlayerIndexDigits), _ => StringFormatter.RightAligned($"{++playerRow}", fastrunPlayerIndexDigits, fillChar: '0'), fastrunPlayerIndexDigits, position: 0); // auto incrementing row number column
            }
            return _fastrunPlayerTable;
        }
    }
    private static int fastrunPlayerIndexDigits = 2;

    private static Table<Coop>? _anygradeCoopTable;
    private static Table<Coop> anygradeCoopTable
    {
        get
        {
            if (_anygradeCoopTable == null)
            {   // Deep copy commmon table and add anygrade specific columns
                _anygradeCoopTable = commonCoopTable.Clone();
                _anygradeCoopTable.AddColumn("  Rate  ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippingRate, strLen: 6)}/h", 8), position: 1);
                _anygradeCoopTable.AddColumn(" Ship ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippedEggs, strLen: 6)}", 6), position: 2);
            }
            return _anygradeCoopTable;
        }
    }

    private static Table<Player>? _anygradePlayerTable;
    private static Table<Player> anygradePlayerTable
    {
        get
        {
            if (_anygradePlayerTable == null)
            {   // Deep copy commmon table and add anygrade specific columns
                int playerRow = 0;
                _anygradePlayerTable = commonPlayerTable.Clone();
                _anygradePlayerTable.RemoveColumn(0);
                _anygradePlayerTable.AddColumn(new string('#', anygradePlayerIndexDigits), _ => StringFormatter.RightAligned($"{++playerRow}", anygradePlayerIndexDigits, fillChar: '0'), anygradePlayerIndexDigits, position: 0); // auto incrementing row number column
            }
            return _anygradePlayerTable;
        }
    }
    private static int anygradePlayerIndexDigits = 2;

    private ActiveContract? targetActiveContract;

    public Fastlane(CoopFlags? targetFlags = null) : base(targetFlags)
    {
        
    }

    public void PopulateTables(string? targetId = null)
    {
        if (targetId != null)
            targetActiveContract = ActiveContracts.FirstOrDefault(ac => ac.Key == targetId).Value
                ?? throw new KeyNotFoundException("`targetId` cannot be found in ActiveContracts. The desired contract must first be declared and fetched.");
        if (targetActiveContract == null)
            throw new Exception("No target ActiveContract has been set.");




        Table<Coop> targetCoopTable;
        Table<Player> targetPlayerTable;
        //int targetPlayerIndexDigits;
        foreach (var flag in TargetFlags.Flags)
        {
            if (flag == "Carry") continue;

            // Grab coops of the flag type
            List<Coop> coops = targetActiveContract.Coops
                .Where(c => c.CoopFlags.Flags.Contains(flag))
                .ToList();

            // Get list of all the players, order them, and take subset
            List<Player> players = coops.Where(c => c.OnTrack)
                .SelectMany(c => c.Contributors)
                .Where(p => p.IGN != "[departed]")
                .OrderBy(p => p)
                .ToList();
            List<Player> playerSubset = players.Take(10).ToList();

            switch (flag)
            {
                case "SpeedRun":
                    targetCoopTable = speedrunCoopTable;
                    targetPlayerTable = speedrunPlayerTable;
                    break;
                case "FastRun":
                    targetCoopTable = fastrunCoopTable;
                    targetPlayerTable = fastrunPlayerTable;
                    break;
                case "AnyGrade":
                    targetCoopTable = anygradeCoopTable;
                    targetPlayerTable = anygradePlayerTable;
                    break;

                default:
                    throw new Exception($"Unknown coop flag encountered: {flag}");
            };

            coops.ForEach(targetCoopTable.AddDataPoint);
            playerSubset.ForEach(targetPlayerTable.AddDataPoint);
            targetPlayerTable.SetFooter($"{(players.Count() > 10 ? $"Only showing top {playerSubset.Count()} players. " : "")}CS calculations assume n-1 CRs {(coops.Any(c => c.UseOldScoring) ? "and max tval." : "")}");

        }
    }

    public List<string> GenerateDiscordOutput()
    {
        if (targetActiveContract == null)
            throw new Exception("No target ActiveContract has been set.");

        // Generate output strings
        var discordTimestampNow = new DiscordTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        string title = $"## {EggType.ToDiscordEmoji(targetActiveContract.Contract.Egg)} {targetActiveContract.Contract.Name} | Fastlane Leaderboards";

        string header = $"Last updated: {discordTimestampNow.Format(DiscordTimestampDisplay.Relative)}\n";

        List<string> bodySegments = new();
        Table<Coop> targetCoopTable;
        Table<Player> targetPlayerTable;
        foreach (string flag in TargetFlags.Flags)
        {
            if (!targetActiveContract.Coops.Any(c => c.CoopFlags.Flags.Contains(flag)))
                continue;

            (targetCoopTable, targetPlayerTable) = flag switch
            {
                "SpeedRun" => (speedrunCoopTable,speedrunPlayerTable),
                "FastRun"  => (fastrunCoopTable,fastrunPlayerTable),
                "AnyGrade" => (anygradeCoopTable,anygradePlayerTable),

                _ => throw new Exception($"Unknown coop flag encountered: {flag}")
            };

            Player[] players = targetCoopTable.GetDataPoints()
                .Where(c => c.OnTrack)
                .SelectMany(c => c.Contributors)
                .Where(p => p.IGN != "[departed]")
                .ToArray();
            double avgCS = players.Any() ? Math.Ceiling(players.Select(p => p.ContractScore).Average()) : 0;

            string coopTable = $"""
                _ _
                **`{StringFormatter.Centered($" {flag} ", targetCoopTable.GetHeader().Length + 1, fillChar: '—')}`**
                {targetCoopTable.GetHeader()}
                {targetCoopTable.GetTable()}
                {targetCoopTable.GetFooter()}
                """;
            string playerTable = $"""
                ```
                {targetPlayerTable.GetHeader()}
                {new string('—', targetPlayerTable.GetHeader().Length + 2)}
                {targetPlayerTable.GetTable()}
                {new string([.. Enumerable.Range(0, targetPlayerTable.GetHeader().Length + 2).Select(i => i % 2 == 0 ? '—' : ' ')])}
                Avg. CS -> {avgCS}
                {new string('—', targetPlayerTable.GetHeader().Length + 2)}
                {targetPlayerTable.GetFooter()}
                ```
                """;

            if (bodySegments.Count == 0)
               coopTable = header + coopTable;

            bodySegments.AddRange(StringFormatter.SplitToCharLimitByLines(coopTable));

            if ((bodySegments.Last() + playerTable).Length > 2000)
                bodySegments.AddRange(StringFormatter.SplitToCharLimitByLines(playerTable));
            else
                bodySegments[bodySegments.Count - 1] += "\n" + playerTable;
        }

        string footer = $"""
            _ _
            *`{cmdStr}` to summon an update!*
            *Note that this is NOT a Wonky command, and is instead generated by WHAL-E*
            """;

        // Split output into Discord message segments
        List<string> discordSegments = new()
        {
            title,
            footer
        };
        discordSegments.InsertRange(1, bodySegments);

        return discordSegments;
    }

    public void CliStart()
    {
        while (true)
        {
            try
            {
                Contract selectedContract = ActiveContractBuilder.CliSelectContract();
                Console.WriteLine($"Selected contract: {selectedContract.Identifier} | {selectedContract.Name}\n"); // print the selected contract
                AddContract(selectedContract);
                break;
            }
            catch (InvalidDataException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        string contractId = ActiveContracts.Keys.Last();

        // Fetch coop codes for selected contract
        Console.WriteLine("Fetching coops for selected contract...   ");
        FetchMajCoops(contractId, TargetFlags);

        foreach (string flag in TargetFlags.Flags)
        {
            string[] codes = FetchMajCoops(contractId, flag)
                .Select(c => c.Code!)
                .ToArray();
            if (codes.Length > 0)
                Console.WriteLine($"  {flag} ({codes.Length}): {string.Join(", ", codes)}");
        }
        Console.WriteLine();


        // Build fetched coops
        Console.WriteLine("Building fetched coops...   ");
        BuildCoops();

        // Order coops
        ActiveContracts[contractId].OrderCoopsBy(x => x);

        // Populate tables
        Console.WriteLine("Populating tables...   ");
        PopulateTables(ActiveContracts.Last().Key);
        Console.WriteLine();

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
