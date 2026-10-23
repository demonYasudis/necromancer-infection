using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class RaisedGuardChecks
    {
        public static IEnumerator Checks()
        {
            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            CrossbowChecks.DisableSceneArchers();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            foreach (var body in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (body != manager.Ground && !body.GetComponentInParent<UnitCombat>())
                {
                    body.enabled = false;
                    if (body.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
                }
            var leader = manager.Necromancer;
            leader.enabled = false;
            leader.transform.position = new Vector3(1, 0, -3);
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            var settings = new SerializedObject(leader.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            var defender = Object.Instantiate(guardPrefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false;
            Require(defender.GetComponent<GuardRemains>() && ability.RaiseGuardCost == 35, "Guards must have configured remains and a cost of 35.");
            Require(defender.Combat.TakeDamage(1000) && !defender.Combat.TakeDamage(1000), "A guard can die only once.");
            Require(Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 1 && manager.GuardsKilled == 1,
                "One real guard death must produce exactly one corpse and one kill statistic.");
            var corpse = Object.FindFirstObjectByType<GuardCorpse>();
            Require(!corpse.GetComponent<UnitCombat>() && !corpse.GetComponent<LivingTarget>()
                && !corpse.GetComponent<UndeadPresence>() && corpse.GetComponent<Collider>().isTrigger,
                "A corpse must be an interactable, non-blocking remnant rather than a living combat target.");
            leader.Energy.TrySpend(66);
            Physics.SyncTransforms();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(corpse.transform.position + Vector3.up * 0.3f);
            Require(!ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 34 && !corpse.IsConsumed,
                "At 34 energy a corpse must remain available without any resource loss.");
            Time.timeScale = 0;
            leader.Energy.Restore(1);
            Require(!ability.TryInfectAtScreenPosition(screen) && !corpse.TryRaise(leader, 35), "Pause must block both raycast and direct raising.");
            Time.timeScale = 1;
            Require(ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == 0 && corpse.IsConsumed,
                "A real LMB raycast at exactly 35 energy must raise one guard and spend exactly 35, without a conversion rebate.");
            Require(!ability.TryInfectAtScreenPosition(screen) && !corpse.TryRaise(leader, 0), "Repeated clicks cannot reuse consumed remains.");
            yield return 0.2f;
            var heavy = Object.FindFirstObjectByType<RaisedGuard>();
            var zombie = heavy.GetComponent<Zombie>();
            zombie.enabled = false;
            Require(!corpse && zombie.Leader == leader && zombie.Combat.CurrentHealth == 150 && zombie.Combat.Damage == 12
                && new SerializedObject(zombie).FindProperty("moveSpeed").floatValue == 2.2f
                && !heavy.GetComponent<Guard>() && !heavy.GetComponent<GuardRemains>(), "Raised guard must be a healthy slow undead, not a living defender.");
            Require(manager.InfectedCount == 0 && manager.GetComponent<GameFeedback>().HordeSize == 2,
                "Raising adds to the horde but must not count as an infected civilian.");
            Debug.Log("PASS: one corpse per death; paid raycast raising at exactly 35, insufficient energy/pause/duplicate rejection; 150 HP heavy joins horde without infection statistics or +5 rebate.");

            // Both final-wave and ordinary guards leave the same usable remains.
            var final = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(FarmAssaultSetup.GuardPath),
                new Vector3(-1, 0, 2), Quaternion.identity).GetComponent<Guard>();
            final.enabled = false;
            final.Combat.TakeDamage(1000);
            corpse = Object.FindFirstObjectByType<GuardCorpse>();
            Require(corpse && Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 1,
                "A real final guard death must also leave usable remains.");
            // Geometry must still occlude LMB selection of the trigger behind it.
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = corpse.transform.position + new Vector3(0, 1.5f, -1);
            blocker.transform.localScale = new Vector3(3, 3, 0.4f);
            leader.Energy.Restore(100);
            screen = manager.LevelCamera.WorldToScreenPoint(corpse.transform.position + Vector3.up * 0.3f);
            Physics.SyncTransforms();
            float before = leader.Energy.Current;
            Require(!ability.TryInfectAtScreenPosition(screen) && leader.Energy.Current == before,
                "A building in front of remains must block resurrection without spending energy.");
            Object.Destroy(blocker);
            yield return 0.1f;
            Capture(manager.LevelCamera, new Vector3(-1, 0, -1.5f));
            Object.Destroy(corpse.gameObject);

            zombie.transform.position = new Vector3(-3, 0, -3);
            zombie.enabled = true;
            yield return 0.6f;
            Require(zombie.transform.position.x > -2.5f && zombie.State == Zombie.BehaviourState.Following,
                "A heavy guard must autonomously follow its leader.");
            zombie.enabled = false;
            zombie.transform.position = new Vector3(-1, 0, -3);
            defender = Object.Instantiate(guardPrefab, new Vector3(-1.8f, 0, -3), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false;
            float health = zombie.Combat.CurrentHealth;
            Require(defender.Combat.TryAttack(zombie.Combat) && zombie.Combat.CurrentHealth == health - 15,
                "A raised guard must be a valid target for living defenders.");
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            var area = Object.Instantiate(protection.AreaPrefab, zombie.transform.position, Quaternion.identity);
            yield return 1.05f;
            health = zombie.Combat.CurrentHealth;
            Require(defender.Combat.TryAttack(zombie.Combat) && zombie.Combat.CurrentHealth == health - 7.5f,
                "Protection must halve real melee damage on heavy undead too.");
            settings = new SerializedObject(defender);
            settings.FindProperty("necromancerPriorityRadius").floatValue = 0.1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            defender.enabled = true;
            zombie.enabled = true;
            double timeout = EditorApplication.timeSinceStartup + 14;
            while (defender && EditorApplication.timeSinceStartup < timeout) yield return 0.1f;
            Require(!defender && zombie && zombie.Combat.IsAlive, "The heavy must autonomously defeat a 100 HP guard and survive.");
            yield return 0.3f;
            Require(!zombie.CurrentTarget && zombie.State == Zombie.BehaviourState.Following,
                "After combat the heavy must return to following rather than attacking the corpse.");
            int remainsBefore = Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length;
            zombie.Combat.TakeDamage(10000);
            yield return 0.1f;
            Require(Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == remainsBefore,
                "Heavy undead cannot create another raisable guard corpse when killed.");
            Debug.Log("PASS: final guard remains, wall occlusion, autonomous heavy following/combat, guard targeting, 50% protection and no resurrection loop after heavy death.");
            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && !corpse && manager.TryRestartLevel(), "Defeat/retry must still work after raising and combat.");
            yield return 0.4f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            Require(fresh && !fresh.IsFinished && fresh.InfectedCount == 0 && fresh.GuardsKilled == 0
                && Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 0
                && Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length == 0,
                "Retry must remove every corpse/heavy and reset attempt statistics.");
            Debug.Log("PASS: raised-guard defeat/retry restores a clean authored scene with no old remains or heavy undead.");
        }

        private static void Capture(Camera source, Vector3 center)
        {
            var camera = new GameObject("RaisedGuardCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 4;
                camera.transform.position = center + new Vector3(0, 10, -7);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/raised-guard.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
