using Ei;
using WHAL_Int.EggIncApi;

namespace WHAL_Int.Maj;

public class CoopBuilder
{
    private readonly Contract contract;
    private readonly string coopCode;

    public CoopBuilder(Contract contract, string coopCode)
    {
        this.contract = contract;
        this.coopCode = coopCode;
    }

    public async Task<Coop?> Build()
    {
        var coopStatus = await Request.GetCoopStatus(contract.Identifier, coopCode);

        if (coopStatus.ResponseStatus != ContractCoopStatusResponse.Types.ResponseStatus.NoError)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Cannot find coop '{coopCode}', ResponseStatus = {coopStatus.ResponseStatus}");
            Console.ResetColor();
            return null;
        }

        return new Coop(coopStatus, contract);
    }
}
