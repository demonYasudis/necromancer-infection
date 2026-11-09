using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class CloseViewChecks
    {
        private const string Key = "CloseViewChecks.Running";
        private static IEnumerator routine;
        private static double next;
        static CloseViewChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Run Close View Checks %&j")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Necromancer Infection/Preview Compact HUD %&k")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(Key + ".Preview", true);
            Run();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine == null) routine = Checks();
                if (routine.MoveNext()) { next = EditorApplication.timeSinceStartup + (double)routine.Current; return; }
                Finish();
                Debug.Log("PASS: close camera follows both routes; near-miss infection charges once; HUD and occlusion reject clicks.");
            }
            catch (Exception e) { Finish(); Debug.LogException(e); }
        }

        private static void Finish()
        {
            SessionState.SetBool(Key, false);
            routine = null;
            SessionState.SetBool(Key + ".Preview", false);
            next = 0;
            Time.timeScale = 1;
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Checks()
        {
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(640, 360), new Vector2(320, 568), new Vector2(2560, 1080) })
            {
                var hud = new HUDLayout(size.x, size.y);
                Require(hud.Status.yMin > hud.Height / 2 && hud.Status.yMax > hud.Height && hud.Status.xMin < 0, "Resources at bottom");
                Require(hud.EnergyOrb.xMin > hud.Width / 2 && hud.Experience.width == hud.Width, "Opposite vessels and full-width XP");
                Require(hud.Contains(hud.EnergyOrb.center) && hud.Contains(hud.Experience.center), "Resource hit areas");
                Require(!hud.Contains(new Vector2(hud.Width / 2, hud.Status.center.y)), "Gap between vessels stays playable");
                Require(hud.Village == Rect.zero && hud.Abilities == Rect.zero, "No village or ability panels");
                Require(hud.PlayBottom - hud.PlayTop >= 68, "Usable play area remains");
                Require(!hud.Status.Overlaps(hud.Objective) && !hud.Status.Overlaps(hud.Warning), "Panels do not overlap");
                Require(!hud.Contains(new Vector2(hud.Width - 30, 30)), "Removed village area accepts clicks");
            }
            // Let the editor finish entering Play Mode before loading a test scene.
            yield return 1.0;
            foreach (string scene in new[] { DemoLevelSetup.ScenePath, JourneySetup.ScenePath })
            {
                SceneManager.LoadScene(scene);
                yield return .4;
                var manager = Object.FindFirstObjectByType<GameManager>();
                var camera = manager.LevelCamera;
                if (scene == DemoLevelSetup.ScenePath && SessionState.GetBool(Key + ".Preview", false))
                {
                    manager.GetComponent<SoulProgression>().AddExperience(2);
                    manager.Necromancer.Combat.TakeDamage(35);
                    manager.Necromancer.Energy.TrySpend(40);
                    Time.timeScale = 0;
                    using (var view = new HUDChecks.GameViewResolution())
                    {
                        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(320, 568) })
                        {
                            view.Set(size.x, size.y);
                            yield return .3;
                            // Reframe after resizing while the actors remain frozen.
                            manager.GetComponent<JourneyCamera>().SendMessage("FrameLeader");
                            yield return .2;
                            ScreenCapture.CaptureScreenshot($"Previews/glass-hud-{size.x}x{size.y}.png");
                            yield return .3;
                        }
                    }
                    Time.timeScale = 1;
                    yield return .3;
                }
                Require(camera.orthographicSize <= 5f, "Close zoom");
                Vector3 start = manager.Necromancer.transform.position;
                yield return .3;
                Require(manager.Necromancer.transform.position != start, "Leader moves");
                Vector3 screenTravel = camera.WorldToScreenPoint(manager.Necromancer.transform.position)
                    - camera.WorldToScreenPoint(start);
                Require(screenTravel.y > 0 && Mathf.Abs(screenTravel.x) < .01f,
                    "Opening road and leader travel bottom to top");
                var layout = new HUDLayout(camera.pixelWidth, camera.pixelHeight);
                Vector3 torso = camera.WorldToScreenPoint(manager.Necromancer.transform.position + Vector3.up);
                float targetFromTop = Mathf.Lerp(layout.PlayTop, layout.PlayBottom, .82f) * layout.Scale;
                Require(Mathf.Abs(torso.x - camera.pixelWidth * .5f) < 1
                    && Mathf.Abs(camera.pixelHeight - torso.y - targetFromTop) < 1,
                    "Camera keeps leader in lower portion of usable field while following");
                // Isolate one real civilian in the clear centre of the view.
                manager.Necromancer.enabled = false;
                foreach (var person in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) person.gameObject.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<Civilian>(CivilianSetup.PrefabPath);
                var subject = Object.Instantiate(prefab, manager.Necromancer.transform.position + Vector3.right * 2, Quaternion.identity);
                subject.enabled = false;
                Physics.SyncTransforms();
                yield return .6;
                subject.enabled = true;
                var ability = Object.FindFirstObjectByType<InfectionAbility>();
                Vector3 center = subject.GetComponent<Collider>().bounds.center;
                Vector2 screen = camera.WorldToScreenPoint(center);
                Vector2 nearMiss = screen + Vector2.right * Mathf.Clamp(Screen.height / 24f, 22, 40) * .9f;
                Require(Physics.Raycast(camera.ScreenPointToRay(nearMiss), out var hit)
                    && hit.collider == manager.Ground, "Test pointer misses physical hitbox and lands on ground");
                Require(ability.TryGetTarget(nearMiss, out var target, out _, out _, out var reason)
                    && target == subject.transform && reason == null, "Forgiving selection");
                Require(!ability.TryGetTarget(new Vector2(-1, 30), out _, out _, out _, out _), "Outside screen rejected");
                Require(!ability.TryGetTarget(new Vector2(layout.Status.center.x * layout.Scale, Screen.height - layout.Status.center.y * layout.Scale), out _, out _, out _, out _), "HUD rejected");
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = center - camera.transform.forward * 2;
                wall.transform.localScale = Vector3.one * 2;
                Physics.SyncTransforms();
                Require(!ability.TryGetTarget(nearMiss, out _, out _, out _, out _), "Occluded target rejected");
                wall.SetActive(false);
                Physics.SyncTransforms();
                float energy = ability.Energy.Current;
                Require(ability.TryInfectAtScreenPosition(nearMiss), "Near miss casts infection");
                Require(Mathf.Approximately(ability.Energy.Current, energy - ability.InfectionCost), "Correct cost");
                Require(!ability.TryInfectAtScreenPosition(nearMiss), "No duplicate cast");
                yield return .1;
                if (scene == DemoLevelSetup.ScenePath)
                {
                    var notices = manager.GetComponent<GameNotifications>();
                    var put = typeof(GameNotifications).GetMethod("Put", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    put.Invoke(notices, new object[] { "qa-timed", "QA", 1000, true, 0 });
                    yield return .1;
                    Require(notices.CurrentKey == "qa-timed", "Notice initially visible");
                    // Freeze leader and remove enemies so the timed presentation check is isolated.
                    foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
                    foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None)) archer.gameObject.SetActive(false);
                    yield return 4.3;
                    Require(notices.CurrentKey != "qa-timed", "Persistent notice hides after four seconds");
                    put.Invoke(notices, new object[] { "qa-timed", "QA", 1000, true, 0 });
                    yield return .1;
                    Require(notices.CurrentKey != "qa-timed", "Polling same event does not replay expired notice");
                    var topPoint = new Vector2(Screen.width * .5f, Screen.height - 100);
                    Require(!manager.GetComponent<GameHUD>().Layout.Objective.Contains(topPoint), "No objective hit region");
                }
            }
        }

        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); }
    }
}
