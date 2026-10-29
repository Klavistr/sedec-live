using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
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
        private static readonly HashSet<string> ConfiguredChairControllers = new HashSet<string>();
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

        [MenuItem("Castle of Ideas/Render Interior Previews")]
        public static void RenderInteriorPreviews()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop PlayMode before rendering editor previews.");
            if (SceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateWorldScene();
            var root = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT").transform;
            var main = FindDeepChild(root, "MAIN_FLOOR").position;
            var screen = FindDeepChild(root, "SCREEN_MAIN_SURFACE").position;
            var fireplace = FindDeepChild(root, "FIREPLACE_FLAME_ANCHOR").position;
            var fire = FindDeepChild(root, "FIREPLACE_FIRE").GetComponent<ParticleSystem>();
            fire.Simulate(1.0f, true, true, true);
            Debug.Log($"Castle of Ideas native fireplace preview: {fire.particleCount} live particles.");
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../preview"));
            Directory.CreateDirectory(directory);
            void Render(string name, Vector3 position, Vector3 target, float fieldOfView)
            {
                var cameraObject = new GameObject("TEMP_INTERIOR_PREVIEW_CAMERA");
                var camera = cameraObject.AddComponent<Camera>();
                var renderTexture = new RenderTexture(1600, 1000, 24);
                var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    camera.transform.position = position;
                    camera.transform.rotation = Quaternion.LookRotation(target - position);
                    camera.fieldOfView = fieldOfView;
                    camera.nearClipPlane = 0.05f;
                    camera.farClipPlane = 100f;
                    camera.targetTexture = renderTexture;
                    camera.Render();
                    RenderTexture.active = renderTexture;
                    pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
            }
            Render("unity-main-interior", new Vector3(main.x, 1.65f, -0.05f), new Vector3(screen.x, 1.5f, screen.z), 70f);
            Render("unity-fireplace", fireplace + new Vector3(-2.7f, 1.18f, 0.3f), fireplace + new Vector3(0, 0.53f, 0), 54f);
            var salon = FindDeepChild(root, "SUB_FLOOR").GetComponent<Renderer>().bounds;
            var salonScreen = FindDeepChild(root, "SCREEN_SUB_SURFACE").position;
            Render("unity-alice-interior", new Vector3(salon.min.x + 0.65f, 1.65f, salon.center.z), new Vector3(salonScreen.x, 1.65f, salonScreen.z), 76f);
            Render("unity-alice-rear", new Vector3(salon.max.x - 0.8f, 1.65f, salon.center.z), new Vector3(salon.min.x, 1.65f, salon.center.z), 76f);
            var arrival = FindDeepChild(root, "RESPAWN_SAFE_CORRIDOR").position;
            var entrance = FindDeepChild(root, "DOOR_MAIN_CENTER").position;
            Render("unity-corridor", arrival + new Vector3(1.8f, 1.6f, 0), new Vector3(entrance.x, 1.6f, entrance.z), 75f);
            var welcome = FindDeepChild(root,"WELCOME_BOARD_SURFACE").position;
            Render("unity-welcome",new Vector3(welcome.x,1.55f,-2.7f),welcome,72f);
            Render("unity-elevator",new Vector3(welcome.x,1.55f,-3.8f),new Vector3(welcome.x,1.3f,-1.3f),75f);
            var portal = FindDeepChild(root,"PORTAL_BRIDGE_SURFACE").position;
            foreach (var ps in FindDeepChild(root,"PORTAL_MIST").GetComponentsInChildren<ParticleSystem>()) ps.Simulate(2,true,true,true);
            Render("unity-portal",portal+Vector3.right*3.2f,portal,72f);
            foreach (var ps in FindDeepChild(root,"PORTAL_MIST").GetComponentsInChildren<ParticleSystem>()) ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Debug.Log("Castle of Ideas Unity interior previews rendered.");
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
            // Imported model prefab children cannot be reparented. Unpack the
            // scene instance before grouping movable chair meshes; mesh assets
            // still refer to the editable, reproducible source FBX.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "EditableWorldModel";
            model.transform.SetParent(worldRoot.transform, false);

            ConfigurePhotoMaterials(model);
            ConfigureLiteratureMaterials(model);
            ConfigureSpeakerMaterials(model);
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
            CreateFireplaceEffect(worldRoot.transform);
            CreatePortalMist(worldRoot.transform);
            ConfigureWelcomeBoard(worldRoot.transform);
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
            var portalPlayer = AttachVideoPlayer(worldRoot.transform,"PORTAL_BRIDGE_PLAYER","PortalBridge.renderTexture",playerType);
            ConfigurePortalAudio(portalPlayer,worldRoot.transform);

            var seatCount = 0;
            ConfiguredChairControllers.Clear();
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
                ConfigureChairMotion(interactable);
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
            if (videoPlayers.Length != 2)
            {
                errors.Add($"Expected one shared program player and one independent portal player, found {videoPlayers.Length}.");
            }
            foreach (var player in videoPlayers)
            {
                if (GetFieldValue(player, "ProjectionTexture") == null)
                {
                    errors.Add($"{player.gameObject.name} has no projection RenderTexture.");
                }
            }
            var programPlayer = videoPlayers.SingleOrDefault(p=>p.name=="PROGRAM_FEED_PLAYER");
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
            var speakers = GetFieldValue(programPlayer, "roomScaleAudioSources") as System.Collections.IList;
            if (speakers == null || speakers.Count != 4
                || GetFieldValue(programPlayer, "audioPlaybackMode").ToString() != "RoomScale")
                errors.Add("The single program receiver must feed four spatial channel speakers.");
            else foreach (var speaker in speakers)
            {
                var source = GetFieldValue(speaker, "audioSource") as AudioSource;
                if (source == null || source.spatialBlend != 1f || source.volume != 1f || source.outputAudioMixerGroup != null)
                    errors.Add("A speaker must preserve unity gain and use native spatial attenuation.");
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

            if (FindDeepChild(worldRoot.transform, "PROGRAM_MEDIA_CONTROL_UI") != null
                || FindDeepChild(worldRoot.transform, "PROGRAM_CONTROL_PANEL") != null)
                errors.Add("The beta world must not contain the removed media control panel.");

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
            if (portalPlayerMarker == null || portalPlayerMarker.GetComponent(playerType) == null)
                errors.Add("The independent portal HLS player is missing.");
            var portalComponent = portalPlayerMarker?.GetComponent(playerType);
            if (portalComponent != null && !string.IsNullOrEmpty(GetFieldValue(GetFieldValue(portalComponent,"playOnAwakeObject"),"videoUrl") as string))
                errors.Add("The source scene must not contain a baked portal URL.");
            var mainSurface = FindDeepChild(worldRoot.transform, "SCREEN_MAIN_SURFACE");
            foreach (var room in new[] { "MAIN", "SUB", "CORRIDOR", "ELEVATOR" })
            {
                var floor = FindDeepChild(worldRoot.transform, room + "_FLOOR")?.GetComponent<Renderer>();
                var ceiling = FindDeepChild(worldRoot.transform, room + "_CEILING");
                var ceilingRenderer = ceiling?.GetComponent<Renderer>();
                if (floor == null || ceilingRenderer == null || ceiling.GetComponent<BoxCollider>() == null
                    || Mathf.Abs(ceilingRenderer.bounds.min.y - floor.bounds.max.y - (room == "ELEVATOR" ? 2.6f : 3f)) > 0.03f)
                    errors.Add($"{room} ceiling height is incorrect.");
            }
            var fire = FindDeepChild(worldRoot.transform, "FIREPLACE_FIRE")?.GetComponent<ParticleSystem>();
            if (fire == null || !fire.main.playOnAwake || fire.GetComponent<Renderer>().sharedMaterial?.shader.name != "CastleOfIdeas/AnalyticFlame")
                errors.Add("The fireplace must contain native animated fire particles.");
            var mantel = FindDeepChild(worldRoot.transform,"FIREPLACE_MANTEL_TOP").GetComponent<Renderer>();
            if (Mathf.Abs(mantel.bounds.max.y - 1f) > .015f)
                errors.Add("The complete fireplace must be 1 metre tall.");
            foreach (var beam in worldRoot.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("MAIN_CEILING_BEAM_")))
                if (Mathf.Abs(beam.bounds.min.y - 2.7f) > .015f || Mathf.Abs(beam.bounds.size.y-.3f)>.01f || Mathf.Abs(beam.bounds.size.z-.3f)>.01f)
                    errors.Add("The lecture beam must be 30 cm square, flush with the 3-m ceiling: " + beam.name);
            foreach (var node in worldRoot.GetComponentsInChildren<Transform>(true))
                if (node.name.StartsWith("ARCH_") || node.name.StartsWith("SCREEN_SUB_FRAME_")
                    || node.name.StartsWith("PORTAL_BRIDGE_FRAME_") || node.name == "MAIN_PILASTER_W_01"
                    || node.name == "ELEVATOR_CONTROL_PANEL" || node.name.StartsWith("ELEVATOR_BUTTON_")
                    || (node.name.StartsWith("MAIN_TABLE_") && node.name.EndsWith("_Plinth")))
                    errors.Add("Removed geometry remains: " + node.name);
            var board = FindDeepChild(worldRoot.transform,"WELCOME_BOARD_SURFACE").GetComponent<Renderer>();
            if (Mathf.Abs(board.bounds.size.x - 3.08f) > .01f || Mathf.Abs(board.bounds.size.y - 1.7325f) > .01f)
                errors.Add("The welcome board must match the portal display dimensions.");
            foreach (var room in new[] {"MAIN","SUB"})
                foreach (var channel in new[] {"L","R"})
                {
                    var speaker=FindDeepChild(worldRoot.transform,$"SPEAKER_{room}_{channel}");
                    var renderers=speaker.GetComponentsInChildren<Renderer>();
                    var combined=renderers[0].bounds;
                    foreach (var renderer in renderers) combined.Encapsulate(renderer.bounds);
                    var expected=room=="MAIN" ? new Vector3(.25f,.39f,.334f) : new Vector3(.334f,.39f,.25f);
                    if ((combined.size-expected).magnitude>.003f)
                        errors.Add("Speaker dimensions are incorrect: " + speaker.name + " " + combined.size);
                    if (renderers.Any(r=>r.sharedMaterial.mainTexture!=null || r.sharedMaterial.shader.name!="Standard"))
                        errors.Add("Speaker plastic must use a plain Standard material without images: " + speaker.name);
                    var cabinet=FindDeepChild(speaker,$"SPEAKER_{room}_{channel}_CABINET").GetComponent<Renderer>().bounds;
                    var wall=FindDeepChild(worldRoot.transform,room=="MAIN" ? "MAIN_WALL_NORTH" : "SUB_WALL_WEST").GetComponent<Renderer>().bounds;
                    if (Mathf.Abs(room=="MAIN" ? cabinet.min.z-wall.max.z : cabinet.max.x-wall.min.x)>.003f)
                        errors.Add("Speaker rear face is not flush with the room wall: " + speaker.name);
                }
            foreach (var room in new[] {"MAIN","SUB"})
            {
                var prefix=room=="MAIN" ? "Main Warm Fill " : "Sub Soft Warm Fill ";
                var fixtures=FindDeepChild(worldRoot.transform,"UNITY_LIGHTING").GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith(prefix)).OrderBy(l=>l.name).ToArray();
                var bounds=FindDeepChild(worldRoot.transform,room+"_FLOOR").GetComponent<Renderer>().bounds;
                if (fixtures.Length!=2) errors.Add("Each room must have two principal lights: " + room);
                else for (int i=0;i<2;i++)
                {
                    var expected=bounds.center; expected.y=2.65f;
                    if (bounds.size.x>bounds.size.z) expected.x=Mathf.Lerp(bounds.min.x,bounds.max.x,(i+1f)/3f);
                    else expected.z=Mathf.Lerp(bounds.min.z,bounds.max.z,(i+1f)/3f);
                    if (Vector3.Distance(fixtures[i].transform.position,expected)>.005f)
                        errors.Add("Room light must be at one-third/two-thirds of the long axis: " + fixtures[i].name);
                }
            }
            foreach (var headerName in new[] {"MAIN_WALL_DOOR_HEADER","SUB_WALL_DOOR_HEADER","SUB_WALL_FRONT_DOOR_HEADER"})
            {
                var bounds = FindDeepChild(worldRoot.transform,headerName).GetComponent<Renderer>().bounds;
                if (Mathf.Abs((headerName.StartsWith("MAIN") ? bounds.size.z : bounds.size.x) - (headerName.StartsWith("MAIN") ? 1.6f : 1.2f)) > .01f
                    || Mathf.Abs(bounds.min.y-2.4f)>.01f)
                    errors.Add("Door height/width is incorrect: " + headerName);
            }
            foreach (var side in new[] {"TOP","BOTTOM","LEFT","RIGHT"})
                if (FindDeepChild(worldRoot.transform,"PORTAL_MIST_"+side)?.GetComponent<ParticleSystem>() == null)
                    errors.Add("Missing purple portal effect: " + side);
            var photographicMaterials = worldRoot.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null && AssetDatabase.GetAssetPath(material).Contains("/Photo-"))
                .Distinct().ToArray();
            if (photographicMaterials.Length != 14 || photographicMaterials.Any(material => material.mainTexture == null || material.GetTexture("_BumpMap") == null))
                errors.Add("All fourteen photograph-derived materials must have color and normal textures.");
            if (FindDeepChild(worldRoot.transform, "PORTAL_BRIDGE_STAND_POST_A") != null)
                errors.Add("The enlarged portal must be wall mounted without a stand.");
            var hearth = FindDeepChild(worldRoot.transform, "FIREPLACE_HEARTH")?.GetComponent<Renderer>();
            var corridorFloor = FindDeepChild(worldRoot.transform, "CORRIDOR_FLOOR")?.GetComponent<Renderer>();
            if (hearth == null || corridorFloor == null || Mathf.Abs(hearth.bounds.size.z - 1.6f) > 0.03f
                || Mathf.Abs(corridorFloor.bounds.min.z - hearth.bounds.max.z - 1.4f) > 0.03f)
                errors.Add("The 1.6-m fireplace must begin 1.4 m beyond the corridor edge toward the screen.");
            var literature = worldRoot.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null && AssetDatabase.GetAssetPath(material).Contains("/Literature-"))
                .Distinct().ToArray();
            if (literature.Length != 4 || literature.Any(material => material.mainTexture == null))
                errors.Add("Alice's continuous four-wall panorama is incomplete.");
            foreach (var removed in new[] { "SUB_DADO_NORTH", "SUB_SCREEN_CHAIR_01", "SUB_READING_CABINET" })
                if (FindDeepChild(worldRoot.transform, removed) != null)
                    errors.Add($"Removed Alice furnishing remains: {removed}");
            if (hearth != null)
                foreach (var pier in worldRoot.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("MAIN_PILASTER_")))
                    if (pier.bounds.Intersects(hearth.bounds))
                        errors.Add($"Measured pier intersects the fireplace: {pier.name}");
            var desks = worldRoot.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("MAIN_TABLE_") && r.name.EndsWith("_Top")).ToArray();
            if (desks.Length != 6 || desks.Any(r => Mathf.Abs(r.bounds.size.x - 1.8f) > 0.03f))
                errors.Add("The lecture room must contain six 1.8-m desks in two columns and three rows.");
            foreach (var desk in desks)
            {
                var chairs = worldRoot.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.name.StartsWith("MAIN_CHAIR_") && r.name.EndsWith("_SeatFrame")
                        && Mathf.Abs((r.transform.parent.name == "CHAIR_VISUAL" ? r.transform.parent.parent.position.z : r.transform.parent.position.z) - desk.bounds.center.z - 0.72f) < 0.03f
                        && Mathf.Abs(r.bounds.center.x - desk.bounds.center.x) < 0.9f).OrderBy(r => r.bounds.center.x).ToArray();
                if (chairs.Length != 2 || Mathf.Abs(chairs[0].bounds.min.x - desk.bounds.min.x - 0.15f) > 0.03f
                    || Mathf.Abs(desk.bounds.max.x - chairs[1].bounds.max.x - 0.15f) > 0.03f)
                    errors.Add($"Chair outer edges must sit 15 cm inside desk ends: {desk.name}");
            }
            if (mainSurface != null && primarySpawn != null
                && Vector3.Dot(mainSurface.position - primarySpawn.position, primarySpawn.right) <= 0f)
            {
                errors.Add($"The main lecture hall must be on the right when leaving the elevator (hall={mainSurface.position}, spawn={primarySpawn.position}, right={primarySpawn.right}).");
            }
            if (
                portalSurface != null
                && mainSurface != null
                // FBX maps Blender (x, y, z) to Unity (-x, z, -y).
                && portalSurface.position.x >= mainSurface.position.x
            )
            {
                errors.Add("The portal must remain on the far east wall of the main lecture hall.");
            }

            var seatMarkers = worldRoot
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.Contains("SeatPoint"))
                .ToArray();
            var configuredSeatCount = 0;
            foreach (var seatMarker in seatMarkers)
            {
                if (Vector3.Dot(seatMarker.up, Vector3.up) < 0.999f || Mathf.Abs(seatMarker.forward.y) > 0.001f)
                    errors.Add("Seat anchor retains an FBX axis tilt: " + seatMarker.name);
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
                $"1 shared program video player, build-time fixed URL, 2 linked lecture screens, " +
                $"1 optional independent portal HLS receiver, " +
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
                if (target.name.StartsWith("MAIN_LANTERN_") || target.name.StartsWith("SUB_PENDANT_") || target.name=="ELEVATOR_CEILING_LIGHT")
                {
                    var lamp=target.GetComponent<Renderer>();
                    if (lamp) lamp.shadowCastingMode=ShadowCastingMode.Off;
                }
                GameObjectUtility.SetStaticEditorFlags(
                    target,
                    StaticEditorFlags.BatchingStatic
                        | StaticEditorFlags.ContributeGI
                        | StaticEditorFlags.OccluderStatic
                        | StaticEditorFlags.OccludeeStatic
                        | StaticEditorFlags.ReflectionProbeStatic
                );

                if (target.name == "SUB_FLOOR_PLANKS" || target.name.StartsWith("ALICE_WALLPAPER_")
                    || target.GetComponent<MeshFilter>() == null || target.GetComponent<Collider>() != null)
                {
                    continue;
                }

                var name = target.name.ToUpperInvariant();
                if (
                    name.Contains("_FLOOR")
                    || name.Contains("_CEILING")
                    || name.Contains("_BEAM")
                    || name.Contains("PROJECTING_PIER")
                    || name.Contains("_WALL")
                    || name.Contains("_TABLE")
                    || name.Contains("_CHAIR")
                    || name.Contains("_BOOKCASE")
                    || name.Contains("_CABINET")
                    || name.Contains("_ARCH_")
                    || name.Contains("ELEVATOR_DOOR")
                    || name.StartsWith("FIREPLACE_")
                )
                {
                    target.AddComponent<BoxCollider>();
                }
            }
        }

        private static void ConfigurePhotoMaterials(GameObject model)
        {
            const string textureRoot = "Assets/CastleOfIdeas/Textures/PhotoDerived";
            var definitions = new Dictionary<string, string>
            {
                { "MAT_MAIN_WALL_PLASTER", "main-stone" },
                { "MAT_MAIN_FLOOR_STONE", "floor-stone" },
                { "MAT_MAIN_CEILING_STONE", "ceiling-stone" },
                { "MAT_CORRIDOR_WALL", "corridor-blue-stone" },
                { "MAT_MAIN_WOOD", "desk-wood" },
                { "MAT_CHAIR_OAK", "chair-oak" },
                { "MAT_MAIN_UPHOLSTERY", "black-leather" },
                { "MAT_DESK_CARVING", "desk-carving" },
                { "MAT_FIREPLACE_STONE", "fireplace-stone" },
                { "MAT_FIREPLACE_RELIEF", "fireplace-relief" },
                { "MAT_SUB_FLOOR_MARBLE", "alice-white-planks" },
                { "MAT_SUB_TRIM_WHITE", "alice-painted-wood" },
                { "MAT_SUB_CLOTH", "alice-red-cloth" },
                { "MAT_CHAIR_BACK_OAK", "chair-back-oak" }
            };
            EnsureAssetFolder(GeneratedPath);
            var materials = new Dictionary<string, Material>();
            foreach (var definition in definitions)
            {
                var colorPath = $"{textureRoot}/{definition.Value}.png";
                var normalPath = $"{textureRoot}/{definition.Value}-normal.png";
                foreach (var path in new[] { colorPath, normalPath })
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        throw new InvalidOperationException($"Photo texture is missing: {path}");
                    var normal = path == normalPath;
                    importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = !normal;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.maxTextureSize = 1024;
                    importer.mipmapEnabled = true;
                    importer.streamingMipmaps = true;
                    importer.SaveAndReimport();
                }
                var materialPath = $"{GeneratedPath}/Photo-{definition.Value}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.name = definition.Key;
                material.color = Color.white;
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.SetFloat("_BumpScale", 0.35f);
                material.SetFloat("_Glossiness", definition.Value == "black-leather" ? 0.38f : 0.16f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                materials[definition.Key] = material;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                    material != null && materials.TryGetValue(material.name, out var replacement)
                        ? replacement : material).ToArray();
            }
        }

        private static void CreateFireplaceEffect(Transform root)
        {
            var anchor = FindDeepChild(root, "FIREPLACE_FLAME_ANCHOR");
            if (anchor == null)
                throw new InvalidOperationException("Fireplace flame anchor is missing.");
            var effect = FindOrCreate(root, "FIREPLACE_FIRE");
            effect.transform.position = anchor.position;
            effect.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var shader = Shader.Find("CastleOfIdeas/AnalyticFlame");
            if (shader == null)
                throw new InvalidOperationException("Analytic flame shader is missing.");
            var materialPath = $"{GeneratedPath}/FireplaceFlame.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            var particles = effect.GetComponent<ParticleSystem>();
            if (!particles)
                particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.duration = 2f;
            main.maxParticles = 48;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.30f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 30f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.70f, 0.02f, 0.12f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.50f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.25f, 1), new Keyframe(1, 0)));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var glow = FindOrCreate(root, "FIREPLACE_LIGHT");
            glow.transform.position = anchor.position + new Vector3(0, 0.30f, 0);
            var light = glow.GetComponent<Light>();
            if (!light)
                light = glow.AddComponent<Light>();
            // Aim into the lecture room, away from the shared Alice wall.
            // The cone also prevents rear spill when the client disables shadows.
            glow.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            light.type = LightType.Spot;
            light.spotAngle = 140f;
            light.innerSpotAngle = 100f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.color = new Color(1, 0.30f, 0.035f);
            light.intensity = 1.1f;
            light.range = 3.0f;
            light.shadows = LightShadows.Hard;
            light.shadowBias = 0.02f;
            light.shadowNormalBias = 0.03f;
        }

        private static void CreatePortalMist(Transform root)
        {
            var surface = FindDeepChild(root, "PORTAL_BRIDGE_SURFACE");
            var frame = FindOrCreate(root, "PORTAL_MIST").transform;
            frame.position = surface.position + Vector3.right * 0.04f;
            frame.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            var path = $"{GeneratedPath}/PortalMist.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("CastleOfIdeas/PortalMist"));
                AssetDatabase.CreateAsset(material, path);
            }
            foreach (var spec in new[] {
                ("TOP", new Vector3(0,.86625f,0), new Vector3(3.08f,.035f,.02f)),
                ("BOTTOM", new Vector3(0,-.86625f,0), new Vector3(3.08f,.035f,.02f)),
                ("LEFT", new Vector3(-1.54f,0,0), new Vector3(.035f,1.7325f,.02f)),
                ("RIGHT", new Vector3(1.54f,0,0), new Vector3(.035f,1.7325f,.02f)) })
            {
                var go = FindOrCreate(frame, "PORTAL_MIST_" + spec.Item1);
                go.transform.localPosition = spec.Item2;
                var ps = go.GetComponent<ParticleSystem>();
                if (!ps) ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true; main.playOnAwake = true; main.prewarm = true;
                main.duration = 3; main.maxParticles = 80;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f,2.2f);
                main.startSpeed = 0;
                main.startSize = new ParticleSystem.MinMaxCurve(.20f,.5f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0,6.28f);
                var emission=ps.emission; emission.rateOverTime=58;
                var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=spec.Item3;
                var velocity=ps.velocityOverLifetime; velocity.enabled=true;
                velocity.x=new ParticleSystem.MinMaxCurve(-.04f,.04f);
                velocity.y=new ParticleSystem.MinMaxCurve(.02f,.12f);
                velocity.z=new ParticleSystem.MinMaxCurve(-.015f,.015f);
                var color=ps.colorOverLifetime; color.enabled=true;
                var gradient=new Gradient();
                gradient.SetKeys(new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});
                color.color=gradient;
                var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
                renderer.renderMode=ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }

        private static void ConfigureWelcomeBoard(Transform root)
        {
            const string texturePath = "Assets/CastleOfIdeas/Textures/Signs/welcome-board.png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            var path = $"{GeneratedPath}/WelcomeBoard.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(Shader.Find("Unlit/Texture")); AssetDatabase.CreateAsset(material,path); }
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(material);
            FindDeepChild(root,"WELCOME_BOARD_SURFACE").GetComponent<Renderer>().sharedMaterial=material;
        }

        private static void ConfigureLiteratureMaterials(GameObject model)
        {
            const string textureRoot = "Assets/CastleOfIdeas/Textures/AliceLiterature";
            var definitions = new Dictionary<string, string>
            {
                { "MAT_ALICE_WALL_N", "wallpaper-north" },
                { "MAT_ALICE_WALL_E", "wallpaper-east" },
                { "MAT_ALICE_WALL_S", "wallpaper-south" },
                { "MAT_ALICE_WALL_W", "wallpaper-west" }
            };
            var replacements = new Dictionary<string, Material>();
            foreach (var definition in definitions)
            {
                var texturePath = $"{textureRoot}/{definition.Value}.png";
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException($"Literature texture is missing: {texturePath}");
                importer.maxTextureSize = 8192;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                var materialPath = $"{GeneratedPath}/Literature-{definition.Value}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Standard"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.name = definition.Key;
                material.color = Color.white;
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                material.SetFloat("_Glossiness", 0.02f);
                EditorUtility.SetDirty(material);
                replacements[definition.Key] = material;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material => material != null && replacements.TryGetValue(material.name, out var replacement) ? replacement : material).ToArray();
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

        private static void ConfigureSpeakerMaterials(GameObject model)
        {
            foreach (var shade in new[] {"Black","White"})
            {
                var path=$"{GeneratedPath}/SpeakerPlastic-{shade}.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,path); }
                material.color=shade=="Black" ? new Color(.018f,.018f,.018f) : new Color(.93f,.93f,.93f);
                material.mainTexture=null; material.SetFloat("_Metallic",0); material.SetFloat("_Glossiness",.58f);
                EditorUtility.SetDirty(material);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("SPEAKER_")))
                    if (renderer.sharedMaterial.name == "MAT_SPEAKER_"+shade.ToUpperInvariant()) renderer.sharedMaterial=material;
            }
        }

        private static void ConfigurePortalAudio(Component player, Transform root)
        {
            var anchor=FindDeepChild(root,"PORTAL_BRIDGE_VOICE_ANCHOR");
            var go=FindOrCreate(root,"PORTAL_AUDIO_OUTPUT");
            go.transform.position=anchor.position;
            var source=go.GetComponent<AudioSource>(); if (!source) source=go.AddComponent<AudioSource>();
            source.playOnAwake=false; source.spatialBlend=1; source.volume=1;
            source.minDistance=1; source.maxDistance=8; source.dopplerLevel=0;
            source.rolloffMode=AudioRolloffMode.Logarithmic;
            TrySetEnumField(player,"audioPlaybackMode","AudioSource");
            TrySetField(player,"customAudioSource",source);
            TrySetField(player,"playbackVolume",1f);
        }

        private static void ConfigureProgramAudio(Component player)
        {
            var audioObject = GameObject.Find("PROGRAM_AUDIO_OUTPUT");
            if (audioObject != null) UnityEngine.Object.DestroyImmediate(audioObject);
            var outputs = CreateListForField(player, "roomScaleAudioSources");
            var entryType = outputs.GetType().GetGenericArguments()[0];
            foreach (var room in new[] { "MAIN", "SUB" })
                foreach (var channel in new[] { "L", "R" })
                {
                    var anchor = FindDeepChild(player.transform.root, $"SPEAKER_{room}_{channel}_AUDIO");
                    var source = anchor.GetComponent<AudioSource>();
                    if (!source) source = anchor.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 1f;
                    source.volume = 1f;
                    source.dopplerLevel = 0f;
                    source.minDistance = 3f;
                    source.maxDistance = 16f;
                    source.rolloffMode = AudioRolloffMode.Logarithmic;
                    source.outputAudioMixerGroup = null;
                    var entry = Activator.CreateInstance(entryType);
                    TrySetEnumField(entry, "type", channel == "L" ? "FrontLeft" : "FrontRight");
                    TrySetField(entry, "audioSource", source);
                    outputs.Add(entry);
                }
            TrySetEnumField(player, "audioPlaybackMode", "RoomScale");
            TrySetField(player, "roomScaleAudioSources", outputs);
            TrySetField(player, "customAudioSource", null);
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
            // FBX empties retain Blender's -90-degree X axis conversion.
            // CVR uses the marker's world up/forward directly for avatar IK.
            var direction = interactable.name.EndsWith("_N") ? Vector3.forward : Vector3.back;
            seatPoint.rotation = Quaternion.LookRotation(direction, Vector3.up);
            exitPoint.rotation = Quaternion.LookRotation(direction, Vector3.up);
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
                collider.isTrigger = true;
            }
            EditorUtility.SetDirty(interactable);
        }

        private static void ConfigureChairMotion(Component interactable)
        {
            var chair = interactable.transform;
            foreach (var node in chair.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(node.gameObject, 0);
            var visual = new GameObject("CHAIR_VISUAL").transform;
            visual.SetParent(chair, false);
            foreach (var child in chair.Cast<Transform>().ToArray())
                if (child != visual && child.GetComponent<Renderer>() != null)
                    child.SetParent(visual, true);
            if (visual.GetComponentsInChildren<Renderer>().Length == 0)
                throw new InvalidOperationException("No movable chair meshes were grouped: " + chair.name);
            var lecture = chair.name.StartsWith("MAIN_");
            var amount = lecture ? 0.45f : 0.48f;
            var worldPush = new Vector3(0, 0, chair.name.EndsWith("_N") ? amount : -amount);
            var tucked = chair.InverseTransformVector(worldPush);
            visual.localPosition = tucked;
            var group = lecture ? "Lecture" : "Salon";
            var controllerPath = $"{GeneratedPath}/Chair-{group}.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            if (ConfiguredChairControllers.Add(group))
            {
              var layers = controller.layers;
              layers[0].defaultWeight = 1f;
              controller.layers = layers;
              foreach (var old in machine.states)
              {
                  machine.RemoveState(old.state);
                  UnityEngine.Object.DestroyImmediate(old.state, true);
              }
              foreach (var stateName in new[] { "Tucked", "Pull", "Tuck" })
              {
                var path = $"{GeneratedPath}/Chair-{group}-{stateName}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, path);
                }
                clip.ClearCurves();
                clip.name = $"Chair-{group}-{stateName}";
                clip.frameRate = 60;
                var start = stateName == "Tuck" ? Vector3.zero : tucked;
                var end = stateName == "Pull" ? Vector3.zero : tucked;
                foreach (var axis in new[] { 0, 1, 2 })
                    AnimationUtility.SetEditorCurve(clip,
                        EditorCurveBinding.FloatCurve("CHAIR_VISUAL", typeof(Transform), "m_LocalPosition." + "xyz"[axis]),
                        AnimationCurve.EaseInOut(0, start[axis], 0.35f, end[axis]));
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                var state = machine.AddState(stateName);
                state.motion = clip;
                if (stateName == "Tucked") machine.defaultState = state;
                EditorUtility.SetDirty(clip);
              }
            }
            var animator = chair.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var enter = new UnityEvent();
            var exit = new UnityEvent();
            UnityEventTools.AddStringPersistentListener(enter, animator.Play, "Pull");
            UnityEventTools.AddStringPersistentListener(exit, animator.Play, "Tuck");
            TrySetField(interactable, "onEnterSeat", enter);
            TrySetField(interactable, "onExitSeat", exit);
            EditorUtility.SetDirty(controller);
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
            Vector3 Above(string markerName, float height)
            {
                var marker = FindDeepChild(root, markerName);
                if (marker == null)
                {
                    throw new InvalidOperationException($"Lighting anchor {markerName} is missing.");
                }
                var position = root.InverseTransformPoint(marker.position);
                position.y = height;
                return position;
            }
            Vector3 LongAxisPoint(string name, float fraction)
            {
                var bounds=FindDeepChild(root,name).GetComponent<Renderer>().bounds;
                var position=bounds.center;
                if (bounds.size.x>bounds.size.z) position.x=Mathf.Lerp(bounds.min.x,bounds.max.x,fraction);
                else position.z=Mathf.Lerp(bounds.min.z,bounds.max.z,fraction);
                position.y=2.65f;
                return root.InverseTransformPoint(position);
            }
            var lights = new List<(string Name, Vector3 Position, Color Color, float Intensity, float Range)>
            {
                ("Main Warm Fill 1", LongAxisPoint("MAIN_FLOOR",1f/3f), new Color(1f, 0.78f, 0.55f), 1.15f, 10f),
                ("Main Warm Fill 2", LongAxisPoint("MAIN_FLOOR",2f/3f), new Color(1f, 0.78f, 0.55f), 1.15f, 10f),
                ("Sub Soft Warm Fill 1", LongAxisPoint("SUB_FLOOR",1f/3f), new Color(1f, 0.95f, 0.87f), 1.3f, 10f),
                ("Sub Soft Warm Fill 2", LongAxisPoint("SUB_FLOOR",2f/3f), new Color(1f, 0.95f, 0.87f), 1.3f, 10f),
                ("Corridor Fill", Above("CORRIDOR_FLOOR", 2.65f), new Color(0.65f, 0.79f, 1f), 1.1f, 10f),
                ("Elevator Cabin", Above("ELEVATOR_FLOOR", 2.45f), new Color(1f, 0.96f, 0.89f), 1.8f, 3.5f)
            };
            foreach (var obsolete in lighting.transform.Cast<Transform>().Where(t=>!lights.Any(l=>l.Name==t.name)).ToArray())
                UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
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
