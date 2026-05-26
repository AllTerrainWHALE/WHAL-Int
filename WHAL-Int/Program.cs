using JsonCompilers;
using EggIncApi;
using Formatter;
using Ei;
using Maj;
using Newtonsoft.Json;

namespace WHAL_Int;

internal class Program
{
    private static bool debug = false;
    public static void Main(string[] args)
    {
        if (string.IsNullOrEmpty(Config.EID))
        {
            Console.WriteLine("\"EID.txt\" not found in root directory, please create the file and only put your EID in the file.");
            return;
        }

        /* =======================
           =  Command line args  =
           ======================= */

        debug = args.Contains("--debug") || args.Contains("-d");
        bool reverse = args.Contains("--reverse") || args.Contains("-r");

        CoopFlags targetFlags = new CoopFlags
        {
            SpeedRun = args.Contains("--speedrun") || args.Contains("-sr"),
            FastRun = args.Contains("--fastrun") || args.Contains("-fr"),
            AnyGrade = args.Contains("--anygrade") || args.Contains("-ag"),
            Carry = args.Contains("--carry") || args.Contains("-c")
        };
        if (targetFlags.Flags.Count() == 0)
        { // if no TargetFlags are set, set SR and FR TargetFlags as default
            targetFlags.SpeedRun = true;
            targetFlags.FastRun = true;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("""
            ======================
              DEVELOPMENT BRANCH
            ======================
            """);
        Console.ResetColor();

        Fastlane fl = new();
        fl.SetTargetFlags(targetFlags);
        fl.CliStart();

    }
}
