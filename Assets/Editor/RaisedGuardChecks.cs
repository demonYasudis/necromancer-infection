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
            var settings = new SerializedObject(leader.Energy);
            settings.FindProperty("regenerationPerSecond").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            leader.Energy.TrySpend(leader.Energy.Current);
            var defender = Object.Instantiate(guardPrefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Guard>();
            defender.enabled = false;
            Time.timeScale = 0;
            Require(!defender.Combat.TakeDamage(1000) && Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length == 0,
                "Pause cannot kill or resurrect a guard.");
            Time.timeScale = 1;
            Require(defender.Combat.TakeDamage(1000) && !defender.Combat.TakeDamage(1000), "A guard dies only once.");
            Require(Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 0
                && Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length == 1 && manager.GuardsKilled == 1,
                "Death immediately creates one allied knight with no corpse target.");
            var heavy = Object.FindFirstObjectByType<RaisedGuard>();
            var zombie = heavy.GetComponent<Zombie>();
            zombie.enabled = false;
            Require(zombie.Leader == leader && zombie.Combat.CurrentHealth == 150 && zombie.Combat.Damage == 12
                && !heavy.GetComponent<Guard>() && !heavy.GetComponent<GuardRemains>(), "Healthy heavy undead joins the leader without a resurrection loop component.");
            Require(leader.Energy.Current == 8 && manager.InfectedCount == 0,
                "Zero initial mana must not block resurrection; only the normal +8 kill reward is applied.");
            var motion = heavy.GetComponent<CombatMotion>();
            Require(motion.CurrentTransformation == CombatMotion.Transformation.HeavyRise, "Automatic resurrection plays rising animation.");
            Time.timeScale = 0;
            float progress = motion.TransformationProgress;
            yield return .2f;
            Require(motion.TransformationProgress == progress, "Pause freezes rising animation.");
            Time.timeScale = 1;
            yield return .9f;
            Require(motion.CurrentTransformation == CombatMotion.Transformation.None
                && manager.GetComponent<GameFeedback>().HordeSize == 2, "Rising finishes and heavy is included in the horde.");
            foreach (string prefabPath in new[] { FarmAssaultSetup.GuardPath, "Assets/Prefabs/JourneyFinalGuard.prefab" })
            {
                int before = Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length;
                var final = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), new Vector3(-1, 0, 2), Quaternion.identity).GetComponent<Guard>();
                final.enabled = false;
                final.Combat.TakeDamage(1000);
                Require(Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length == before + 1
                    && Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 0, "Wave and tutorial knights also resurrect without corpses.");
                foreach (var raised in Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None))
                    if (raised != heavy) Object.Destroy(raised.gameObject);
                yield return .1f;
            }
            Debug.Log("PASS: automatic resurrection at zero mana, normal kill reward only, one ally per knight, all knight prefabs, rising animation and pause.");

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
            Require(defender.Combat.TryAttack(zombie.Combat), "Defender must start a melee swing.");
            yield return defender.Combat.MeleeWindup + 0.05f;
            Require(zombie.Combat.CurrentHealth == health - 15,
                "A raised guard must be a valid target for living defenders.");
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            var area = Object.Instantiate(protection.AreaPrefab, zombie.transform.position, Quaternion.identity);
            yield return 1.05f;
            health = zombie.Combat.CurrentHealth;
            Require(defender.Combat.TryAttack(zombie.Combat), "Defender must start a protected melee swing.");
            yield return defender.Combat.MeleeWindup + 0.05f;
            Require(zombie.Combat.CurrentHealth == health - 7.5f,
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
            int remainsBefore = Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length;
            zombie.Combat.TakeDamage(10000);
            yield return 0.1f;
            Require(Object.FindObjectsByType<RaisedGuard>(FindObjectsSortMode.None).Length == remainsBefore - 1 && Object.FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None).Length == 0,
                "Heavy undead cannot create another raisable guard corpse when killed.");
            Debug.Log("PASS: automatic resurrection, autonomous heavy following/combat, guard targeting, 50% protection and no resurrection loop after heavy death.");
            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && manager.TryRestartLevel(), "Defeat/retry must still work after raising and combat.");
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
