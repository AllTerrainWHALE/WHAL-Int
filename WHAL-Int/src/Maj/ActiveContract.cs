using Ei;

namespace WHAL_Int.Maj;

public class ActiveContract
{
    private Contract contract;
    private List<Coop> coops = new List<Coop>();
    public IEnumerable<Coop> Coops => coops.AsEnumerable();

    public ActiveContract(Contract contract) => this.contract = contract;

    public async Task<Coop?> AddCoop(string coopCode)
    {
        CoopBuilder builder = new(contract, coopCode);
        Coop? coop = await builder.Build();
        if (coop != null)
            coops.Add(coop);
        return coop;
    }
}
