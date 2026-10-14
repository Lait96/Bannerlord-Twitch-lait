$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../BannerlordTwitch/BannerlordTwitch/Util/GameVersion.cs') -Raw
# Only the engine file accessor is simulated; compile the full production version utility.
$engine = @'
namespace TaleWorlds.Library
{
    public static class BasePath { public static string Name = "fixture"; }
    public static class VirtualFolders
    {
        public static string GetFileContent(string path)
            => "<Version><Singleplayer Value='v1.4.8.119303'/></Version>";
    }
}
'@
Add-Type -TypeDefinition ($source + $engine)
$setter = [BannerlordTwitch.Util.GameVersion].GetProperty('GameVersionString').GetSetMethod($true)
$cases = @(
    @{Actual='v1.4.8.119303'; Expected='v1.4.*'; Match=$true},
    @{Actual='v1.4.7'; Expected='v1.4.8'; Match=$true},
    @{Actual='v1.4.99'; Expected='v1.4.7'; Match=$true},
    @{Actual='v1.5.0'; Expected='v1.4.*'; Match=$false},
    @{Actual='v2.4.0'; Expected='v1.4.*'; Match=$false},
    @{Actual='v1.40.0'; Expected='v1.4.*'; Match=$false},
    @{Actual='v11.4.0'; Expected='v1.4.*'; Match=$false},
    @{Actual='e1.3.99'; Expected='v1.3.*'; Match=$true},
    @{Actual='v1.4.8'; Expected='v1.3.*'; Match=$false},
    @{Actual='1.4.8'; Expected='1.4'; Match=$true},
    @{Actual=' V1.4.8 '; Expected='v1.4.*'; Match=$true},
    @{Actual='v1.4.8-beta'; Expected='v1.4.*'; Match=$true},
    @{Actual=''; Expected='v1.4.*'; Match=$false},
    @{Actual=$null; Expected='v1.4.*'; Match=$false},
    @{Actual='unknown'; Expected='v1.4.*'; Match=$false},
    @{Actual='v1.4.8'; Expected=$null; Match=$false},
    @{Actual='v1.4bad'; Expected='v1.4.*'; Match=$false}
)
foreach ($case in $cases) {
    $setter.Invoke($null, @($case.Actual)) | Out-Null
    $actualMatch = [BannerlordTwitch.Util.GameVersion]::IsVersion($case.Expected)
    if ($actualMatch -ne $case.Match) { throw "Incorrect compatibility: $($case.Actual) / $($case.Expected)" }
}
"$($cases.Count) production version compatibility checks passed"
