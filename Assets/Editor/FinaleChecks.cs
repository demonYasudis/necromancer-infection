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
    public static class FinaleChecks
    {
        private const string Key = "NecromancerInfection.FinaleChecks";
        private const string BatchKey = Key + ".Batch";
        private const string PreviewKey = Key + ".Preview";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static FinaleChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Run Finale Checks")]
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
                    routine = SessionState.GetBool(PreviewKey, false) ? Preview() : Checks();
                    deadline = EditorApplication.timeSinceStartup + 120;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Finale checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, SessionState.GetBool(PreviewKey, false)
                    ? "Finale Game View previews rendered: victory and defeat screens."
                    : "Finale checks passed on both levels: capture progress/reset/pause, light, victory burst, staggered statistics, defeat fade and immediate retry.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            SessionState.SetBool(PreviewKey, false);
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
            foreach (string scene in new[] { DemoLevelSetup.ScenePath, JourneySetup.ScenePath })
            {
                SceneManager.LoadSceneAsync(scene);
                yield return .35f;
                var manager = Object.FindFirstObjectByType<GameManager>();
                var visual = manager.GetComponent<FinalePresentation>();
                Require(visual && visual.Effects && visual.VisibleRunes == 0 && visual.LightIntensity == 0
                    && !visual.BurstVisible, "Both authored levels must start without corruption effects");
                Prepare(manager, 4);
                var assault = manager.Farm.GetComponent<FarmAssault>();
                yield return 1.2f;
                Require(visual.VisibleRunes > 0 && visual.VisibleRunes < 24 && visual.LightIntensity > 0
                    && Mathf.Abs(visual.CaptureProgress - assault.Progress) < .02f,
                    "Runes and light must follow actual partial capture");
                int earlyRunes = visual.VisibleRunes;
                Time.timeScale = 0;
                float light = visual.LightIntensity, progress = visual.CaptureProgress;
                yield return .2f;
                Require(visual.CaptureProgress == progress && visual.LightIntensity == light,
                    "An ordinary pause must freeze capture and pulsing light");
                Time.timeScale = 1;
                manager.Necromancer.transform.position = manager.Farm.transform.position + Vector3.left * 15;
                yield return .1f;
                Require(visual.CaptureProgress == 0 && visual.VisibleRunes == 0 && visual.LightIntensity == 0,
                    "Leaving the radius resets both the real progress and visual corruption");
                manager.Necromancer.transform.position = manager.Farm.ArrivalPoint.position;
                yield return 2.4f;
                Require(visual.VisibleRunes > earlyRunes && visual.LightIntensity > light,
                    "Later capture must spread runes and strengthen the light");
                visual.enabled = false;
                Require(!visual.Effects.gameObject.activeSelf, "Disabling presentation hides all generated effects");
                visual.enabled = true;
                yield return .05f;
                Require(visual.Effects.gameObject.activeSelf && visual.VisibleRunes > 0,
                    "Re-enabling presentation restores current real progress");
                if (scene == DemoLevelSetup.ScenePath)
                    PeasantRigSetup.Capture(manager.LevelCamera, "Previews/finale-corruption.png");
                while (!manager.IsFinished) yield return .02f;
                yield return .02f;
                Require(manager.IsVictorious && Time.timeScale == 0 && visual.BurstVisible
                    && visual.StatisticAlpha(3) < 1, "Victory starts a short burst before all statistics are visible");
                Require(manager.TryRestartLevel() && !manager.TryRestartLevel(),
                    "Retry must work during the very beginning of victory without accepting a duplicate request");
                yield return .35f;
                CheckFresh();

                manager = Object.FindFirstObjectByType<GameManager>();
                Prepare(manager, .4f);
                visual = manager.GetComponent<FinalePresentation>();
                while (!manager.IsFinished) yield return .02f;
                yield return .22f;
                Require(visual.StatisticAlpha(0) > visual.StatisticAlpha(3) && visual.BurstVisible,
                    "Statistics must appear in sequence while the world is frozen");
                float frozen = Time.time;
                Vector3 position = manager.Necromancer.transform.position;
                float burst = visual.BurstProgress;
                yield return .25f;
                Require(visual.BurstProgress > burst && Time.time == frozen && manager.Necromancer.transform.position == position,
                    "Result presentation must advance independently of frozen gameplay");
                yield return .65f;
                Require(visual.StatisticAlpha(3) == 1 && !visual.BurstVisible && visual.VisibleRunes == 24,
                    "Victory settles with all four rows, completed corruption and no lingering burst");
                if (!Application.isBatchMode && scene == DemoLevelSetup.ScenePath)
                    ScreenCapture.CaptureScreenshot("Previews/finale-victory.png");
                yield return .1f;
                Require(manager.TryRestartLevel(), "Retry also works after the presentation finishes");
                yield return .35f;
                CheckFresh();

                manager = Object.FindFirstObjectByType<GameManager>();
                Prepare(manager, 4);
                yield return .5f;
                visual = manager.GetComponent<FinalePresentation>();
                manager.Necromancer.Combat.TakeDamage(1000);
                yield return .04f;
                float fade = visual.DefeatFade;
                frozen = Time.time;
                yield return .5f;
                Require(manager.IsDefeated && visual.DefeatFade > fade && Time.time == frozen && !visual.BurstVisible,
                    "Defeat must darken the scene in unscaled time without a victory burst");
                yield return .8f;
                Require(visual.DefeatFade > .91f && visual.VisibleRunes == 0 && visual.LightIntensity == 0,
                    "Defeat extinguishes corruption and settles the scene fade");
                if (!Application.isBatchMode && scene == DemoLevelSetup.ScenePath)
                    ScreenCapture.CaptureScreenshot("Previews/finale-defeat.png");
                yield return .1f;
                Require(manager.TryRestartLevel(), "Defeat retry works after fading");
                yield return .35f;
                CheckFresh();
                manager = Object.FindFirstObjectByType<GameManager>();
                manager.Necromancer.Combat.TakeDamage(1000);
                Require(manager.TryRestartLevel(), "Defeat retry works immediately before fading starts");
                yield return .35f;
                CheckFresh();
            }
        }

        private static void Prepare(GameManager manager, float duration)
        {
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            var journey = manager.GetComponent<JourneyRoute>();
            if (journey) journey.enabled = false;
            var assault = manager.Farm.GetComponent<FarmAssault>();
            var settings = new SerializedObject(assault);
            settings.FindProperty("corruptionDuration").floatValue = duration;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var leader = manager.Necromancer;
            leader.BeginRoute(manager.Farm.ArrivalPoint.position, new[] { manager.Farm.ArrivalPoint });
            Require(manager.Farm.TryBeginFinalPhase(leader), "Start capture through the actual farm objective");
            // Keep the regular wave script, but isolate visuals from combat by moving spawn points away.
            var points = settings.FindProperty("reinforcementPoints");
            for (int i = 0; i < points.arraySize; i++)
                ((Transform)points.GetArrayElementAtIndex(i).objectReferenceValue).position += Vector3.right * 100;
            var camera = manager.LevelCamera;
            camera.orthographicSize = 7;
            camera.transform.position = manager.Farm.transform.position + new Vector3(0, 12, -12);
            camera.transform.LookAt(manager.Farm.transform.position + Vector3.forward * 1.5f);
        }

        private static IEnumerator Preview()
        {
            Directory.CreateDirectory("Previews");
            yield return .6f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager, 1);
            while (!manager.IsFinished) yield return .02f;
            yield return 1.1f;
            ScreenCapture.CaptureScreenshot("Previews/finale-victory.png");
            yield return .4f;
            Require(manager.TryRestartLevel(), "Preview retry");
            yield return .5f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Prepare(manager, 10);
            yield return .5f;
            manager.Necromancer.Combat.TakeDamage(1000);
            yield return 1.3f;
            ScreenCapture.CaptureScreenshot("Previews/finale-defeat.png");
            yield return .5f;
            Require(File.Exists("Previews/finale-victory.png") && File.Exists("Previews/finale-defeat.png"),
                "Game View must render both result-screen previews");
        }

        private static void CheckFresh()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            var visual = manager.GetComponent<FinalePresentation>();
            Require(!manager.IsFinished && Time.timeScale == 1 && visual && visual.VisibleRunes == 0
                && visual.LightIntensity == 0 && !visual.BurstVisible && visual.ResultAge == 0,
                "Retry restores a clean presentation and normal gameplay time");
            Require(Object.FindObjectsByType<FinalePresentation>(FindObjectsSortMode.None).Length == 1,
                "A reload must not retain a previous attempt's presentation");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
