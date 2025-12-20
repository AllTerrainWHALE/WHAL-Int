using JsonCompilers;
using EggIncApi;
using Formatter;
using Ei;
using Maj;

namespace WHAL_Int;

internal class Program
{
    private static bool debug = false;
    private static readonly string command = "!!fluc";
    public static async Task Main(string[] args)
    {
        if (string.IsNullOrEmpty(Config.EID))
        {
            Console.WriteLine("\"EID.txt\" not found in root directory, please create the file and only put your EID in the file.");
            return;
        }

        /* =======================
           =  Command line args  =
           ======================= */

        debug = args.Contains("--debug") || args.Contains("-d");
        bool reverse = args.Contains("--reverse") || args.Contains("-r");

        CoopFlags targetFlags = new CoopFlags
        {
            SpeedRun = args.Contains("--speedrun") || args.Contains("-sr"),
            FastRun  = args.Contains("--fastrun")  || args.Contains("-fr"),
            AnyGrade = args.Contains("--anygrade") || args.Contains("-ag"),
            Carry    = args.Contains("--carry")    || args.Contains("-c")
        };
        if (targetFlags.Flags.Count() == 0)
        { // if no targetFlags are set, set SR and FR targetFlags as default
            targetFlags.SpeedRun = true;
            targetFlags.FastRun  = true;
        }



        /* =====================
           =  Get contract id  =
           ===================== */

        var contracts = ActiveContract.PeriodicalsContracts;

        // ask user to select a contract
        Console.WriteLine("Select contract ID:");
        int counter = 1;
        foreach (var contract in contracts)
        {
            if (counter == 1) // highlight the first contract
                Console.Write($"\t[{counter}] ");
            else // normal print for other contracts
                Console.Write($"\t({counter}) ");

            Console.WriteLine($"{contract.Identifier} | {contract.Name}");
            counter++;
        }

        Console.Write("> ");
        string? input = Console.ReadLine();
        if (!int.TryParse(input, out int selectedContractIdx))
        { // if input is not a number, take the first contract
            selectedContractIdx = 0;
        }
        else
        { // if input is a number, subtract 1 to get the index
            selectedContractIdx -= 1;
        }
        var selectedContract = contracts.ElementAt(selectedContractIdx); // get the contract at the selected index
        Console.WriteLine($"\nSelected contract: {selectedContract.Identifier} | {selectedContract.Name}"); // print the selected contract

        string contractId = selectedContract.Identifier; // get the contract id from the selected contract




        ///* ======================
        //   =  Get JsonCompilers coops  =
        //   ====================== */

        Majeggstics majeggstics = new Majeggstics();
        majeggstics.AddContract(contractId);
        majeggstics.FetchCoopsForContract(contractId, force: true);
        majeggstics.BuildCoops();

        var orderedCoops = majeggstics.ActiveContracts[contractId].OrderCoopsBy(x => x);


        Console.WriteLine("Coop Codes:");
        //foreach (string flag in typeof(CoopFlags).GetProperties().Select(p => p.Name))
        foreach (string flag in targetFlags.Flags)
        {
            string[] codes = majeggstics.ActiveContracts[contractId].Coops
                .Where(c => c.CoopFlags.Flags.Contains(flag))
                .Select(c => c.CoopId)
                .ToArray();
            Console.WriteLine($"\t{flag}: {string.Join(", ", codes)}");
        }
        Console.WriteLine();



        /* ==========================
           =  Construct !!fuc table  =
           ========================== */

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var discordTimestampNow = new DiscordTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        Func<Coop?, bool> srExpression = c => c!.CoopFlags.SpeedRun!.Value;
        Func<Coop?, bool> frExpression = c => c!.CoopFlags.FastRun!.Value;
        Func<Coop?, bool> agExpression = c => c!.CoopFlags.AnyGrade!.Value;
        Func<Coop?, bool> cExpression = c => c!.CoopFlags.Carry!.Value; // && c.CoopId.Substring(0, 3) != "f--";

        var outputSegments = new List<string>
        {
            $"## {EggType.ToDiscordEmoji(selectedContract.Egg)} {selectedContract.Name} | Fastlane Leaderboards" // add the header to the output segments
        }; // create a list to hold the output segments

        string coopTable = ""; string playerTable = "";

        string starter = $"Last updated: {discordTimestampNow.Format(DiscordTimestampDisplay.Relative)}\n"; // create a starter string for the output segments

        if (targetFlags.SpeedRun.Value && orderedCoops.Any(c => c.CoopFlags.SpeedRun == true)) // if the speedrun flag is set and there are speedrun coops
        {
            coops = orderedCoops.Where(srExpression).ToArray();

            coopTable = $"""
                {starter}
                {SRTable(coops)}
                """;
            playerTable = PlayerTable(coops);

            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(coopTable));

            if ((outputSegments.Last() + playerTable).Length > 2000)
                outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(playerTable));
            else
                outputSegments[outputSegments.Count - 1] += "\n" + playerTable; // append the player table to the last segment if it fits

            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (targetFlags.FastRun.Value && orderedCoops.Any(c => c.CoopFlags.FastRun == true)) // if the fastrun flag is set and there are fastrun coops
        {
            coops = orderedCoops.Where(frExpression).ToArray();

            coopTable = $"""
                {starter}
                {FRTable(coops)}
                """;
            playerTable = PlayerTable(coops);

            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(coopTable));

            if ((outputSegments.Last() + playerTable).Length > 2000)
                outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(playerTable));
            else
                outputSegments[outputSegments.Count - 1] += "\n" + playerTable; // append the player table to the last segment if it fits

            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (targetFlags.AnyGrade.Value && orderedCoops.Any(c => c.CoopFlags.AnyGrade == true)) // if the anygrade flag is set and there are anygrade coops
        {
            coops = orderedCoops.Where(agExpression).ToArray();

            coopTable = $"""
                {starter}
                {AGTable(coops)}
                """;
            playerTable = PlayerTable(coops);

            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(coopTable));

            if ((outputSegments.Last() + playerTable).Length > 2000)
                outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(playerTable));
            else
                outputSegments[outputSegments.Count - 1] += "\n" + playerTable; // append the player table to the last segment if it fits

            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (flags["Carry"] && orderedCoops.Any(cExpression)) // if the anygrade flag is set and there are anygrade coops
        {
            coops = orderedCoops.Where(cExpression).ToArray();

            coopTable = $"""
                {starter}
                {FRTable(coops)}
                """;
            playerTable = PlayerTable(coops);

            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(coopTable));

            if ((outputSegments.Last() + playerTable).Length > 2000)
                outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines(playerTable));
            else
                outputSegments[outputSegments.Count - 1] += "\n" + playerTable; // append the player table to the last segment if it fits

            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        outputSegments.Add($"""
            _ _
            *`{command}` to summon an update!*
            *Note that this is NOT a Wonky command, and is still generated by WHAL-Int*
            """);

        // test for combining the tables into one message
        string combinedTables = string.Join('\n', outputSegments.GetRange(1, outputSegments.Count() - 2)); // combine the tables into a single string, excluding the header and footer
        if (targetFlags.Flags.Count() >= outputSegments.Count()-2 && combinedTables.Length <= 2000)
        {
            outputSegments[1] = combinedTables; // if all targetFlags are set and the combined tables are less than 2000 characters, combine the tables into the first segment
            outputSegments.RemoveRange(2, outputSegments.Count() - 3); // remove the other segments
        }

        Console.WriteLine($"""
            {"\x1b[92m"}========================= Output Start ========================={"\x1b[39m"}

            {string.Join("\n", outputSegments)}

            {"\x1b[92m"}=========================  Output End  ========================={"\x1b[39m"}

            """); // "\x1b[92m" is green and "\x1b[39m" is reset color


        foreach (var (segment,index) in outputSegments.Select((v,i) => (v,i))) // print each segment of the !!fuc table
        {
            Console.Write($"Press ENTER to copy segment {index+1}/{outputSegments.Count()} ");
            Console.WriteLine(index == 0 ? "(HEADER)" : index == outputSegments.Count() - 1 ? "(FOOTER)" : "");
            Console.ReadLine();
            ClipboardHelper.CopyToClipboard(segment);
        }
    }

    private static string SRTable(Coop[] coops)
    {
        // Create table for coop stats
        var coopTable = new Table<Coop>(); // create a new table for the coops
        coopTable.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
        coopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7));
        coopTable.AddColumn("Tokens", coop => StringFormatter.Centered($"{coop.TotalTokens}", 6));
        coopTable.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
        coopTable.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // Add the coops and players to the tables
        foreach (var coop in coops) { coopTable.AddDataPoint(coop); }

        return $"""
            **`{StringFormatter.Centered(" Speedruns ", coopTable.GetHeader().Length+1, fillChar: '—')}`**
            {coopTable.GetHeader()}
            {coopTable.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static string FRTable(Coop[] coops)
    {
        // Create table for coop stats
        var coopTable = new Table<Coop>(); // create a new table for the coops
        coopTable.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
        //table.AddColumn(" Layrate ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippingRate, strLen: 5)}/h", 9));
        coopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7));
        coopTable.AddColumn(" Ship ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippedEggs, strLen: 6)}", 6));
        coopTable.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
        coopTable.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // Add the coops and players to the tables
        foreach (var coop in coops) { coopTable.AddDataPoint(coop); }

        return $"""
            **`{StringFormatter.Centered(" Fastruns ", coopTable.GetHeader().Length+1, fillChar: '—')}`**
            {coopTable.GetHeader()}
            {coopTable.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static string AGTable(Coop[] coops)
    {
        var table = new Table<Coop>(); // create a new table for the coops
        table.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
        table.AddColumn("  Rate  ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippingRate, strLen: 6)}/h", 8));
        table.AddColumn(" Ship ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippedEggs, strLen: 6)}", 6));
        table.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
        table.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // add the coops to the table
        foreach (var coop in coops) { table.AddDataPoint(coop); }
        return $"""
            **`{StringFormatter.Centered(" Anygrades ", table.GetHeader().Length+1, fillChar: '—')}`**
            {table.GetHeader()}
            {table.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static string PlayerTable(Coop[] coops)
    {
        // Get list of all players in the coops, and order them
        var players = coops.Where(c => c.OnTrack)
            .SelectMany(c => c.Contributors)
            .Where(p => p.UserName != "[departed]")
            .OrderBy(p => p);
        var playersSubset = players.Take(10);

        // Create table for player stats
        int playerRow = 0;
        int playerRowDigits = (int)Math.Floor(Math.Log10(Math.Max(1, players.Count()))) + 1; // get the number of digits in the player count for formatting purposes

        var table = new Table<Player>(); // create a new table for the players
        table.AddColumn(new string('#', playerRowDigits), _ => StringFormatter.RightAligned($"{++playerRow}", playerRowDigits, fillChar: '0'), playerRowDigits); // auto incrementing row number column
        table.AddColumn(" Player ", player => $"{StringFormatter.LeftAligned(player.UserName.Substring(0, Math.Min(8, player.UserName.Length)), 8)}");
        table.AddColumn("  CS  ", player => StringFormatter.Centered($"{Math.Round(player.ContractScore)}", 6));
        table.AddColumn(" Rate ", player => StringFormatter.Centered($"{StringFormatter.BigNumberToString(player.ContributionRate * Duration.SECONDS_IN_AN_HOUR, strLen: 6)}", 6));

        if (debug) // if debug flag is set, add additional columns to the player table
        {
            table.AddColumn(" CR ", player => StringFormatter.Centered($"{Math.Round(player.ContributionRatio, 2)}", 4));
            table.AddColumn(" TS ", player => StringFormatter.Centered($"{Math.Round(player.TeamworkScore, 2)}", 4));
            table.AddColumn(" CR_F ", player => StringFormatter.Centered($"{Math.Round(player.ChickenRunFactor, 2)}", 6));
            table.AddColumn("Buff_F", player => StringFormatter.Centered($"{Math.Round(player.BuffTimeValue, 0)}", 6));
            table.AddColumn("Tok_F ", player => StringFormatter.Centered($"{Math.Round(player.TokenFactor, 2)}", 6));
        }

        // Add the players to the table
        foreach (var player in playersSubset) { table.AddDataPoint(player); }

        // Calculate average CS
        double averageCS = players.Any() ? Math.Ceiling(players.Select(p => p.ContractScore).Average()) : 0;

        // Return the table as a string
        return $"""
            ```
            {table.GetHeader()}
            {new string('—', table.GetHeader().Length + 2)}
            {table.GetTable()}
            {new string([.. Enumerable.Range(0, table.GetHeader().Length + 2).Select(i => i % 2 == 0 ? '—' : ' ')])}
            Avg. CS -> {averageCS}
            {new string('—', table.GetHeader().Length + 2)}
            Only showing top {playersSubset.Count()} players. CS calculations assume n-1 CRs {(coops.All(c => c.IsLeggacy) ? "and max tval."
            : "\nCS likely off due to uncertainty in new formula understanding.")}
            ```
            """;
    }

    private static void Debug(PeriodicalsResponse periodicals, MajCoopsResponse majCoopsResponse)
    {

    }
}
