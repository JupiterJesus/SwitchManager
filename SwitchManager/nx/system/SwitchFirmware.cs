using SwitchManager.util;
using System.ComponentModel;
using System.Linq;

namespace SwitchManager.nx.system
{
    /// <summary>
    /// Describes the "type" of an NCA. Every NCA in a game has a type, which is coded into headers and
    /// describes what sort of content it contains.
    /// </summary>
    public static class SwitchFirmware
    {
        private static readonly string[] versionStrings = {
            "1.0.0",
            "2.0.0",
            "2.1.0",
            "2.2.0",
            "2.3.0",
            "3.0.0",
            "3.0.1",
            "3.0.2",
            "4.0.0",
            "4.0.1",
            "4.1.0",
            "5.0.0",
            "5.0.1",
            "5.0.2",
            "5.1.0",
            "6.0.0",
            "6.0.1",
            "6.1.0",
            "6.2.0",
            "7.0.0",
            "7.0.1",
        };

        private static readonly uint[] versionNumbers = new uint[]
        {
            450,
            65796,
            131162,
            196628,
            262164,
            201327002,
            201392178,
            201457684,
            268435656,
            268501002,
            269484082,
            335544750,
            335609886,
            335675432,
            336592976,
            402653544, // 6.0.0
            496766464, // 6.0.1, estimate
            708248576, // 6.1.0, estimate
            710345728, // 6.2.0, estimate
            969430016, // 7.0.0, estimate
            999430016, // 7.0.1, wild guess
        };
        
        public static string VersionToString(long? requiredSystemVersion)
        {
            if (!requiredSystemVersion.HasValue) return null;

            uint version = (uint)requiredSystemVersion & 0xFFFFFFFF;
            for (int i = 0; i < versionNumbers.Length; i++)
            {
                if (versionNumbers[i] > version)
                    return versionStrings[i];
            }

            return versionStrings.Last();
        }

        public static long StringToVersion(string v)
        {
            for (int i = 0; i < versionStrings.Length; i++)
            {
                if (versionStrings[i].Equals(v))
                    return versionNumbers[i];
            }

            return 0;
        }
    }
}
