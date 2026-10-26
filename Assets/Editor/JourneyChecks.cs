using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    /// <summary>Plays the complete journey through the public mouse-ray abilities, without stat cheats.</summary>
    [InitializeOnLoad]
    public static class JourneyChecks
    {
        private const string Key = "NecromancerInfection.JourneyChecks";
        private static double deadline;
        private static double pauseUntil;
        private static Vector3 pausedPosition;
        private static float nextAction, nextShield, nextReport;
        private static int phase, stages, casts, infections;
        private static int screenshots;
        private static GameManager original;
        private static string error;

        static JourneyChecks() { EditorApplication.update += Tick; }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            var journey = manager.GetComponent<JourneyRoute>();
            Require(journey, "Journey component missing.");
            Require(Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length == 93, "Expected 93 residents.");
            Require(Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 18, "Expected seven patrols with 18 guards.");
            Require(Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None).Length == 3, "Expected three ranged posts.");
            var settings = new SerializedObject(manager);
            var route = settings.FindProperty("intermediateWaypoints");
            Vector3 from = manager.StartPoint.position;
            for (int i = 0; i <= route.arraySize; i++)
            {
                Vector3 to = i == route.arraySize ? manager.Farm.ArrivalPoint.position
                    : ((Transform)route.GetArrayElementAtIndex(i).objectReferenceValue).position;
                Vector3 delta = to - from;
                foreach (var hit in Physics.SphereCastAll(from + Vector3.up * 0.8f, 0.35f, delta.normalized, delta.magnitude))
                    Require(hit.collider.GetComponentInParent<UnitCombat>(), "Route intersects scenery: " + hit.collider.name);
                from = to;
            }
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (deadline == 0)
                {
                    deadline = EditorApplication.timeSinceStartup + 300;
                    Application.logMessageReceived += Watch;
                    UnityEngine.Random.InitState(1501);
                    Time.maximumDeltaTime = 0.05f;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Journey checks timed out.");
                var manager = Object.FindFirstObjectByType<GameManager>();
                if (!manager || manager.ElapsedSeconds < 0.2) return;
                var journey = manager.GetComponent<JourneyRoute>();
                var leader = manager.Necromancer;
                if (phase == 0)
                {
                    Require(Mathf.Abs(journey.Length - 681.5f) < 0.1f, "Authored route must last 340.75 seconds at speed 2.");
                    pausedPosition = leader.transform.position;
                    Time.timeScale = 0;
                    pauseUntil = EditorApplication.timeSinceStartup + 0.3;
                    phase = 1;
                    return;
                }
                if (phase == 1)
                {
                    if (EditorApplication.timeSinceStartup < pauseUntil) return;
                    Require(leader.transform.position == pausedPosition, "Pause moved the leader.");
                    var ability = Object.FindFirstObjectByType<ProtectionAbility>();
                    Require(!ability.TryCastAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(leader.transform.position)), "Paused shield must be rejected.");
                    phase = 2;
                    Time.timeScale = 8;
                }
                if (phase == 3)
                {
                    if (manager == original) return;
                    Require(!manager.IsFinished && journey.Stage == 0 && manager.GuardsKilled == 0 && manager.InfectedCount == 0,
                        "Retry did not reset progress and statistics.");
                    Require(!Object.FindFirstObjectByType<Grave>().IsConsumed && !Object.FindFirstObjectByType<AlarmBell>().IsRaised,
                        "Retry did not reset side objectives.");
                    Time.timeScale = 8;
                    phase = 5; // A second attempt deliberately makes no player inputs.
                    return;
                }
                if (phase == 5)
                {
                    Require(manager.ElapsedSeconds < 120, "Unprepared leader unexpectedly survived the village patrols.");
                    if (!manager.IsDefeated) return;
                    Debug.Log($"PASS: unattended attempt defeated at {manager.ElapsedSeconds:0.0}s; an army is required.");
                    Require(manager.InfectedCount == 0 && manager.TryRestartLevel(), "Unprepared defeat/retry failed.");
                    original = manager;
                    phase = 4;
                    return;
                }
                if (phase == 4)
                {
                    if (manager == original) return;
                    Require(!manager.IsFinished && journey.Stage == 0 && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                        "Defeat retry must remove the previous army.");
                    Finish(true, "Journey: real paid LMB/RMB, full autonomous route, all three stages, victory, pause and both retries passed.");
                    return;
                }
                Require(!manager.IsDefeated, $"Prepared run lost at {journey.DistanceTravelled:0}m; infected={manager.InfectedCount}, killed={manager.GuardsKilled}.");
                stages |= 1 << journey.Stage;
                if (manager.IsVictorious)
                {
                    Require(stages == 7 && infections > 0 && casts > 0 && manager.GuardsKilled >= 18, "Full run did not exercise stages, abilities and combat.");
                    Require(manager.ElapsedSeconds >= 350 && manager.ElapsedSeconds <= 365, "Duration outside the expected 5–6 minute range.");
                    Debug.Log($"PASS: victory at {manager.ElapsedSeconds:0.0}s, infected {manager.InfectedCount}, guards {manager.GuardsKilled}, survivors {manager.SurvivingUndead}, LMB {infections}, shields {casts}.");
                    original = manager;
                    Require(manager.TryRestartLevel(), "Victory retry failed.");
                    phase = 3;
                    return;
                }
                if (manager.ElapsedSeconds > nextReport)
                {
                    Debug.Log($"JOURNEY {manager.ElapsedSeconds:0}s / {journey.DistanceTravelled:0}m / HP {leader.CurrentHealth} / horde {manager.GetComponent<GameFeedback>().HordeSize} / kills {manager.GuardsKilled}");
                    nextReport += 30;
                }
                if (journey.DistanceTravelled > 40 && screenshots == 0 || journey.DistanceTravelled > 300 && screenshots == 1 || journey.DistanceTravelled > 550 && screenshots == 2)
                    Capture(manager.LevelCamera, ++screenshots);
                if (Time.time < nextAction) return;
                nextAction = Time.time + 0.3f;
                var infection = Object.FindFirstObjectByType<InfectionAbility>();
                var protection = Object.FindFirstObjectByType<ProtectionAbility>();
                bool danger = false;
                foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None))
                    if (Vector3.Distance(guard.transform.position, leader.transform.position) < 6) danger = true;
                foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None))
                    if (Vector3.Distance(archer.transform.position, leader.transform.position) < 8) danger = true;
                if (danger && Time.time >= nextShield)
                {
                    foreach (float side in new[] { 0f, 1f, -1f, 2f, -2f })
                    {
                        Vector3 ground = leader.transform.position + leader.transform.forward * 1.8f + leader.transform.right * side;
                        if (!protection.TryCastAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(ground))) continue;
                        casts++; nextShield = Time.time + 2.5f; break;
                    }
                }
                var grave = Object.FindFirstObjectByType<Grave>();
                if (grave && !grave.IsConsumed && Vector3.Distance(grave.transform.position, leader.transform.position) < 7)
                    infection.TryInfectAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(grave.transform.position + Vector3.up * 0.4f));
                foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                {
                    if (!human.IsAvailableTarget || Vector3.Distance(human.transform.position, leader.transform.position) > 8
                        || leader.Energy.Current < (danger ? 45 : 15)) continue;
                    if (infection.TryInfectAtScreenPosition(manager.LevelCamera.WorldToScreenPoint(human.transform.position + Vector3.up * 0.7f)))
                    { infections++; break; }
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Capture(Camera camera, int index)
        {
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes($"Logs/journey-{index}.png", texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
            }
        }
        private static void Watch(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message; }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Watch;
            if (success) Debug.Log("PASS: " + message); else Debug.LogError("FAIL: " + message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
