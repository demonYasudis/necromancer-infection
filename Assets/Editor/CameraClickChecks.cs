using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class CameraClickChecks
    {
        private const string Key = "CameraClickChecks.Running";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static CameraClickChecks() => EditorApplication.update += Tick;
        public static void RunBatch() { SessionState.SetBool(Key + ".Batch", true); Run(); }
        [MenuItem("Necromancer Infection/Run Camera And Click Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the scene first.");
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
                    routine = Checks(); deadline = EditorApplication.timeSinceStartup + 150;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else Finish(true, "Travel and finale camera, standard and ultrawide framing, pause/retry, and infection/protection clicks passed.");
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        private static void Watch(string message, string stack, LogType type)
        {
            if (Application.isBatchMode && stack.Contains("UnityEditor.Search.SearchDatabase")
                && stack.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message + "\n" + stack;
        }
        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Watch;
            routine = null; error = null; next = 0; Time.timeScale = 1;
            if (success) Debug.Log("PASS: " + message); else Debug.LogError(message);
            if (SessionState.GetBool(Key + ".Batch", false)) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
        private static IEnumerator Checks()
        {
            foreach (string scene in new[] { TutorialLevelSetup.ScenePath(1), JourneySetup.ScenePath, DemoLevelSetup.ScenePath })
            {
                if (SceneManager.GetActiveScene().path != scene) SceneManager.LoadScene(scene);
                yield return .4;
                var game = Object.FindFirstObjectByType<GameManager>();
                var leader = game.Necromancer;
                var camera = game.LevelCamera;
                leader.enabled = false;
                game.GetComponent<SoulProgression>().enabled = false;
                foreach (var living in Object.FindObjectsByType<LivingTarget>(FindObjectsSortMode.None)) living.gameObject.SetActive(false);
                var waypoint = new GameObject("CameraTestWaypoint").transform;
                Vector3 start = leader.transform.position;
                foreach (var direction in new[] { Vector3.forward, Vector3.left, Vector3.back, Vector3.right })
                {
                    waypoint.position = start + direction * 20;
                    leader.BeginRoute(start, new[] { waypoint });
                    yield return .7;
                    Require(Vector3.Angle(Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up), direction) < .1f, "Camera follows route heading " + direction);
                    Vector3 a = camera.WorldToScreenPoint(start), b = camera.WorldToScreenPoint(start + direction * 2);
                    Require(b.y > a.y && Mathf.Abs(a.x - b.x) < .1f, "Road always goes up the screen");
                    var layout = new HUDLayout(camera.pixelWidth, camera.pixelHeight);
                    Vector3 torso = camera.WorldToScreenPoint(start + Vector3.up);
                    float desired = Mathf.Lerp(layout.PlayTop, layout.PlayBottom, .82f) * layout.Scale;
                    Require(Mathf.Abs(torso.x - camera.pixelWidth / 2f) < 1
                        && Mathf.Abs(camera.pixelHeight - torso.y - desired) < 1, "Framing survives rotation");
                }
                Quaternion heading = camera.transform.rotation;
                Time.timeScale = 0;
                waypoint.position = start + Vector3.left * 20;
                yield return .3;
                Require(Quaternion.Angle(heading, camera.transform.rotation) < .01f, "Pause freezes camera rotation");
                waypoint.position = start + Vector3.right * 20;
                Time.timeScale = 1;
                var energySettings = new SerializedObject(leader.Energy);
                energySettings.FindProperty("regenerationPerSecond").floatValue = 0;
                energySettings.ApplyModifiedPropertiesWithoutUndo();
                var ability = Object.FindFirstObjectByType<InfectionAbility>();
                foreach (var path in new[] { GuardSetup.PrefabPath, CrossbowSetup.PrefabPath })
                {
                    var subject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), start + Vector3.right * 3, Quaternion.identity);
                    var other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), start + Vector3.right * 5 + Vector3.forward * 2, Quaternion.identity);
                    var defender = subject.GetComponent<DefenderInfection>();
                    int needed = subject.GetComponent<Guard>() ? 3 : 2;
                    float total = needed == 3 ? 35 : 30;
                    Require(defender.RequiredClicks == needed, "Correct click threshold");
                    Physics.SyncTransforms();
                    Vector2 pointer = camera.WorldToScreenPoint(subject.GetComponent<Collider>().bounds.center);
                    float before = leader.Energy.Current;
                    for (int i = 1; i < needed; i++)
                    {
                        Require(ability.TryGetTarget(pointer, out var target, out _, out float cost, out var reason)
                            && target == subject.transform && cost == 0 && reason == null, "Partial click preview is free");
                        Require(ability.TryInfectAtScreenPosition(pointer), "Partial click accepted");
                        Require(defender.AcceptedClicks == i && !defender.IsInfected && leader.Energy.Current == before, "Partial progress without charge or conversion");
                    }
                    Require(other.GetComponent<DefenderInfection>().AcceptedClicks == 0, "Progress belongs to individual target");
                    Time.timeScale = 0;
                    Require(!ability.TryInfectAtScreenPosition(pointer) && defender.AcceptedClicks == needed - 1, "Pause rejects clicks");
                    Time.timeScale = 1;
                    leader.Energy.TrySpend(leader.Energy.Current);
                    leader.Energy.Restore(total - 1);
                    Require(!ability.TryInfectAtScreenPosition(pointer) && !defender.IsInfected && defender.AcceptedClicks == needed - 1, "Insufficient energy preserves progress");
                    var hud = new HUDLayout(Screen.width, Screen.height);
                    Vector2 blocked = hud.Status.center * hud.Scale;
                    blocked.y = Screen.height - blocked.y;
                    Require(!ability.TryInfectAtScreenPosition(blocked), "HUD rejects clicks");
                    leader.Energy.Restore(1);
                    int count = game.InfectedCount;
                    Require(ability.TryInfectAtScreenPosition(pointer) && defender.IsInfected && leader.Energy.Current == 0, "Final click pays exact full price once");
                    Require(!ability.TryInfectAtScreenPosition(pointer), "No duplicate conversion");
                    Time.timeScale = 0;
                    yield return .25;
                    Require(subject && game.InfectedCount == count, "Paused conversion cannot finish");
                    Time.timeScale = 1;
                    Object.Destroy(other);
                    yield return 1.5;
                    Require(!subject && game.InfectedCount == count + 1, "Exactly one converted zombie");
                    foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(zombie.gameObject);
                    leader.Energy.Restore(100);
                    yield return .1;
                }
                var dead = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath), start + Vector3.right * 3, Quaternion.identity);
                Physics.SyncTransforms();
                Vector2 deadPointer = camera.WorldToScreenPoint(dead.GetComponent<Collider>().bounds.center);
                Require(ability.TryInfectAtScreenPosition(deadPointer), "First click before death");
                dead.GetComponent<UnitCombat>().TakeDamage(10000);
                Require(!dead.GetComponent<DefenderInfection>().TryClick(AssetDatabase.LoadAssetAtPath<GameObject>(InfectionSetup.PrefabPath).GetComponent<Zombie>(), leader, 1), "Dead defender rejects progress");
                Object.Destroy(dead);
                yield return .1;
            }

            using var view = new HUDChecks.GameViewResolution();
            foreach (string scene in new[] { TutorialLevelSetup.ScenePath(1), TutorialLevelSetup.ScenePath(2),
                TutorialLevelSetup.ScenePath(3), TutorialLevelSetup.ScenePath(4), JourneySetup.ScenePath, DemoLevelSetup.ScenePath })
            {
                view.Set(1280, 720);
                SceneManager.LoadScene(scene);
                yield return .5;
                var game = Object.FindFirstObjectByType<GameManager>();
                var leader = game.Necromancer;
                var camera = game.LevelCamera;
                var farm = game.Farm;
                var assault = farm.GetComponent<FarmAssault>();
                leader.enabled = false;
                game.GetComponent<SoulProgression>().enabled = false;
                foreach (var target in Object.FindObjectsByType<LivingTarget>(FindObjectsSortMode.None))
                    target.gameObject.SetActive(false);
                assault.WaveArrived += _ =>
                {
                    foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>())
                        guard.enabled = false;
                };

                var waypoint = new GameObject("FinaleTestWaypoint").transform;
                Vector3 travelDirection = game.Objective ? Vector3.right : Vector3.forward;
                waypoint.position = leader.transform.position + travelDirection * 20;
                leader.BeginRoute(leader.transform.position, new[] { waypoint });
                yield return .65;
                Quaternion travelHeading = camera.transform.rotation;
                leader.transform.rotation = Quaternion.LookRotation(travelDirection);
                leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
                leader.enabled = true;
                Require(farm.TryBeginFinalPhase(leader), scene + ": enter the finale");
                yield return .15;
                if (game.Objective && game.Objective.TutorialLevel == 1)
                {
                    Require(game.IsVictorious && Quaternion.Angle(travelHeading, camera.transform.rotation) < .01f,
                        "Tutorial 1 wins without a camera turn");
                    continue;
                }
                foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.enabled = false;
                foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None)) archer.enabled = false;

                float turned = Quaternion.Angle(travelHeading, camera.transform.rotation);
                Require(turned > 0 && turned < 60, scene + ": camera starts turning without snapping");
                Quaternion pausedHeading = camera.transform.rotation;
                Vector3 pausedPosition = camera.transform.position;
                float pausedZoom = camera.orthographicSize;
                Time.timeScale = 0;
                yield return .3;
                Require(Quaternion.Angle(pausedHeading, camera.transform.rotation) < .01f
                    && camera.transform.position == pausedPosition && camera.orthographicSize == pausedZoom,
                    scene + ": pause freezes the transition");
                Time.timeScale = 1;
                yield return 1.3;

                var heading = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
                if (!game.Objective)
                {
                    Require(Vector3.Angle(heading, Vector3.back) < 10, scene + ": faces the reinforcement road");
                    Require(assault.Reinforcements.childCount == 0, scene + ": frames the approach before enemies spawn");
                    foreach (Transform marker in assault.WarningMarkers)
                        RequireInPlay(camera, marker.position);
                    yield return 2;
                }
                else
                {
                    Require(Vector3.Dot(heading, Vector3.forward) > .5f, scene + ": faces the farm defenders");
                }

                Quaternion encounterHeading = camera.transform.rotation;
                var defenders = Object.FindObjectsByType<DefenderInfection>(FindObjectsSortMode.None);
                Require(defenders.Length > 0, scene + ": actual enemies spawned");
                var infection = Object.FindFirstObjectByType<InfectionAbility>();
                var protection = Object.FindFirstObjectByType<ProtectionAbility>();
                foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(2560, 1080) })
                {
                    view.Set(size.x, size.y);
                    yield return .7;
                    Require(camera.pixelWidth == size.x && camera.pixelHeight == size.y,
                        $"Expected {size.x}x{size.y} camera, got {camera.pixelWidth}x{camera.pixelHeight}");
                    Require(Quaternion.Angle(encounterHeading, camera.transform.rotation) < .01f,
                        scene + ": heading stays stable when resizing");
                    RequireInPlay(camera, farm.transform.position);
                    foreach (var defender in defenders)
                    {
                        Vector3 center = defender.GetComponent<Collider>().bounds.center;
                        RequireInPlay(camera, center);
                        Vector2 pointer = camera.WorldToScreenPoint(center);
                        if (!infection.TryGetTarget(pointer, out var target, out _, out _, out _)
                            || target != defender.transform)
                        {
                            Physics.Raycast(camera.ScreenPointToRay(pointer), out var hit);
                            throw new InvalidOperationException($"{scene}: cannot select {defender.name} at {pointer} "
                                + $"(hit {hit.collider}, HUD blocked {AbilityPointer.IsBlocked(pointer)}).");
                        }
                    }
                    Vector2 groundPointer = camera.WorldToScreenPoint(leader.transform.position);
                    if (!protection.TryCastAtScreenPosition(groundPointer))
                    {
                        Physics.Raycast(camera.ScreenPointToRay(groundPointer), out var hit);
                        throw new InvalidOperationException($"{scene}: cannot cast protection at {groundPointer} "
                            + $"(hit {hit.collider}, HUD blocked {AbilityPointer.IsBlocked(groundPointer)}).");
                    }
                }
                Vector2 infectionPointer = camera.WorldToScreenPoint(defenders[0].GetComponent<Collider>().bounds.center);
                if (defenders[0].TryGetComponent<Guard>(out var selectedGuard)) selectedGuard.enabled = true;
                if (defenders[0].TryGetComponent<Crossbowman>(out var selectedArcher)) selectedArcher.enabled = true;
                Require(infection.TryInfectAtScreenPosition(infectionPointer), scene + ": infection click after turning");
                defenders[0].transform.position += camera.transform.right;
                yield return .25;
                Require(Quaternion.Angle(encounterHeading, camera.transform.rotation) < .01f,
                    scene + ": moving a defender does not change the heading");

                leader.Combat.TakeDamage(10000);
                Require(game.TryRestartLevel(), scene + ": retry after the encounter");
                yield return .5;
                var fresh = Object.FindFirstObjectByType<GameManager>();
                var freshHeading = Vector3.ProjectOnPlane(fresh.LevelCamera.transform.forward, Vector3.up);
                Require(!fresh.FinalPhase && Time.timeScale == 1
                    && Vector3.Angle(freshHeading, fresh.Necromancer.TravelDirection) < 1,
                    scene + ": retry restores travelling camera");
            }
        }

        private static void RequireInPlay(Camera camera, Vector3 position)
        {
            var layout = new HUDLayout(camera.pixelWidth, camera.pixelHeight);
            Vector3 screen = camera.WorldToScreenPoint(position);
            float fromTop = (camera.pixelHeight - screen.y) / layout.Scale;
            Require(screen.z > 0 && screen.x > 0 && screen.x < camera.pixelWidth
                && fromTop > layout.PlayTop && fromTop < layout.PlayBottom,
                $"Encounter point {position} projects outside the playable view at {screen}.");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
