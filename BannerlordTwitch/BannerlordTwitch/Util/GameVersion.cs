using System.IO;
using System.Xml;
using System;
using System.Text.RegularExpressions;
using TaleWorlds.Library;

namespace BannerlordTwitch.Util
{
    public static class GameVersion
    {
        public static string GameVersionString { get; private set; }

        public static bool IsVersion(string version)
            => TryGetMajorMinor(GameVersionString, out var actual)
               && TryGetMajorMinor(version, out var expected)
               && actual == expected;

        private static bool TryGetMajorMinor(string value, out Version version)
        {
            var match = Regex.Match(value?.Trim() ?? "", @"^[ve]?([0-9]+\.[0-9]+)(?:\.|$)", RegexOptions.IgnoreCase);
            return Version.TryParse(match.Groups[1].Value, out version);
        }

        static GameVersion()
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(VirtualFolders.GetFileContent(Path.Combine(BasePath.Name, "Parameters", "Version.xml")));
            GameVersionString = xmlDocument.SelectSingleNode("Version")?.SelectSingleNode("Singleplayer")?.Attributes?["Value"]?.InnerText;
        }
    }
}
