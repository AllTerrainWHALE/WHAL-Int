using Ei;
using Majcoops;
using WHAL_Int.EggIncApi;
using WHAL_Int.Formatter;
using WHAL_Int.Maj;

namespace WHAL_Int;

internal class Program
{
    private static bool debug = false;
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
        Dictionary<string, bool> flags = new()
        {
            { "SpeedRun", args.Contains("--speedrun") || args.Contains("-sr") },
            { "FastRun" , args.Contains("--fastrun")  || args.Contains("-fr") },
            { "AnyGrade", args.Contains("--anygrade") || args.Contains("-ag") }
        };
        if (!flags.ContainsValue(true)) // if no flags are set, set SR and FR flags
        {
            flags["SpeedRun"] = true;
            flags["FastRun"]  = true;
        }

        /* =====================
           =  Get contract id  =
           ===================== */

        var periodicals = await Request.GetPeriodicals();
        var contracts = periodicals.Contracts.Contracts
            .OrderByDescending(c => c.StartTime)
            .Where(c => c.Identifier != "first-contract");

        // ask user to select a contract
        Console.WriteLine("Select contract ID:");
        int counter = 1;
        foreach (Contract contract in contracts)
        {
            if (counter == 1) // highlight the first contract
                Console.Write($"\t[{counter}] ");
            else // normal print for other contracts
                Console.Write($"\t({counter}) ");

            Console.WriteLine($"{contract.Identifier} | {contract.Name}");
            counter++;
        }

        Console.Write("> ");
        if (!int.TryParse(Console.ReadLine(), out int selectedContractIdx))
        { // if input is not a number, take the first contract
            selectedContractIdx = 0;
        }
        else
        { // if input is a number, subtract 1 to get the index
            selectedContractIdx -= 1;
        }
        Contract selectedContract = contracts.ElementAt(selectedContractIdx); // get the contract at the selected index
        Console.WriteLine($"\nSelected contract: {selectedContract.Identifier} | {selectedContract.Name}"); // print the selected contract

        string contractId = selectedContract.Identifier; // get the contract id from the selected contract


        /* ======================
           =  Get Maj SR coops  =
           ====================== */

        var majCoopsResponse = await Request.GetMajCoops(contractId); // get the maj coops for the selected contract
        var majCoops = majCoopsResponse.Items.Last().Coops; // get the coops from the maj coops response

        Dictionary<string, string[]> coopCodesAndFlags = new() // create a dictionary to hold the maj coop codes by their coop flags
        {
            { "SpeedRun", majCoops.Where(c => c.CoopFlags.SpeedRun == true && c.Code != null).Select(c => c.Code!).ToArray() },
            { "FastRun" , majCoops.Where(c => c.CoopFlags.FastRun  == true && c.Code != null).Select(c => c.Code!).ToArray() },
            { "AnyGrade", majCoops.Where(c => c.CoopFlags.AnyGrade == true && c.Code != null).Select(c => c.Code!).ToArray() },
            { "Carry"   , majCoops.Where(c => c.CoopFlags.Carry    == true && c.Code != null).Select(c => c.Code!).ToArray() }
        };

        Console.WriteLine("Coop codes:");
        foreach (var flag in flags)
        {
            if (flag.Value) // if the flag is set, print the coop codes for that flag
            {
                Console.WriteLine($"\t{flag.Key}: {string.Join(",", coopCodesAndFlags[flag.Key])}");
            }
        }


        /* ==============================
           =  Build contract and coops  =
           ============================== */

        var activeContract = await new ActiveContractBuilder(contractId).Build(); // build the active contract from the contract id

        string[] coopCodes = [.. coopCodesAndFlags
            .Where(kvp => flags.ContainsKey(kvp.Key) && flags[kvp.Key]) // filter the coop codes by the flags that are set
            .ToDictionary().Values // get the values of the filtered coop codes
            .SelectMany(c => c) // flatten the coop codes into a single list
            .Where(c => !string.IsNullOrEmpty(c)) // filter out any empty coop codes
            .Distinct()]; // convert to an array

        var tasks = new List<Task<Coop?>>();
        foreach (string coopCode in coopCodes)
        { // loop through the coop codes and add them to the tasks list
            tasks.Add(activeContract.AddCoop(coopCode));
        }

        // wait for all tasks to complete and get the results
        var coops = await Task.WhenAll(tasks);
        coops = [.. coops.Where(c => c != null)]; // filter out any null coops
        var orderedCoops = coops.OrderBy(x => x); // order the coops by their predicted duration
        if (reverse)
        { // if the reverse flag is set, reverse the order of the coops
            orderedCoops = orderedCoops.Reverse().OrderBy(x => 0);
        }

        // set coop flags based on the coop codes
        foreach (var coop in orderedCoops)
        {
            coop.CoopFlags.SpeedRun = coopCodesAndFlags["SpeedRun"].Contains(coop.CoopId);
            coop.CoopFlags.FastRun  = coopCodesAndFlags["FastRun"].Contains(coop.CoopId);
            coop.CoopFlags.AnyGrade = coopCodesAndFlags["AnyGrade"].Contains(coop.CoopId);
            coop.CoopFlags.Carry    = coopCodesAndFlags["Carry"].Contains(coop.CoopId);
        }

        Console.WriteLine();

        //Console.WriteLine($"{orderedCoops.ElementAt(2).CoopId}, {orderedCoops.ElementAt(2).PredictedDuration.Format()}");
        //Console.WriteLine(string.Join("\n", orderedCoops.ElementAt(2).contributors.Select(c =>
        //    $"{c.UserName}: {Math.Round(c.ContributionRatio, 3)}, {Math.Round(c.BuffTimeValue)}, {Math.Round(c.TeamworkScore, 3)}, {Math.Round(c.ContractScore)}"
        //)));
        ////Console.WriteLine(string.Join("\n", orderedCoops.ElementAt(2).contributors.Select(c =>
        ////    $"{c.userName}: {Math.Round(c.contributionRatio, 3)}, {Math.Round(c.contributionRate,3)}, {Math.Round(c.offlineContribution, 3)}, {Math.Round(c.predictedContribution)}"
        ////)));
        //throw new NotImplementedException();


        /* ==========================
           =  Construct !!fuc table  =
           ========================== */

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var discordTimestampNow = new DiscordTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var outputSegments = new List<string>(); // create a list to hold the output segments
        outputSegments.Add($"## {EggType.ToDiscordEmoji(selectedContract.Egg)} {selectedContract.Name} | Fastlane Leaderboards"); // add the header to the output segments

        string starter = $"Last updated: {discordTimestampNow.Format(DiscordTimestampDisplay.Relative)}\n"; // create a starter string for the output segments

        if (flags["SpeedRun"] && orderedCoops.Any(c => c.CoopFlags.SpeedRun == true)) // if the speedrun flag is set and there are speedrun coops
        {
            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines($"""
                {starter}
                {SRTable(orderedCoops.Where(c => c.CoopFlags.SpeedRun == true).ToArray())}
                """));
            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (flags["FastRun"] && orderedCoops.Any(c => c.CoopFlags.FastRun == true)) // if the fastrun flag is set and there are fastrun coops
        {
            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines($"""
                {starter}
                {FRTable(orderedCoops.Where(c => c.CoopFlags.FastRun == true).ToArray())}
                """));
            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (flags["AnyGrade"] && orderedCoops.Any(c => c.CoopFlags.AnyGrade == true)) // if the anygrade flag is set and there are anygrade coops
        {
            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines($"""
                {starter}
                {AGTable(orderedCoops.Where(c => c.CoopFlags.AnyGrade == true).ToArray())}
                """));
            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        outputSegments.Add("""
            _ _
            *`!!fuc` to summon an update!*
            *Note that this is NOT a Wonky command, and is still generated by WHAL-Int*
            """);

        // test for combining the tables into one message
        string combinedTables = string.Join('\n', outputSegments.GetRange(1, outputSegments.Count() - 2)); // combine the tables into a single string, excluding the header and footer
        if (flags.Where(f => f.Value).Count() >= outputSegments.Count()-2 && combinedTables.Length <= 2000)
        {
            outputSegments[1] = combinedTables; // if all flags are set and the combined tables are less than 2000 characters, combine the tables into the first segment
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
        // Get list of all players in the coops, and order them
        var players = coops.Where(c => c.OnTrack)
            .SelectMany(c => c.Contributors)
            .Where(p => p.UserName != "[departed]")
            .OrderBy(p => p);
        var playersSubset = players.Take(10);

        // Create table for coop stats
        var coopTable = new Table<Coop>(); // create a new table for the coops
        coopTable.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
        coopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7));
        coopTable.AddColumn("Tokens", coop => StringFormatter.Centered($"{coop.TotalTokens}", 6));
        coopTable.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
        coopTable.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // Create table for player stats
        int playerRow = 0;
        int playerRowDigits = (int)Math.Floor(Math.Log10(Math.Max(1, players.Count()))) + 1; // get the number of digits in the player count for formatting purposes

        var playerTable = new Table<Player>(); // create a new table for the players
        playerTable.AddColumn(new string('#', playerRowDigits), _ => StringFormatter.RightAligned($"{++playerRow}", playerRowDigits, fillChar:'0'), playerRowDigits); // auto incrementing row number column
        playerTable.AddColumn(" Player ", player => $"{StringFormatter.LeftAligned(player.UserName.Substring(0,Math.Min(8, player.UserName.Length)), 8)}");
        playerTable.AddColumn("  CS  ", player => StringFormatter.Centered($"{Math.Round(player.ContractScore)}", 6));
        playerTable.AddColumn(" Rate ", player => StringFormatter.Centered($"{StringFormatter.BigNumberToString(player.ContributionRate * Duration.SECONDS_IN_AN_HOUR, strLen:6)}", 6));

        if (debug) // if debug flag is set, add additional columns to the player table
        {
            playerTable.AddColumn(" CR ", player => StringFormatter.Centered($"{Math.Round(player.ContributionRatio, 2)}", 6));
            playerTable.AddColumn(" TS ", player => StringFormatter.Centered($"{Math.Round(player.TeamworkScore, 2)}", 6));
            playerTable.AddColumn(" CR_F ", player => StringFormatter.Centered($"{Math.Round(player.ChickenRunFactor, 2)}", 6));
            playerTable.AddColumn("Buff_F", player => StringFormatter.Centered($"{Math.Round(player.BuffTimeValue, 2)}", 6));
            playerTable.AddColumn("Tok_F ", player => StringFormatter.Centered($"{Math.Round(player.TokenFactor, 2)}", 6));
        }

        // Add the coops and players to the tables
        foreach (var coop in coops) { coopTable.AddDataPoint(coop); }
        foreach (var player in playersSubset) { playerTable.AddDataPoint(player); }

        double averageCS = players.Any() ? Math.Ceiling(players.Select(p => p.ContractScore).Average()) : 0;

        return $"""
            **`{StringFormatter.Centered(" Speedruns ", coopTable.GetHeader().Length+1, fillChar: '—')}`**
            {coopTable.GetHeader()}
            {coopTable.GetTable()}
            `Primary order based off of duration`
            ```
            {playerTable.GetHeader()}
            {new string('—', playerTable.GetHeader().Length+2)}
            {playerTable.GetTable()}
            {new string([.. Enumerable.Range(0,(playerTable.GetHeader().Length + 2)).Select(i => i % 2 == 0 ? '—' : ' ')])}
            Avg. CS -> {(averageCS > 0 ? averageCS : "null")}
            {new string('—', playerTable.GetHeader().Length + 2)}
            Only showing top {playersSubset.Count()} players. CS calculations assume n-1 CRs and max Tval.
            ```
            """;
    }

    private static string FRTable(Coop[] coops)
    {
        // Get list of all players in the coops, and order them
        var players = coops.Where(c => c.OnTrack)
            .SelectMany(c => c.Contributors)
            .Where(p => p.UserName != "[departed]")
            .OrderBy(p => p);
        var playersSubset = players.Take(10);

        // Create table for coop stats
        var coopTable = new Table<Coop>(); // create a new table for the coops
        coopTable.AddColumn("`  Coop  ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)}");
        //table.AddColumn(" Layrate ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippingRate, strLen: 5)}/h", 9));
        coopTable.AddColumn("Boosted", coop => StringFormatter.Centered($"{coop.BoostedCount}", 7));
        coopTable.AddColumn(" Ship ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.TotalShippedEggs, strLen: 6)}", 6));
        coopTable.AddColumn("Duration", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 8));
        coopTable.AddColumn("Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // Create table for player stats
        int playerRow = 0;
        int playerRowDigits = (int)Math.Floor(Math.Log10(Math.Max(1, players.Count()))) + 1; // get the number of digits in the player count for formatting purposes

        var playerTable = new Table<Player>(); // create a new table for the players
        playerTable.AddColumn(new string('#', playerRowDigits), _ => StringFormatter.RightAligned($"{++playerRow}", playerRowDigits, fillChar: '0'), playerRowDigits); // auto incrementing row number column
        playerTable.AddColumn(" Player ", player => $"{StringFormatter.LeftAligned(player.UserName.Substring(0, Math.Min(8, player.UserName.Length)), 8)}");
        playerTable.AddColumn("  CS  ", player => StringFormatter.Centered($"{Math.Round(player.ContractScore)}", 6));
        playerTable.AddColumn(" Rate ", player => StringFormatter.Centered($"{StringFormatter.BigNumberToString(player.ContributionRate * Duration.SECONDS_IN_AN_HOUR, strLen: 6)}", 6));

        // Add the coops and players to the tables
        foreach (var coop in coops) { coopTable.AddDataPoint(coop); }
        foreach (var player in playersSubset) { playerTable.AddDataPoint(player); }

        double averageCS = players.Any() ? Math.Ceiling(players.Select(p => p.ContractScore).Average()) : 0;

        return $"""
            **`{StringFormatter.Centered(" Fastruns ", coopTable.GetHeader().Length+1, fillChar: '—')}`**
            {coopTable.GetHeader()}
            {coopTable.GetTable()}
            `Primary order based off of duration`
            ```
            {playerTable.GetHeader()}
            {new string('—', playerTable.GetHeader().Length+2)}
            {playerTable.GetTable()}
            {new string([.. Enumerable.Range(0, (playerTable.GetHeader().Length + 2)).Select(i => i % 2 == 0 ? '—' : ' ')])}
            Avg. CS -> {averageCS}
            {new string('—', playerTable.GetHeader().Length+2)}
            Only showing top {playersSubset.Count()} players. CS calculations assume n-1 CRs and max Tval.
            ```
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

    private static void Debug(PeriodicalsResponse periodicals, MajCoopsResponse majCoopsResponse)
    {

    }
}
