using Ei;
using WHAL_Int.Formatter;

namespace WHAL_Int.Maj;

public class Player : IComparable<Player>
{
    private readonly ContractCoopStatusResponse.Types.ContributionInfo playerInfo;
    private readonly Coop coop;

    public string UserName => playerInfo.UserName;

    public bool Sink = false;

    // Contribution calculations
    public double Contribution => playerInfo.ContributionAmount;
    public double ContributionRate => playerInfo.ContributionRate;
    public double OfflineContribution => Contribution
        + (ContributionRate * Math.Max(0,(-(playerInfo.FarmInfo?.Timestamp) ?? 0) - coop.SecondsSinceAllGoalsAchieved));
    public double PredictedContribution => OfflineContribution
        + (ContributionRate * Math.Max(0, coop.PredictedSecondsRemaining));
    public double ContributionRatio => PredictedContribution / (coop.EggGoal / coop.MaxCoopSize);

    public Player(ContractCoopStatusResponse.Types.ContributionInfo playerInfo, Coop coop)
    {
        this.playerInfo = playerInfo ?? throw new ArgumentNullException(nameof(playerInfo));
        this.coop = coop ?? throw new ArgumentNullException(nameof(coop));
    }

    // ================================================
    // ================ CS Calulations ================
    // ================================================

    // Based coop score
    private short basePoints = 1;
    private double durationPoints = 1.0 / 259200.0;
    private double contractLength => coop.ContractFarmMaximumTimeAllowed;

    // Grade multiplier
    private double gradeMultiplier => coop.Grade switch
    {
        "GradeAaa" => 7,
        "GradeAa" => 5,
        "GradeA" => 3.5,
        "GradeB" => 2,
        "GradeC" => 1,

        _ => throw new InvalidDataException("Unknown grade: " + coop.Grade)
    };

    private int completionFactor = 1; // cases where completionPercent != 1 do not interest me

    // Contribution score
    private double contributionFactor => ContributionRatio <= 2.5
        ? 3 * Math.Pow(ContributionRatio,0.15) + 1
        : 0.02221 * Math.Min(ContributionRatio, 12.5) + 4.386486;

    // Completion time score
    private double completionTimeBonus => 4.0 * Math.Pow(1.0 - (coop.PredictedDuration.DurationInSeconds / contractLength), 3.0) + 1.0;

    // Teamwork score
    private double teamworkBonus => 0.19 * TeamworkScore + 1;
    public double TeamworkScore => (5.0 * buffFactor + ChickenRunFactor + TokenFactor) / 19.0;

    // Buff score
    public double BuffTimeValue
    {
        get
        {
            double sum = 0;
            for (int i = 0; i < playerInfo.BuffHistory.Count; i++)
            {
                var buff = playerInfo.BuffHistory[i];
                double timeEquipped = i < playerInfo.BuffHistory.Count - 1
                    ? buff.ServerTimestamp - playerInfo.BuffHistory[i + 1].ServerTimestamp
                    : buff.ServerTimestamp + coop.PredictedSecondsRemaining - coop.SecondsSinceAllGoalsAchieved;

                sum += timeEquipped * 7.5 * (buff.EggLayingRate - 1);
                sum += timeEquipped * .75 * (buff.Earnings - 1);
            }
            return sum;
        }
    }
    private double buffFactor => Math.Min(BuffTimeValue / coop.PredictedDuration.DurationInSeconds, 2);

    // Chicken runs score (assuming n-1 CRs)
    public double ChickenRunFactor => Math.Min(fcr * (coop.MaxCoopSize-1.0), 6); // Assuming n-1 CRs
    private double fcr => Math.Max(12.0/(coop.MaxCoopSize * (contractLength / Duration.SECONDS_IN_A_DAY)), 0.3);
    private double chickenRunCap => Math.Min(Math.Ceiling(((contractLength / Duration.SECONDS_IN_A_DAY) * coop.MaxCoopSize) / 2.0), 20);

    // Token score (assuming max tval)
    private double boostTokenAllotment => Math.Floor(coop.PredictedDuration.DurationInSeconds / (coop.MinutesPerToken * 1.0));
    public double TokenFactor => boostTokenAllotment <= 42
        ? (2.0 / 3.0) * 3.0 + (8.0 / 3.0) * 3.0
        : (200.0 / (7.0 * boostTokenAllotment)) * (0.07 * boostTokenAllotment)
            + (800.0 / (7.0 * boostTokenAllotment)) * (0.07 * boostTokenAllotment);

    public double ContractScore => Math.Ceiling(
        (basePoints + durationPoints * contractLength)
        * gradeMultiplier
        * completionFactor
        * contributionFactor
        * completionTimeBonus
        * teamworkBonus
        * 187.5);

    public int CompareTo(Player? other)
    {
        if (other is null) return 1;
        int result = other.ContractScore.CompareTo(ContractScore);
        if (result == 0)
            result = other.ContributionRate.CompareTo(ContributionRate);
        if (result == 0)
            result = other.OfflineContribution.CompareTo(OfflineContribution);
        return result;
    }
}
