using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class LevelRevisionChecks
    {
        private const string Key = "LevelRevisionChecks.Running";
        private static IEnumerator routine;
        private static double next;
        static LevelRevisionChecks() => EditorApplication.update += Tick;

        [MenuItem("Necromancer Infection/Run Level Revision Checks %&i")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
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
                Debug.Log("PASS LevelRevision: 120-second route, defender prices/conversion/repeat/insufficient energy/pause, necromancer shield and expiry.");
            }
            catch (Exception e) { Finish(); Debug.LogException(e); }
        }

        private static void Finish()
        {
            SessionState.SetBool(Key, false);
            routine = null;
            next = 0;
            Time.timeScale = 1;
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Checks()
        {
            yield return .5;
            var game = Object.FindFirstObjectByType<GameManager>();
            var leader = game.Necromancer;
            leader.enabled = false;
            game.GetComponent<SoulProgression>().enabled = false;
            game.GetComponent<OnboardingGuide>().enabled = false;
            foreach (var living in Object.FindObjectsByType<LivingTarget>(FindObjectsSortMode.None)) living.gameObject.SetActive(false);
            Require(Mathf.Abs(game.GetComponent<JourneyRoute>().Length / leader.MoveSpeed - 120) < .01f, "Route must take 120 seconds.");
            var infection = Object.FindFirstObjectByType<InfectionAbility>();
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            var energySettings = new SerializedObject(leader.Energy);
            energySettings.FindProperty("regenerationPerSecond").floatValue = 0;
            energySettings.ApplyModifiedPropertiesWithoutUndo();
            foreach (string path in new[] { GuardSetup.PrefabPath, CrossbowSetup.PrefabPath })
            {
                var subject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    leader.transform.position + Vector3.right * 3, Quaternion.identity);
                Physics.SyncTransforms();
                Vector2 screen = game.LevelCamera.WorldToScreenPoint(subject.GetComponent<Collider>().bounds.center);
                float expected = subject.GetComponent<Guard>() ? 35 : 30;
                var defender = subject.GetComponent<DefenderInfection>();
                float before = leader.Energy.Current;
                for (int click = 1; click < defender.RequiredClicks; click++)
                    Require(infection.TryInfectAtScreenPosition(screen) && defender.AcceptedClicks == click
                        && !defender.IsInfected && leader.Energy.Current == before, "Partial clicks do not charge or convert.");
                Require(infection.TryGetTarget(screen, out var target, out _, out float price, out _)
                    && target == subject.transform && price == expected && price > infection.InfectionCost, "Correct defender target and price.");
                leader.Energy.TrySpend(leader.Energy.Current);
                leader.Energy.Restore(expected - 1);
                Require(!infection.TryInfectAtScreenPosition(screen) && !subject.GetComponent<DefenderInfection>().IsInfected, "Insufficient energy must not infect.");
                leader.Energy.Restore(1);
                int count = game.InfectedCount, killed = game.GuardsKilled;
                Require(infection.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 0, "Exact defender payment.");
                Require(!infection.TryInfectAtScreenPosition(screen), "Duplicate cast must fail.");
                Require(!subject.GetComponent<UnitCombat>().IsMeleeAttacking, "Pending attack must stop.");
                Time.timeScale = 0;
                yield return 1.4;
                Require(subject && game.InfectedCount == count, "Pause must suspend conversion.");
                Time.timeScale = 1;
                yield return 1.5;
                Require(!subject && game.InfectedCount == count + 1 && game.GuardsKilled == killed, "One zombie, no kill reward.");
                foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(zombie.gameObject);
                yield return .1;
            }
            infection.ReduceCost();
            Require(Mathf.Approximately(infection.SoldierInfectionCost, 28)
                && Mathf.Approximately(infection.ArcherInfectionCost, 24)
                && Mathf.Approximately(infection.InfectionCost, 12), "Upgrade applies to every infection cost.");
            leader.Energy.Restore(100);
            Physics.SyncTransforms();
            Vector2 leaderScreen = game.LevelCamera.WorldToScreenPoint(leader.GetComponent<Collider>().bounds.center);
            Require(protection.TryCastAtScreenPosition(leaderScreen), "Cast directly on necromancer.");
            float hp = leader.CurrentHealth;
            leader.Combat.TakeDamage(10);
            Require(leader.CurrentHealth == hp - 5, "Leader takes half damage.");
            yield return .1;
            Require(leader.GetComponent<ProtectionMarker>().IsVisible, "Leader shield marker visible.");
            yield return 5.1;
            hp = leader.CurrentHealth;
            leader.Combat.TakeDamage(10);
            Require(leader.CurrentHealth == hp - 10, "Expired protection restores full damage.");
            leader.enabled = true;
            double started = Time.timeAsDouble;
            Time.timeScale = 10;
            double deadline = EditorApplication.timeSinceStartup + 40;
            while (!leader.HasArrived)
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Route traversal timed out.");
                yield return .05;
            }
            Require(Math.Abs(Time.timeAsDouble - started - 120) < 1.5, "Actual movement arrives after approximately 120 game seconds.");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
