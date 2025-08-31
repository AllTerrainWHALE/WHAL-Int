using Ei;
using Google.Protobuf.Collections;
using Majcoops;
using WHAL_Int.Formatter;

using System.IO;

namespace WHAL_Int.Maj;

public class Player
{
    private readonly ContractCoopStatusResponse.Types.ContributionInfo playerInfo;
    private readonly Coop coop;

    public string userName => playerInfo.UserName;

    public double contribution => playerInfo.ContributionAmount;
    public double offlineContribtuion => contribution
        + (playerInfo.ContributionRate * (-(playerInfo.FarmInfo?.Timestamp) ?? 0));
    public double predictedContribution => offlineContribtuion
        + (playerInfo.ContributionRate * Math.Max(0, coop.predictedSecondsRemaining));
    public double contributionRatio => predictedContribution / (coop.eggGoal / coop.size);

    public Player(ContractCoopStatusResponse.Types.ContributionInfo playerInfo, Coop coop)
    {
        this.playerInfo = playerInfo;
        this.coop = coop;
    }

    private short basePoints = 1;
    private float durationPoints = 1 / 259200;
    private double contractLength => coop.contractFarmMaximumTimeAllowed;

    private float gradeMultiplier => coop.grade switch
    {
        "GradeAaa" => 7f,
        "GradeAa" => 5f,
        "GradeA" => 3.5f,
        "GradeB" => 2f,
        "GradeC" => 1f,

        _ => throw new InvalidDataException("Unknown grade: " + coop.grade)
    };

    private int completionFactor = 1; // cases where completionPercent != 1 do not interest me

    private double contributionFactor => contributionRatio <= 2.5
        ? 3 * Math.Pow(contributionRatio,0.15) + 1
        : 0.02221 * Math.Min(contributionRatio, 12.5) + 4.386486;

    private double completionTimeBonus => 4 * Math.Pow(1 - (coop.PredictedDuration.DurationInSeconds / contractLength), 3) + 1;

    private double teamWorkBonus => 0.19 * teamworkScore + 1;
    private double teamworkScore => 0;

    public double contractScore =>
        (basePoints + durationPoints * contractLength)
        * gradeMultiplier
        * completionFactor
        * contributionFactor
        * completionTimeBonus
        * teamWorkBonus
        * 187.5;
}
