using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class DefeatChecks
    {
        public static IEnumerator Checks(GameManager manager, InfectionAbility infection, Zombie zombiePrefab)
        {
            yield return 0.1f;
            Require(manager.GetComponent<DefeatScreen>() && !manager.IsDefeated && !manager.TryRestartLevel(),
                "A running attempt must have its defeat UI and reject premature restarts.");
            var leader = manager.Necromancer;
            leader.transform.position = Vector3.zero;
            var humanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath);
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            var unrelated = Object.Instantiate(guardPrefab, new Vector3(-8, 0, -8), Quaternion.identity).GetComponent<Guard>();
            unrelated.enabled = false;
            unrelated.Combat.TakeDamage(1000);
            Require(!manager.IsDefeated && Time.timeScale == 1, "Other units dying must not end the attempt.");
            var pending = Object.Instantiate(humanPrefab, new Vector3(-4, 0, 0), Quaternion.identity).GetComponent<Civilian>();
            Require(pending.TryInfect(zombiePrefab, leader, infection.InfectionDelay), "Set up a pending conversion before defeat.");
            var wandering = Object.Instantiate(humanPrefab, new Vector3(-8, 0, -3), Quaternion.identity).GetComponent<Civilian>();
            var follower = Object.Instantiate(zombiePrefab, new Vector3(2, 0, 0), Quaternion.identity);
            follower.Initialize(leader);
            var defender = Object.Instantiate(guardPrefab, new Vector3(0.8f, 0, 0), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false;
            var area = Object.Instantiate(infection.GetComponent<ProtectionAbility>().AreaPrefab,
                new Vector3(14, 0, -8), Quaternion.identity);
            var energy = leader.Energy;
            var settings = new SerializedObject(energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            energy.TrySpend(20);
            int deaths = 0;
            bool repeatedDamageRejected = false;
            var leaderCombat = leader.Combat;
            leaderCombat.Died += attacker => { deaths++; repeatedDamageRejected = !leaderCombat.TakeDamage(1000); };
            leaderCombat.TakeDamage(Mathf.CeilToInt(leader.CurrentHealth) - 1);
            Require(!manager.IsDefeated && leader.Combat.IsAlive, "Low but positive necromancer HP must not trigger defeat.");
            Require(defender.Combat.TryAttack(leaderCombat) && !manager.IsDefeated, "Lethal swing must begin with a windup.");
            yield return defender.Combat.MeleeWindup + 0.05f;
            Require(manager.IsDefeated && Time.timeScale == 0,
                "A real lethal guard strike must synchronously end the attempt and freeze game time.");
            Require(deaths == 1 && repeatedDamageRejected, "Defeat must only be signaled once.");
            Vector3 humanPosition = wandering.transform.position;
            Vector3 zombiePosition = follower.transform.position;
            float guardHP = defender.Combat.CurrentHealth;
            float remainingEnergy = energy ? energy.Current : 0;
            float frozenTime = Time.time;
            Require(!defender.Combat.TakeDamage(10) && defender.Combat.CurrentHealth == guardHP,
                "Even direct damage must be blocked after defeat.");
            Vector2 humanScreen = manager.LevelCamera.WorldToScreenPoint(wandering.transform.position + Vector3.up * 0.75f);
            Require(!infection.TryInfectAtScreenPosition(humanScreen)
                && !infection.GetComponent<ProtectionAbility>().TryCastAtScreenPosition(humanScreen),
                "Both player abilities must reject input on the defeat screen.");
            yield return infection.InfectionDelay + 0.25f;
            Require(manager.IsDefeated && !leader && deaths == 1 && Time.time == frozenTime,
                "Defeat state must survive destruction of the necromancer and keep time frozen.");
            Require(wandering.transform.position == humanPosition && follower.transform.position == zombiePosition
                && pending && pending.State == Civilian.BehaviourState.Infected
                && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 1 && area && area.IsActive,
                "Defeat must freeze movement, pending conversions and protective-area lifetime.");
            Require(!energy || energy.Current == remainingEnergy, "Defeat must not regenerate the energy pool.");
            Debug.Log("PASS: lethal guard strike triggers one persistent defeat; movement, damage, abilities, conversions and timers freeze.");

            Require(manager.TryRestartLevel() && !manager.TryRestartLevel(), "Restart must accept one request and reject a double click.");
            yield return 0.3f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            CheckFreshAttempt(fresh);
            Require(fresh != manager, "Retry must load a new scene and manager instance.");
            Debug.Log("PASS: retry reloads DemoLevel with 18 civilians, 2 guards, 100 HP/energy, no old zombies/areas and a restarted route.");
            fresh.Necromancer.Combat.TakeDamage(1000);
            Require(fresh.IsDefeated && Time.timeScale == 0 && fresh.TryRestartLevel(),
                "Death and retry must still work on the second attempt without stale subscriptions.");
            yield return 0.3f;
            CheckFreshAttempt(Object.FindFirstObjectByType<GameManager>());
            Debug.Log("PASS: a second defeat and retry restore normal game time and a clean attempt again.");
        }

        private static void CheckFreshAttempt(GameManager manager)
        {
            Require(manager && !manager.IsDefeated && !manager.IsRestarting && Time.timeScale == 1
                && manager.GetComponent<DefeatScreen>(), "Reload must reset defeat, UI state and game time.");
            Require(manager.Necromancer && manager.Necromancer.CurrentHealth == 100 && manager.Necromancer.Energy.Current == 100
                && Vector3.Distance(manager.Necromancer.transform.position, manager.StartPoint.position) < 1.5f
                && !manager.Necromancer.HasArrived, "The new necromancer must start a fresh route at full HP and energy.");
            Require(Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None).Length == 18
                && Object.FindObjectsByType<Guard>(FindObjectsSortMode.None).Length == 2
                && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0
                && Object.FindObjectsByType<ProtectionArea>(FindObjectsSortMode.None).Length == 0,
                "Scene reload must restore the authored population and remove all previous attempt effects.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
