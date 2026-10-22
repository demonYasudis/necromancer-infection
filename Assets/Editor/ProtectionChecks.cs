using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class ProtectionChecks
    {
        public static IEnumerator Checks(GameManager manager, InfectionAbility infection, Zombie zombiePrefab)
        {
            foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(zombie.gameObject);
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) Object.Destroy(civilian.gameObject);
            yield return 0.1f;
            var ability = infection.GetComponent<ProtectionAbility>();
            Require(ability && ability.AreaPrefab && ability.EnergyCost == 25
                && ability.AreaPrefab.Duration == 5 && ability.AreaPrefab.DamageReduction == 0.5f,
                "Scene must include protection with cost 25, lifetime 5 and 50% reduction.");
            var leader = manager.Necromancer;
            var energy = leader.Energy;
            var settings = new SerializedObject(energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            energy.Restore(100);
            Vector3 center = new Vector3(-8, 0, 0);
            leader.transform.position = center + Vector3.forward;
            Vector2 screen = ScreenPoint(manager, center);
            Require(!ability.TryCastAtScreenPosition(ScreenPoint(manager, new Vector3(-7, 2.5f, 6)))
                && energy.Current == 100, "Buildings must block ground casting without spending energy.");
            Require(!ability.TryCastAtScreenPosition(ScreenPoint(manager, new Vector3(30, 0, 0)))
                && energy.Current == 100, "Off-map clicks must not create areas or spend energy.");
            var hud = infection.GetComponent<DarkEnergyHUD>();
            Vector2 hudPoint = new Vector2(40, 40) * Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            hudPoint.y = Screen.height - hudPoint.y;
            Require(hud.ContainsScreenPosition(hudPoint) && !ability.TryCastAtScreenPosition(hudPoint)
                && energy.Current == 100, "HUD must block RMB world casts.");
            Time.timeScale = 0;
            Require(!ability.TryCastAtScreenPosition(screen) && energy.Current == 100, "Paused casts must be rejected.");
            Time.timeScale = 1;
            energy.TrySpend(76);
            Require(!ability.TryCastAtScreenPosition(screen) && energy.Current == 24,
                "24 energy must not buy a 25-energy area.");
            energy.Restore(1);
            Require(ability.TryCastAtScreenPosition(screen) && energy.Current == 0,
                "Exactly 25 energy must buy one area and empty the pool.");
            var area = Object.FindFirstObjectByType<ProtectionArea>();
            Require(area && area.IsActive && Vector3.Distance(area.transform.position, center) < 0.01f,
                "Ground raycast must create an active area at the clicked position.");
            var ring = area.GetComponent<LineRenderer>();
            Require(ring && ring.enabled && ring.loop && ring.positionCount == 96 && ring.sharedMaterial
                && ring.sharedMaterial.shader.isSupported && area.GetComponent<MeshFilter>().sharedMesh
                && area.GetComponent<MeshRenderer>().sharedMaterial && !area.GetComponent<Collider>(),
                "Area must have a supported visible ring and disc without a blocking collider.");
            Require(!ability.TryCastAtScreenPosition(screen)
                && Object.FindObjectsByType<ProtectionArea>(FindObjectsSortMode.None).Length == 1,
                "An exhausted repeat cast must not create another area.");
            Debug.Log("PASS: protection ground raycast, cost 25, exact balance, exhaustion, buildings/map/HUD rejection and visuals.");

            var zombieUnit = Object.Instantiate(zombiePrefab, center, Quaternion.identity);
            zombieUnit.Initialize(leader);
            zombieUnit.enabled = false;
            var guard = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                center + Vector3.right * 0.8f, Quaternion.identity).GetComponent<Guard>();
            guard.enabled = false;
            Require(guard.Combat.TryAttack(zombieUnit.Combat) && zombieUnit.Combat.CurrentHealth == 42.5f,
                "A real guard strike of 15 must deal exactly 7.5 inside protection.");
            zombieUnit.Combat.TakeDamage(1);
            Require(zombieUnit.Combat.CurrentHealth == 42, "A protected hit of 1 must deal 0.5 without rounding away damage.");
            float leaderHP = leader.CurrentHealth;
            leader.Combat.TakeDamage(10);
            Require(leader.CurrentHealth == leaderHP - 5, "Necromancer must also take half damage.");
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CivilianSetup.PrefabPath),
                center, Quaternion.identity).GetComponent<Civilian>();
            human.enabled = false;
            human.Combat.TakeDamage(10);
            guard.Combat.TakeDamage(10);
            Require(human.Combat.CurrentHealth == 20 && guard.Combat.CurrentHealth == 90,
                "Living humans and guards must not gain protection inside the circle.");
            energy.Restore(100);
            Require(!ability.TryCastAtScreenPosition(ScreenPoint(manager, guard.transform.position + Vector3.up * 0.8f))
                && energy.Current == 100,
                "Unit collider must block ground casting.");
            zombieUnit.transform.position = center + Vector3.right * (area.Radius + 0.01f);
            zombieUnit.Combat.TakeDamage(10);
            Require(zombieUnit.Combat.CurrentHealth == 32, "Leaving the area must immediately restore full incoming damage.");
            zombieUnit.transform.position = center + Vector3.right * area.Radius;
            zombieUnit.Combat.TakeDamage(1);
            Require(zombieUnit.Combat.CurrentHealth == 31.5f, "The visible circle boundary must still protect the unit.");
            Object.Destroy(human.gameObject);
            Object.Destroy(guard.gameObject);
            zombieUnit.transform.position = center;
            area.enabled = false;
            zombieUnit.Combat.TakeDamage(1);
            Require(zombieUnit.Combat.CurrentHealth == 30.5f, "Disabling an area must immediately remove protection.");
            area.enabled = true;
            energy.Restore(100);
            // Cast beside the unit so its collider does not hide the ground.
            Require(ability.TryCastAtScreenPosition(ScreenPoint(manager, center + Vector3.left)),
                "A second paid area must be allowed on visible ground.");
            zombieUnit.Combat.TakeDamage(1);
            Require(zombieUnit.Combat.CurrentHealth == 30, "Overlapping areas must not stack damage reduction.");
            Debug.Log("PASS: guard melee 15 -> 7.5, fractional hits, necromancer/zombie protection, living exclusions, boundary/exit and overlap.");

            Time.timeScale = 0;
            yield return 0.4f;
            Require(area && area.IsActive, "Pause must freeze area lifetime.");
            Time.timeScale = 1;
            yield return area.Duration + 0.15f;
            Require(!area && Object.FindObjectsByType<ProtectionArea>(FindObjectsSortMode.None).Length == 0,
                "After five game seconds all areas and their visuals must be destroyed.");
            zombieUnit.Combat.TakeDamage(10);
            Require(zombieUnit.Combat.CurrentHealth == 20, "Expiration must remove protection and restore full damage.");
            Object.Destroy(zombieUnit.gameObject);
            Debug.Log("PASS: protection lifetime freezes on pause, expires after 5 game seconds, removes visuals and restores damage.");
        }

        private static Vector2 ScreenPoint(GameManager manager, Vector3 world)
        {
            Physics.SyncTransforms();
            return manager.LevelCamera.WorldToScreenPoint(world);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
