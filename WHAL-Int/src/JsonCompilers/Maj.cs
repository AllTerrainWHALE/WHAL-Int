using System.Text.Json.Serialization;

namespace JsonCompilers;

public class MajCoopResponse
{
    [JsonPropertyName("items")]
    public List<MajGroup> Items { get; set; } = new();
}

public class MajGroup
{
    [JsonPropertyName("_id")]
    public string? Id { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("contract")]
    public string? Contract { get; set; }

    [JsonPropertyName("startTime")]
    public long? StartTime { get; set; }

    [JsonPropertyName("activeCoops")]
    public bool? ActiveCoops { get; set; }

    [JsonPropertyName("trackingPostIDs")]
    public Dictionary<string, List<string>>? TrackingPostIDs { get; set; }

    [JsonPropertyName("coops")]
    public List<MajCoop> Coops { get; set; } = new();

    [JsonPropertyName("__v")]
    public int? Version { get; set; }
}

public class MajCoop
{
    [JsonPropertyName("coopFlags")]
    public CoopFlags CoopFlags { get; set; } = new();

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("grade")]
    public string? Grade { get; set; }

    [JsonPropertyName("timeslot")]
    public int? Timeslot { get; set; }

    [JsonPropertyName("threadLink")]
    public string? ThreadLink { get; set; }

    [JsonPropertyName("users")]
    public List<User> Users { get; set; } = new();

    [JsonPropertyName("makeCoopsTimestamp")]
    public long? MakeCoopsTimestamp { get; set; }

    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}

public class CoopFlags
{
    [JsonPropertyName("speedRun")]
    public bool? SpeedRun { get; set; }
    [JsonPropertyName("fastRun")]
    public bool? FastRun { get; set; }
    [JsonPropertyName("anyGrade")]
    public bool? AnyGrade { get; set; }
    [JsonPropertyName("carry")]
    public bool? Carry { get; set; }

    public string[] Flags
    {
        get
        {
            List<string> flags = new();
            if (SpeedRun == true) flags.Add("SpeedRun");
            if (FastRun == true) flags.Add("FastRun");
            if (AnyGrade == true) flags.Add("AnyGrade");
            if (Carry == true) flags.Add("Carry");
            return flags.ToArray();
        }
    }
}

public class User
{
    [JsonPropertyName("ID")]
    public string? ID { get; set; }

    [JsonPropertyName("IGN")]
    public string? IGN { get; set; }

    [JsonPropertyName("UUID")]
    public string? UUID { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("metMinimums")]
    public bool? MetMinimums { get; set; }

    [JsonPropertyName("isExternal")]
    public bool? IsExternal { get; set; }

    [JsonPropertyName("contributionAmount")]
    public double? ContributionAmount { get; set; }

    [JsonPropertyName("contributionRate")]
    public double? ContributionRate { get; set; }

    [JsonPropertyName("timeslotOverride")]
    public int? TimeslotOverride { get; set; }

    [JsonPropertyName("_id")]
    public string? Id { get; set; }
}



public class MajUsersResponse : List<MajUser> { }

public class MajUser
{
    [JsonPropertyName("_id")]
    public string? _id { get; set; }

    [JsonPropertyName("ID")]
    public string? DiscordId { get; set; }

    [JsonPropertyName("discordName")]
    public string? DiscordUsername { get; set; }

    [JsonPropertyName("farmerRole")]
    public string? FarmerRole { get; set; }

    [JsonPropertyName("EB")]
    public double? EB { get; set; }

    [JsonPropertyName("grade")]
    public string? Grade { get; set; }

    [JsonPropertyName("active")]
    public bool? Active { get; set; }

    [JsonPropertyName("IGN")]
    public string? IGN { get; set; }

    [JsonPropertyName("displayName")]
    public string? DiscordDisplayname { get; set; }

    [JsonPropertyName("PE")]
    public double? PE { get; set; }

    [JsonPropertyName("SE")]
    public double? SE { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("joinedAt")]
    public DateTime? JoinedAt { get; set; }

    [JsonPropertyName("numPrestiges")]
    public int? NumPrestiges { get; set; }

    [JsonPropertyName("TE")]
    public double? TE { get; set; }
}
