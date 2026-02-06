namespace EggIncApi;

public static class Config
{
    private const string eid_txt_path = "EID.txt";
    public static string EID { get; } = "";
    public static string? USER_ID { get; }

    public const uint CLIENT_VERSION = 62;
    public const string VERSION = "1.29.1";
    public const string BUILD = "111279";
    public const string PLATFORM = "IOS";
    public const uint CURRENT_CLIENT_VERSION = 999;

    static Config()
    {
        if (File.Exists(eid_txt_path))
        {
            string[] parameters = File.ReadAllText(eid_txt_path).Split("\n");
            EID = parameters.ElementAtOrDefault(0) ?? "";
            USER_ID = parameters.ElementAtOrDefault(1);
        }
    }
}
