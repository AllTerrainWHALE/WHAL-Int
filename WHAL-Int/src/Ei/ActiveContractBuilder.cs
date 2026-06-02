using EggIncApi;
using JsonCompilers;

namespace Ei;

public class ActiveContractBuilder
{
    public static class Periodicals
    {
        private static PeriodicalsResponse periodicalsResponse = null!; // Initialize as null-forgiving to satisfy the compiler
        public static PeriodicalsResponse PeriodicalsResponse
        {
            get
            {
                if (periodicalsResponse == null)
                {
                    Task<PeriodicalsResponse> periodicalsResponseTask = Request.GetPeriodicals();
                    periodicalsResponseTask.Wait();
                    periodicalsResponse = periodicalsResponseTask.Result;
                }
                return periodicalsResponse;
            }
        }
        public static IEnumerable<Contract> Contracts =>
            PeriodicalsResponse.Contracts.Contracts
                .OrderBy(c => c.StartTime)
                .Where(c => c.Identifier != "first-contract");
        public static Contract GetContractById(string id) =>
            Contracts.FirstOrDefault(c => c.Identifier == id)
            ?? throw new InvalidDataException($"Contract ID invalid: {id}");
        public static List<string> ContractIds =>
            Contracts.Select(c => c.Identifier).ToList();
    }

    public static class Archive
    {
        private static JsonCompilers.Contract[] contractArchive = null!;
        public static JsonCompilers.Contract[] Contracts
        {
            get
            {
                if (contractArchive == null)
                {
                    Task<JsonCompilers.EggIncFirstContactResponse> firstContractTask = EggIncApi.Request.GetFirstContact();
                    firstContractTask.Wait();
                    JsonCompilers.EggIncFirstContactResponse firstContractResponse = firstContractTask.Result;

                    List<JsonCompilers.Contract> contractArchive = firstContractResponse
                        .Backup.Contracts.Archive
                        .Select(a => a.Contract)
                        .ToList();

                    contractArchive.AddRange(
                        firstContractResponse.Backup.Contracts.Contracts
                        .Select(c => c.Contract));
                    Archive.contractArchive = [.. contractArchive.OrderBy(c => c.StartTime)];
                }
                return contractArchive;
            }
        }

        public static string[] ContractIds = [.. Contracts.Select(c => c.Identifier)];
    }

    public static Contract[] Contracts = [.. Archive.Contracts.Concat(Periodicals.Contracts).DistinctBy(c => c.Identifier)];
    public static string[] ContractIds = [.. Contracts.Select(c => c.Identifier)];

    private string contractId;

    public ActiveContractBuilder(string contractId) => this.contractId = contractId;

    public ActiveContract Build()
    {
        JsonCompilers.Contract contract =
            Contracts.LastOrDefault(c => c.Identifier == contractId)
            ?? throw new InvalidDataException($"Contract ID invalid: {contractId}");
        return new ActiveContract(contract);
    }

    public static JsonCompilers.Contract CliSelectContract()
    {
        var contracts = Contracts;
        var displayedContracts = contracts.TakeLast(6).Reverse().ToArray();

        // Ask user to select a contract
        Console.WriteLine("Select contract ID:");
        int counter = 1;
        foreach (var contract in displayedContracts)
        {
            if (counter == 1) // highlight the first contract
                Console.Write($"\t[{counter}] ");
            else // normal print for other contracts
                Console.Write($"\t({counter}) ");

            Console.WriteLine($"{contract.Identifier} | {contract.Name}");
            counter++;
        }
        Console.Write("> ");

        // Get users selected input
        string? selectedContractId = Console.ReadLine();
        bool isIdxSelected = int.TryParse(selectedContractId, out int selectedContractIdx);
        if (!isIdxSelected)
        { // if selectedContractId is not a number, take the first contract
            selectedContractIdx = 0;
        }
        else
        { // if selectedContractId is a number, subtract 1 to get the index
            selectedContractIdx -= 1;
        }

        // Find the selected contract
        JsonCompilers.Contract selectedContract;
        if (selectedContractId == "" || isIdxSelected)
            selectedContract = displayedContracts.ElementAt(selectedContractIdx); // get the contract at the selected index
        else
            selectedContract = contracts.LastOrDefault(c => c.Identifier == selectedContractId) // get the contract by id
                ?? throw new InvalidDataException($"Contract ID invalid: {selectedContractId}");

        return selectedContract;
    }
}
