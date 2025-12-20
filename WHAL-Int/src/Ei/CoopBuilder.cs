using JsonCompilers;
using EggIncApi;

namespace Ei;

public class CoopBuilder
{
    private readonly global::JsonCompilers.Contract contract;
    private readonly string coopCode;
    private readonly CoopFlags coopFlags;

    public CoopBuilder(global::JsonCompilers.Contract contract, string coopCode, CoopFlags? coopFlags = null)
    {
        this.contract = contract;
        this.coopCode = coopCode;
        this.coopFlags = coopFlags!;
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

        return new Coop(coopStatus, contract, coopFlags);
    }
}
