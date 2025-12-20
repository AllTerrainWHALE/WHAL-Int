using JsonCompilers;
using EggIncApi;
using Ei;

namespace Maj;
public class Majeggstics
{

    // Create variable that can store multiple contracts and a list of their corresponding coops
    public Dictionary<string, ActiveContract> ActiveContracts = new();
    private Dictionary<string, List<MajCoop>> contractMajCoops = new();

    private CoopFlags targetFlags;

    public Majeggstics(CoopFlags? targetFlags = null)
    {
        this.targetFlags = targetFlags ?? new CoopFlags
        {
            SpeedRun = true,
            FastRun = true,
            AnyGrade = true,
            Carry = true,
        };
    }

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

    public List<MajCoop> FetchCoopsForContract(string contractId, string? flag = null, bool force = false)
    {
        if (!ActiveContract.ContractIds.Contains(contractId))
        {
            throw new InvalidDataException($"Contract ID invalid: {contractId}");
        }

        if (force || !contractMajCoops.ContainsKey(contractId))
        {
            Task<MajResponse> majCoopsResponseTask = Request.GetMajCoops(contractId);
            majCoopsResponseTask.Wait();
            MajResponse majCoopsResponse = majCoopsResponseTask.Result;
            List<MajCoop> majCoops = majCoopsResponse.Items.Last().Coops;

            contractMajCoops[contractId] = majCoops;
        }

        if (flag == null) return contractMajCoops[contractId];

        var filteredCoops = contractMajCoops[contractId]
            .Where(c => c.CoopFlags.Flags.Contains(flag))
            .ToList();

        return filteredCoops;
    }
    public List<MajCoop> FetchCoopsForContract(global::JsonCompilers.Contract contract, string? flag = null, bool force = false) =>
        FetchCoopsForContract(contract.Identifier, flag: flag, force: force);

    public void BuildCoops()
    {
        foreach (string contractId in contractMajCoops.Keys)
        {
            if (!ActiveContract.ContractIds.Contains(contractId))
            {
                throw new InvalidDataException($"Contract ID invalid: {contractId}");
            }

            if (!ActiveContracts.ContainsKey(contractId))
            {
                AddContract(contractId);
            }
            var activeContract = ActiveContracts[contractId];

            List<Task<Coop?>> coopTasks = new();
            foreach (MajCoop coop in FetchCoopsForContract(contractId))
            {
                if (!targetFlags.Flags.Any(f => coop.CoopFlags.Flags.Contains(f)))
                    continue;

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
