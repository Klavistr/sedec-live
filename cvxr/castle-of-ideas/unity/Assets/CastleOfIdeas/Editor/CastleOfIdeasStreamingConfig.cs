#if UNITY_EDITOR && CVR_CCK_4_OR_NEWER
using System;
using System.IO;
using System.Linq;
using ABI.CCK.Components;
using CVR.CCKEditor.ContentBuilder;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace CastleOfIdeas.Editor
{
    internal static class CastleOfIdeasStreamingConfig
    {
        private const string ConfigFileName = "streaming.local.json";

        [Serializable]
        private sealed class LocalConfig
        {
            public string receiverUrl;
            public string portalReceiverUrl;
        }

        internal static string RequireReceiverUrl()
        {
            var configPath = FindConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                throw new InvalidOperationException(
                    $"{ConfigFileName} was not found. Copy streaming.local.example.json " +
                    "to streaming.local.json and set the HTTPS .m3u8 receiver URL before building."
                );
            }

            LocalConfig config;
            try
            {
                config = JsonUtility.FromJson<LocalConfig>(File.ReadAllText(configPath));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not read {ConfigFileName}: {exception.Message}",
                    exception
                );
            }

            var receiverUrl = config?.receiverUrl?.Trim();
            if (!IsSupportedReceiverUrl(receiverUrl))
            {
                throw new InvalidOperationException(
                    $"{ConfigFileName} must contain an HTTPS receiverUrl ending in .m3u8 " +
                    "(a query string or fragment is allowed)."
                );
            }
            return receiverUrl;
        }

        internal static void ApplyToBuildRoot(GameObject worldRoot, string receiverUrl, string portalReceiverUrl = null)
        {
            var sceneRoot = GetSceneRoot(worldRoot);
            if (sceneRoot == null || !IsCastleOfIdeasWorld(sceneRoot))
            {
                return;
            }

            var player = sceneRoot
                .GetComponentsInChildren<CVRVideoPlayer>(true)
                .SingleOrDefault(component => component.gameObject.name == "PROGRAM_FEED_PLAYER");
            if (player == null)
            {
                throw new InvalidOperationException(
                    "PROGRAM_FEED_PLAYER with CVRVideoPlayer was not found in the temporary world."
                );
            }

            var playOnAwake = new CVRVideoPlayerPlaylistEntity
            {
                videoUrl = receiverUrl,
                videoTitle = "Program Feed"
            };
            player.playOnAwakeObject = playOnAwake;
            player.autoplay = true;

            var portalUrl = portalReceiverUrl ?? OptionalPortalReceiverUrl();
            var portal = sceneRoot.GetComponentsInChildren<CVRVideoPlayer>(true)
                .Single(component => component.gameObject.name == "PORTAL_BRIDGE_PLAYER");
            portal.playOnAwakeObject = string.IsNullOrEmpty(portalUrl) ? null : new CVRVideoPlayerPlaylistEntity
            {
                videoUrl = portalUrl,
                videoTitle = "Portal Feed"
            };
            portal.autoplay = !string.IsNullOrEmpty(portalUrl);
            EditorUtility.SetDirty(portal);
            Debug.Log("Castle of Ideas portal HLS: " + (portal.autoplay ? "configured (redacted)" : "not configured; receiver remains idle"));

            EditorUtility.SetDirty(player);
            Debug.Log(
                $"Castle of Ideas receiver URL injected into the temporary build " +
                $"(redacted, {receiverUrl.Length} characters)."
            );
        }

        internal static string OptionalPortalReceiverUrl()
        {
            var path = FindConfigPath();
            if (string.IsNullOrEmpty(path)) return null;
            var config = JsonUtility.FromJson<LocalConfig>(File.ReadAllText(path));
            var value = config?.portalReceiverUrl?.Trim();
            if (string.IsNullOrEmpty(value)) return null;
            if (!IsSupportedReceiverUrl(value))
                throw new InvalidOperationException("portalReceiverUrl must be empty or an HTTPS .m3u8 receiver URL.");
            return value;
        }

        public static void AuditBuildInjection()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity");
            var original=GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            var sourceState=original.GetComponentsInChildren<CVRVideoPlayer>(true)
                .ToDictionary(p=>p.name,p=>EditorJsonUtility.ToJson(p));
            var copy=UnityEngine.Object.Instantiate(original);
            copy.name=original.name;
            try
            {
                const string programUrl="https://example.invalid/program/index.m3u8";
                const string portalUrl="https://example.invalid/portal/index.m3u8";
                ApplyToBuildRoot(copy,programUrl,portalUrl);
                var players=copy.GetComponentsInChildren<CVRVideoPlayer>(true);
                var program=players.Single(p=>p.name=="PROGRAM_FEED_PLAYER");
                var portal=players.Single(p=>p.name=="PORTAL_BRIDGE_PLAYER");
                if (program.playOnAwakeObject.videoUrl!=programUrl || portal.playOnAwakeObject.videoUrl!=portalUrl
                    || !program.autoplay || !portal.autoplay || program.ProjectionTexture==portal.ProjectionTexture)
                    throw new InvalidOperationException("Independent HLS URL injection failed.");
                ApplyToBuildRoot(copy,programUrl,string.Empty);
                if (portal.autoplay || portal.playOnAwakeObject!=null)
                    throw new InvalidOperationException("An empty portal URL must leave the portal idle.");
                if (original.GetComponentsInChildren<CVRVideoPlayer>(true).Any(p=>EditorJsonUtility.ToJson(p)!=sourceState[p.name]))
                    throw new InvalidOperationException("Build injection changed the source scene.");
                Debug.Log("Castle of Ideas build injection audit passed: two independent feeds, empty portal idle, source scene unchanged. No network requests made.");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        internal static bool IsCastleOfIdeasWorld(GameObject worldRoot)
        {
            var sceneRoot = GetSceneRoot(worldRoot);
            return sceneRoot != null
                && sceneRoot.name == "CASTLE_OF_IDEAS_WORLD_ROOT"
                && FindDeepChild(sceneRoot.transform, "PROGRAM_FEED_PLAYER") != null;
        }

        private static GameObject GetSceneRoot(GameObject target)
        {
            return target == null ? null : target.transform.root.gameObject;
        }

        private static bool IsSupportedReceiverUrl(string value)
        {
            if (
                string.IsNullOrWhiteSpace(value)
                || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
            )
            {
                return false;
            }
            return uri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindConfigPath()
        {
            var directory = new DirectoryInfo(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath
            );
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, ConfigFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    break;
                }
                directory = directory.Parent;
            }
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }
            if (parent.name == name)
            {
                return parent;
            }
            foreach (Transform child in parent)
            {
                var match = FindDeepChild(child, name);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }
    }

    public sealed class CastleOfIdeasStreamingBuildProcessor : CCKBuildProcessor
    {
        public override int CallbackOrder => -1000;

        public override void OnWantsToBuildWorld(GameObject world)
        {
            if (!CastleOfIdeasStreamingConfig.IsCastleOfIdeasWorld(world))
            {
                return;
            }
            if (BuildPurpose != BuildPurpose.PlayMode)
            {
                CastleOfIdeasStreamingConfig.RequireReceiverUrl();
            }
        }

        public override void OnPreProcessWorld(GameObject world)
        {
            if (!CastleOfIdeasStreamingConfig.IsCastleOfIdeasWorld(world))
            {
                return;
            }
            if (BuildPurpose == BuildPurpose.PlayMode)
            {
                try
                {
                    CastleOfIdeasStreamingConfig.ApplyToBuildRoot(
                        world,
                        CastleOfIdeasStreamingConfig.RequireReceiverUrl()
                    );
                }
                catch (InvalidOperationException exception)
                {
                    Debug.LogWarning(
                        $"Castle of Ideas PlayMode started without an embedded receiver URL: " +
                        exception.Message
                    );
                }
                return;
            }

            CastleOfIdeasStreamingConfig.ApplyToBuildRoot(
                world,
                CastleOfIdeasStreamingConfig.RequireReceiverUrl()
            );
        }
    }
}
#endif
