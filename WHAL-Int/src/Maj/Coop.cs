using Ei;
using Google.Protobuf.Collections;
using Majcoops;
using WHAL_Int.Formatter;

namespace WHAL_Int.Maj;

public class Coop : IComparable<Coop>
{
    private readonly ContractCoopStatusResponse coopStatus;
    private readonly Contract contract;
    private readonly Contract.Types.GradeSpec gradeSpec;
    public string grade => gradeSpec.Grade.ToString();
    public double contractFarmMaximumTimeAllowed;
    public double coopAllowableTimeRemaining => coopStatus.SecondsRemaining;
    public double eggGoal => gradeSpec.Goals.MaxBy(g => g.TargetAmount)!.TargetAmount;
    public uint size => contract.MaxCoopSize;

    public double shippedEggs => coopStatus.TotalAmount;
    public double totalShippedEggs => shippedEggs + totalOfflineEggs;

    public double totalShippingRate => coopStatus.Contributors.Where(player => player.UserName != "[departed]").Sum(player => player.ContributionRate);

    // `FarmInfo.Timestamp` is basically (LastSyncUnix - currentUnix) in seconds, so the negative is required in the maths
    // Credits to WHALE for figuring out the maths for this :happywiggle:
    // `FarmInfo` is also nullable if the player is `[departed]` or has a private farm
    private double totalOfflineEggs =>
        coopStatus.Contributors.Sum(player =>
            player.ContributionRate * (-(player.FarmInfo?.Timestamp) ?? 0));

    public double eggsRemaining => Math.Max(0, eggGoal - totalShippedEggs);
    public long predictedSecondsRemaining => totalShippingRate != 0 ? Convert.ToInt64(eggsRemaining / totalShippingRate) : 0;
    private readonly long unixNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public List<Player> contributors;

    // Assign CoopFlags
    public CoopFlags CoopFlags { get; set; } = new();

    public Coop(ContractCoopStatusResponse coopStatus, Contract contract)
    {
        if (coopStatus.ResponseStatus != ContractCoopStatusResponse.Types.ResponseStatus.NoError)
        {
            throw new InvalidDataException("Cannot find coop, ResponseStatus = " + coopStatus.ResponseStatus);
        }

        this.coopStatus = coopStatus;
        this.contract = contract;
        gradeSpec = contract.GradeSpecs.SingleOrDefault(g => g.Grade == coopStatus.Grade)!;
        contractFarmMaximumTimeAllowed = gradeSpec.LengthSeconds;
        PredictedCompletionTimeUnix =
            new DiscordTimestamp(unixNow + predictedSecondsRemaining - (long)coopStatus.SecondsSinceAllGoalsAchieved);
        PredictedDuration = new Duration(Convert.ToInt64(contractFarmMaximumTimeAllowed -
                                                         coopAllowableTimeRemaining +
                                                         predictedSecondsRemaining -
                                                         coopStatus.SecondsSinceAllGoalsAchieved));

        contributors = coopStatus.Contributors.Select(playerInfo => new Player(playerInfo,this)).ToList();
    }

    /// <summary>
    /// Returns the Coop Code/ID of the Coop.
    /// </summary>
    public string CoopId => coopStatus.CoopIdentifier;

    /// <summary>
    /// Returns the Contract ID.
    /// </summary>
    public string ContractId => coopStatus.ContractIdentifier;

    /// <summary>
    /// Returns the first 6 characters of the Coop Code/ID. For use typically in formatted tables.
    /// </summary>
    public string StrippedCoopId => CoopId.Substring(0, Math.Min(CoopId.Length, 6));

    /// <summary>
    /// Returns the number of players that has spent more than or equal to 4 tokens.
    /// 4 tokens spent usually denotes that the particular player has boosted/began boosting in a SR setting.
    /// </summary>
    public int BoostedCount => coopStatus.Contributors.Count(x => x.BoostTokensSpent >= 4);

    /// <summary>
    /// Returns the combined total amount of tokens of all players in the Coop, including tokens that have been spent.
    /// </summary>
    public int TotalTokens => coopStatus.Contributors.Sum(x => (int)(x.BoostTokensSpent + x.BoostTokens));

    public DiscordTimestamp PredictedCompletionTimeUnix { get; private set; }
    public Duration PredictedDuration { get; private set; }

    public int CompareTo(Coop? other)
    {
        if (other is null) return 1;
        int result = PredictedDuration.CompareTo(other.PredictedDuration);
        if (result == 0)
            result = PredictedCompletionTimeUnix.CompareTo(other.PredictedCompletionTimeUnix);
        if (result == 0)
            result = other.BoostedCount.CompareTo(BoostedCount);
        if (result == 0)
            result = other.TotalTokens.CompareTo(TotalTokens);
        return result;
    }
}
