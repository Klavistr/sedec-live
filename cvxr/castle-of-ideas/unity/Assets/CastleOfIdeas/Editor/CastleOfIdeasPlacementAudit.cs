using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace CastleOfIdeas.Editor
{
    [InitializeOnLoad]
    public static class CastleOfIdeasPlacementAudit
    {
        private const string PlayAuditKey = "CastleOfIdeas.PlayAudit";
        static CastleOfIdeasPlacementAudit()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("CastleOfIdeas.BatchAudit", false))
                {
                    SessionState.SetBool("CastleOfIdeas.BatchAudit", false);
                    EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetInt("CastleOfIdeas.BatchAuditResult", 1));
                    return;
                }
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayAuditKey, false)) return;
                SessionState.SetBool(PlayAuditKey, false);
                EditorApplication.delayCall += () =>
                {
                    try { Run(); SessionState.SetInt("CastleOfIdeas.BatchAuditResult", 0); }
                    catch (Exception exception) { Debug.LogException(exception); SessionState.SetInt("CastleOfIdeas.BatchAuditResult", 1); }
                    finally { EditorApplication.isPlaying = false; }
                };
            };
        }

        public static void RunBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/CastleOfIdeas/Scenes/Castle-of-Ideas.unity");
            SessionState.SetBool("CastleOfIdeas.BatchAudit", true);
            RunInPlayMode();
        }

        [MenuItem("Castle of Ideas/Audit Chair Motion In PlayMode")]
        public static void RunInPlayMode()
        {
            SessionState.SetBool(PlayAuditKey, true);
            EditorApplication.isPlaying = true;
        }
        [Serializable] private class Item
        {
            public string name;
            public Vector3 center;
            public Vector3 size;
        }
        [Serializable] private class Report
        {
            public string status;
            public string scope = "World meshes, pulled-out chairs, fixed seat anchors, seat callbacks, walls, floors, ceilings, columns, hearth, screens and arrival";
            public string dimensionReference = "Floor-plan axes; JPEG-derived pier rectangles, not survey-grade CAD. Walls have 0.2 m thickness.";
            public string intentionalJoins = "Wall/ceiling/frame joins and tucked seats under desks are intentional; placement collision checks sample pulled-out chairs.";
            public Item[] furniture;
            public string[] issues;
        }

        [MenuItem("Castle of Ideas/Audit World Placement")]
        public static void Run()
        {
            CastleOfIdeasWorldSetup.ValidateWorldScene();
            var root = GameObject.Find("CASTLE_OF_IDEAS_WORLD_ROOT").transform;
            var all = root.GetComponentsInChildren<Transform>(true);
            var errors = new List<string>();
            var furniture = all.Where(t => Regex.IsMatch(t.name,
                @"^(MAIN_TABLE_R\d{2}_C\d{2}|SUB_TABLE_\d{2}|MAIN_CHAIR_\d{3}|SUB_CHAIR_\d{3}_[NS])$")).ToArray();
            var saved = new Dictionary<Transform, Vector3>();
            Matrix4x4 WorldMatrix(Transform node)
            {
                var chain = new Stack<Transform>();
                for (var t = node; t != null; t = t.parent) chain.Push(t);
                var matrix = Matrix4x4.identity;
                foreach (var t in chain)
                    matrix *= Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                return matrix;
            }
            Bounds MeshBounds(Transform node)
            {
                var filters = node.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null).ToArray();
                if (filters.Length == 0) throw new InvalidOperationException("No meshes: " + node.name);
                var bounds = new Bounds();
                var initialized = false;
                foreach (var filter in filters)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer != null && renderer.isPartOfStaticBatch)
                    {
                        if (!initialized) { bounds = renderer.bounds; initialized = true; }
                        else bounds.Encapsulate(renderer.bounds);
                        continue;
                    }
                    var local = filter.sharedMesh.bounds;
                    var matrix = WorldMatrix(filter.transform);
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                        if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
            bool Intersects(Bounds a, Bounds b)
            {
                a.Expand(-0.008f); b.Expand(-0.008f);
                return a.Intersects(b);
            }
            try
            {
                foreach (var chair in furniture.Where(t => t.name.Contains("CHAIR")))
                {
                    var visual = chair.Find("CHAIR_VISUAL");
                    var seat = chair.GetComponentsInChildren<Transform>(true).Single(t => t.name.EndsWith("_SeatPoint"));
                    var anchor = seat.position;
                    if (Mathf.Abs(seat.position.y-chair.position.y-.54f)>.005f
                        || Mathf.Abs(Vector3.Dot(seat.position-chair.position,seat.forward)-.3f)>.005f)
                        errors.Add("Default-animation origin calibration is incorrect: " + chair.name);
                    var animator = chair.GetComponent<Animator>();
                    if (visual == null || animator == null || animator.runtimeAnimatorController == null)
                    {
                        errors.Add("Missing chair motion: " + chair.name); continue;
                    }
                    saved[visual] = visual.localPosition;
                    animator.Rebind();
                    animator.Play("Tucked", 0, 0);
                    animator.Update(0);
                    var tuckedBounds = MeshBounds(chair);
                    var initialLocal = visual.localPosition;
                    animator.Play("Pull", 0, 0);
                    animator.Update(0);
                    animator.Update(0.35f);
                    if (chair.name == "MAIN_CHAIR_001")
                        Debug.Log("Chair audit evaluator: playmode=" + Application.isPlaying + ", initialized=" + animator.isInitialized
                            + ", active=" + animator.isActiveAndEnabled + ", state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash
                            + ", clips=" + animator.GetCurrentAnimatorClipInfo(0).Length);
                    var expectedPush = new Vector3(0, 0, chair.name.EndsWith("_N") ? 0.48f : chair.name.StartsWith("MAIN_") ? -0.45f : -0.48f);
                    if ((tuckedBounds.center - MeshBounds(chair).center - expectedPush).magnitude > 0.01f)
                        errors.Add("Wrong tucked/pulled pose: " + chair.name + " delta=" + (tuckedBounds.center - MeshBounds(chair).center) + " expected=" + expectedPush + " local=" + initialLocal + " -> " + visual.localPosition);
                    if ((seat.position - anchor).magnitude > 0.001f)
                        errors.Add("The seat anchor moved with its visual chair: " + chair.name);
                    var interactable = chair.GetComponent("CVRInteractable");
                    foreach (var field in new[] { "onEnterSeat", "onExitSeat" })
                    {
                        var callback = interactable?.GetType().GetField(field)?.GetValue(interactable) as UnityEvent;
                        if (callback == null || callback.GetPersistentEventCount() != 1
                            || callback.GetPersistentTarget(0) != animator || callback.GetPersistentMethodName(0) != "Play")
                            errors.Add("Missing native seat callback: " + chair.name + "." + field);
                    }
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                        if (GameObjectUtility.AreStaticEditorFlagsSet(renderer.gameObject, StaticEditorFlags.BatchingStatic)
                            || renderer.isPartOfStaticBatch)
                            errors.Add("Animated chair is marked batching-static: " + chair.name);
                }
                var bounds = furniture.ToDictionary(t => t, MeshBounds);
                for (var i = 0; i < furniture.Length; i++)
                    for (var j = i + 1; j < furniture.Length; j++)
                        if (Intersects(bounds[furniture[i]], bounds[furniture[j]]))
                            errors.Add("Furniture meshes overlap: " + furniture[i].name + " / " + furniture[j].name);
                var piers = all.Where(t => t.name.StartsWith("MAIN_PILASTER_")).ToArray();
                var hearth = all.Single(t => t.name == "FIREPLACE_HEARTH");
                foreach (var item in furniture)
                {
                    var room = all.Single(t => t.name == (item.name.StartsWith("MAIN_") ? "MAIN_FLOOR" : "SUB_FLOOR"));
                    var footprint = MeshBounds(room);
                    var box = bounds[item];
                    if (box.min.x < footprint.min.x + 0.09f || box.max.x > footprint.max.x - 0.09f
                        || box.min.z < footprint.min.z + 0.09f || box.max.z > footprint.max.z - 0.09f)
                        errors.Add("Furniture crosses a room wall: " + item.name);
                    foreach (var pier in piers)
                        if (Intersects(box, MeshBounds(pier))) errors.Add("Furniture crosses a plan pier: " + item.name + " / " + pier.name);
                    if (Intersects(box, MeshBounds(hearth))) errors.Add("Furniture crosses the hearth: " + item.name);
                }
                foreach (var side in new[] { "N", "E", "W", "S_LEFT", "S_RIGHT", "S_HEADER", "S_FRONT", "S_FRONT_HEADER" })
                    if (!all.Any(t => t.name == "ALICE_WALLPAPER_" + side)) errors.Add("Missing wallpaper surface: " + side);
                var east = MeshBounds(all.Single(t => t.name == "ALICE_WALLPAPER_E"));
                var ivory = MeshBounds(all.Single(t => t.name == "SUB_WALL_EAST_IVORY_FACE"));
                if (east.center.x <= ivory.max.x) errors.Add("Rear wallpaper is hidden inside the shared wall facing.");
                var report = new Report { status = errors.Count == 0 ? "passed" : "failed",
                    furniture = furniture.Select(t => new Item { name = t.name, center = bounds[t].center, size = bounds[t].size }).ToArray(),
                    issues = errors.ToArray() };
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/placement-audit.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                Debug.Log("Castle of Ideas placement audit passed: " + furniture.Length + " furniture groups, four wallpaper faces, stationary seat anchors and native chair animations.");
            }
            finally
            {
                foreach (var pair in saved)
                {
                    var animator = pair.Key.parent.GetComponent<Animator>();
                    animator.Play("Tucked", 0, 0);
                    animator.Update(0);
                    pair.Key.localPosition = pair.Value;
                }
            }
        }
    }
}
