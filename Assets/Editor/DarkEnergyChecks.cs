using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class DarkEnergyChecks
    {
        public static IEnumerator Checks(GameManager manager, InfectionAbility ability, Zombie prefab)
        {
            foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(zombie.gameObject);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) Object.Destroy(human.gameObject);
            foreach (var defender in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) Object.Destroy(defender.gameObject);
            var leader = manager.Necromancer;
            leader.transform.position = new Vector3(11, 0, 3.5f);
            var energy = leader.Energy;
            SetRegeneration(energy, 0);
            energy.Restore(energy.Maximum);
            yield return 0.1f;

            Vector2 ground = manager.LevelCamera.WorldToScreenPoint(new Vector3(-15, 0, -8));
            Require(!ability.TryInfectAtScreenPosition(ground) && energy.Current == 100,
                "Ground clicks must not spend energy.");
            for (int i = 0; i < 6; i++)
            {
                var target = Human();
                Vector2 screen = ScreenPoint(manager, target);
                Require(ability.TryInfectAtScreenPosition(screen) && energy.Current == 100 - (i + 1) * 15,
                    "Each successful LMB infection must spend exactly 15.");
                Require(!ability.TryInfectAtScreenPosition(screen) && energy.Current == 100 - (i + 1) * 15,
                    "Duplicate infection must not spend again.");
                target.gameObject.SetActive(false);
                Object.Destroy(target.gameObject);
            }
            var denied = Human();
            Require(!ability.TryInfectAtScreenPosition(ScreenPoint(manager, denied))
                && energy.Current == 10 && denied.State != Civilian.BehaviourState.Infected,
                "Insufficient energy must leave the human and resource unchanged.");
            energy.Restore(5);
            Require(ability.TryInfectAtScreenPosition(ScreenPoint(manager, denied)) && energy.Current == 0,
                "Exactly 15 energy must permit one infection and reach zero.");
            yield return ability.InfectionDelay + 0.15f;
            var converted = Object.FindFirstObjectByType<Zombie>();
            Require(!denied && converted && energy.Current == 5,
                "Delayed conversion must award +5 without a human-death reward.");
            converted.enabled = false;
            converted.Initialize(leader);
            Require(energy.Current == 5, "Reinitializing an existing zombie must not repeat its spawn reward.");
            Debug.Log("PASS: six paid raycast infections, duplicate/ground rejection, exhaustion, exact cost and delayed +5 reward.");

            var victim = Human();
            victim.Combat.TakeDamage(1, converted.Combat);
            Require(energy.Current == 5, "Nonlethal damage must not grant death energy.");
            victim.Combat.TakeDamage(100, converted.Combat);
            Require(energy.Current == 13, "Zombie kill must award +3 for death and +5 for the new zombie.");
            victim.Combat.TakeDamage(100, converted.Combat);
            Require(energy.Current == 13, "Repeated lethal damage must not repeat death or spawn rewards.");
            var naturalDeath = Human();
            naturalDeath.Combat.TakeDamage(100);
            Require(energy.Current == 16, "Human death without conversion must award only +3.");
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            var guard = Object.Instantiate(guardPrefab, new Vector3(-4, 0, 0), Quaternion.identity).GetComponent<Guard>();
            guard.enabled = false;
            guard.Combat.TakeDamage(100, converted.Combat);
            guard.Combat.TakeDamage(100, converted.Combat);
            Require(energy.Current == 24, "Guard death must award +8 exactly once.");
            converted.Combat.TakeDamage(100);
            Require(energy.Current == 24, "Undead death must not grant a human or guard reward.");
            energy.Restore(1000);
            Require(energy.Current == 100, "Rewards must stop at maximum energy.");
            Require(!energy.TrySpend(-1) && !energy.TrySpend(float.NaN) && !energy.TrySpend(float.PositiveInfinity)
                && energy.Current == 100, "Invalid spends must not alter energy.");
            energy.Restore(float.NaN);
            Require(energy.Current == 100, "Invalid restoration must not poison the resource.");
            Debug.Log("PASS: real deaths and chain conversion award +3/+5/+8 exactly once; nonlethal/undead hits and resource cap.");

            energy.TrySpend(100);
            energy.enabled = false;
            var disabledDeath = Human();
            disabledDeath.Combat.TakeDamage(100);
            Require(energy.Current == 0, "Disabled resource must unsubscribe from death rewards.");
            energy.enabled = true;
            Require(energy.Current == 0, "Re-enabling must preserve energy rather than resetting to 100.");
            SetRegeneration(energy, 1);
            float startTime = Time.time;
            yield return 1.2f;
            Require(Mathf.Abs(energy.Current - (Time.time - startTime)) < 0.15f && energy.Current >= 1,
                "An empty pool must regenerate one energy per scaled second.");
            Time.timeScale = 0;
            float pausedEnergy = energy.Current;
            var pausedTarget = Human();
            Require(!ability.TryInfectAtScreenPosition(ScreenPoint(manager, pausedTarget)),
                "Paused gameplay must reject infection.");
            yield return 0.4f;
            Require(Mathf.Abs(energy.Current - pausedEnergy) < 0.001f, "Pause must stop regeneration.");
            Time.timeScale = 1;
            energy.Restore(100);
            var hud = ability.GetComponent<DarkEnergyHUD>();
            Vector2 panelPoint = new Vector2(40, 40) * Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            panelPoint.y = Screen.height - panelPoint.y;
            Require(hud.ContainsScreenPosition(panelPoint) && !ability.TryInfectAtScreenPosition(panelPoint)
                && energy.Current == 100, "HUD clicks must not reach the world or spend energy.");
            var freshLeader = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NecromancerSetup.PrefabPath));
            Require(freshLeader.GetComponent<DarkEnergy>().Current == 100, "A fresh attempt must start with full energy.");
            Object.Destroy(freshLeader);
            Debug.Log("PASS: empty-pool +1/sec regeneration, pause, disable/re-enable lifecycle, HUD click blocking and fresh start.");
        }

        private static Civilian Human()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                new Vector3(-4, 0, 0), Quaternion.identity);
            var human = root.GetComponent<Civilian>();
            var settings = new SerializedObject(human);
            settings.FindProperty("movementArea").rectValue = new Rect(-4, 0, 0, 0);
            settings.ApplyModifiedPropertiesWithoutUndo();
            return human;
        }

        private static Vector2 ScreenPoint(GameManager manager, Civilian target)
        {
            Physics.SyncTransforms();
            return manager.LevelCamera.WorldToScreenPoint(target.transform.position + Vector3.up * 0.75f);
        }

        private static void SetRegeneration(DarkEnergy energy, float rate)
        {
            var settings = new SerializedObject(energy);
            settings.FindProperty("regenerationPerSecond").floatValue = rate;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
