using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class TutorialLevelChecks
    {
        private const string Key = "TutorialChecks.Running";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static TutorialLevelChecks() => EditorApplication.update += Tick;

        public static void CreateAndCheckBatch() { TutorialLevelSetup.Create(); RunBatch(); }
        public static void RunBatch()
        {
            SessionState.SetBool(Key + ".Batch", true);
            Run();
        }

        public static void PreviewBatch()
        {
            System.IO.Directory.CreateDirectory("Previews");
            SessionState.SetBool(Key + ".Preview", true);
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.Show(); view.Focus();
            RunBatch();
        }

        [MenuItem("Necromancer Infection/Run Tutorial Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes first.");
            EditorSceneManager.OpenScene(TutorialLevelSetup.ScenePath(1));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (routine == null)
                {
                    routine = Checks(); deadline = EditorApplication.timeSinceStartup + 90;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, "Tutorial checks passed: four compositions, end triggers, boss victory gates, next/retry, defeat, 900-second survival default and paused timer.");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Watch(string message, string stack, LogType type)
        {
            // Unity 6000.3 can fail to initialize its editor search index in a fresh batch project.
            // This editor-only stack is unrelated to gameplay; all game exceptions still fail the run.
            if (Application.isBatchMode && stack.Contains("UnityEditor.Search.SearchDatabase")
                && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message + "\n" + stack;
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Watch;
            routine = null; error = null; next = 0;
            Time.timeScale = 1;
            if (success) Debug.Log("PASS: " + message); else Debug.LogError(message);
            if (SessionState.GetBool(Key + ".Batch", false)) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        private static void Prepare(GameManager game)
        {
            game.Necromancer.enabled = false;
            game.GetComponent<SoulProgression>().enabled = false;
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);
        }

        private static IEnumerator Checks()
        {
            for (int level = 1; level <= 4; level++)
            {
                yield return .35;
                var game = Object.FindFirstObjectByType<GameManager>();
                Require(game.Objective && game.Objective.TutorialLevel == level, "Correct tutorial after scene transition");
                Require(!game.IsFinished && Time.timeScale == 1, "Fresh attempt");
                Require(Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length > 0, "Peasants present");
                Require(Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 0
                    && Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None).Length == 0, "No early defenders");
                Require(Object.FindObjectsByType<AlarmBell>(FindObjectsSortMode.None).Length == (level == 4 ? 1 : 0), "Bell only in level four");
                Require(!game.Farm.GetComponent<FarmAssault>().enabled, "No legacy assault waves");
                if (SessionState.GetBool(Key + ".Preview", false) && level == 1)
                {
                    using (var view = new HUDChecks.GameViewResolution())
                    {
                        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(320, 568) })
                        {
                            view.Set(size.x, size.y);
                            yield return .4;
                            ScreenCapture.CaptureScreenshot($"Previews/tutorial-start-{size.x}x{size.y}.png");
                            yield return .3;
                        }
                    }
                }
                Prepare(game);
                game.Necromancer.BeginRoute(game.Farm.ArrivalPoint.position, new[] { game.Farm.ArrivalPoint });
                game.Necromancer.enabled = true;
                yield return .2;
                var guards = Object.FindObjectsByType<Guard>(FindObjectsSortMode.None);
                var archers = Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None);
                Require(guards.Length == (level == 1 ? 0 : level == 4 ? 2 : 1), "Exact knight count");
                Require(archers.Length == (level < 3 ? 0 : level == 3 ? 1 : 2), "Exact archer count");
                if (level > 1)
                {
                    Require(!game.IsFinished && game.Objective.EncounterStarted, "Arrival cannot bypass bosses");
                    foreach (var guard in guards) guard.enabled = false;
                    if (archers.Length > 0)
                    {
                        yield return 2.1;
                        foreach (var archer in archers) Require(archer.ShotsFired > 0, "Every archer must have an unobstructed shot from its spawn");
                    }
                    foreach (var archer in archers) archer.enabled = false;
                    if (SessionState.GetBool(Key + ".Preview", false) && level == 4)
                    {
                        using (var view = new HUDChecks.GameViewResolution())
                        {
                            view.Set(1280, 720);
                            yield return .4;
                            ScreenCapture.CaptureScreenshot("Previews/tutorial-bell.png");
                            yield return .3;
                        }
                    }
                    if (level == 4)
                    {
                        var bell = Object.FindFirstObjectByType<AlarmBell>();
                        Require(bell.IsRaised && bell.Patrol.childCount == 4 && !bell.TryRaiseTutorial(), "Bell rings once");
                    }
                    foreach (var archer in archers) archer.Combat.TakeDamage(10000);
                    for (int i = 1; i < guards.Length; i++) guards[i].Combat.TakeDamage(10000);
                    yield return .1;
                    Require(!game.IsFinished, "Last knight blocks victory");
                    if (level == 3)
                    {
                        guards[0].enabled = true;
                        var zombie = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zombie.prefab").GetComponent<Zombie>();
                        Require(guards[0].GetComponent<DefenderInfection>().TryInfect(zombie, game.Necromancer, .3f), "Boss can be converted");
                        yield return .1;
                        Require(!game.IsFinished, "Victory waits for conversion reward");
                        yield return .4;
                        Require(game.InfectedCount == 1, "Conversion reward precedes victory");
                    }
                    else { guards[0].Combat.TakeDamage(10000); yield return .15; }
                }
                Require(game.IsVictorious && Time.timeScale == 0, $"Objective completes and freezes time: level={level}, final={game.FinalPhase}, finished={game.IsFinished}, alive={game.Necromancer.Combat.IsAlive}, scale={Time.timeScale}, position={game.Necromancer.transform.position}, farm={game.Farm.transform.position}");
                Require(Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 0, "No knight corpse targets");
                Require(game.SurvivingUndead == (level == 1 ? 1 : level == 4 ? 3 : 2), "Killed knights and converted bosses join the victory count before time freezes");
                Require(game.CanAdvance == (level < 4), "Next level availability");
                if (level < 4) Require(game.TryAdvanceLevel() && !game.TryAdvanceLevel(), "Single next-level request");
                else Require(game.TryRestartLevel() && !game.TryRestartLevel(), "Single retry request");
            }
            yield return .35;
            var retry = Object.FindFirstObjectByType<GameManager>();
            Require(retry.Objective.TutorialLevel == 4 && !retry.Objective.EncounterStarted && !retry.IsFinished, "Retry resets encounter");
            retry.Necromancer.Combat.TakeDamage(10000);
            Require(retry.IsDefeated && !retry.TryAdvanceLevel(), "Defeat cannot advance");
            Require(retry.TryRestartLevel(), "Retry defeat");
            yield return .35;
            retry = Object.FindFirstObjectByType<GameManager>();
            Prepare(retry);
            var settings = new SerializedObject(retry.Objective);
            Require(settings.FindProperty("survivalDuration").floatValue == 900, "15-minute default");
            settings.FindProperty("mode").enumValueIndex = 1;
            settings.FindProperty("survivalDuration").floatValue = (float)retry.ElapsedSeconds + .5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            float remaining = retry.Objective.RemainingSeconds;
            Time.timeScale = 0;
            yield return .7;
            Require(!retry.IsFinished && Mathf.Abs(retry.Objective.RemainingSeconds - remaining) < .02f, "Pause cannot consume survival time");
            Time.timeScale = 1;
            yield return .7;
            Require(retry.IsVictorious, "Survival deadline wins without farm capture");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
