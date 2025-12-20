using JsonCompilers;
using EggIncApi;
using Formatter;
using Ei;
using Maj;

namespace WHAL_Int;

internal class Program
{
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

        bool debug = args.Contains("--debug") || args.Contains("-d");
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

        var outputSegments = new List<string>(); // create a list to hold the output segments
        outputSegments.Add($"## {EggType.ToDiscordEmoji(selectedContract.Egg)} {selectedContract.Name} | Fastlane Leaderboards"); // add the header to the output segments

        string starter = $"Last updated: {discordTimestampNow.Format(DiscordTimestampDisplay.Relative)}\n"; // create a starter string for the output segments

        if (targetFlags.SpeedRun.Value && orderedCoops.Any(c => c.CoopFlags.SpeedRun == true)) // if the speedrun flag is set and there are speedrun coops
        {
            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines($"""
                {starter}
                {SRTable(orderedCoops.Where(c => c.CoopFlags.SpeedRun == true).ToArray())}
                """));
            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (targetFlags.FastRun.Value && orderedCoops.Any(c => c.CoopFlags.FastRun == true)) // if the fastrun flag is set and there are fastrun coops
        {
            outputSegments.AddRange(StringFormatter.SplitToCharLimitByLines($"""
                {starter}
                {FRTable(orderedCoops.Where(c => c.CoopFlags.FastRun == true).ToArray())}
                """));
            starter = "_ _"; // reset the starter to an empty string so it doesn't repeat in the next segment
        }

        if (targetFlags.AnyGrade.Value && orderedCoops.Any(c => c.CoopFlags.AnyGrade == true)) // if the anygrade flag is set and there are anygrade coops
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
        if (targetFlags.Flags.Count() >= outputSegments.Count()-2 && combinedTables.Length <= 2000)
        {
            outputSegments[1] = combinedTables; // if all targetFlags are set and the combined tables are less than 2000 characters, combine the tables into the first segment
            outputSegments.RemoveRange(2, outputSegments.Count() - 3); // remove the other segments
        }

        Console.WriteLine($"""
            {"\x1b[92m"}========================= Output Start ========================={"\x1b[39m"}

            {String.Join("\n", outputSegments)}

            {"\x1b[92m"}=========================  Output End  ========================={"\x1b[39m"}

            """); // "\x1b[92m" is green and "\x1b[39m" is reset color


        foreach (var (segment,index) in outputSegments.Select((v,i) => (v,i))) // print each segment of the sruc table
        {
            Console.Write($"Press ENTER to copy segment {index+1}/{outputSegments.Count()} ");
            Console.WriteLine(index == 0 ? "(HEADER)" : index == outputSegments.Count() - 1 ? "(FOOTER)" : "");
            Console.ReadLine();
            ClipboardHelper.CopyToClipboard(segment);
        }
    }

    private static string SRTable(Coop[] coops)
    {
        var table = new Table<Coop>(); // create a new table for the coops
        table.AddColumn("`  Coop   ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)} ");
        table.AddColumn(" Boosted ", coop => StringFormatter.Centered($"{coop.BoostedCount}", 9));
        table.AddColumn(" Tokens ", coop => StringFormatter.Centered($"{coop.TotalTokens}", 8));
        table.AddColumn(" Duration ", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 10));
        table.AddColumn(" Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // add the coops to the table
        foreach (var coop in coops) { table.AddDataPoint(coop); }

        return $"""
            **`{StringFormatter.Centered(" Speedruns ", table.GetHeader().Length-2, fillChar: '—')}`**
            {table.GetHeader()}
            {table.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static string FRTable(Coop[] coops)
    {
        var table = new Table<Coop>(); // create a new table for the coops
        table.AddColumn("`  Coop   ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)} ");
        //table.AddColumn(" Layrate ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippingRate, strLen: 5)}/h", 9));
        table.AddColumn(" Boosted ", coop => StringFormatter.Centered($"{coop.BoostedCount}", 9));
        table.AddColumn("  Ship  ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippedEggs, strLen: 6)}", 8));
        table.AddColumn(" Duration ", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 10));
        table.AddColumn(" Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // add the coops to the table
        foreach (var coop in coops) { table.AddDataPoint(coop); }

        return $"""
            **`{StringFormatter.Centered(" Fastruns ", table.GetHeader().Length - 2, fillChar: '—')}`**
            {table.GetHeader()}
            {table.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static string AGTable(Coop[] coops)
    {
        var table = new Table<Coop>(); // create a new table for the coops
        table.AddColumn("`  Coop   ", coop => $"[⧉](<https://eicoop-carpet.netlify.app/{coop.ContractId}/{coop.CoopId}>)`{StringFormatter.LeftAligned(coop.StrippedCoopId, 6)} ");
        table.AddColumn(" Layrate ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippingRate, strLen: 5)}/h", 9));
        table.AddColumn(" Shipped ", coop => StringFormatter.Centered($"{StringFormatter.BigNumberToString(coop.totalShippedEggs, strLen: 7)}", 9));
        table.AddColumn(" Duration ", coop => StringFormatter.Centered(coop.PredictedDuration.DurationInSeconds < 8640000 ? coop.PredictedDuration.Format() : "too long", 10));
        table.AddColumn(" Finish`", coop => $"`{coop.PredictedCompletionTimeUnix.Format(DiscordTimestampDisplay.FullDateTime)}");

        // add the coops to the table
        foreach (var coop in coops) { table.AddDataPoint(coop); }
        return $"""
            **`{StringFormatter.Centered(" Anygrades ", table.GetHeader().Length - 2, fillChar: '—')}`**
            {table.GetHeader()}
            {table.GetTable()}
            `Primary order based off of duration`
            """;
    }

    private static void Debug(PeriodicalsResponse periodicals, MajResponse majCoopsResponse)
    {

    }
}
