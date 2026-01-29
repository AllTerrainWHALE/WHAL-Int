namespace Ei;

public class ActiveContractBuilder
{
    private static JsonCompilers.Contract[] contractArchive = null!;
    public static JsonCompilers.Contract[] ContractsArchive
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
                ActiveContractBuilder.contractArchive = [.. contractArchive.OrderBy(c => c.StartTime)];
            }
            return contractArchive;
        }
    }

    private string contractId;

    public ActiveContractBuilder(string contractId) => this.contractId = contractId;

    public ActiveContract Build()
    {
        JsonCompilers.Contract contract =
            ContractsArchive.LastOrDefault(c => c.Identifier == contractId)
            ?? throw new InvalidDataException($"Contract ID invalid: {contractId}");
        return new ActiveContract(contract);
    }

    public static JsonCompilers.Contract CliSelectContract()
    {
        var contracts = ContractsArchive;
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
