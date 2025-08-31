using Ei;
using Majcoops;
using WHAL_Int.EggIncApi;
using WHAL_Int.Formatter;
using WHAL_Int.Maj;

using System.Windows.Forms;
using System.Linq;

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
        string input = Console.ReadLine();
        int selectedContractIdx;
        if (!int.TryParse(input, out selectedContractIdx))
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
            { "SpeedRun", majCoops.Where(c => c.CoopFlags.SpeedRun == true).Select(c => c.Code).ToArray() },
            { "FastRun" , majCoops.Where(c => c.CoopFlags.FastRun  == true).Select(c => c.Code).ToArray() },
            { "AnyGrade", majCoops.Where(c => c.CoopFlags.AnyGrade == true).Select(c => c.Code).ToArray() },
            { "Carry"   , majCoops.Where(c => c.CoopFlags.Carry    == true).Select(c => c.Code).ToArray() }
        };

        Console.WriteLine("Coop codes:");
        foreach (var flag in flags)
        {
            if (flag.Value) // if the flag is set, print the coop codes for that flag
            {
                Console.WriteLine($"\t{flag.Key}: {String.Join(",", coopCodesAndFlags[flag.Key])}");
            }
        }


        /* ==============================
           =  Build contract and coops  =
           ============================== */

        var activeContract = await new ActiveContractBuilder(contractId).Build(); // build the active contract from the contract id

        var coopCodes = coopCodesAndFlags
            .Where(kvp => flags.ContainsKey(kvp.Key) && flags[kvp.Key]) // filter the coop codes by the flags that are set
            .ToDictionary().Values // get the values of the filtered coop codes
            .SelectMany(c => c) // flatten the coop codes into a single list
            .Where(c => !string.IsNullOrEmpty(c)) // filter out any empty coop codes
            .Distinct() // remove duplicates
            .ToArray(); // convert to an array

        var tasks = new List<Task<Coop>>();
        foreach (var coopCode in coopCodes)
        { // loop through the coop codes and add them to the tasks list
            tasks.Add(activeContract.AddCoop(coopCode));
        }

        // wait for all tasks to complete and get the results
        var coops = await Task.WhenAll(tasks);
        coops = coops.Where(c => c != null).ToArray(); // filter out any null coops
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


        foreach (var player in orderedCoops.First().contributors)
        {
            Console.WriteLine($"{player.userName} | {player.contributionRatio}");
        }
        throw new NotImplementedException();


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

    private static void Debug(PeriodicalsResponse periodicals, MajCoopsResponse majCoopsResponse)
    {

    }
}
