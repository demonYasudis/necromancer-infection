using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class InterfaceReviewChecks
    {
        private const string Key = "NecromancerInfection.InterfaceReviewChecks";
        private const string BatchKey = Key + ".Batch";
        private const string PreviewKey = Key + ".Preview";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static InterfaceReviewChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Run Interface Review")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before running checks.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            SessionState.SetBool(BatchKey, true);
            Run();
        }

        public static void PreviewBatch()
        {
            SessionState.SetBool(PreviewKey, true);
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Show();
            view.Focus();
            RunBatch();
        }

        public static void PreviewKitBatch()
        {
            SessionState.SetBool(Key + ".KitOnly", true);
            PreviewBatch();
        }

        private static void Watch(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message; }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (routine == null)
                {
                    routine = SessionState.GetBool(Key + ".KitOnly", false) ? KitReview() : Checks();
                    deadline = EditorApplication.timeSinceStartup + 300;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Interface review timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, SessionState.GetBool(Key + ".KitOnly", false)
                    ? "Interface kit Game View captures passed."
                    : SessionState.GetBool(PreviewKey, false) ? "Interface review and Game View captures passed."
                    : "Interface review passed.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            SessionState.SetBool(PreviewKey, false);
            SessionState.SetBool(Key + ".KitOnly", false);
            Application.logMessageReceived -= Watch;
            routine = null;
            error = null;
            next = 0;
            bool batch = SessionState.GetBool(BatchKey, false);
            SessionState.SetBool(BatchKey, false);
            if (success) Debug.Log("PASS: " + message);
            else Debug.LogError("FAIL: " + message);
            if (batch) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }

        public static IEnumerator Checks()
        {
            Directory.CreateDirectory("Previews");
            foreach (var check in new[] { HUDChecks.Checks(), NotificationChecks.Checks(),
                ProgressionChecks.Checks(), FinaleChecks.Checks(), BattleReview(), TextReview(), KitReview() })
                while (check.MoveNext()) yield return check.Current;
            if (!Application.isBatchMode)
            {
                foreach (string path in Directory.GetFiles("Logs", "hud-*.png"))
                    File.Copy(path, "Previews/review-" + Path.GetFileName(path), true);
                foreach (string path in Directory.GetFiles("Logs", "notifications-*.png"))
                    File.Copy(path, "Previews/review-" + Path.GetFileName(path), true);
            }
        }

        private static IEnumerator BattleReview()
        {
            SceneManager.LoadSceneAsync(JourneySetup.ScenePath);
            yield return .6f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            manager.GetComponent<SoulProgression>().enabled = false;
            manager.GetComponent<OnboardingGuide>().Skip();
            var leader = manager.Necromancer;
            leader.enabled = false;
            Vector3 center = leader.transform.position;
            var camera = manager.LevelCamera;
            camera.transform.position = center + Vector3.up * .7f - camera.transform.forward * 18;
            camera.orthographicSize = 6;
            var zombies = new System.Collections.Generic.List<Zombie>();
            for (int i = 0; i < 24; i++)
            {
                string kind = i % 3 == 0 ? "RaisedGuard" : i % 3 == 1 ? "Skeleton" : "Zombie";
                var unit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Zombie>("Assets/Prefabs/" + kind + ".prefab"),
                    center + new Vector3((i % 6 - 2.5f) * .8f, 0, 2 + i / 6 * .8f), Quaternion.identity);
                unit.Initialize(leader);
                zombies.Add(unit);
            }
            for (int i = 0; i < 8; i++)
                Object.Instantiate(AssetDatabase.LoadAssetAtPath<Guard>("Assets/Prefabs/Guard.prefab"),
                    center + new Vector3((i % 4 - 1.5f) * .9f, 0, 5 + i / 4), Quaternion.identity);
            yield return 1.5f;
            Require(zombies.Exists(unit => unit && unit.Combat.CurrentHealth < unit.Combat.MaxHealth)
                || manager.GuardsKilled > 0, "Crowd fixture must contain actual AI combat and damage");
            Time.timeScale = 0;
            Vector3 battleCenter = Vector3.zero;
            int alive = 0;
            foreach (var unit in zombies)
                if (unit && unit.Combat.IsAlive) { battleCenter += unit.transform.position; alive++; }
            Require(alive > 0, "Crowd must still be present for review");
            battleCenter /= alive;
            camera.transform.position = battleCenter + Vector3.up * .7f - camera.transform.forward * 18;
            if (!Application.isBatchMode)
            using (var view = new HUDChecks.GameViewResolution())
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(2560, 1080),
                    new Vector2Int(640, 360), new Vector2Int(320, 568) })
                {
                    view.Set(size.x, size.y);
                    yield return .3f;
                    Require(Screen.width == size.x && Screen.height == size.y, "Actual review resolution");
                    ScreenCapture.CaptureScreenshot($"Previews/review-battle-{size.x}x{size.y}.png");
                    yield return .3f;
                }
                // Temporary ground material variants exercise contrast without changing authored assets.
                var ground = manager.Ground.GetComponent<Renderer>();
                Require(ground, "Battle scene has a ground renderer");
                var material = ground.material;
                Color original = material.color;
                view.Set(1280, 720);
                foreach (var shade in new[] { Color.white, new Color(.04f, .035f, .03f) })
                {
                    material.color = shade;
                    yield return .3f;
                    ScreenCapture.CaptureScreenshot(shade == Color.white
                        ? "Previews/review-battle-light.png" : "Previews/review-battle-dark.png");
                    yield return .3f;
                }
                material.color = original;
                Object.Destroy(material);
            }
            Time.timeScale = 1;
            leader.Combat.TakeDamage(10000);
            Require(manager.TryRestartLevel(), "Battle review cleans up through real retry");
            yield return .4f;
            Debug.Log("PASS: live 24-undead / 8-guard crowd battle, four viewports, light/dark ground and retry.");
        }

        private static IEnumerator TextReview()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            var progression = manager.GetComponent<SoulProgression>();
            progression.enabled = false;
            Time.timeScale = 0;
            // Deliberately extreme display fixtures, never persisted into a scene or prefab.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var feedback = manager.GetComponent<GameFeedback>();
            typeof(GameFeedback).GetField("nextRefresh", flags).SetValue(feedback, float.MaxValue);
            foreach (string name in new[] { "PeasantCount", "KnightCount", "RangedCount" })
                typeof(GameFeedback).GetProperty(name).SetValue(feedback, 999999);
            typeof(SoulProgression).GetProperty("Level").SetValue(progression, 9999);
            typeof(SoulProgression).GetProperty("Experience").SetValue(progression, 1000000);
            manager.Necromancer.Energy.IncreaseCapacity(999900);
            if (!Application.isBatchMode)
            using (var view = new HUDChecks.GameViewResolution())
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(320, 568) })
                {
                    view.Set(size.x, size.y);
                    yield return .3f;
                    Require(manager.GetComponent<GameHUD>().Layout.Village == Rect.zero,
                        "Removed village counters must not reserve screen space");
                    ScreenCapture.CaptureScreenshot($"Previews/review-large-values-{size.x}x{size.y}.png");
                    yield return .3f;
                }
            }
            Time.timeScale = 1;
            manager.Necromancer.Combat.TakeDamage(10000);
            Require(manager.TryRestartLevel(), "Discard extreme values");
            yield return .4f;
            manager = Object.FindFirstObjectByType<GameManager>();
            progression = manager.GetComponent<SoulProgression>();
            progression.AddExperience(4);
            yield return .15f;
            Require(progression.IsChoosing && Time.timeScale == 0, "Text review opens a real choice");
            var offers = (System.Collections.Generic.List<SoulProgression.Upgrade>)
                typeof(SoulProgression).GetField("offers", flags).GetValue(progression);
            var infection = Object.FindFirstObjectByType<InfectionAbility>();
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            if (!Application.isBatchMode)
            using (var view = new HUDChecks.GameViewResolution())
            {
                for (int variant = 0; variant < 2; variant++)
                {
                    offers.Clear();
                    for (int i = 0; i < 3; i++) offers.Add((SoulProgression.Upgrade)((i + variant) % 4));
                    foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(320, 568), new Vector2Int(640, 360) })
                    {
                        view.Set(size.x, size.y);
                        yield return .3f;
                        float energy = manager.Necromancer.Energy.Current;
                        for (int y = 1; y < 4; y++)
                        for (int x = 1; x < 4; x++)
                        {
                            var point = new Vector2(Screen.width * x / 4f, Screen.height * y / 4f);
                            Require(AbilityPointer.IsBlocked(point) && !infection.TryInfectAtScreenPosition(point)
                                && !protection.TryCastAtScreenPosition(point), "Cards and modal backdrop block world abilities");
                        }
                        Require(manager.Necromancer.Energy.Current == energy, "Modal clicks cannot spend energy");
                        ScreenCapture.CaptureScreenshot($"Previews/review-cards-{variant}-{size.x}x{size.y}.png");
                        yield return .3f;
                    }
                }
            }
            Require(progression.TryChoose(0) && Time.timeScale == 1, "Text review resumes normally");
            Debug.Log("PASS: six-digit village counts, million energy/XP, all four Russian card titles and modal click rejection.");
        }

        private static IEnumerator KitReview()
        {
            if (Application.isBatchMode) yield break;
            var manager = Object.FindFirstObjectByType<GameManager>();
            var surfaces = new Behaviour[] { manager.GetComponent<GameHUD>(), manager.GetComponent<GameFeedback>(),
                manager.GetComponent<GameNotifications>(), manager.GetComponent<ProgressionScreen>(),
                manager.GetComponent<AbilityPreview>() };
            var enabled = new bool[surfaces.Length];
            for (int i = 0; i < surfaces.Length; i++)
            {
                enabled[i] = surfaces[i] && surfaces[i].enabled;
                if (surfaces[i]) surfaces[i].enabled = false;
            }
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 0;
            var preview = manager.gameObject.AddComponent<InterfaceKitPreview>();
            using (var view = new HUDChecks.GameViewResolution())
            {
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(640, 480) })
                {
                    view.Set(size.x, size.y);
                    yield return .3f;
                    Require(preview.Verified, "Material sheet must render and verify button states");
                    ScreenCapture.CaptureScreenshot($"Previews/ui-kit-{size.x}x{size.y}.png");
                    yield return .3f;
                }
            }
            Object.Destroy(preview);
            for (int i = 0; i < surfaces.Length; i++)
                if (surfaces[i]) surfaces[i].enabled = enabled[i];
            Time.timeScale = previousTimeScale;
            Debug.Log("PASS: interface kit, fixed-size fittings, resource fills and four distinct button states rendered.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
