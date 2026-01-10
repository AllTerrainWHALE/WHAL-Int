using JsonCompilers;
using EggIncApi;
using Ei;

namespace Maj;
public class Majeggstics
{

    // Create variable that can store multiple contracts and a list of their corresponding coops
    public Dictionary<string, ActiveContract> ActiveContracts = new();
    private static Dictionary<string, List<MajCoop>> contractMajCoops = new();

    public CoopFlags TargetFlags { get; private set; }

    public Majeggstics(CoopFlags? targetFlags = null)
    {
        this.TargetFlags = targetFlags ?? CoopFlags.NewCoopFlags(all: true);
    }

    public void SetTargetFlags(CoopFlags targetFlags) =>
        this.TargetFlags = targetFlags;

    public void AddContract(string contractId)
    {
        if (!ActiveContracts.ContainsKey(contractId))
        {
            ActiveContractBuilder activeContractBuilder = new ActiveContractBuilder(contractId);
            Task<ActiveContract> activeContractTask = activeContractBuilder.Build();
            activeContractTask.Wait();
            ActiveContracts[contractId] = activeContractTask.Result;
        }
    }
    public void AddContract(Contract contract) =>
        AddContract(contract.Identifier);

    public static List<MajCoop> FetchMajCoops(string contractId, CoopFlags? flags = null, bool force = false)
    {
        if (!ActiveContract.ContractIds.Contains(contractId))
        {
            throw new InvalidDataException($"Contract ID invalid: {contractId}");
        }

        if (force || !contractMajCoops.ContainsKey(contractId))
        {
            Task<MajCoopResponse> majCoopsResponseTask = Request.GetMajCoops(contractId);
            majCoopsResponseTask.Wait();
            MajCoopResponse majCoopsResponse = majCoopsResponseTask.Result;
            List<MajCoop> majCoops = majCoopsResponse.Last().Coops;
            majCoops = majCoops.Where(c => System.Text.RegularExpressions.Regex.IsMatch(c.Code!, @"^[a-z]{6}\d{3}$")).ToList();

            contractMajCoops[contractId] = majCoops;
        }

        if (flags == null) return contractMajCoops[contractId];

        var filteredCoops = contractMajCoops[contractId]
            .Where(c => c.CoopFlags.Flags.Any(f => flags.Flags.Contains(f)))
            .ToList();

        return filteredCoops;
    }
    public static List<MajCoop> FetchCoopsForContract(string contractId, string? flag, bool force = false)
    {
        List<MajCoop> contractMajCoops = FetchMajCoops(contractId: contractId, flags: null, force: force);

        var filteredCoops = flag == null ? contractMajCoops :
            contractMajCoops
            .Where(c => c.CoopFlags.Flags.Contains(flag))
            .ToList();

        return filteredCoops;
    }

    public void BuildCoops()
    {
        foreach (string contractId in ActiveContracts.Keys)
        {
            var activeContract = ActiveContracts[contractId];

            List<Task<Coop?>> coopTasks = new();
            foreach (MajCoop coop in FetchMajCoops(contractId, TargetFlags))
            {
                if (coop.Code == null)
                    continue;

                if (activeContract.Coops.Select(c => c.CoopId).Contains(coop.Code))
                    continue;

                Task<Coop?> coopTask = activeContract.AddCoop(coop.Code, coop.CoopFlags);
                if (coopTask != null) coopTasks.Add(coopTask);
            }
            Task.WaitAll(coopTasks.ToArray());
        }
    }
}
