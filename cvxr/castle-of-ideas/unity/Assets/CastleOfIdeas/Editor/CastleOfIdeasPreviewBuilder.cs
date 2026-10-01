#if UNITY_EDITOR && CVR_CCK_4_OR_NEWER
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
                var packagePath = await BuildWindowsPreview();
                Debug.Log($"Castle of Ideas Windows Preview created at {packagePath}");
                EditorUtility.RevealInFinder(packagePath);
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
                var packagePath = await BuildWindowsPreview();
                Debug.Log($"Castle of Ideas Windows Preview created at {packagePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static async Task<string> BuildWindowsPreview()
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
            var rawOutputPath = Path.Combine(buildRoot, "windows-preview-raw");
            var packagePath = Path.Combine(buildRoot, PackageName);
            RecreateDirectory(rawOutputPath);
            RecreateDirectory(packagePath);

            var localEditorIdentifier = assetInfo.localEditorIdentifier;
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
                CastleOfIdeasStreamingConfig.ApplyToBuildRoot(
                    buildAsset.RootObject,
                    receiverUrl
                );
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
                    sourceAssetInfo.randomNum = originalRandomNumber;
                    EditorUtility.SetDirty(sourceAssetInfo);
                }
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
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
                CreateWindowsLauncher(localEditorIdentifier)
            );
            File.WriteAllText(
                Path.Combine(packagePath, "launch-castle-of-ideas-debug.cmd"),
                CreateDebugLauncher()
            );
            File.WriteAllText(
                Path.Combine(packagePath, "watch-castle-of-ideas-log.ps1"),
                CreateLogWatcher()
            );
            File.WriteAllText(Path.Combine(packagePath, "README.txt"), CreateReadme());
            File.WriteAllText(
                Path.Combine(packagePath, "build-info.json"),
                CreateBuildInfo(localEditorIdentifier, cckVersion)
            );

            var zipPath = Path.Combine(buildRoot, PackageName + ".zip");
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

            return zipPath;
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

        private static string CreateReadme()
        {
            return @"Castle of Ideas - Windows Preview

Requirements:
- Windows 10 or later
- ChilloutVR installed and registered for chilloutvr:// links

Run launch-castle-of-ideas.cmd to open this build in ChilloutVR.
Run launch-castle-of-ideas-debug.cmd instead to also open a separate diagnostics
window. The diagnostics window follows ChilloutVR's Player.log and filters
video-player, HLS, and Castle of Ideas messages. It redacts HTTPS URLs.

This is an offline Local Test preview. Other players cannot join it, and networking
features are unavailable. The published multiplayer version is distributed through
ChilloutVR itself rather than this ZIP package.

This private preview contains a build-time HLS receiver URL. The URL is not printed
in this README or in build-info.json, but it can be extracted from the world bundle;
do not redistribute this ZIP. The main and sub lecture screens share one program
player. Use Reload if the HLS feed was not ready when the world opened. Per-user
volume presets are available from -12 dB through +12 dB.

The purple portal is on the rear wall opposite the white salon screen. It remains a
visual and integration scaffold; its future low-latency bridge is not connected yet.
";
        }

        private static string CreateBuildInfo(string localEditorIdentifier, string cckVersion)
        {
            return $@"{{
  ""name"": ""Castle of Ideas"",
  ""kind"": ""windows-preview"",
  ""localEditorIdentifier"": ""{localEditorIdentifier}"",
  ""unityVersion"": ""{Application.unityVersion}"",
  ""cckVersion"": ""{cckVersion}"",
  ""programVideoPlayers"": 1,
  ""linkedLectureScreens"": 2,
  ""receiverUrl"": ""embedded-redacted"",
  ""runtimeReceiverUrlInput"": false,
  ""volumePresetsDb"": [-12, -6, 0, 6, 12],
  ""externalDiagnostics"": true,
  ""portalStatus"": ""scaffold-only"",
  ""builtAtUtc"": ""{DateTime.UtcNow:O}""
}}
";
        }
    }
}
#endif
