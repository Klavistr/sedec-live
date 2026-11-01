#if UNITY_EDITOR && CVR_CCK_4_OR_NEWER
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ABI.CCK.Components;
using CVR.CCKEditor.ContentBuilder;
using CVR.CCKEditor.Validations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CastleOfIdeas.Editor
{
    public static class CastleOfIdeasPreviewBuilder
    {
        private const string ScenePath = "Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity";
        private const string PackageName = "castle-of-ideas-windows-preview";
        private const string BundleName = "castle-of-ideas.cvrworld";

        [MenuItem("Castle of Ideas/Build Windows Preview")]
        public static async void BuildWindowsPreviewFromMenu()
        {
            try
            {
                var packages = await BuildAudioComparisonPreviews();
                Debug.Log("Castle of Ideas audio comparison previews created: " + string.Join(", ", packages));
                EditorUtility.RevealInFinder(Path.GetDirectoryName(packages[0]));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Castle of Ideas",
                    $"Windows Preview build failed.\n\n{exception.Message}",
                    "Close"
                );
            }
        }

        public static async void BuildWindowsPreviewBatch()
        {
            try
            {
                var packages = await BuildAudioComparisonPreviews();
                Debug.Log("Castle of Ideas audio comparison previews created: " + string.Join(", ", packages));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static async Task<string[]> BuildAudioComparisonPreviews()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CastleOfIdeasWorldSetup.EnsureProgramAudioMixer();
            var sourcePortal=UnityEngine.Object.FindObjectsOfType<CVRVideoPlayer>(true).Single(p=>p.name=="PORTAL_BRIDGE_PLAYER");
            CastleOfIdeasWorldSetup.ConfigurePortalAudio(sourcePortal,sourcePortal.transform.root);
            CastleOfIdeasWorldSetup.ValidateWorldScene();
            var spatial = await BuildWindowsPreview(false);
            var stereo = await BuildWindowsPreview(true);
            return new[] { spatial, stereo };
        }

        private static async Task<string> BuildWindowsPreview(bool stereo)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                throw new InvalidOperationException(
                    "Switch the active Unity build target to Windows (64-bit), then run the build again."
                );
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            CastleOfIdeasWorldSetup.RepairPlayModePreviewCamera();

            var assetInfo = UnityEngine.Object
                .FindObjectsOfType<CVRAssetInfo>(true)
                .SingleOrDefault();
            if (assetInfo == null)
            {
                throw new InvalidOperationException("The scene must contain exactly one CVRAssetInfo component.");
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Could not resolve the Unity project directory.");
            }

            var buildRoot = Path.Combine(projectRoot, "build");
            var variant = stereo ? "stereo" : "spatial";
            var variantPackageName = PackageName + "-" + variant;
            var rawOutputPath = Path.Combine(buildRoot, "windows-preview-raw-" + variant);
            var packagePath = Path.Combine(buildRoot, variantPackageName);
            RecreateDirectory(rawOutputPath);
            RecreateDirectory(packagePath);

            // Separate local-test identities prevent one comparison version
            // from being mistaken for the other by the client cache.
            var sourceIdentifier = assetInfo.localEditorIdentifier;
            var localEditorIdentifier = stereo ? "d1e2352a-106c-41f1-a31d-c93bec5c0f6c" : sourceIdentifier;
            var cckVersion = assetInfo.cckVersion;
            var receiverUrl = CastleOfIdeasStreamingConfig.RequireReceiverUrl();
            var originalRandomNumber = assetInfo.randomNum;
            assetInfo.randomNum = new System.Random()
                .Next(11111111, 99999999)
                .ToString();
            EditorUtility.SetDirty(assetInfo);

            string builtBundlePath = null;
            TempBuildAsset buildAsset = null;
            try
            {
                var config = new BuildConfig
                {
                    BuildTarget = BuildTarget.StandaloneWindows64,
                    BuildOutputPath = rawOutputPath
                };
                buildAsset = TempBuildAsset.Create(assetInfo, BuildPurpose.LocalTest);
                buildAsset.AssetInfo.localEditorIdentifier = localEditorIdentifier;
                CastleOfIdeasStreamingConfig.ApplyToBuildRoot(
                    buildAsset.RootObject,
                    receiverUrl
                );
                // CCK converts/removes authoring components during preprocessing;
                // configure the native player before that conversion runs.
                ConfigureComparisonAudio(buildAsset.RootObject, stereo);
                buildAsset.ValidateAsset();
                buildAsset.PreProcessAsset();
                buildAsset.SaveChangesToAsset();

                var validation = await Validator.Validate(buildAsset.AssetInfo);
                foreach (var result in validation.StepResults.Where(
                    result => result.Severity != ValidationSeverity.None
                ))
                {
                    Debug.Log($"CCK validation {result.Severity}: {result.Message}");
                }
                if (!validation.IsSuccess)
                {
                    var errors = validation.StepResults
                        .Where(result => result.Severity == ValidationSeverity.Error)
                        .Select(result => result.Message);
                    throw new InvalidOperationException(
                        "CCK validation failed:\n- " + string.Join("\n- ", errors)
                    );
                }

                builtBundlePath = buildAsset.BuildAssetBundle(config);
                buildAsset.PostProcessAsset();
                buildAsset.Dispose();
                buildAsset = null;
            }
            finally
            {
                if (buildAsset != null)
                {
                    buildAsset.PostProcessAsset();
                    buildAsset.Dispose();
                }

                if (SceneManager.GetActiveScene().path != ScenePath)
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                var sourceAssetInfo = UnityEngine.Object
                    .FindObjectsOfType<CVRAssetInfo>(true)
                    .SingleOrDefault();
                if (sourceAssetInfo != null)
                {
                    if (sourceAssetInfo.localEditorIdentifier != sourceIdentifier)
                        throw new InvalidOperationException("A comparison build changed the source world identity.");
                    sourceAssetInfo.randomNum = originalRandomNumber;
                    EditorUtility.SetDirty(sourceAssetInfo);
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
                var sourceProgram = UnityEngine.Object.FindObjectsOfType<CVRVideoPlayer>(true)
                    .Single(p => p.name == "PROGRAM_FEED_PLAYER");
                if (sourceProgram.audioPlaybackMode != CVRVideoPlayer.AudioMode.RoomScale || sourceProgram.roomScaleAudioSources.Count != 4)
                    throw new InvalidOperationException("A comparison build changed the source audio topology.");
            }

            if (string.IsNullOrEmpty(builtBundlePath) || !File.Exists(builtBundlePath))
            {
                throw new InvalidOperationException(
                    "The CCK build completed without producing a Windows world bundle."
                );
            }

            File.Copy(builtBundlePath, Path.Combine(packagePath, BundleName), true);
            File.WriteAllText(
                Path.Combine(packagePath, "launch-castle-of-ideas.cmd"),
                CreateWindowsLauncher(localEditorIdentifier).Replace("\r\n", "\n").Replace("\n", "\r\n")
            );
            File.Copy(Path.Combine(packagePath, "launch-castle-of-ideas.cmd"),
                Path.Combine(packagePath, "launch-castle-of-ideas-" + variant + ".cmd"), true);
            File.WriteAllText(
                Path.Combine(packagePath, "launch-castle-of-ideas-debug.cmd"),
                CreateDebugLauncher().Replace("\r\n", "\n").Replace("\n", "\r\n")
            );
            File.WriteAllText(
                Path.Combine(packagePath, "watch-castle-of-ideas-log.ps1"),
                CreateLogWatcher()
            );
            File.WriteAllText(Path.Combine(packagePath, "README.txt"), CreateReadme(stereo));
            File.WriteAllText(
                Path.Combine(packagePath, "build-info.json"),
                CreateBuildInfo(localEditorIdentifier, cckVersion, !string.IsNullOrEmpty(CastleOfIdeasStreamingConfig.OptionalPortalReceiverUrl()), stereo)
            );

            var zipPath = Path.Combine(buildRoot, variantPackageName + ".zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(
                packagePath,
                zipPath,
                System.IO.Compression.CompressionLevel.Optimal,
                true
            );
            using (var stream=File.OpenRead(zipPath))
            using (var sha=SHA256.Create())
                File.WriteAllText(zipPath+".sha256",BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()+"  "+Path.GetFileName(zipPath)+"\n");

            return zipPath;
        }

        private static void ConfigureComparisonAudio(GameObject root, bool stereo)
        {
            // For a world, CCK's RootObject is the descriptor child; the
            // player and speakers are siblings under the complete scene root.
            root = root.transform.root.gameObject;
            var player = root.GetComponentsInChildren<CVRVideoPlayer>(true)
                .Single(p => p.name == "PROGRAM_FEED_PLAYER");
            if (stereo)
            {
                foreach (var output in player.roomScaleAudioSources)
                {
                    output.audioSource.enabled = false;
                    output.audioSource.outputAudioMixerGroup = null;
                }
                player.roomScaleAudioSources.Clear();
                player.customAudioSource = null;
                player.audioPlaybackMode = CVRVideoPlayer.AudioMode.Direct;
            }
            player.playbackVolume = 1f;
            var portal=root.GetComponentsInChildren<CVRVideoPlayer>(true).Single(p=>p.name=="PORTAL_BRIDGE_PLAYER");
            var portalSource=portal.customAudioSource;
            if (portalSource==null || portalSource.spatialBlend!=1f || portalSource.spread!=0f
                || portalSource.outputAudioMixerGroup==null
                || Vector3.Distance(portalSource.transform.position, root.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="PORTAL_BRIDGE_SURFACE").position)>.001f
                || portalSource.GetComponentsInChildren<Renderer>().Length!=0)
                throw new InvalidOperationException("Portal audio must be an invisible mono point at the display centre.");
            if (stereo)
            {
                portalSource.enabled=false;
                portalSource.outputAudioMixerGroup=null;
                portal.customAudioSource=null;
                portal.audioPlaybackMode=CVRVideoPlayer.AudioMode.Direct;
            }
            portal.playbackVolume=1f;
            if (portal.audioPlaybackMode!=(stereo ? CVRVideoPlayer.AudioMode.Direct : CVRVideoPlayer.AudioMode.AudioSource))
                throw new InvalidOperationException("Portal audio comparison mode is incorrect.");
            if (stereo ? player.audioPlaybackMode != CVRVideoPlayer.AudioMode.Direct || player.roomScaleAudioSources.Count != 0
                       : player.audioPlaybackMode != CVRVideoPlayer.AudioMode.RoomScale || player.roomScaleAudioSources.Count != 4
                         || player.roomScaleAudioSources.Any(s => !s.audioSource.enabled || s.audioSource.outputAudioMixerGroup == null))
                throw new InvalidOperationException("Audio comparison variant configuration is invalid.");
            Debug.Log("Castle of Ideas audio variant: " + (stereo ? "program and portal: stereo Direct / 0 dB / no distance attenuation" : "program: RoomScale stereo speakers; portal: invisible central mono point / both +6 dB"));
        }

        private static void RecreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
        }

        private static string CreateWindowsLauncher(string localEditorIdentifier)
        {
            return $@"@echo off
setlocal
set ""BUNDLE=%~dp0{BundleName}""
if not exist ""%BUNDLE%"" (
  echo {BundleName} was not found next to this launcher.
  pause
  exit /b 1
)
for /f ""usebackq delims="" %%U in (`powershell -NoProfile -Command ""[uri]::EscapeDataString($env:BUNDLE)""`) do set ""ENCODED=%%U""
start """" ""chilloutvr://test/world?objectId={localEditorIdentifier}&filepath=%ENCODED%""
endlocal
";
        }

        private static string CreateDebugLauncher()
        {
            return @"@echo off
start ""Castle of Ideas diagnostics"" powershell.exe -NoProfile -NoExit -ExecutionPolicy Bypass -File ""%~dp0watch-castle-of-ideas-log.ps1""
call ""%~dp0launch-castle-of-ideas.cmd""
";
        }

        private static string CreateLogWatcher()
        {
            return @"$ErrorActionPreference = ""Continue""
$candidatePaths = @(
    (Join-Path $env:USERPROFILE ""AppData\LocalLow\ChilloutVR\ChilloutVR\Player.log""),
    (Join-Path $env:USERPROFILE ""AppData\LocalLow\Alpha Blend Interactive\ChilloutVR\Player.log"")
)

Write-Host ""Castle of Ideas diagnostics"" -ForegroundColor Cyan
Write-Host ""Waiting for ChilloutVR Player.log..."" -ForegroundColor DarkGray

$logPath = $null
while (-not $logPath) {
    $logPath = $candidatePaths |
        Where-Object { Test-Path -LiteralPath $_ } |
        ForEach-Object { Get-Item -LiteralPath $_ } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $logPath) { Start-Sleep -Seconds 1 }
}

Write-Host (""Watching: "" + $logPath) -ForegroundColor Green
Write-Host ""The receiver URL is never printed by Castle of Ideas diagnostics."" -ForegroundColor DarkGray
Write-Host ""Press Ctrl+C to stop watching.`n"" -ForegroundColor DarkGray

$pattern = ""CastleOfIdeas|VideoPlayer|AVPro|HLS|m3u8|Exception|Missing|referenced script|Error""
Get-Content -LiteralPath $logPath -Tail 250 -Wait | ForEach-Object {
    if ($_ -match $pattern -and $_ -notmatch ""\[Cohtml\]"") {
        $redactedLine = $_ -replace ""https://\S+"", ""https://[redacted]""
        Write-Host $redactedLine
    }
}
";
        }

        private static string CreateReadme(bool stereo)
        {
            var mode = stereo ? "2D stereo / 0 dB / no distance attenuation" : "RoomScale spatial speakers / +6 dB / distance attenuation";
            return "Audio comparison version: " + mode + "\n\n" + @"Castle of Ideas - Windows Preview

Requirements:
- Windows 10 or later
- ChilloutVR installed and registered for chilloutvr:// links

Run launch-castle-of-ideas.cmd to open this build in ChilloutVR.
Extract the entire ZIP first; do not run the launcher inside a compressed folder.
Run launch-castle-of-ideas-debug.cmd instead to also open a separate diagnostics
window. The diagnostics window follows ChilloutVR's Player.log and filters
video-player, HLS, and Castle of Ideas messages. It redacts HTTPS URLs.

This is an offline Local Test preview. Other players cannot join it, and networking
features are unavailable. The published multiplayer version is distributed through
ChilloutVR itself rather than this ZIP package.

This private preview contains a build-time HLS receiver URL. The URL is not printed
in this README or in build-info.json, but it can be extracted from the world bundle;
do not redistribute this ZIP. The main and sub lecture screens share one program
player and start it automatically. Start the broadcast before opening the world.
The beta build has no in-world media control panel. Reopen the world if
the feed was unavailable at startup. The spatial and stereo builds are separate
world bundles with separate launchers. Close ChilloutVR before switching versions
for a clean comparison. Use identical client/Windows/headset volume settings.
Both program and portal use the selected variant. In the spatial version the
portal is an invisible mono point at the display centre, with +6 dB gain and
the same gentle distance curve as the main room. No speaker mesh is added.
No Lua or in-world audio switching UI is used.

The 70-inch-scale portal is on the far wall of the main lecture hall, directly
opposite its corridor entrance. Its independent HLS receiver starts only when
portalReceiverUrl is configured at build time. HLS portal video has the normal
streaming delay and is not a low-latency, bidirectional voice connection.
";
        }

        private static string CreateBuildInfo(string localEditorIdentifier, string cckVersion, bool portalConfigured, bool stereo)
        {
            return $@"{{
  ""name"": ""Castle of Ideas"",
  ""kind"": ""windows-preview"",
  ""worldVersion"": ""0.5.4"",
  ""audioVariant"": ""{(stereo ? "stereo" : "spatial")}"",
  ""audioPlaybackMode"": ""{(stereo ? "Direct" : "RoomScale")}"",
  ""portalAudioMode"": ""{(stereo ? "Direct" : "AudioSource")}"",
  ""portalAudioChannels"": ""{(stereo ? "stereo" : "mono")}"",
  ""portalGainDb"": {(stereo ? 0 : 6)},
  ""distanceAttenuation"": {(stereo ? "false" : "true")},
  ""localEditorIdentifier"": ""{localEditorIdentifier}"",
  ""unityVersion"": ""{Application.unityVersion}"",
  ""cckVersion"": ""{cckVersion}"",
  ""programVideoPlayers"": 1,
  ""linkedLectureScreens"": 2,
  ""receiverUrl"": ""embedded-redacted"",
  ""runtimeReceiverUrlInput"": false,
  ""launcherVersion"": 2,
  ""mediaControlPanel"": false,
  ""defaultVolumeDb"": {(stereo ? 0 : 6)},
  ""releaseStage"": ""beta"",
  ""externalDiagnostics"": true,
  ""portalVideoPlayers"": 1,
  ""portalStatus"": ""{(portalConfigured ? "embedded-redacted" : "not-configured")}"",
  ""builtAtUtc"": ""{DateTime.UtcNow:O}""
}}
";
        }
    }
}
#endif
