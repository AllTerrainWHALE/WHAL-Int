using Ei;
using Majcoops;
using WHAL_Int.EggIncApi;

namespace WHAL_Int.Maj;

public class ActiveContract
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
    public static IEnumerable<Ei.Contract> PeriodicalsContracts =>
        PeriodicalsResponse.Contracts.Contracts
            .OrderByDescending(c => c.StartTime)
            .Where(c => c.Identifier != "first-contract");
    public static Ei.Contract GetContractById(string id) =>
        PeriodicalsContracts.FirstOrDefault(c => c.Identifier == id)
        ?? throw new InvalidDataException($"Contract ID invalid: {id}");
    public static List<string> ContractIds =>
        PeriodicalsContracts.Select(c => c.Identifier).ToList();



    private Ei.Contract contract;
    private List<Coop> coops = new List<Coop>();

    public string ContractId => contract.Identifier;
    public IEnumerable<Coop> Coops => coops.AsEnumerable();

    public ActiveContract(Ei.Contract contract) => this.contract = contract;

    public async Task<Coop?> AddCoop(string coopCode, CoopFlags? flags = null)
    {
        CoopBuilder builder = new(contract, coopCode, flags);
        Coop? coop = await builder.Build();
        if (coop != null) coops.Add(coop);
        return coop;
    }

    public List<Coop> OrderCoopsBy(Func<Coop, Coop> keySelector) =>
        coops = coops.OrderBy(keySelector).ToList();
}
