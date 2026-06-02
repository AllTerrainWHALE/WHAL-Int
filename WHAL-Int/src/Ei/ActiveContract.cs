using JsonCompilers;
using EggIncApi;

namespace Ei;

public class ActiveContract
{
    public Contract Contract;
    private List<Coop> coops = new List<Coop>();

    public string ContractId => Contract.Identifier;
    public IEnumerable<Coop> Coops => coops.AsEnumerable();

    public ActiveContract(Contract contract) => Contract = contract;

    public async Task<Coop?> AddCoop(string coopCode, CoopFlags? flags = null)
    {
        CoopBuilder builder = new(Contract, coopCode, flags);
        Coop? coop = await builder.Build();
        if (coop != null) coops.Add(coop);
        return coop;
    }

    public List<Coop> OrderCoopsBy(Func<Coop, Coop> keySelector) =>
        coops = coops.OrderBy(keySelector).ToList();
}
