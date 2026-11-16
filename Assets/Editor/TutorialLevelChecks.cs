using System;
using System.Collections;
using System.Collections.Generic;
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
        private const string ArenaPath = MainMenu.SurvivalLevelPath;
        static TutorialLevelChecks() => EditorApplication.update += Tick;

        public static void CreateAndCheckBatch() { TutorialLevelSetup.Create(); RunBatch(); }
        public static void RunBatch()
        {
            SessionState.SetBool(Key + ".Batch", true);
            Run();
        }

        public static void RunFullSurvivalBatch()
        {
            SessionState.SetBool(Key + ".Batch", true);
            RunFullSurvival();
        }

        [MenuItem("Necromancer Infection/Run Full Survival Check")]
        public static void RunFullSurvival()
        {
            Begin(ArenaPath, true);
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
            Begin(TutorialLevelSetup.ScenePath(1), false);
        }

        private static void Begin(string scenePath, bool fullSurvival)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes first.");
            EditorSceneManager.OpenScene(scenePath);
            SessionState.SetBool(Key + ".FullSurvival", fullSurvival);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                bool fullSurvival = SessionState.GetBool(Key + ".FullSurvival", false);
                if (routine == null)
                {
                    routine = fullSurvival ? FullSurvivalCheck() : Checks();
                    deadline = EditorApplication.timeSinceStartup + (fullSurvival ? 600 : 90);
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, fullSurvival ? "Full Survival01 check passed."
                    : "Tutorial and survival checks passed: encounters, level selection, arena circuit, timer, pause, retry, nine safe waves, resource replenishment, enemy limit and remains cleanup.");
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
            if (SessionState.GetBool(Key + ".FullSurvival", false)) Time.captureDeltaTime = 0;
            Time.timeScale = 1;
            if (success) Debug.Log("PASS: " + message); else Debug.LogError(message);
            if (SessionState.GetBool(Key + ".Batch", false)) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }

        private static IEnumerator FullSurvivalCheck()
        {
            yield return .1;
            var arena = Object.FindFirstObjectByType<GameManager>();
            var waves = arena.GetComponent<SurvivalWaves>();
            var progression = arena.GetComponent<SoulProgression>();
            // Advance the normal simulation without modifying the attempt clock or wave schedule.
            Time.captureDeltaTime = .05f;
            int observedWaves = 0;
            bool paused = false;
            bool clearedFinalWave = false;
            while (!arena.IsFinished)
            {
                if (progression.IsChoosing)
                {
                    progression.TryChoose(0);
                    yield return 0;
                    continue;
                }
                if (waves.WavesSpawned != observedWaves)
                {
                    observedWaves = waves.WavesSpawned;
                    Debug.Log($"Survival01 wave {observedWaves}: {arena.ElapsedSeconds:F2}s, {waves.LivingEnemyCount} enemies");
                    if (observedWaves < 9) ClearWave(waves);
                    else Require(arena.ElapsedSeconds >= 850 && arena.ElapsedSeconds < 855
                        && waves.LivingEnemyCount == 10, "Final wave arrives with all ten defenders");
                }
                if (!clearedFinalWave && observedWaves == 9 && arena.ElapsedSeconds >= 880)
                {
                    clearedFinalWave = true;
                    Debug.Log($"Survival01 at 880s: health {arena.Necromancer.Combat.CurrentHealth:F0}, "
                        + $"{waves.LivingEnemyCount} enemies, upgrade level {progression.Level}");
                    ClearWave(waves);
                }
                if (!paused && arena.ElapsedSeconds >= 450)
                {
                    paused = true;
                    Require(arena.TryPauseGame(), "Pause midway through the full attempt");
                    double elapsed = arena.ElapsedSeconds;
                    yield return .5;
                    Require(arena.ElapsedSeconds == elapsed, "Mid-run pause freezes the clock");
                    Require(arena.TryResumeGame(), "Resume the full attempt");
                }
                yield return 0;
            }
            Require(paused && clearedFinalWave && arena.IsVictorious && arena.ElapsedSeconds >= 900
                && arena.ElapsedSeconds < 900.1, $"Expected victory at 900s after nine waves, got {arena.ElapsedSeconds:F2}s and {observedWaves} waves");
            Require(arena.TryRestartLevel(), "Retry after the full attempt");
            while (arena) yield return 0;
            yield return .1;
            arena = Object.FindFirstObjectByType<GameManager>();
            waves = arena.GetComponent<SurvivalWaves>();
            Require(!arena.IsFinished && arena.ElapsedSeconds < 30 && waves.WavesSpawned == 0
                && arena.InfectedCount == 0 && arena.GuardsKilled == 0, "Retry resets the full attempt");

            var clock = typeof(GameManager).GetField("attemptStartedAt",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            clock.SetValue(arena, Time.timeAsDouble - 899.5);
            arena.Necromancer.Combat.TakeDamage(10000);
            yield return .5;
            Require(arena.IsDefeated && !arena.IsVictorious && Math.Abs(arena.ElapsedSeconds - 899.5) < .001,
                "Death before the deadline freezes the defeat result");
            Require(arena.TryRestartLevel(), "Retry after late defeat");
            while (arena) yield return 0;
            Require(!Object.FindFirstObjectByType<GameManager>().IsFinished, "Retry clears late defeat");
        }

        private static void ClearWave(SurvivalWaves waves)
        {
            foreach (var enemy in waves.Reinforcements.GetComponentsInChildren<UnitCombat>())
                if (enemy.IsAlive) enemy.TakeDamage(10000);
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
                Require(game.CanAdvance && game.TryAdvanceLevel() && !game.TryAdvanceLevel(), "Single next-level request");
            }
            yield return .6;
            var arena = Object.FindFirstObjectByType<GameManager>();
            Require(arena.gameObject.scene.path == ArenaPath && arena.Objective.Mode == LevelObjective.ObjectiveMode.Survival
                && Time.timeScale == 1 && !arena.Farm && !arena.FinalPhase, "Tutorial victory starts a fresh Survival01 attempt");
            var settings = new SerializedObject(arena.Objective);
            Require(settings.FindProperty("survivalDuration").floatValue == 900, "15-minute default");

            var leader = arena.Necromancer;
            arena.GetComponent<SurvivalPopulation>().enabled = false;
            var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zombie.prefab").GetComponent<Zombie>();
            Civilian first = null;
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                if (!first || (civilian.transform.position - leader.transform.position).sqrMagnitude
                    < (first.transform.position - leader.transform.position).sqrMagnitude) first = civilian;
            Require(first && first.TryInfect(zombiePrefab, leader, .2f), "Arena has a starting target to infect");
            yield return .35;
            var undead = Object.FindFirstObjectByType<Zombie>();
            Require(undead && undead.Leader == leader, "Converted undead joins the arena horde");
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);

            Vector3 start = arena.StartPoint.position;
            bool passedEast = false, passedNorth = false, passedWest = false, hordePassedNorth = false;
            float patrolDeadline = (float)arena.ElapsedSeconds + 60;
            Time.timeScale = 5;
            while (!passedWest || Vector3.Distance(start, leader.transform.position) > 1.5f)
            {
                Require(arena.ElapsedSeconds < patrolDeadline, "The circuit returns to the start");
                Vector3 position = leader.transform.position;
                passedEast |= position.x > 12;
                passedNorth |= position.z > 10;
                passedWest |= position.x < -9;
                hordePassedNorth |= undead.transform.position.z > 10.5f;
                Vector3 framed = arena.LevelCamera.WorldToViewportPoint(position + Vector3.up);
                Require(framed.z > 0 && Mathf.Abs(framed.x - .5f) < .03f && framed.y > .1f && framed.y < .65f,
                    "The camera keeps the necromancer in the play area through turns");
                yield return .05;
            }
            Require(passedEast && passedNorth && !leader.HasArrived && !arena.IsFinished, "The patrol covers the arena and keeps moving");
            Require(hordePassedNorth && Vector3.Distance(undead.transform.position, leader.transform.position) < undead.MaxHordeDistance,
                "The horde follows the circuit and moves beyond the demo bounds");
            double nextLapDeadline = arena.ElapsedSeconds + 4;
            while (leader.transform.position.x <= start.x + 2)
            {
                Require(arena.ElapsedSeconds < nextLapDeadline, "The next lap continues from the start");
                yield return .05;
            }

            Time.timeScale = 1;
            Require(arena.TryPauseGame(), "Arena can pause");
            Vector3 pausedAt = leader.transform.position;
            float remaining = arena.Objective.RemainingSeconds;
            yield return .7;
            Require(!arena.IsFinished && leader.transform.position == pausedAt
                && Mathf.Abs(arena.Objective.RemainingSeconds - remaining) < .02f, "Pause freezes the patrol and survival clock");
            settings.FindProperty("survivalDuration").floatValue = (float)arena.ElapsedSeconds + .5f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            Require(arena.TryResumeGame(), "Arena can resume");
            yield return .7;
            Require(arena.IsVictorious && arena.SurvivingUndead == 2, "Survival deadline wins and counts the horde");
            Require(arena.TryRestartLevel(), "Survival can restart");
            yield return .6;
            arena = Object.FindFirstObjectByType<GameManager>();
            Require(!arena.IsFinished && arena.Objective.RemainingSeconds > 895 && arena.InfectedCount == 0
                && Time.timeScale == 1, "Retry resets the arena timer and horde");
            var waveChecks = SurvivalWaveChecks(arena);
            while (waveChecks.MoveNext()) yield return waveChecks.Current;
            var resourceChecks = SurvivalResourceChecks(Object.FindFirstObjectByType<GameManager>(), zombiePrefab);
            while (resourceChecks.MoveNext()) yield return resourceChecks.Current;
            var remainsChecks = SurvivalRemainsChecks(Object.FindFirstObjectByType<GameManager>());
            while (remainsChecks.MoveNext()) yield return remainsChecks.Current;
            var selectionChecks = LevelSelectionChecks();
            while (selectionChecks.MoveNext()) yield return selectionChecks.Current;
        }

        private static IEnumerator LevelSelectionChecks()
        {
            var game = Object.FindFirstObjectByType<GameManager>();
            Require(game.TryPauseGame() && game.TryReturnToMenu(), "Survival can return to the main menu");
            for (int index = 0; index < 5; index++)
            {
                yield return .6;
                var menu = Object.FindFirstObjectByType<MainMenu>();
                Require(menu && Time.timeScale == 1 && !menu.TryStartLevel(-1) && !menu.TryStartLevel(5),
                    "The main menu resets time and rejects invalid level selections");
                Require(menu.TryStartLevel(index) && !menu.TryStartGame(), "The menu loads one level per selection");
                yield return .6;
                game = Object.FindFirstObjectByType<GameManager>();
                string path = index == 4 ? ArenaPath : TutorialLevelSetup.ScenePath(index + 1);
                Require(game && game.gameObject.scene.path == path && !game.IsFinished && !game.CanAdvance,
                    "The selected level starts a fresh attempt: " + path);
                if (index == 3)
                {
                    game.Necromancer.Combat.TakeDamage(10000);
                    Require(game.IsDefeated && !game.TryAdvanceLevel(), "Tutorial defeat cannot unlock the next scene");
                    Require(game.TryRestartLevel() && !game.TryRestartLevel(), "Single retry request");
                    yield return .6;
                    game = Object.FindFirstObjectByType<GameManager>();
                    Require(game.Objective.TutorialLevel == 4 && !game.Objective.EncounterStarted && !game.IsFinished,
                        "Retry clears defeat and resets the encounter");
                }
                Require(game.TryPauseGame() && game.TryReturnToMenu(), "The selected level can return to the main menu");
            }
            yield return .6;
            Require(Object.FindFirstObjectByType<MainMenu>(), "Survival from level selection returns to the main menu");
        }

        private static IEnumerator SurvivalWaveChecks(GameManager arena)
        {
            var waves = arena.GetComponent<SurvivalWaves>();
            var notices = arena.GetComponent<GameNotifications>();
            Require(waves && waves.WavesSpawned == 0 && waves.Reinforcements.childCount == 0 && !waves.HasWarning,
                "A new attempt starts with a clean wave schedule");
            var settings = new SerializedObject(waves);
            var schedule = settings.FindProperty("waves");
            var sectors = settings.FindProperty("sectors");
            var offsets = settings.FindProperty("spawnOffsets");
            Require(schedule.arraySize == 9 && sectors.arraySize == 4, "Nine waves and four authored sectors");
            int enemyLimit = settings.FindProperty("enemyLimit").intValue;
            Require(enemyLimit == 20, "Arena has an authored live enemy limit");

            arena.Necromancer.enabled = false;
            arena.GetComponent<SoulProgression>().enabled = false;
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) civilian.gameObject.SetActive(false);
            Physics.SyncTransforms();
            for (int i = 0; i < sectors.arraySize; i++)
            {
                var anchor = (Transform)sectors.GetArrayElementAtIndex(i).FindPropertyRelative("anchor").objectReferenceValue;
                for (int slot = 0; slot < offsets.arraySize; slot++)
                {
                    Vector2 offset = offsets.GetArrayElementAtIndex(slot).vector2Value;
                    Vector3 point = anchor.TransformPoint(new Vector3(offset.x, 0, offset.y));
                    Require(arena.Ground.Raycast(new Ray(point + Vector3.up * 2, Vector3.down), out _, 4)
                        && !Physics.CheckCapsule(point + Vector3.up * .5f, point + Vector3.up * 1.45f, .45f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                        $"{anchor.name} slot {slot + 1} is on clear ground");
                }
            }

            int previousSize = 0;
            float previousTime = 0;
            for (int i = 0; i < schedule.arraySize; i++)
            {
                var wave = schedule.GetArrayElementAtIndex(i);
                var arrival = wave.FindPropertyRelative("arrivalSeconds");
                int knights = wave.FindPropertyRelative("knights").intValue;
                int archers = wave.FindPropertyRelative("archers").intValue;
                int sectorIndex = wave.FindPropertyRelative("sector").intValue;
                var anchor = (Transform)sectors.GetArrayElementAtIndex(sectorIndex).FindPropertyRelative("anchor").objectReferenceValue;
                Require(arrival.floatValue > previousTime && (int)(arrival.floatValue / 300) == i / 3
                    && knights + archers > previousSize, $"Wave {i + 1} increases strength in its five-minute phase");
                previousTime = arrival.floatValue;
                previousSize = knights + archers;

                Vector3 safeLeaderPosition = -anchor.position;
                arena.Necromancer.transform.position = i == 0 ? anchor.position : safeLeaderPosition;
                var blocker = i == 0 ? GameObject.CreatePrimitive(PrimitiveType.Cube) : null;
                if (blocker)
                {
                    Vector2 offset = offsets.GetArrayElementAtIndex(0).vector2Value;
                    blocker.transform.position = anchor.TransformPoint(new Vector3(offset.x, .5f, offset.y));
                    blocker.transform.localScale = new Vector3(.85f, 1, .85f);
                }
                float delay = i == 0 ? 1.5f : .25f;
                if (i == 1) delay = -.1f; // Simulate a scheduled time that passed while the first sector was blocked.
                if (i > 5) delay = 30; // Free slots before making the next wave due.
                arrival.floatValue = (float)arena.ElapsedSeconds + delay;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (i == 1) Require(waves.SecondsUntilWave > .25, "An overdue wave still gets its own warning window");

                if (i == 0)
                {
                    yield return .25;
                    Require(waves.HasWarning && notices.CurrentKey == "survival-wave"
                        && notices.CurrentText.Contains("Восток") && waves.WavesSpawned == 0,
                        "HUD warns before the first wave and names its sector");
                    var indicators = new List<GameNotifications.Indicator>();
                    var camera = arena.LevelCamera;
                    Vector3 original = camera.transform.position;
                    camera.transform.position += camera.transform.right * 100;
                    notices.GetIndicators(camera, new HUDLayout(Screen.width, Screen.height), indicators);
                    camera.transform.position = original;
                    Require(indicators.Count == 1 && indicators[0].Text == "Новая волна",
                        "A warning outside the frame has a direction marker");

                    Require(arena.TryPauseGame(), "Pause before a wave");
                    double untilWave = waves.SecondsUntilWave;
                    yield return .3;
                    Require(waves.WavesSpawned == 0 && waves.SecondsUntilWave == untilWave,
                        "Paused game time freezes the warning countdown");
                    Require(arena.TryResumeGame(), "Resume before a wave");
                    double holdUntil = arena.ElapsedSeconds + untilWave + 4.5;
                    Time.timeScale = 5;
                    while (arena.ElapsedSeconds < holdUntil) yield return .05;
                    Require(waves.WavesSpawned == 0 && waves.Reinforcements.childCount == 0
                        && notices.CurrentKey == "survival-wave" && notices.CurrentText.Contains("ждёт места"),
                        "An occupied sector delays the whole wave and keeps its warning");
                    Time.timeScale = 1;
                    settings.FindProperty("warningSeconds").floatValue = 1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    arena.Necromancer.transform.position = safeLeaderPosition;
                }

                if (i == 5)
                {
                    double due = arena.ElapsedSeconds + waves.SecondsUntilWave;
                    while (arena.ElapsedSeconds < due + .4) yield return .05;
                    Require(waves.WavesSpawned == i && waves.LivingEnemyCount == enemyLimit
                        && notices.CurrentText.Contains("лимит врагов"), "A full arena delays the entire wave and explains why");
                    var knight = waves.Reinforcements.GetChild(0).GetComponent<Guard>();
                    knight.enabled = true;
                    var defender = knight.GetComponent<DefenderInfection>();
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Zombie.prefab").GetComponent<Zombie>();
                    Require(defender.TryInfect(prefab, arena.Necromancer, .1f)
                        && waves.LivingEnemyCount == enemyLimit, "An infection in progress still occupies a slot");
                    yield return .4;
                    Require(waves.LivingEnemyCount == enemyLimit - 1 && waves.WavesSpawned == i,
                        "A converted defender frees exactly one slot");
                    foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                        Object.Destroy(zombie.gameObject);

                    int roomNeeded = knights + archers;
                    while (waves.LivingEnemyCount + roomNeeded > enemyLimit + 1)
                    {
                        Object.Destroy(waves.Reinforcements.GetChild(0).gameObject);
                        yield return .05;
                    }
                    yield return .35;
                    Require(waves.WavesSpawned == i, "One slot short still holds the entire wave");
                    var archer = waves.Reinforcements.GetComponentInChildren<Crossbowman>();
                    Require(archer && archer.Combat.TakeDamage(10000)
                        && waves.LivingEnemyCount + roomNeeded == enemyLimit, "A killed defender frees a slot immediately");
                }
                else if (i > 5)
                {
                    while (waves.LivingEnemyCount + knights + archers > enemyLimit)
                    {
                        Object.Destroy(waves.Reinforcements.GetChild(0).gameObject);
                        yield return .05;
                    }
                    arrival.floatValue = (float)arena.ElapsedSeconds + .25f;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }

                int oldCount = waves.LivingEnemyCount;
                double spawnDeadline = arena.ElapsedSeconds + 2;
                while (waves.WavesSpawned == i)
                {
                    Require(arena.ElapsedSeconds < spawnDeadline, $"Wave {i + 1} spawns as soon as the sector is clear");
                    yield return .05;
                }
                Require(waves.WavesSpawned == i + 1 && waves.Reinforcements.childCount == oldCount + knights + archers,
                    $"Wave {i + 1} spawns once with the full count");
                Require(waves.LivingEnemyCount <= enemyLimit, $"Wave {i + 1} respects the live enemy limit");
                int actualKnights = 0;
                int actualArchers = 0;
                for (int index = oldCount; index < waves.Reinforcements.childCount; index++)
                {
                    var unit = waves.Reinforcements.GetChild(index);
                    if (unit.TryGetComponent<Guard>(out var guard)) { actualKnights++; guard.enabled = false; }
                    if (unit.TryGetComponent<Crossbowman>(out var archer)) { actualArchers++; archer.enabled = false; }
                    Require(Vector3.Distance(unit.position, anchor.position) < 3.6f
                        && Vector3.Distance(unit.position, safeLeaderPosition) >= 4
                        && (!blocker || Vector3.Distance(unit.position, blocker.transform.position) > .9f),
                        $"Wave {i + 1} avoids the player and blocked points inside its sector");
                    for (int other = 0; other < index; other++)
                        Require(Vector3.Distance(unit.position, waves.Reinforcements.GetChild(other).position) >= .9f,
                            $"Wave {i + 1} keeps clear of both current and previous waves");
                }
                Require(actualKnights == knights && actualArchers == archers, $"Wave {i + 1} matches its composition");
                if (blocker) Object.Destroy(blocker);
            }
            yield return .2;
            Require(waves.WavesSpawned == waves.WaveCount && !waves.HasWarning && !notices.HasMessage,
                "The last arrival clears the schedule and warning");
            Require(arena.TryPauseGame() && arena.TryRestartLevel(), "Restart after the completed schedule");
            yield return .6;
            var fresh = Object.FindFirstObjectByType<GameManager>().GetComponent<SurvivalWaves>();
            Require(fresh.WavesSpawned == 0 && fresh.Reinforcements.childCount == 0 && fresh.SecondsUntilWave > 55,
                "Restart restores the authored schedule without leftover reinforcements");
        }

        private static IEnumerator SurvivalResourceChecks(GameManager arena, Zombie zombiePrefab)
        {
            var population = arena.GetComponent<SurvivalPopulation>();
            var settings = new SerializedObject(population);
            var group = (Transform)settings.FindProperty("civilianGroup").objectReferenceValue;
            var leader = arena.Necromancer;
            var energy = leader.Energy;
            var souls = arena.GetComponent<SoulProgression>();
            int populationSize = group.childCount;
            Require(populationSize == 10 && Mathf.Approximately(energy.Regeneration, .65f), "Arena resource settings");
            arena.GetComponent<SurvivalWaves>().enabled = false;
            leader.enabled = false;
            var safePosition = leader.transform.position;
            var resident = group.GetChild(0).GetComponent<Civilian>();
            foreach (Transform child in group)
                if ((child.position - safePosition).sqrMagnitude > (resident.transform.position - safePosition).sqrMagnitude)
                    resident = child.GetComponent<Civilian>();
            foreach (Transform child in group)
                if (child != resident.transform) child.gameObject.SetActive(false);

            // Jump the attempt clock between checks while keeping the authored delays.
            var clock = typeof(GameManager).GetField("attemptStartedAt",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            void SetElapsed(double seconds) => clock.SetValue(arena, Time.timeAsDouble - seconds);

            for (int cycle = 0; cycle < 2; cycle++)
            {
                double start = cycle == 0 ? 60 : 860;
                SetElapsed(start);
                Vector3 vacancy = resident.transform.position;
                leader.transform.position = vacancy;
                float gained = 0;
                void OnReward(float amount, Vector3 position) => gained += amount;
                energy.Rewarded += OnReward;
                Require(energy.TrySpend(15) && resident.TryInfect(zombiePrefab, leader, .1f),
                    "A resident can be infected in the early and final minutes");
                yield return .6;
                energy.Rewarded -= OnReward;
                var recruit = Object.FindFirstObjectByType<Zombie>();
                Require(recruit && !resident && souls.Experience == cycle + 1 && Mathf.Approximately(gained, 7),
                    "A conversion produces one recruit, one experience and seven energy");
                recruit.enabled = false;
                Require(group.childCount == populationSize - 1, "Infected resident leaves a vacancy");
                SetElapsed(start + 20);
                yield return .4;
                Require(group.childCount == populationSize - 1, "Vacancy waits the authored delay");
                SetElapsed(start + 27);
                yield return .4;
                Require(group.childCount == populationSize - 1, "Resident waits while the leader occupies the spawn");
                recruit.transform.position = safePosition + Vector3.right * 2;
                leader.transform.position = safePosition;
                float beforePause = energy.Current;
                Require(arena.TryPauseGame(), "Resource cycle can pause");
                yield return .5;
                Require(group.childCount == populationSize - 1 && energy.Current == beforePause,
                    "Pause freezes replenishment and regeneration");
                Require(arena.TryResumeGame(), "Resource cycle can resume");
                yield return .6;
                Require(group.childCount == populationSize && energy.Current > beforePause, "Vacancy refills and energy regenerates");
                resident = group.GetChild(populationSize - 1).GetComponent<Civilian>();
                Require(resident.IsAvailableTarget, "Returned resident is a normal infection target");

                if (cycle == 0)
                {
                    SetElapsed(start + 121);
                    yield return .4;
                    Require(!recruit, "Undead expires after its authored lifetime");
                }
            }

            Require(arena.TryPauseGame() && arena.TryRestartLevel(), "Restart after late resource replenishment");
            yield return .6;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            Require(fresh.GetComponent<SoulProgression>().Experience == 0
                && fresh.Necromancer.Energy.Current == fresh.Necromancer.Energy.Maximum
                && !Object.FindFirstObjectByType<Zombie>()
                && Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length == populationSize,
                "Restart restores civilians, energy and progression");
        }

        private static IEnumerator SurvivalRemainsChecks(GameManager arena)
        {
            var waves = new SerializedObject(arena.GetComponent<SurvivalWaves>());
            var lifetime = waves.FindProperty("corpseLifetime");
            Require(lifetime.floatValue == 20, "Corpse lifetime is authored");
            lifetime.floatValue = 1;
            waves.ApplyModifiedPropertiesWithoutUndo();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GuardCorpse.prefab").GetComponent<GuardCorpse>();
            var corpse = Object.Instantiate(prefab, arena.StartPoint.position, Quaternion.identity);
            yield return .25;
            Require(corpse && corpse.UnavailableReason(arena.Necromancer) == null, "Fresh remains are still raisable");
            Require(arena.TryPauseGame(), "Pause during corpse lifetime");
            yield return 1.1;
            Require(corpse, "Pause freezes corpse expiry");
            Require(arena.TryResumeGame(), "Resume corpse lifetime");
            yield return 1.1;
            Require(!corpse, "Old remains are removed automatically");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
