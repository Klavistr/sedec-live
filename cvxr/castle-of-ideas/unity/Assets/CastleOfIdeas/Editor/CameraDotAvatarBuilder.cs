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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CastleOfIdeas.Editor
{
    public static class CameraDotAvatarBuilder
    {
        private const string Folder = "Assets/CameraDot";
        private const string PrefabPath = Folder + "/Camera-Dot.prefab";
        private const string Identity = "88bd78c2-badc-46f7-a58f-06d34a189a01";
        private const float EyeHeight = 1.5f;
        private const float StandOff = .25f;
        private static float captureYaw;

        private static Transform CreateHumanoidRig(GameObject root)
        {
            var bones=new System.Collections.Generic.List<Transform>();
            Transform Bone(string name, Transform parent, Vector3 position)
            {
                var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position;
                bones.Add(t); return t;
            }
            var hips=Bone("Hips",root.transform,new Vector3(0,.82f,0));
            var spine=Bone("Spine",hips,new Vector3(0,.18f,0));
            var chest=Bone("Chest",spine,new Vector3(0,.16f,0));
            var neck=Bone("Neck",chest,new Vector3(0,.20f,0));
            var head=Bone("Head",neck,new Vector3(0,.14f,0));
            foreach (var side in new[] {"Left","Right"})
            {
                var sign=side=="Left" ? 1f : -1f;
                var shoulder=Bone(side+"Shoulder",chest,new Vector3(sign*.08f,.08f,0));
                var upperArm=Bone(side+"UpperArm",shoulder,new Vector3(sign*.10f,0,0));
                var forearm=Bone(side+"LowerArm",upperArm,new Vector3(sign*.24f,0,0));
                Bone(side+"Hand",forearm,new Vector3(sign*.20f,0,0));
                var thigh=Bone(side+"UpperLeg",hips,new Vector3(sign*.10f,-.05f,0));
                var calf=Bone(side+"LowerLeg",thigh,new Vector3(0,-.36f,.01f));
                var foot=Bone(side+"Foot",calf,new Vector3(0,-.36f,.03f));
                Bone(side+"Toes",foot,new Vector3(0,0,.12f));
            }
            var description=new HumanDescription {
                human=bones.Select(t=>new HumanBone {boneName=t.name,humanName=HumanTrait.BoneName[(int)(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),t.name)],limit=new HumanLimit {useDefaultValues=true}}).ToArray(),
                skeleton=new[] {root.transform}.Concat(bones).Select(t=>new SkeletonBone {name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                armStretch=.05f,legStretch=.05f,upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f
            };
            var rig=AvatarBuilder.BuildHumanAvatar(root,description);
            if (!rig.isValid || !rig.isHuman) throw new InvalidOperationException("Camera Dot humanoid rig is invalid.");
            rig.name="CameraDotHumanoid";
            var existing=AssetDatabase.LoadAssetAtPath<Avatar>(Folder+"/CameraDotHumanoid.asset");
            if (existing) { EditorUtility.CopySerialized(rig,existing); UnityEngine.Object.DestroyImmediate(rig); rig=existing; }
            else AssetDatabase.CreateAsset(rig,Folder+"/CameraDotHumanoid.asset");
            root.GetComponent<Animator>().avatar=rig;
            return head;
        }

        [MenuItem("Castle of Ideas/Build Camera Dot Avatar")]
        public static async void BuildFromMenu()
        {
            try { EditorUtility.RevealInFinder(await Build()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static async void BuildBatch()
        {
            try { Debug.Log("Camera Dot avatar preview: " + await Build()); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static void CreateSource()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "CameraDot");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Dot.mat");
            if (!material)
            {
                material = new Material(Shader.Find("CameraDot/Invisible"));
                AssetDatabase.CreateAsset(material, Folder + "/Dot.mat");
            }
            material.shader=Shader.Find("CameraDot/Invisible");
            if (!material.shader || material.shader.name!="CameraDot/Invisible")
                throw new InvalidOperationException("Camera Dot invisible shader is missing.");
            material.color = Color.clear;
            EditorUtility.SetDirty(material);
            var root = new GameObject("Camera Dot");
            try
            {
                root.AddComponent<Animator>();
                var head=CreateHumanoidRig(root); // Normal humanoid handling; only the tiny dot is rendered.
                var descriptor = root.AddComponent<CVRAvatar>();
                descriptor.viewPosition = new Vector3(0,EyeHeight,0);
                descriptor.voicePosition = descriptor.viewPosition;
                descriptor.useEyeMovement = false;
                descriptor.useVisemeLipsync = false;
                descriptor.useBlinkBlendshapes = false;
                var info = root.GetComponent<CVRAssetInfo>() ?? root.AddComponent<CVRAssetInfo>();
                // Retain an assigned online identity if the editable prefab is uploaded later.
                var previous = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<CVRAssetInfo>();
                info.objectId = previous ? previous.objectId : string.Empty;
                info.type = CVRAssetInfo.AssetType.Avatar;
                info.localEditorIdentifier = Identity;
                info.randomNum = "12345678";
                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Invisible_Dot_1mm";
                dot.transform.SetParent(head, false);
                dot.transform.localPosition = new Vector3(0,0,-.04f);
                dot.transform.localScale = Vector3.one*.001f;
                UnityEngine.Object.DestroyImmediate(dot.GetComponent<Collider>());
                var renderer = dot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!prefab || prefab.GetComponentsInChildren<Renderer>().Length != 1
                    || prefab.GetComponentsInChildren<Camera>().Length != 0
                    || prefab.GetComponentsInChildren<Collider>().Length != 0)
                    throw new InvalidOperationException("Camera Dot must contain one tiny renderer, no camera or collider.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                EditorSceneManager.SaveScene(scene, Folder + "/Camera-Dot.unity");
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Vector3 RenderCaptureProof(string package)
        {
            var scene = EditorSceneManager.OpenScene("Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity", OpenSceneMode.Single);
            var portal = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                .Single(t=>t.name=="PORTAL_BRIDGE_SURFACE");
            // This mirrored world's portal is on the east wall: face west (+Unity X).
            var stand = portal.position + Vector3.right*StandOff;
            stand.y = 0;
            var cameraObject = new GameObject("TEMP_CAMERA_DOT_CAPTURE_PROOF");
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var texture = new RenderTexture(1920,1080,24);
            var pixels = new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                var floor=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                    .Single(t=>t.name=="MAIN_FLOOR").GetComponent<Renderer>().bounds;
                var direction=floor.center-stand; direction.y=0;
                var framing=Quaternion.LookRotation(direction);
                captureYaw=framing.eulerAngles.y;
                avatar.transform.SetPositionAndRotation(stand, framing);
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(stand+Vector3.up*EyeHeight, avatar.transform.rotation);
                camera.fieldOfView=60;
                camera.aspect=16f/9f;
                camera.nearClipPlane=.03f;
                camera.farClipPlane=100f;
                camera.targetTexture=texture;
                // Include both the portal-normal view and the room-centred shot.
                camera.transform.rotation=Quaternion.Euler(0,90,0);
                camera.Render();
                RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(package,"portal-camera-straight.png"),pixels.EncodeToPNG());
                camera.transform.rotation=framing;
                camera.Render();
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(package,"portal-camera-framing.png"),pixels.EncodeToPNG());
                // Make the otherwise tiny object large and put it directly in
                // front of the lens: it must still alter no rendered pixels.
                var baseline=pixels.GetRawTextureData<byte>().ToArray();
                var dot=avatar.GetComponentsInChildren<Renderer>().Single().transform;
                dot.position=camera.transform.position+camera.transform.forward*.4f;
                dot.localScale=Vector3.one*.3f;
                camera.Render();
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
                if (!baseline.SequenceEqual(pixels.GetRawTextureData<byte>().ToArray()))
                    throw new InvalidOperationException("Invisible Camera Dot changed the camera image.");
                Debug.Log("Camera Dot invisibility render audit passed: enlarged mesh in front of lens changed zero pixels.");
                var proofDirectory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../preview"));
                Directory.CreateDirectory(proofDirectory);
                File.WriteAllBytes(Path.Combine(proofDirectory,"camera-dot-framing.png"),pixels.EncodeToPNG());
                return stand;
            }
            finally
            {
                RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        private static async Task<string> Build()
        {
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity", OpenSceneMode.Single);
            CreateSource();
            var buildRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../build"));
            var package=Path.Combine(buildRoot,"camera-dot-windows-preview-v0.1.1");
            Directory.CreateDirectory(package);
            Directory.CreateDirectory(Path.Combine(buildRoot,"camera-dot-raw"));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            TempBuildAsset asset=null;
            try
            {
                var info=instance.GetComponent<CVRAssetInfo>();
                info.randomNum=new System.Random().Next(11111111,99999999).ToString();
                asset=TempBuildAsset.Create(info,BuildPurpose.LocalTest);
                asset.ValidateAsset(); asset.PreProcessAsset(); asset.SaveChangesToAsset();
                var validation=await Validator.Validate(asset.AssetInfo);
                foreach (var result in validation.StepResults.Where(r=>r.Severity!=ValidationSeverity.None))
                    Debug.Log("Camera Dot validation "+result.Severity+": "+result.Message);
                if (!validation.IsSuccess) throw new InvalidOperationException("Camera Dot failed CCK validation.");
                var bundle=asset.BuildAssetBundle(new BuildConfig {BuildTarget=BuildTarget.StandaloneWindows64,
                    BuildOutputPath=Path.Combine(buildRoot,"camera-dot-raw")});
                File.Copy(bundle,Path.Combine(package,"camera-dot.cvravatar"),true);
            }
            finally
            {
                if (asset!=null) { asset.PostProcessAsset(); asset.Dispose(); }
                UnityEngine.Object.DestroyImmediate(instance);
            }
            var stand=RenderCaptureProof(package);
            var launcher=$@"@echo off
setlocal
set ""BUNDLE=%~dp0camera-dot.cvravatar""
if not exist ""%BUNDLE%"" (
  echo camera-dot.cvravatar was not found next to this launcher.
  pause
  exit /b 1
)
for /f ""usebackq delims="" %%U in (`powershell -NoProfile -Command ""[uri]::EscapeDataString($env:BUNDLE)""`) do set ""ENCODED=%%U""
start """" ""chilloutvr://test/avatar?objectId={Identity}&filepath=%ENCODED%""
endlocal
";
            File.WriteAllText(Path.Combine(package,"launch-camera-dot.cmd"),launcher.Replace("\r\n","\n").Replace("\n","\r\n"));
            File.WriteAllText(Path.Combine(package,"README.txt"),
                "Camera Dot 0.1.1 - Windows Local Test\n\n"+
                "Open a Castle of Ideas world preview first, then run launch-camera-dot.cmd.\n"+
                "This loads an avatar, not a world. It does not teleport you.\n"+
                "The reference images stand 0.25 m in front of the portal centre. Actual minimum distance depends on CVR's player collision.\n"+
                "Face the lecture-room centre, with your back to the portal wall.\n"+
                "The invisible skeleton is a standard humanoid rig. Normal CVR movement/collision applies; no noclip or teleport script is included.\n"+
                "The viewpoint is 1.5 m above the floor. The 1-mm mesh writes no colour or depth and casts no shadow.\n"+
                "Making the mesh invisible does not shrink CVR's movement collider. The avatar/root scale and humanoid rig stay normal size.\n"+
                "Use Desktop mode for a fixed shot. Capture the first-person game window in OBS at 16:9; hide HUD/nameplates using client settings.\n"+
                "The included framing images use a 60-degree vertical field of view; match this in the client if available, or adjust by eye.\n"+
                "portal-camera-framing.png faces the room centre; portal-camera-straight.png faces perpendicular to the portal wall.\n"+
                "An avatar does not control the client's FOV, HUD, monitor output, HLS encoder or network camera.\n"+
                "Local Test changes the avatar only for you: other players still see your previously selected avatar.\n"+
                "Upload the editable prefab through CCK later if other people must see the dot avatar. No online upload was performed.\n"+
                "Mute the capture client's mic if the real-venue monitor audio could feed back into it.\n");
            File.WriteAllText(Path.Combine(package,"build-info.json"),
                "{\n  \"name\": \"Camera Dot\",\n  \"version\": \"0.1.1\",\n  \"localEditorIdentifier\": \""+Identity+"\",\n"+
                "  \"eyeHeightMeters\": 1.5,\n  \"dotDiameterMeters\": 0.001,\n  \"visibleGeometry\": false,\n  \"movementCollision\": \"client-controlled-unchanged\",\n  \"rig\": \"humanoid\",\n  \"portalStandOffMeters\": 0.25,\n"+
                "  \"standingPointUnity\": "+JsonUtility.ToJson(stand)+",\n  \"yawDegrees\": "+captureYaw.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"portalNormalYawDegrees\": 90,\n  \"referenceVerticalFov\": 60\n}\n");
            var zip=package+".zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(package,zip,System.IO.Compression.CompressionLevel.Optimal,true);
            using (var input=File.OpenRead(zip)) using (var sha=SHA256.Create())
                File.WriteAllText(zip+".sha256",BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant()+"  "+Path.GetFileName(zip)+"\n");
            return zip;
        }
    }
}
#endif
