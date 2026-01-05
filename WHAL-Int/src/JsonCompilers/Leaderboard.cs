using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JsonCompilers;
public class LBInfoResponse
{
    [JsonPropertyName("seasonsList")]
    public List<SeasonLB>? SeasonsList { get; set; }

    [JsonPropertyName("allTimeScope")]
    public string? AllTimeScope { get; set; }
}

public class SeasonLB
{
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}



public class LBResponse
{
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("grade")]
    public int? Grade { get; set; }

    [JsonPropertyName("topEntriesList")]
    public List<LBEntry>? Entries { get; set; }

    [JsonPropertyName("count")]
    public long? Count { get; set; }

    [JsonPropertyName("rank")]
    public long? Rank {  get; set; }

    [JsonPropertyName("score")]
    public long? Score { get; set; }
}

public class LBEntry
{
    [JsonPropertyName("rank")]
    public long? Rank { get; set; }

    [JsonPropertyName("alias")]
    public string? IGN { get; set; }

    [JsonPropertyName("score")]
    public long? Score { get; set; }
}
