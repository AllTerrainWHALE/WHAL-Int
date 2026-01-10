using System.IO.Compression;
using JsonCompilers;
using Google.Protobuf;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EggIncApi;

public class Request
{
    private static Dictionary<string,MajCoopResponse> majCoopsCache = new();
    private static MajUsersResponse? majPlayersCache;
    private static LBInfoResponse? lBInfoCache;
    private static List<LBResponse> lBCache = new();

    private static BasicRequestInfo rInfo = new()
    {
        EiUserId = Config.EID,
        ClientVersion = Config.CLIENT_VERSION,
        Version = Config.VERSION,
        Build = Config.BUILD,
        Platform = Config.PLATFORM
    };

    public static async Task<ContractCoopStatusResponse> GetCoopStatus(string contractId, string coopId)
    {
        ContractCoopStatusRequest coopStatusRequest = new()
        {
            Rinfo = rInfo,
            ContractIdentifier = contractId,
            CoopIdentifier = coopId,
            UserId = Config.EID
        };

        return await makeEggIncApiRequest("coop_status", coopStatusRequest, ContractCoopStatusResponse.Parser.ParseFrom);
    }

    public static async Task<PeriodicalsResponse> GetPeriodicals()
    {
        GetPeriodicalsRequest getPeriodicalsRequest = new()
        {
            Rinfo = rInfo,
            UserId = Config.EID,
            CurrentClientVersion = Config.CURRENT_CLIENT_VERSION
        };

        return await makeEggIncApiRequest("get_periodicals", getPeriodicalsRequest, PeriodicalsResponse.Parser.ParseFrom);
    }

    public static async Task<MajCoopResponse> GetMajCoops(string contractId, bool force = false)
    {
        if (majCoopsCache.ContainsKey(contractId) && !force)
            return majCoopsCache[contractId];

        string url = $"https://eiapi-production.up.railway.app/majCoops?contract={contractId}";

        string rawJson = await getRequest(url);

        MajCoopResponse response = parseJsonRsponse<MajCoopResponse>(rawJson);

        majCoopsCache[contractId] = response;
        return response;
    }

    public static async Task<MajUsersResponse> GetAllMaj(bool force = false)
    {
        if (majPlayersCache != null && !force)
            return majPlayersCache;

        string url = $"https://eiapi-production.up.railway.app/allMaj";
        string rawJson = await getRequest(url);

        return parseJsonRsponse<MajUsersResponse>(rawJson, out majPlayersCache);
    }

    public static async Task<LBInfoResponse> GetLBInfo(bool force = false)
    {
        if (lBInfoCache != null && !force)
            return lBInfoCache;

        string url = "https://ei_worker.tylertms.workers.dev/leaderboard_info";
        string rawJson = await getRequest(url);

        return parseJsonRsponse<LBInfoResponse>(rawJson, out lBInfoCache);
    }

    public static async Task<LBResponse> GetLeaderboard(string scope, int grade = 5, bool force = false)
    {
        if (lBCache.Count() > 0 && !force)
        {
            LBResponse? cachedResponse = lBCache.FirstOrDefault(lb => lb.Scope == scope && lb.Grade == grade);
            if (cachedResponse != null)
                return cachedResponse;
        }
        else if (lBCache.Count() > 0 && force)
        {
            lBCache.RemoveAll(lb => lb.Scope == scope && lb.Grade == grade);
        }

        string url = $"https://ei_worker.tylertms.workers.dev/leaderboard?EID={Config.EID}&grade={grade}&scope={scope}";
        string rawJson = await getRequest(url);

        LBResponse response = parseJsonRsponse<LBResponse>(rawJson);
        lBCache.Add(response);
        return response;
    }
    public static async Task<LBResponse> GetLeaderboard(string scope, Contract.Types.PlayerGrade grade, bool force = false)
        => await GetLeaderboard(scope, (int)grade, force);

    private static async Task<T> makeEggIncApiRequest<T>(string endpoint, IMessage data, Func<byte[], T> parseMethod, bool isAuthenticatedMsg = true)
    {
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            data.WriteTo(stream);
            bytes = stream.ToArray();
        }

        Dictionary<string, string> body = new Dictionary<string, string> { { "data", Convert.ToBase64String(bytes) } };

        string response = await postRequest($"https://www.auxbrain.com/ei/{endpoint}", new FormUrlEncodedContent(body));

        if (!isAuthenticatedMsg)
        {
            return parseMethod(Convert.FromBase64String(response));
        }
        else
        {
            AuthenticatedMessage authMsg = AuthenticatedMessage.Parser.ParseFrom(Convert.FromBase64String(response));
            return parseMethod(parseAuthenticatedMsg(authMsg));
        }

    }

    private static async Task<string> postRequest(string url, FormUrlEncodedContent body)
    {
        using (var client = new HttpClient())
        {
            //string url = $"https://www.auxbrain.com/ei/{endpoint}";
            var response = await client.PostAsync(url, body);
            return await response.Content.ReadAsStringAsync();
        }
    }

    private static async Task<string> getRequest(string url)
    {
        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }

    private static byte[] parseAuthenticatedMsg(AuthenticatedMessage authMsg)
    {
        byte[] resBytes = authMsg.Message.ToByteArray();
        var resStream = new MemoryStream(resBytes);

        if (authMsg.Compressed)
        {
            var zls = new ZLibStream(resStream, CompressionMode.Decompress);
            var decompressed = new MemoryStream();
            zls.CopyToAsync(decompressed);
            return decompressed.ToArray();
        }
        else
        {
            return resBytes.ToArray();
        }
    }

    private static T parseJsonRsponse<T>(string json, out T output)
    {

        if (string.IsNullOrEmpty(json))
        {
            throw new InvalidOperationException("Received empty JSON response.");
        }

        // Configure options if you need case-insensitive or custom converters:
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        T? response = JsonSerializer
            .Deserialize<T>(json, options);

        output = response ?? throw new InvalidOperationException("Failed to deserialize JSON response.");
        return output;
    }
    private static T parseJsonRsponse<T>(string json) =>
        parseJsonRsponse<T>(json, out T _);
}
