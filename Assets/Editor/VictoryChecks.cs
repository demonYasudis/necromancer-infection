using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class VictoryChecks
    {
        public static IEnumerator Checks(GameManager manager, Zombie prefab)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Require(manager && manager.GetComponent<VictoryScreen>() && !manager.IsFinished
                    && manager.InfectedCount == 0 && manager.GuardsKilled == 0 && manager.SurvivingUndead == 0,
                    "A new attempt must reset victory and all counters and include victory UI.");
                Require(!manager.TryRestartLevel() && !manager.CanReturnToMenu && !manager.TryReturnToMenu(),
                    "Running attempts and an unavailable menu must reject navigation.");
                foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
                foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
                var leader = manager.Necromancer;
                leader.enabled = false;
                leader.transform.position = Vector3.zero;
                var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath);
                var subject = Object.Instantiate(humanPrefab, new Vector3(-10, 0, -8), Quaternion.identity).GetComponent<Civilian>();
                Require(subject.TryInfect(prefab, leader, 0.1f), "Set up a real delayed infection.");
                yield return 0.2f;
                var seed = Object.FindFirstObjectByType<Zombie>();
                Require(seed && manager.InfectedCount == 1, "A completed manual infection must count once.");
                seed.enabled = false;
                seed.Initialize(leader);
                Require(manager.InfectedCount == 1, "Repeated initialization must not duplicate infection counts.");
                var chained = Object.Instantiate(humanPrefab, new Vector3(-12, 0, -8), Quaternion.identity).GetComponent<Civilian>();
                chained.Combat.TakeDamage(1000, seed.Combat);
                Require(manager.InfectedCount == 2, "A real chain conversion must count immediately and exactly once.");
                foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) zombie.enabled = false;
                var defender = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                    new Vector3(-10, 0, 8), Quaternion.identity).GetComponent<Guard>();
                defender.enabled = false;
                defender.Combat.TakeDamage(1000, seed.Combat);
                Require(!defender.Combat.TakeDamage(1000) && manager.GuardsKilled == 1,
                    "A guard death must count once, even on repeated lethal damage.");
                seed.Combat.TakeDamage(1000);
                yield return 0.1f;
                Require(manager.InfectedCount == 2 && manager.GuardsKilled == 1,
                    "Zombie deaths must not remove conversions or count as guard kills.");
                double beforePause = manager.ElapsedSeconds;
                Time.timeScale = 0;
                yield return 0.2f;
                Require(manager.ElapsedSeconds == beforePause, "Attempt duration must exclude paused time.");
                Time.timeScale = 1;
                var pending = Object.Instantiate(humanPrefab, new Vector3(-14, 0, -8), Quaternion.identity).GetComponent<Civilian>();
                Require(pending.TryInfect(prefab, leader, 5), "Set up a conversion still pending at victory.");
                var farm = manager.Farm;
                var assault = farm.GetComponent<FarmAssault>();
                var settings = new SerializedObject(assault);
                settings.FindProperty("corruptionDuration").floatValue = 0.4f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                leader.enabled = true;
                leader.BeginRoute(farm.ArrivalPoint.position, new[] { farm.ArrivalPoint });
                Require(farm.TryBeginFinalPhase(leader), "Start the real farm capture.");
                foreach (var guard in assault.Reinforcements.GetComponentsInChildren<Guard>()) guard.enabled = false;
                yield return 0.65f;
                Require(assault.IsCaptured && manager.IsVictorious && !manager.IsDefeated && Time.timeScale == 0,
                    "A living farm capture must synchronously win and freeze the attempt.");
                Require(manager.InfectedCount == 2 && manager.GuardsKilled == 1 && manager.SurvivingUndead == 2,
                    "Results must contain two conversions, one guard kill and one surviving zombie plus the necromancer.");
                double finishedTime = manager.ElapsedSeconds;
                Vector3 leaderPosition = leader.transform.position;
                Require(!leader.Combat.TakeDamage(1000), "Victory must prevent subsequent damage and defeat.");
                var infection = Object.FindFirstObjectByType<InfectionAbility>();
                Require(!infection.TryInfectAtScreenPosition(Vector2.zero)
                    && !infection.GetComponent<ProtectionAbility>().TryCastAtScreenPosition(Vector2.zero),
                    "Both abilities must reject input while victory is displayed.");
                yield return 0.3f;
                Require(manager.ElapsedSeconds == finishedTime && finishedTime > 0 && manager.InfectedCount == 2
                    && manager.SurvivingUndead == 2 && pending && leader.transform.position == leaderPosition,
                    "Victory must freeze time, results, movement and pending conversion.");
                Require(manager.TryRestartLevel() && !manager.TryRestartLevel(), "Victory retry must accept one request.");
                yield return 0.3f;
                manager = Object.FindFirstObjectByType<GameManager>();
                Require(!manager.IsFinished && Time.timeScale == 1 && manager.ElapsedSeconds < 1
                    && manager.InfectedCount == 0 && manager.GuardsKilled == 0
                    && !manager.Farm.GetComponent<FarmAssault>().IsCaptured
                    && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                    "Victory retry must restore a clean scene, time, objective and statistics.");
            }
            Debug.Log("PASS: two victories/retries; real manual and chain conversions, guard deaths, surviving undead, pause-aware duration, frozen results and clean statistics after reload.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
