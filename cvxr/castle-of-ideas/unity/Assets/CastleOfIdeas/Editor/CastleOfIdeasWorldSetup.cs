using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CastleOfIdeas.Editor
{
    public static class CastleOfIdeasWorldSetup
    {
        private const string ModelPath = "Assets/CastleOfIdeas/Models/castle-of-ideas.fbx";
        private const string GeneratedPath = "Assets/CastleOfIdeas/Generated";
        private const string ScenePath = "Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity";
        private const string ProgramAudioMixerPath =
            "Assets/CastleOfIdeas/Generated/ProgramAudio.mixer";
        private const string ProgramRenderTextureName = "ProgramFeed.renderTexture";
        private const string StableLocalEditorIdentifier =
            "62b20d81-a4fa-4a0d-bad0-34fed1cb476d";

        private sealed class WorldIdentity
        {
            public string objectId;
            public string randomNum;
        }

        [MenuItem("Castle of Ideas/Build Complete CCK World")]
        public static void BuildCompleteCckWorld()
        {
            var identity = CaptureWorldIdentity();
            EnsureProgramAudioMixer();
            BuildWorldScene();
            AttachAvailableCckComponents(identity);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            ValidateWorldScene();
        }

        [MenuItem("Castle of Ideas/Repair PlayMode Preview Camera")]
        public static void RepairPlayModePreviewCamera()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var worldRoot = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            var descriptor = worldRoot == null
                ? null
                : FindDeepChild(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var worldType = FindComponentType("CVRWorld");
            var worldComponent = descriptor == null || worldType == null
                ? null
                : descriptor.GetComponent(worldType);
            var primarySpawn = worldRoot == null
                ? null
                : FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            var safeRespawn = worldRoot == null
                ? null
                : FindDeepChild(worldRoot.transform, "RESPAWN_SAFE_CORRIDOR");
            if (
                worldRoot == null
                || worldComponent == null
                || primarySpawn == null
                || safeRespawn == null
            )
            {
                throw new InvalidOperationException(
                    "The generated world, CVRWorld descriptor, EV spawn, or corridor marker is missing."
                );
            }

            NormalizePrimarySpawn(primarySpawn, safeRespawn);
            ConfigureReferenceCamera(worldRoot.transform, primarySpawn, worldComponent);
            CreateUnityLights(worldRoot.transform);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            ValidateWorldScene();
        }

        [MenuItem("Castle of Ideas/Build World Scene")]
        public static void BuildWorldScene()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Generated model not found at {ModelPath}. Run `make cvxr-world` first."
                );
            }

            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldRoot = new GameObject("CASTLE_OF_IDEAS_WORLD_ROOT");
            var model = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (model == null)
            {
                throw new InvalidOperationException("Unity could not instantiate the generated model.");
            }
            model.name = "EditableWorldModel";
            model.transform.SetParent(worldRoot.transform, false);

            ConfigureGeometry(model);
            ConfigureScreen(model, "SCREEN_MAIN_SURFACE", "MainScreen", ProgramRenderTextureName);
            ConfigureScreen(model, "SCREEN_SUB_SURFACE", "SubScreen", ProgramRenderTextureName);
            ConfigureScreen(
                model,
                "PORTAL_BRIDGE_SURFACE",
                "PortalBridge",
                "PortalBridge.renderTexture"
            );
            CreateUnityLights(worldRoot.transform);
            CreateSetupNotes(worldRoot.transform);

            EnsureAssetFolder("Assets/CastleOfIdeas/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = worldRoot;
            Debug.Log($"Castle of Ideas scene created at {ScenePath}");
        }

        [MenuItem("Castle of Ideas/Attach Available CCK Components")]
        public static void AttachAvailableCckComponents()
        {
            AttachAvailableCckComponents(CaptureWorldIdentity());
        }

        private static void AttachAvailableCckComponents(WorldIdentity identity)
        {
            var worldRoot = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            if (worldRoot == null)
            {
                throw new InvalidOperationException("Build the world scene before attaching CCK components.");
            }

            var worldType = FindComponentType("CVRWorld");
            var assetInfoType = FindComponentType("CVRAssetInfo");
            var playerType = FindComponentType("CVRVideoPlayer");
            var interactableType = FindComponentType("CVRInteractable");
            var actionType = FindType("CVRInteractableAction");
            var operationType = FindType("CVRInteractableActionOperation");
            if (
                worldType == null
                || assetInfoType == null
                || playerType == null
                || interactableType == null
                || actionType == null
                || operationType == null
            )
            {
                throw new InvalidOperationException(
                    "CCK components were not found. Import the official CCK 4 package into this project first."
                );
            }

            var descriptor = FindOrCreate(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var worldComponent = AddComponentIfMissing(descriptor, worldType);
            var assetInfo = AddComponentIfMissing(descriptor, assetInfoType);
            RestoreWorldIdentity(assetInfo, identity);
            var primarySpawn = FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            var safeRespawn = FindDeepChild(worldRoot.transform, "RESPAWN_SAFE_CORRIDOR");
            NormalizePrimarySpawn(primarySpawn, safeRespawn);
            TryAssignPrimarySpawn(worldComponent, primarySpawn);
            TrySetEnumField(worldComponent, "spawnRule", "Sequential");
            TrySetField(worldComponent, "spawnRadius", 0.25f);
            ConfigureReferenceCamera(worldRoot.transform, primarySpawn, worldComponent);

            var programPlayer = AttachVideoPlayer(
                worldRoot.transform,
                "PROGRAM_FEED_PLAYER",
                ProgramRenderTextureName,
                playerType
            );
            if (programPlayer == null)
            {
                throw new InvalidOperationException("PROGRAM_FEED_PLAYER was not found in the model.");
            }
            ConfigureProgramAudio(programPlayer);
            ConfigureProgramControlPanel(
                worldRoot.transform,
                interactableType,
                actionType,
                operationType
            );

            var seatCount = 0;
            foreach (var transform in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.Contains("CHAIR") || transform.name.EndsWith("Seat") || transform.name.EndsWith("Back") || transform.name.EndsWith("Pedestal"))
                {
                    continue;
                }
                var seatPoint = FindMarkerChild(transform, "SeatPoint");
                var exitPoint = FindMarkerChild(transform, "ExitPoint");
                if (seatPoint == null || exitPoint == null)
                {
                    continue;
                }
                var interactable = AddComponentIfMissing(transform.gameObject, interactableType);
                ConfigureSeat(
                    interactable,
                    seatPoint,
                    exitPoint,
                    actionType,
                    operationType
                );
                seatCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                $"Attached the CCK world descriptor, one shared program video player, " +
                $"and {seatCount} configured seats."
            );
        }

        [MenuItem("Castle of Ideas/Validate World Scene")]
        public static void ValidateWorldScene()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var errors = new List<string>();
            var worldRoot = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            if (worldRoot == null)
            {
                throw new InvalidOperationException(
                    "CASTLE_OF_IDEAS_WORLD_ROOT is missing from the generated scene."
                );
            }

            if (!EditorBuildSettings.scenes.Any(item => item.enabled && item.path == ScenePath))
            {
                errors.Add("The generated scene is not enabled in Editor Build Settings.");
            }

            var worldType = FindComponentType("CVRWorld");
            var assetInfoType = FindComponentType("CVRAssetInfo");
            var playerType = FindComponentType("CVRVideoPlayer");
            var interactableType = FindComponentType("CVRInteractable");
            if (
                worldType == null
                || assetInfoType == null
                || playerType == null
                || interactableType == null
            )
            {
                throw new InvalidOperationException("CCK 4 is not installed or did not compile.");
            }

            var primarySpawn = FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            var safeRespawn = FindDeepChild(worldRoot.transform, "RESPAWN_SAFE_CORRIDOR");
            var descriptor = FindDeepChild(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var worldComponent = descriptor == null ? null : descriptor.GetComponent(worldType);
            var assetInfo = descriptor == null ? null : descriptor.GetComponent(assetInfoType);
            if (
                assetInfo == null
                || GetFieldValue(assetInfo, "localEditorIdentifier") as string
                    != StableLocalEditorIdentifier
            )
            {
                errors.Add("CVRAssetInfo does not preserve the stable local world identifier.");
            }
            if (worldComponent == null || !CollectionFieldContains(worldComponent, "spawns", primarySpawn?.gameObject))
            {
                errors.Add("CVRWorld does not use SPAWN_PRIMARY_EV as its spawn point.");
            }
            if (primarySpawn != null && Vector3.Dot(primarySpawn.up, Vector3.up) < 0.999f)
            {
                errors.Add("SPAWN_PRIMARY_EV is not upright in Unity coordinates.");
            }
            if (primarySpawn != null && safeRespawn != null)
            {
                var corridorDirection = safeRespawn.position - primarySpawn.position;
                corridorDirection.y = 0f;
                if (
                    corridorDirection.sqrMagnitude < 0.0001f
                    || Vector3.Dot(primarySpawn.forward, corridorDirection.normalized) < 0.999f
                )
                {
                    errors.Add("SPAWN_PRIMARY_EV does not face the corridor.");
                }
            }
            var referenceCameraObject = GetFieldValue(worldComponent, "referenceCamera") as GameObject;
            var referenceCamera = referenceCameraObject == null
                ? null
                : referenceCameraObject.GetComponent<Camera>();
            if (referenceCamera == null || !referenceCamera.enabled)
            {
                errors.Add("CVRWorld has no enabled reference camera for PlayMode preview.");
            }

            var videoPlayers = worldRoot.GetComponentsInChildren(playerType, true);
            if (videoPlayers.Length != 1)
            {
                errors.Add($"Expected 1 shared CVRVideoPlayer component, found {videoPlayers.Length}.");
            }
            foreach (var player in videoPlayers)
            {
                if (GetFieldValue(player, "ProjectionTexture") == null)
                {
                    errors.Add($"{player.gameObject.name} has no projection RenderTexture.");
                }
            }
            var programPlayer = videoPlayers.SingleOrDefault();
            if (programPlayer != null && programPlayer.gameObject.name != "PROGRAM_FEED_PLAYER")
            {
                errors.Add("The shared CVRVideoPlayer is not attached to PROGRAM_FEED_PLAYER.");
            }
            var playOnAwakeObject = programPlayer == null
                ? null
                : GetFieldValue(programPlayer, "playOnAwakeObject");
            if (
                !string.IsNullOrWhiteSpace(
                    GetFieldValue(playOnAwakeObject, "videoUrl") as string
                )
            )
            {
                errors.Add("The program player must not contain a baked receiver URL.");
            }
            var customAudioSource = programPlayer == null
                ? null
                : GetFieldValue(programPlayer, "customAudioSource") as AudioSource;
            if (
                customAudioSource == null
                || customAudioSource.outputAudioMixerGroup == null
                || customAudioSource.spatialBlend != 0f
            )
            {
                errors.Add("The program player does not use the 2D ProgramAudio mixer path.");
            }
            var programTexture = programPlayer == null
                ? null
                : GetFieldValue(programPlayer, "ProjectionTexture") as RenderTexture;
            foreach (var surfaceName in new[] { "SCREEN_MAIN_SURFACE", "SCREEN_SUB_SURFACE" })
            {
                var surface = FindDeepChild(worldRoot.transform, surfaceName);
                var renderer = surface == null ? null : surface.GetComponent<Renderer>();
                if (
                    renderer == null
                    || renderer.sharedMaterial == null
                    || renderer.sharedMaterial.mainTexture != programTexture
                )
                {
                    errors.Add($"{surfaceName} does not use the shared program RenderTexture.");
                }
                var mesh = surface == null ? null : surface.GetComponent<MeshFilter>()?.sharedMesh;
                if (!HasFullUvCoverage(mesh))
                {
                    errors.Add($"{surfaceName} does not have a full-range display UV map.");
                }
            }

            var controlPanel = FindDeepChild(worldRoot.transform, "PROGRAM_MEDIA_CONTROL_UI");
            var reloadButton = controlPanel == null
                ? null
                : FindDeepChild(controlPanel, "ReloadStreamButton");
            var volumeButtons = controlPanel == null
                ? Array.Empty<Transform>()
                : new[] { -12, -6, 0, 6, 12 }
                    .Select(db => FindDeepChild(controlPanel, VolumeButtonName(db)))
                    .ToArray();
            if (
                controlPanel == null
                || reloadButton?.GetComponent(interactableType) == null
                || volumeButtons.Any(button => button?.GetComponent(interactableType) == null)
            )
            {
                errors.Add("The native reload/dB media control panel is incomplete.");
            }

            var portalSurface = FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_SURFACE");
            var portalPlayerMarker = FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_PLAYER");
            var portalViewAnchor = FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_VIEW_ANCHOR");
            var portalVoiceAnchor = FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_VOICE_ANCHOR");
            var portalCameraTarget = FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_CAMERA_TARGET");
            if (
                portalSurface == null
                || portalPlayerMarker == null
                || portalViewAnchor == null
                || portalVoiceAnchor == null
                || portalCameraTarget == null
            )
            {
                errors.Add("The portal bridge surface or one of its future integration markers is missing.");
            }
            if (portalPlayerMarker != null && portalPlayerMarker.GetComponent(playerType) != null)
            {
                errors.Add(
                    "PORTAL_BRIDGE_PLAYER must remain a scaffold until its low-latency transport is validated."
                );
            }
            var subSurface = FindDeepChild(worldRoot.transform, "SCREEN_SUB_SURFACE");
            if (
                portalSurface != null
                && subSurface != null
                && portalSurface.position.x <= subSurface.position.x
            )
            {
                errors.Add("The portal must remain on the rear wall opposite the salon screen.");
            }

            var seatMarkers = worldRoot
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.Contains("SeatPoint"))
                .ToArray();
            var configuredSeatCount = 0;
            foreach (var seatMarker in seatMarkers)
            {
                var chair = seatMarker.parent;
                var interactable = chair == null ? null : chair.GetComponent(interactableType);
                if (
                    interactable != null
                    && HasConfiguredSeatAction(
                        interactable,
                        seatMarker,
                        FindMarkerChild(chair, "ExitPoint")
                    )
                )
                {
                    configuredSeatCount++;
                }
            }
            if (configuredSeatCount != seatMarkers.Length || seatMarkers.Length == 0)
            {
                errors.Add($"Configured {configuredSeatCount} of {seatMarkers.Length} seats.");
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Castle of Ideas validation failed:\n- " + string.Join("\n- ", errors)
                );
            }

            Debug.Log(
                $"Castle of Ideas validation passed: 1 EV spawn, 1 reference camera, " +
                $"1 shared program video player, build-time URL/native dB controls, 2 linked lecture screens, " +
                $"1 rear-wall portal scaffold, " +
                $"and {configuredSeatCount} seats."
            );
        }

        private static bool HasFullUvCoverage(Mesh mesh)
        {
            if (mesh == null || mesh.uv == null || mesh.uv.Length < 4)
            {
                return false;
            }
            var minX = mesh.uv.Min(item => item.x);
            var maxX = mesh.uv.Max(item => item.x);
            var minY = mesh.uv.Min(item => item.y);
            var maxY = mesh.uv.Max(item => item.y);
            return minX <= 0.001f && maxX >= 0.999f && minY <= 0.001f && maxY >= 0.999f;
        }

        private static void ConfigureReferenceCamera(
            Transform worldRoot,
            Transform primarySpawn,
            Component worldComponent
        )
        {
            if (primarySpawn == null)
            {
                throw new InvalidOperationException(
                    "SPAWN_PRIMARY_EV is required before creating the reference camera."
                );
            }

            var cameraObject = FindOrCreate(worldRoot, "WORLD_REFERENCE_CAMERA");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = primarySpawn.position + Vector3.up * 1.65f;
            cameraObject.transform.rotation = primarySpawn.rotation;

            var camera = cameraObject.GetComponent<Camera>();
            if (!camera)
            {
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.enabled = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 250f;
            camera.depth = -100f;

            TrySetField(worldComponent, "referenceCamera", cameraObject);
            EditorUtility.SetDirty(camera);
        }

        private static void NormalizePrimarySpawn(
            Transform primarySpawn,
            Transform safeRespawn
        )
        {
            if (primarySpawn == null)
            {
                throw new InvalidOperationException("SPAWN_PRIMARY_EV was not found.");
            }
            if (safeRespawn == null)
            {
                throw new InvalidOperationException("RESPAWN_SAFE_CORRIDOR was not found.");
            }

            // Blender FBX empties inherit a -90 degree X-axis conversion. CVR spawn
            // transforms need an upright Unity rotation. Derive yaw from the safe
            // corridor marker so it remains correct if the floor plan is edited.
            var corridorDirection = safeRespawn.position - primarySpawn.position;
            corridorDirection.y = 0f;
            if (corridorDirection.sqrMagnitude < 0.0001f)
            {
                throw new InvalidOperationException(
                    "RESPAWN_SAFE_CORRIDOR must not overlap SPAWN_PRIMARY_EV."
                );
            }
            primarySpawn.rotation = Quaternion.LookRotation(
                corridorDirection.normalized,
                Vector3.up
            );
            EditorUtility.SetDirty(primarySpawn);
        }

        private static void ConfigureGeometry(GameObject model)
        {
            foreach (var transform in model.GetComponentsInChildren<Transform>(true))
            {
                var target = transform.gameObject;
                GameObjectUtility.SetStaticEditorFlags(
                    target,
                    StaticEditorFlags.BatchingStatic
                        | StaticEditorFlags.ContributeGI
                        | StaticEditorFlags.OccluderStatic
                        | StaticEditorFlags.OccludeeStatic
                        | StaticEditorFlags.ReflectionProbeStatic
                );

                if (target.GetComponent<MeshFilter>() == null || target.GetComponent<Collider>() != null)
                {
                    continue;
                }

                var name = target.name.ToUpperInvariant();
                if (
                    name.Contains("_FLOOR")
                    || name.Contains("_WALL")
                    || name.Contains("_TABLE")
                    || name.Contains("_CHAIR")
                    || name.Contains("_BOOKCASE")
                    || name.Contains("_CABINET")
                    || name.Contains("_ARCH_")
                    || name.Contains("ELEVATOR_DOOR")
                )
                {
                    target.AddComponent<BoxCollider>();
                }
            }
        }

        private static void ConfigureScreen(
            GameObject model,
            string surfaceName,
            string materialName,
            string renderTextureName
        )
        {
            var surface = FindDeepChild(model.transform, surfaceName);
            if (surface == null)
            {
                Debug.LogWarning($"Screen surface {surfaceName} was not found in the generated model.");
                return;
            }

            EnsureAssetFolder(GeneratedPath);
            var renderTexturePath = $"{GeneratedPath}/{renderTextureName}";
            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(renderTexturePath);
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32)
                {
                    name = renderTextureName,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                AssetDatabase.CreateAsset(renderTexture, renderTexturePath);
            }

            var materialPath = $"{GeneratedPath}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                material = new Material(shader) { name = materialName, mainTexture = renderTexture };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                material.mainTexture = renderTexture;
                EditorUtility.SetDirty(material);
            }

            var renderer = surface.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static Component AttachVideoPlayer(
            Transform root,
            string objectName,
            string renderTextureName,
            Type playerType
        )
        {
            var playerTransform = FindDeepChild(root, objectName);
            if (playerTransform == null)
            {
                Debug.LogWarning($"Video player marker {objectName} was not found.");
                return null;
            }

            var component = AddComponentIfMissing(playerTransform.gameObject, playerType);
            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                $"{GeneratedPath}/{renderTextureName}"
            );
            TryAssignProjectionTexture(component, renderTexture);
            TrySetField(component, "syncEnabled", true);
            TrySetEnumField(component, "audioPlaybackMode", "Direct");
            TrySetField(component, "playbackVolume", 1f);
            TrySetField(component, "interactiveUI", false);
            TrySetField(component, "autoplay", false);
            TrySetField(component, "playOnAwakeObject", null);
            return component;
        }

        private static void ConfigureProgramAudio(Component player)
        {
            var mixerGroup = EnsureProgramAudioMixer();
            var audioObject = GameObject.Find("PROGRAM_AUDIO_OUTPUT");
            if (audioObject == null)
            {
                audioObject = new GameObject("PROGRAM_AUDIO_OUTPUT");
            }
            var source = audioObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = audioObject.AddComponent<AudioSource>();
            }
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.outputAudioMixerGroup = mixerGroup;

            TrySetEnumField(player, "audioPlaybackMode", "AudioSource");
            TrySetField(player, "customAudioSource", source);
            TrySetField(player, "playbackVolume", 1f);
            TrySetField(player, "interactiveUI", false);
            TrySetField(player, "autoplay", false);
            TrySetField(player, "playOnAwakeObject", null);
        }

        private static AudioMixerGroup EnsureProgramAudioMixer()
        {
            EnsureAssetFolder(GeneratedPath);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(ProgramAudioMixerPath);
            if (mixer == null)
            {
                var controllerType = FindType("AudioMixerController");
                var createMethod = controllerType?.GetMethod(
                    "CreateMixerControllerAtPath",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                );
                mixer = createMethod?.Invoke(null, new object[] { ProgramAudioMixerPath }) as AudioMixer;
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(ProgramAudioMixerPath, ImportAssetOptions.ForceUpdate);
                mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(ProgramAudioMixerPath) ?? mixer;
            }
            if (mixer == null)
            {
                throw new InvalidOperationException("Could not create the program AudioMixer asset.");
            }

            EnsureExposedMixerVolume(mixer, "ProgramGain");
            EnsureProgramVolumeSnapshots(mixer);
            var group = mixer.FindMatchingGroups("Master").FirstOrDefault();
            if (group == null)
            {
                throw new InvalidOperationException("ProgramAudio.mixer has no Master group.");
            }
            return group;
        }

        private static void EnsureExposedMixerVolume(AudioMixer mixer, string parameterName)
        {
            var controllerType = mixer.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var exposedProperty = controllerType.GetProperty("exposedParameters", flags);
            var masterProperty = controllerType.GetProperty("masterGroup", flags);
            var masterGroup = masterProperty?.GetValue(mixer);
            var guid = masterGroup
                ?.GetType()
                .GetMethod("GetGUIDForVolume", flags)
                ?.Invoke(masterGroup, null);
            var current = exposedProperty?.GetValue(mixer) as Array;
            if (exposedProperty == null || guid == null || current == null)
            {
                throw new InvalidOperationException(
                    "Unity AudioMixer internals changed; ProgramGain could not be exposed."
                );
            }

            var elementType = exposedProperty.PropertyType.GetElementType();
            var guidField = elementType?.GetField("guid", flags);
            var nameField = elementType?.GetField("name", flags);
            if (elementType == null || guidField == null || nameField == null)
            {
                throw new InvalidOperationException("Unity exposed AudioMixer parameter fields were not found.");
            }
            foreach (var item in current)
            {
                if (nameField.GetValue(item) as string == parameterName)
                {
                    return;
                }
            }

            var updated = Array.CreateInstance(elementType, current.Length + 1);
            Array.Copy(current, updated, current.Length);
            var parameter = Activator.CreateInstance(elementType);
            guidField.SetValue(parameter, guid);
            nameField.SetValue(parameter, parameterName);
            updated.SetValue(parameter, current.Length);
            exposedProperty.SetValue(mixer, updated);
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureProgramVolumeSnapshots(AudioMixer mixer)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var controllerType = mixer.GetType();
            var snapshotsProperty = controllerType.GetProperty("snapshots", flags);
            var targetSnapshotProperty = controllerType.GetProperty("TargetSnapshot", flags);
            var startSnapshotProperty = controllerType.GetProperty("startSnapshot", flags);
            var cloneSnapshotMethod = controllerType.GetMethod(
                "CloneNewSnapshotFromTarget",
                flags,
                null,
                new[] { typeof(bool) },
                null
            );
            var masterGroup = controllerType.GetProperty("masterGroup", flags)?.GetValue(mixer);
            var setVolumeMethod = masterGroup?.GetType().GetMethod(
                "SetValueForVolume",
                flags
            );
            if (
                snapshotsProperty == null
                || targetSnapshotProperty == null
                || startSnapshotProperty == null
                || cloneSnapshotMethod == null
                || masterGroup == null
                || setVolumeMethod == null
            )
            {
                throw new InvalidOperationException(
                    "Unity AudioMixer internals changed; volume snapshots could not be created."
                );
            }

            foreach (var db in new[] { -12, -6, 0, 6, 12 })
            {
                var snapshotName = VolumeSnapshotName(db);
                var snapshot = mixer.FindSnapshot(snapshotName);
                if (snapshot == null)
                {
                    var before = (snapshotsProperty.GetValue(mixer) as Array)?
                        .Cast<AudioMixerSnapshot>()
                        .ToArray() ?? Array.Empty<AudioMixerSnapshot>();
                    cloneSnapshotMethod.Invoke(mixer, new object[] { false });
                    snapshot = (snapshotsProperty.GetValue(mixer) as Array)?
                        .Cast<AudioMixerSnapshot>()
                        .FirstOrDefault(candidate => !before.Contains(candidate));
                    if (snapshot == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not create AudioMixer snapshot {snapshotName}."
                        );
                    }
                    snapshot.name = snapshotName;
                    EditorUtility.SetDirty(snapshot);
                }

                setVolumeMethod.Invoke(
                    masterGroup,
                    new object[] { mixer, snapshot, (float)db }
                );
            }
            targetSnapshotProperty.SetValue(
                mixer,
                startSnapshotProperty.GetValue(mixer)
            );
            AssetDatabase.SaveAssets();
        }

        private static string VolumeSnapshotName(int db)
        {
            return db > 0 ? $"Program +{db} dB" : $"Program {db} dB";
        }

        private static string VolumeButtonName(int db)
        {
            var sign = db > 0 ? "Plus" : db < 0 ? "Minus" : string.Empty;
            return $"Volume{sign}{Math.Abs(db)}DbButton";
        }

        private static void ConfigureProgramControlPanel(
            Transform worldRoot,
            Type interactableType,
            Type actionType,
            Type operationType
        )
        {
            var marker = FindDeepChild(worldRoot, "PROGRAM_CONTROL_PANEL");
            if (marker == null)
            {
                throw new InvalidOperationException("PROGRAM_CONTROL_PANEL marker was not found.");
            }
            var existing = FindDeepChild(marker, "PROGRAM_MEDIA_CONTROL_UI");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            var root = new GameObject(
                "PROGRAM_MEDIA_CONTROL_UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster)
            );
            root.transform.SetParent(marker, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(920f, 390f);
            rootRect.localScale = Vector3.one * 0.0015f;
            rootRect.localPosition = Vector3.zero;
            rootRect.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            var wrapperType = FindComponentType("CVRCanvasWrapper");
            if (wrapperType != null)
            {
                var wrapper = AddComponentIfMissing(root, wrapperType);
                TrySetField(wrapper, "interactionDistance", 4f);
            }

            var panel = root.AddComponent<Image>();
            panel.color = new Color(0.035f, 0.045f, 0.07f, 0.96f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateUiText("Title", rootRect, new Vector2(0, 150), new Vector2(850, 54), "PROGRAM FEED CONTROL", 34, font, TextAnchor.MiddleLeft, Color.white);
            CreateUiText("ReceiverStatus", rootRect, new Vector2(0, 102), new Vector2(850, 40), "受信URL: ビルド時設定済み（非表示）", 22, font, TextAnchor.MiddleLeft, new Color(0.63f, 0.8f, 1f));
            CreateUiText("ReceiverNote", rootRect, new Vector2(0, 66), new Vector2(850, 34), "映像が始まらない場合は再読込してください", 19, font, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.9f));

            var reloadButton = CreateUiButton("ReloadStreamButton", rootRect, new Vector2(0, 12), new Vector2(300, 56), "配信を再読込", font);
            ConfigureNativeButton(
                reloadButton.gameObject,
                "Reload program feed",
                interactableType,
                actionType,
                operationType,
                "GlobalInstanceOwnerOnly",
                null
            );

            CreateUiText("VolumeLabelTitle", rootRect, new Vector2(-345, -58), new Vector2(180, 36), "手元の音量", 22, font, TextAnchor.MiddleLeft, Color.white);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(ProgramAudioMixerPath);
            var buttonX = new[] { -244f, -122f, 0f, 122f, 244f };
            var presets = new[] { -12, -6, 0, 6, 12 };
            for (var index = 0; index < presets.Length; index++)
            {
                var db = presets[index];
                var label = db > 0 ? $"+{db} dB" : $"{db} dB";
                var button = CreateUiButton(
                    VolumeButtonName(db),
                    rootRect,
                    new Vector2(buttonX[index], -112),
                    new Vector2(108, 52),
                    label,
                    font
                );
                var snapshot = mixer?.FindSnapshot(VolumeSnapshotName(db));
                if (snapshot == null)
                {
                    throw new InvalidOperationException(
                        $"Program AudioMixer snapshot {VolumeSnapshotName(db)} was not found."
                    );
                }
                ConfigureNativeButton(
                    button.gameObject,
                    $"Set program volume to {label}",
                    interactableType,
                    actionType,
                    operationType,
                    "LocalNotNetworked",
                    operation =>
                    {
                        var customEvent = GetFieldValue(operation, "customEvent") as UnityEvent;
                        UnityEventTools.AddFloatPersistentListener(
                            customEvent,
                            snapshot.TransitionTo,
                            0f
                        );
                    }
                );
            }
        }

        private static void ConfigureNativeButton(
            GameObject button,
            string tooltip,
            Type interactableType,
            Type actionType,
            Type operationType,
            string executionType,
            Action<object> configureOperation
        )
        {
            var interactable = AddComponentIfMissing(button, interactableType);
            TrySetField(interactable, "version", 1);
            TrySetField(interactable, "tooltip", tooltip);
            var actions = CreateListForField(interactable, "actions");
            actions.Clear();
            var action = Activator.CreateInstance(actionType);
            TrySetEnumField(action, "actionType", "OnUnityUIPointerUp");
            TrySetEnumField(action, "execType", executionType);
            var operations = CreateListForField(action, "operations");
            var operation = Activator.CreateInstance(operationType);
            if (operation == null)
            {
                throw new InvalidOperationException("Could not create a CCK action operation.");
            }
            TrySetEnumField(operation, "type", "MethodCall");
            TrySetField(operation, "customEvent", new UnityEvent());
            configureOperation?.Invoke(operation);
            operations.Add(operation);
            TrySetField(action, "operations", operations);
            actions.Add(action);
            TrySetField(interactable, "actions", actions);
            EditorUtility.SetDirty(interactable);
        }

        private static Text CreateUiText(
            string name,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            string value,
            int fontSize,
            Font font,
            TextAnchor alignment,
            Color color
        )
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = target.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateUiButton(
            string name,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            string label,
            Font font
        )
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            target.transform.SetParent(parent, false);
            var rect = (RectTransform)target.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = target.GetComponent<Image>();
            image.color = new Color(0.16f, 0.42f, 0.64f, 1f);
            var button = target.GetComponent<Button>();
            button.targetGraphic = image;
            CreateUiText("Label", rect, Vector2.zero, size, label, 23, font, TextAnchor.MiddleCenter, Color.white);
            return button;
        }

        private static WorldIdentity CaptureWorldIdentity()
        {
            var worldRoot = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            var descriptor = worldRoot == null
                ? null
                : FindDeepChild(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var assetInfoType = FindComponentType("CVRAssetInfo");
            var assetInfo = descriptor == null || assetInfoType == null
                ? null
                : descriptor.GetComponent(assetInfoType);
            if (assetInfo == null)
            {
                return null;
            }
            return new WorldIdentity
            {
                objectId = GetFieldValue(assetInfo, "objectId") as string,
                randomNum = GetFieldValue(assetInfo, "randomNum") as string
            };
        }

        private static void RestoreWorldIdentity(Component assetInfo, WorldIdentity identity)
        {
            TrySetField(assetInfo, "localEditorIdentifier", StableLocalEditorIdentifier);
            if (!string.IsNullOrEmpty(identity?.objectId))
            {
                TrySetField(assetInfo, "objectId", identity.objectId);
            }
            if (!string.IsNullOrEmpty(identity?.randomNum))
            {
                TrySetField(assetInfo, "randomNum", identity.randomNum);
            }
        }

        private static void ConfigureSeat(
            Component interactable,
            Transform seatPoint,
            Transform exitPoint,
            Type actionType,
            Type operationType
        )
        {
            var action = Activator.CreateInstance(actionType);
            var operation = Activator.CreateInstance(operationType);
            TrySetEnumField(action, "actionType", "OnInteractDown");
            TrySetEnumField(action, "execType", "LocalNotNetworked");
            TrySetEnumField(operation, "type", "SitAtPosition");
            TrySetField(operation, "gameObjectVal", seatPoint.gameObject);

            var targets = CreateListForField(operation, "targets");
            targets.Add(exitPoint.gameObject);
            TrySetField(operation, "targets", targets);

            var operations = CreateListForField(action, "operations");
            operations.Add(operation);
            TrySetField(action, "operations", operations);

            var actions = CreateListForField(interactable, "actions");
            actions.Add(action);
            TrySetField(interactable, "actions", actions);
            TrySetField(interactable, "tooltip", "Sit");
            TrySetField(interactable, "version", 1);

            if (interactable.GetComponent<BoxCollider>() == null)
            {
                var collider = interactable.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.6f, -0.05f);
                collider.size = new Vector3(0.7f, 1.2f, 0.8f);
            }
            EditorUtility.SetDirty(interactable);
        }

        private static IList CreateListForField(object target, string fieldName)
        {
            var field = GetField(target, fieldName);
            if (field == null || !typeof(IList).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    $"{target.GetType().FullName}.{fieldName} is not a supported list field."
                );
            }
            return (IList)Activator.CreateInstance(field.FieldType);
        }

        private static bool HasConfiguredSeatAction(
            Component interactable,
            Transform seatPoint,
            Transform exitPoint
        )
        {
            var actions = GetFieldValue(interactable, "actions") as IList;
            if (actions == null || actions.Count != 1)
            {
                return false;
            }
            var operations = GetFieldValue(actions[0], "operations") as IList;
            if (operations == null || operations.Count != 1)
            {
                return false;
            }
            var operation = operations[0];
            var targets = GetFieldValue(operation, "targets") as IList;
            return GetFieldValue(operation, "type")?.ToString() == "SitAtPosition"
                && GetFieldValue(operation, "gameObjectVal") as GameObject == seatPoint.gameObject
                && targets != null
                && targets.Count == 1
                && targets[0] as GameObject == exitPoint?.gameObject;
        }

        private static void TryAssignProjectionTexture(Component component, RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var candidate = component
                .GetType()
                .GetFields(flags)
                .FirstOrDefault(field =>
                    typeof(RenderTexture).IsAssignableFrom(field.FieldType)
                    && (
                        field.Name.IndexOf("projection", StringComparison.OrdinalIgnoreCase) >= 0
                        || field.Name.IndexOf("texture", StringComparison.OrdinalIgnoreCase) >= 0
                    )
                );
            if (candidate != null)
            {
                candidate.SetValue(component, texture);
                EditorUtility.SetDirty(component);
                return;
            }

            Debug.LogWarning(
                $"{component.GetType().FullName} was attached, but its projection texture field was not recognized. " +
                "Assign the generated RenderTexture in the inspector."
            );
        }

        private static void TryAssignPrimarySpawn(Component component, Transform primarySpawn)
        {
            if (primarySpawn == null)
            {
                Debug.LogWarning("SPAWN_PRIMARY_EV was not found; assign the CVRWorld spawn point manually.");
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var field in component.GetType().GetFields(flags))
            {
                if (field.Name.IndexOf("spawn", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (field.FieldType == typeof(Transform))
                {
                    field.SetValue(component, primarySpawn);
                    EditorUtility.SetDirty(component);
                    return;
                }
                if (field.FieldType == typeof(Transform[]))
                {
                    field.SetValue(component, new[] { primarySpawn });
                    EditorUtility.SetDirty(component);
                    return;
                }
                if (field.FieldType == typeof(GameObject[]))
                {
                    field.SetValue(component, new[] { primarySpawn.gameObject });
                    EditorUtility.SetDirty(component);
                    return;
                }
                if (field.FieldType == typeof(List<Transform>))
                {
                    field.SetValue(component, new List<Transform> { primarySpawn });
                    EditorUtility.SetDirty(component);
                    return;
                }
                if (field.FieldType == typeof(List<GameObject>))
                {
                    field.SetValue(component, new List<GameObject> { primarySpawn.gameObject });
                    EditorUtility.SetDirty(component);
                    return;
                }
            }

            Debug.LogWarning(
                $"{component.GetType().FullName} was attached, but its spawn field was not recognized. " +
                "Assign SPAWN_PRIMARY_EV as the only primary spawn in the inspector."
            );
        }

        private static Component AddComponentIfMissing(GameObject target, Type type)
        {
            return target.GetComponent(type) ?? Undo.AddComponent(target, type);
        }

        private static Type FindComponentType(string shortName)
        {
            var type = FindType(shortName);
            return type != null && typeof(Component).IsAssignableFrom(type) ? type : null;
        }

        private static Type FindType(string shortName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var match = assembly
                        .GetTypes()
                        .FirstOrDefault(type => type.Name == shortName);
                    if (match != null)
                    {
                        return match;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Optional editor packages can expose partially loadable assemblies.
                }
            }
            return null;
        }

        private static FieldInfo GetField(object target, string name)
        {
            return target
                ?.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private static object GetFieldValue(object target, string name)
        {
            return GetField(target, name)?.GetValue(target);
        }

        private static void TrySetField(object target, string name, object value)
        {
            var field = GetField(target, name);
            if (field == null)
            {
                throw new InvalidOperationException($"{target.GetType().FullName}.{name} was not found.");
            }
            field.SetValue(target, value);
            if (target is UnityEngine.Object unityObject)
            {
                EditorUtility.SetDirty(unityObject);
            }
        }

        private static void TrySetEnumField(object target, string name, string value)
        {
            var field = GetField(target, name);
            if (field == null || !field.FieldType.IsEnum)
            {
                throw new InvalidOperationException($"{target.GetType().FullName}.{name} is not an enum field.");
            }
            TrySetField(target, name, Enum.Parse(field.FieldType, value));
        }

        private static bool CollectionFieldContains(object target, string name, GameObject expected)
        {
            if (expected == null || !(GetFieldValue(target, name) is IEnumerable values))
            {
                return false;
            }
            return values.Cast<object>().Any(value => value as GameObject == expected);
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
        }

        private static Transform FindMarkerChild(Transform parent, string markerName)
        {
            return parent
                .Cast<Transform>()
                .FirstOrDefault(item => item.name.Contains(markerName));
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }
            var result = new GameObject(name);
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void CreateUnityLights(Transform root)
        {
            var lighting = FindOrCreate(root, "UNITY_LIGHTING");
            var lights = new List<(string Name, Vector3 Position, Color Color, float Intensity, float Range)>
            {
                // The FBX importer maps Blender (x, y, z) to Unity (-x, z, y).
                ("Main Warm Fill", new Vector3(0f, 4.2f, 3.7f), new Color(1f, 0.55f, 0.28f), 2.2f, 18f),
                ("Sub Cool Fill", new Vector3(-16.5f, 4.2f, 9.2f), new Color(0.75f, 0.88f, 1f), 2.6f, 16f),
                ("Corridor Fill", new Vector3(-17.5f, 3.2f, 2.7f), new Color(0.9f, 0.72f, 0.58f), 1.2f, 10f),
                ("Elevator Cabin", new Vector3(-17.5f, 2.85f, 0f), new Color(0.68f, 0.84f, 1f), 1.8f, 5f)
            };
            foreach (var definition in lights)
            {
                var lightObject = FindOrCreate(lighting.transform, definition.Name);
                lightObject.transform.localPosition = definition.Position;
                var light = lightObject.GetComponent<Light>();
                if (!light)
                {
                    light = lightObject.AddComponent<Light>();
                }
                light.type = LightType.Point;
                light.color = definition.Color;
                light.intensity = definition.Intensity;
                light.range = definition.Range;
                light.shadows = LightShadows.Soft;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.24f, 0.32f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.11f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.055f);
        }

        private static void CreateSetupNotes(Transform root)
        {
            var notes = new GameObject(
                "SETUP_REQUIRED__IMPORT_CCK4__OPTIONAL_LOCAL_STREAM_CONFIG__BAKE_LIGHTING"
            );
            notes.transform.SetParent(root, false);
            notes.SetActive(false);
        }

        private static void EnsureAssetFolder(string path)
        {
            var current = "Assets";
            foreach (var segment in path.Split('/').Skip(1))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }
                current = next;
            }
        }
    }

    public sealed class CastleOfIdeasWorldModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("Assets/CastleOfIdeas/Models/castle-of-ideas.fbx"))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importBlendShapeNormals = ModelImporterNormals.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
