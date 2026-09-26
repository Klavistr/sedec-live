using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Sedec.World.Editor
{
    public static class SEDECWorldSetup
    {
        private const string ModelPath = "Assets/SEDECWorld/Models/sedec-school-world.fbx";
        private const string GeneratedPath = "Assets/SEDECWorld/Generated";
        private const string ScenePath = "Assets/SEDECWorld/Scenes/SEDEC-School-World.unity";

        [MenuItem("SEDEC/Build World Scene")]
        public static void BuildWorldScene()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Generated model not found at {ModelPath}. Run `make cvxr-world` first."
                );
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var worldRoot = new GameObject("SEDEC_WORLD_ROOT");
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

            EnsureAssetFolder("Assets/SEDECWorld/Scenes");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = worldRoot;
            Debug.Log($"SEDEC world scene created at {ScenePath}");
        }

        [MenuItem("SEDEC/Attach Available CCK Components")]
        public static void AttachAvailableCckComponents()
        {
            var worldRoot = GameObject.Find("SEDEC_WORLD_ROOT");
            if (worldRoot == null)
            {
                throw new InvalidOperationException("Build the world scene before attaching CCK components.");
            }

            var worldType = FindComponentType("CVRWorld");
            var playerType = FindComponentType("CVRVideoPlayer");
            var interactableType = FindComponentType("CVRInteractable");
            if (worldType == null || playerType == null || interactableType == null)
            {
                throw new InvalidOperationException(
                    "CCK components were not found. Import the official CCK 4 package into this project first."
                );
            }

            var descriptor = FindOrCreate(worldRoot.transform, "CVR_WORLD_DESCRIPTOR");
            var worldComponent = AddComponentIfMissing(descriptor, worldType);
            var primarySpawn = FindDeepChild(worldRoot.transform, "SPAWN_PRIMARY_EV");
            TryAssignPrimarySpawn(worldComponent, primarySpawn);

            AttachVideoPlayer(worldRoot.transform, "SCREEN_MAIN_PLAYER", "MainScreen.renderTexture", playerType);
            AttachVideoPlayer(worldRoot.transform, "SCREEN_SUB_PLAYER", "SubScreen.renderTexture", playerType);

            var seatCount = 0;
            foreach (var transform in worldRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.Contains("CHAIR") || transform.name.EndsWith("Seat") || transform.name.EndsWith("Back") || transform.name.EndsWith("Pedestal"))
                {
                    continue;
                }
                if (transform.Find("SeatPoint") == null || transform.Find("ExitPoint") == null)
                {
                    continue;
                }
                AddComponentIfMissing(transform.gameObject, interactableType);
                seatCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                $"Attached CCK descriptors and {seatCount} seat interactables. " +
                "Assign Sit At Position actions to SeatPoint/ExitPoint after validating the imported CCK schema."
            );
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
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var match = assembly
                        .GetTypes()
                        .FirstOrDefault(type => type.Name == shortName && typeof(Component).IsAssignableFrom(type));
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

        private static Transform FindDeepChild(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == name);
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
            var lighting = new GameObject("UNITY_LIGHTING");
            lighting.transform.SetParent(root, false);
            var lights = new List<(string Name, Vector3 Position, Color Color, float Intensity, float Range)>
            {
                ("Main Warm Fill", new Vector3(0f, 3.7f, 4.2f), new Color(1f, 0.55f, 0.28f), 2.2f, 18f),
                ("Sub Cool Fill", new Vector3(16.5f, 9.2f, 4.2f), new Color(0.75f, 0.88f, 1f), 2.6f, 16f),
                ("Corridor Fill", new Vector3(17.5f, 2.7f, 3.2f), new Color(0.9f, 0.72f, 0.58f), 1.2f, 10f),
                ("Elevator Cabin", new Vector3(17.5f, 0f, 2.85f), new Color(0.68f, 0.84f, 1f), 1.8f, 5f)
            };
            foreach (var definition in lights)
            {
                var lightObject = new GameObject(definition.Name);
                lightObject.transform.SetParent(lighting.transform, false);
                lightObject.transform.localPosition = definition.Position;
                var light = lightObject.AddComponent<Light>();
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

    public sealed class SEDECWorldModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("Assets/SEDECWorld/Models/sedec-school-world.fbx"))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
