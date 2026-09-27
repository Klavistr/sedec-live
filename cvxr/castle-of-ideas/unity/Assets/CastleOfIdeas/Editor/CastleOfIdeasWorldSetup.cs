using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CastleOfIdeas.Editor
{
    public static class CastleOfIdeasWorldSetup
    {
        private const string ModelPath = "Assets/CastleOfIdeas/Models/castle-of-ideas.fbx";
        private const string GeneratedPath = "Assets/CastleOfIdeas/Generated";
        private const string ScenePath = "Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity";

        [MenuItem("Castle of Ideas/Build Complete CCK World")]
        public static void BuildCompleteCckWorld()
        {
            BuildWorldScene();
            AttachAvailableCckComponents();
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
            ConfigureScreen(model, "SCREEN_MAIN_SURFACE", "MainScreen", "MainScreen.renderTexture");
            ConfigureScreen(model, "SCREEN_SUB_SURFACE", "SubScreen", "SubScreen.renderTexture");
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
            var worldRoot = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT");
            if (worldRoot == null)
            {
                throw new InvalidOperationException("Build the world scene before attaching CCK components.");
            }

            var worldType = FindComponentType("CVRWorld");
            var playerType = FindComponentType("CVRVideoPlayer");
            var interactableType = FindComponentType("CVRInteractable");
            var actionType = FindType("CVRInteractableAction");
            var operationType = FindType("CVRInteractableActionOperation");
            if (
                worldType == null
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
            var primarySpawn = FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            var safeRespawn = FindDeepChild(worldRoot.transform, "RESPAWN_SAFE_CORRIDOR");
            NormalizePrimarySpawn(primarySpawn, safeRespawn);
            TryAssignPrimarySpawn(worldComponent, primarySpawn);
            TrySetEnumField(worldComponent, "spawnRule", "Sequential");
            TrySetField(worldComponent, "spawnRadius", 0.25f);
            ConfigureReferenceCamera(worldRoot.transform, primarySpawn, worldComponent);

            AttachVideoPlayer(worldRoot.transform, "SCREEN_MAIN_PLAYER", "MainScreen.renderTexture", playerType);
            AttachVideoPlayer(worldRoot.transform, "SCREEN_SUB_PLAYER", "SubScreen.renderTexture", playerType);

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
                $"Attached the CCK world descriptor, two video players, and {seatCount} configured seats."
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
            var playerType = FindComponentType("CVRVideoPlayer");
            var interactableType = FindComponentType("CVRInteractable");
            if (worldType == null || playerType == null || interactableType == null)
            {
                throw new InvalidOperationException("CCK 4 is not installed or did not compile.");
            }

            var primarySpawn = FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            var safeRespawn = FindDeepChild(worldRoot.transform, "RESPAWN_SAFE_CORRIDOR");
            var descriptor = FindDeepChild(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var worldComponent = descriptor == null ? null : descriptor.GetComponent(worldType);
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
                errors.Add($"Expected 2 CVRVideoPlayer components, found {videoPlayers.Length}.");
            }
            foreach (var player in videoPlayers)
            {
                if (GetFieldValue(player, "ProjectionTexture") == null)
                {
                    errors.Add($"{player.gameObject.name} has no projection RenderTexture.");
                }
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
                $"{videoPlayers.Length} video players, " +
                $"and {configuredSeatCount} seats."
            );
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

        private static void AttachVideoPlayer(
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
                return;
            }

            var component = AddComponentIfMissing(playerTransform.gameObject, playerType);
            var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                $"{GeneratedPath}/{renderTextureName}"
            );
            TryAssignProjectionTexture(component, renderTexture);
            TrySetField(component, "syncEnabled", true);
            TrySetField(component, "interactiveUI", true);
            TrySetField(component, "autoplay", false);
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
                "SETUP_REQUIRED__IMPORT_CCK4__CONFIGURE_VIDEO_URLS__BAKE_LIGHTING"
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
