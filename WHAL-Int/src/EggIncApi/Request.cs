using System.IO.Compression;
using Ei;
using Majcoops;
using Google.Protobuf;
using Google.Protobuf.Collections;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WHAL_Int.EggIncApi;

public class Request
{
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

    public static async Task<MajCoopsResponse> GetMajCoops(string contractId)
    {
        string url = $"https://eiapi-production.up.railway.app/majCoops?contract={contractId}";

        var rawJson = await getRequest(url);
        if (string.IsNullOrEmpty(rawJson))
        {
            throw new InvalidOperationException("Received empty JSON response.");
        }
        string wrappedJson = $"{{ \"items\": {rawJson} }}";

        // Configure options if you need case-insensitive or custom converters:
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        MajCoopsResponse? response = JsonSerializer
            .Deserialize<MajCoopsResponse>(wrappedJson, options);

        return response ?? throw new JsonException("Deserialization returned null.");
    }

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
}
