using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class GuardChecks
    {
        public static IEnumerator Checks(Necromancer leader, Zombie zombiePrefab)
        {
            foreach (var unit in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(unit.gameObject);
            yield return 0.1f;
            leader.transform.position = new Vector3(-6, 0, 0);
            var guard = Defender(Vector3.zero);
            var zombie = Object.Instantiate(zombiePrefab, new Vector3(-0.8f, 0, 0), Quaternion.identity);
            zombie.Initialize(leader);
            zombie.enabled = false;
            yield return 0.1f;
            Require(guard.Combat.MaxHealth == 100 && guard.Combat.Damage == 15, "Guard prefab must have 100 HP and 15 damage.");
            Require(guard.CurrentTarget == zombie.Combat && zombie.Combat.CurrentHealth == 35,
                "Guard must detect and hit a nearby zombie for 15 damage.");
            int zombieHealth = zombie.Combat.CurrentHealth;
            int leaderHealth = leader.CurrentHealth;
            leader.transform.position = new Vector3(0.8f, 0, 0);
            yield return 0.1f;
            Require(guard.CurrentTarget == leader.Combat, "Nearby necromancer must preempt an existing zombie target.");
            yield return 1.05f;
            Require(leader.CurrentHealth < leaderHealth && zombie.Combat.CurrentHealth == zombieHealth,
                "Guard must attack the priority necromancer instead of continuing to hit the zombie.");
            leader.transform.position = new Vector3(-12, 0, 0);
            zombie.gameObject.SetActive(false);
            yield return 0.3f;
            Require(!guard.CurrentTarget, "Disabled undead and out-of-chase targets must be discarded.");
            Object.Destroy(guard.gameObject);
            Object.Destroy(zombie.gameObject);
            yield return 0.1f;
            Debug.Log("PASS: guard stats, undead damage, live necromancer priority and invalid target rejection.");

            guard = Defender(Vector3.zero);
            zombie = Object.Instantiate(zombiePrefab, new Vector3(3, 0, 0), Quaternion.identity);
            zombie.Initialize(leader);
            zombie.enabled = false;
            yield return 0.3f;
            Require(guard.CurrentTarget == zombie.Combat && guard.transform.position.x > 0.2f,
                "Guard must close distance to detected undead.");
            zombie.gameObject.SetActive(false);
            yield return 0.6f;
            Require(!guard.CurrentTarget && Vector3.Distance(guard.transform.position, Vector3.zero) <= 0.3f,
                "Guard must return to its post after losing the target.");
            guard.transform.position = new Vector3(5, 0, 0);
            zombie.transform.position = new Vector3(9, 0, 0);
            zombie.gameObject.SetActive(true);
            yield return 0.1f;
            Require(!guard.CurrentTarget && guard.transform.position.x < 5,
                "Guard must ignore undead beyond its post's chase radius and head home.");
            Object.Destroy(guard.gameObject);
            Object.Destroy(zombie.gameObject);
            yield return 0.1f;
            Debug.Log("PASS: guard pursuit, return to post and bounded chase.");

            leader.transform.position = new Vector3(-6, 0, 0);
            guard = Defender(Vector3.zero);
            zombie = Object.Instantiate(zombiePrefab, new Vector3(-0.8f, 0, 0), Quaternion.identity);
            zombie.Initialize(leader);
            double timeout = EditorApplication.timeSinceStartup + 6;
            while (guard && zombie && EditorApplication.timeSinceStartup < timeout) yield return 0.1f;
            Require(guard && guard.Combat.IsAlive && !zombie,
                "One guard must defeat a lone zombie using actual autonomous combat.");
            Object.Destroy(guard.gameObject);
            yield return 0.1f;
            Debug.Log("PASS: small horde balance — one guard defeats one zombie.");

            guard = Defender(Vector3.zero);
            var horde = new Zombie[4];
            for (int i = 0; i < horde.Length; i++)
            {
                horde[i] = Object.Instantiate(zombiePrefab, new Vector3(-0.8f, 0, -0.45f + i * 0.3f), Quaternion.identity);
                horde[i].Initialize(leader);
            }
            timeout = EditorApplication.timeSinceStartup + 6;
            while (guard && EditorApplication.timeSinceStartup < timeout) yield return 0.1f;
            int survivors = 0;
            foreach (var member in horde) if (member && member.Combat.IsAlive) survivors++;
            Require(!guard && survivors >= 2, "Four zombies must overwhelm one guard with surviving undead.");
            yield return 2f;
            foreach (var member in horde)
                if (member) Require(!member.CurrentTarget && member.State == Zombie.BehaviourState.Following,
                    "Zombies must drop the dead guard and resume following their leader.");
            Debug.Log("PASS: large horde balance — four zombies defeat one guard and resume following.");
        }

        private static Guard Defender(Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            return Object.Instantiate(prefab, position, Quaternion.identity).GetComponent<Guard>();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
