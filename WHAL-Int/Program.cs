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
        { // if no TargetFlags are set, set SR and FR TargetFlags as default
            targetFlags.SpeedRun = true;
            targetFlags.FastRun  = true;
        }

        CookieCache cc = new CookieCache();
        cc.SetTargetFlags(targetFlags);
        cc.CliStart();

        return;

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



        /* ===================
           =  Get Maj coops  =
           =================== */

        Majeggstics majeggstics = new Majeggstics();
        majeggstics.AddContract(contractId);

        Console.WriteLine("Coop Codes:");
        foreach (string flag in targetFlags.Flags)
        {
            string[] codes = Majeggstics.FetchMajCoops(contractId, flag)
                .Select(c => c.Code!)
                .ToArray();
            Console.WriteLine($"\t{flag}: {string.Join(", ", codes)}");
        }
        Console.WriteLine();

        majeggstics.BuildCoops();

        var orderedCoops = majeggstics.ActiveContracts[contractId].OrderCoopsBy(x => x);



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

        Coop[] coops;

        if (targetFlags.SpeedRun.Value && orderedCoops.Any(srExpression)) // if the speedrun flag is set and there are speedrun coops
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

        if (targetFlags.FastRun.Value && orderedCoops.Any(frExpression)) // if the fastrun flag is set and there are fastrun coops
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

        if (targetFlags.AnyGrade.Value && orderedCoops.Any(agExpression)) // if the anygrade flag is set and there are anygrade coops
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

        if (targetFlags.Carry.Value && orderedCoops.Any(cExpression)) // if the anygrade flag is set and there are anygrade coops
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
        Console.ResetColor();

        Fastlane fl = new();
        fl.SetTargetFlags(targetFlags);
        fl.CliStart();

    }
}
