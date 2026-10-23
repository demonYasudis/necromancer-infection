using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class CrossbowChecks
    {
        // Legacy fixtures isolate their own opponents; keep the newly authored sentry out of those fights.
        public static void DisableSceneArchers()
        {
            foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None))
                archer.gameObject.SetActive(false);
        }

        public static IEnumerator Checks()
        {
            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.4f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            Require(Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None).Length == 1,
                "The authored demo must include one crossbowman behind the patrol.");
            DisableSceneArchers();
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
            leader.transform.position = new Vector3(2, 0, -3);
            var energySettings = new SerializedObject(leader.Energy);
            energySettings.FindProperty("regenerationPerSecond").floatValue = 0;
            energySettings.ApplyModifiedPropertiesWithoutUndo();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.PrefabPath);
            var archer = Object.Instantiate(prefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Crossbowman>();
            var ally = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath),
                new Vector3(-1, 0, -3), Quaternion.identity).GetComponent<Guard>();
            ally.enabled = false;
            Physics.SyncTransforms();
            yield return 0.2f;
            Require(archer.Combat.MaxHealth == 60 && archer.Combat.Damage == 12 && archer.Combat.AttackRange == 7
                && archer.AimDuration == 1.5f && archer.State == Crossbowman.BehaviourState.Aiming
                && archer.CurrentTarget == leader.Combat, "Sentry must acquire an undead target at range with authored stats.");
            Require(archer.transform.Find("AimWarning").GetComponent<LineRenderer>().enabled
                && archer.transform.Find("TargetWarning").GetComponent<LineRenderer>().enabled,
                "Aiming must display a visible line and target ring before any damage.");
            Require(leader.CurrentHealth == 100 && archer.ShotsFired == 0, "Windup cannot deal instant damage.");
            float remaining = archer.AimRemaining;
            Time.timeScale = 0;
            yield return 0.25f;
            Require(archer.AimRemaining == remaining && archer.ShotsFired == 0, "Pause must freeze the warning timer.");
            Time.timeScale = 1;

            // Respond to the real warning using the actual paid RMB raycast onto nearby free ground.
            var protection = Object.FindFirstObjectByType<ProtectionAbility>();
            Vector2 screen = manager.LevelCamera.WorldToScreenPoint(leader.transform.position + Vector3.left * 1.5f);
            Physics.SyncTransforms();
            Require(protection.TryCastAtScreenPosition(screen) && leader.Energy.Current == 75,
                "A player must be able to place paid protection during the 1.5-second warning.");
            Capture(manager.LevelCamera, new Vector3(-1, 0, -3));
            yield return archer.AimRemaining + 0.45f;
            Require(archer.ShotsFired == 1 && leader.CurrentHealth == 94,
                "The first real bolt must hit for 12 -> 6 damage under protection.");
            Require(!archer.transform.Find("AimWarning").GetComponent<LineRenderer>().enabled,
                "Release must clear the warning during reload.");
            foreach (var area in Object.FindObjectsByType<ProtectionArea>(FindObjectsSortMode.None)) Object.Destroy(area.gameObject);
            int fired = archer.ShotsFired;
            yield return 0.5f;
            Require(archer.ShotsFired == fired, "Reload must prevent an immediate second shot.");
            double timeout = EditorApplication.timeSinceStartup + 5;
            while (archer.ShotsFired == fired && EditorApplication.timeSinceStartup < timeout) yield return 0.05f;
            yield return 0.4f;
            Require(archer.ShotsFired == 2 && leader.CurrentHealth == 82,
                "An unprotected second bolt must deal 12 damage after reload and a fresh warning.");
            Require(ally.Combat.CurrentHealth == 100, "Firing from behind a friendly guard cannot damage that guard.");
            Object.Destroy(ally.gameObject);
            Debug.Log("PASS: crossbow warning, paid protection during windup, real bolt damage 12 -> 6, reload, unprotected 12 damage and firing past a friendly guard.");

            // Loss of line of sight or range cancels an in-progress shot instead of skipping its warning.
            Object.Destroy(archer.gameObject);
            yield return 0.1f;
            archer = Object.Instantiate(prefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Crossbowman>();
            yield return 0.2f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "CrossbowCheckWall";
            wall.transform.position = new Vector3(-1, 1, -3);
            wall.transform.localScale = new Vector3(0.4f, 2, 3);
            Physics.SyncTransforms();
            yield return 0.2f;
            Require(archer.State == Crossbowman.BehaviourState.Idle && archer.ShotsFired == 0,
                "A wall introduced during windup must cancel the shot.");
            yield return 1.6f;
            Require(archer.ShotsFired == 0 && leader.CurrentHealth == 82, "The sentry cannot fire through a wall.");
            Object.Destroy(wall);
            leader.transform.position = new Vector3(5, 0, -3);
            yield return 0.2f;
            Require(archer.State == Crossbowman.BehaviourState.Idle && !archer.CurrentTarget, "Out-of-range undead cannot be acquired.");
            leader.transform.position = new Vector3(2, 0, -3);
            yield return 0.2f;
            Require(archer.State == Crossbowman.BehaviourState.Aiming && archer.AimRemaining > 1,
                "Reacquisition must start a fresh full warning.");
            archer.enabled = false;
            Require(!archer.CurrentTarget && !archer.transform.Find("TargetWarning").GetComponent<LineRenderer>().enabled,
                "Disabling an archer must immediately clear its target and warning.");
            yield return 1.6f;
            Require(archer.ShotsFired == 0 && leader.CurrentHealth == 82, "A cancelled windup cannot produce a ghost bolt.");
            Object.Destroy(archer.gameObject);
            yield return 0.1f;
            Debug.Log("PASS: walls/range cancel crossbow windup; reacquisition requires a full warning; disable cancels without a ghost shot.");

            // A wall that appears after release also blocks the moving bolt. Pause freezes its flight.
            var source = Object.Instantiate(prefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Crossbowman>();
            source.enabled = false;
            var bolt = CrossbowBolt.Launch(source.Combat, new Vector3(-3, 0.8f, -3),
                leader.transform.position + Vector3.up * 0.8f, 5, 8);
            Vector3 pausedPosition = bolt.transform.position;
            Time.timeScale = 0;
            yield return 0.2f;
            Require(bolt && bolt.transform.position == pausedPosition, "Pause must freeze a bolt already in flight.");
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-1, 1, -3);
            wall.transform.localScale = new Vector3(0.4f, 2, 3);
            Physics.SyncTransforms();
            Time.timeScale = 1;
            yield return 0.7f;
            Require(!bolt && leader.CurrentHealth == 82, "A moving bolt must stop at the first solid wall without damage beyond it.");
            Object.Destroy(wall);

            var zombiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfectionSetup.PrefabPath).GetComponent<Zombie>();
            var zombie = Object.Instantiate(zombiePrefab, new Vector3(1, 0, -3), Quaternion.identity);
            zombie.Initialize(leader);
            zombie.enabled = false;
            source.enabled = true;
            yield return 0.2f;
            Require(source.CurrentTarget == zombie.Combat, "The sentry must also acquire nearby zombies rather than only the necromancer.");
            yield return source.AimRemaining + 0.4f;
            Require(zombie.Combat.CurrentHealth == 38 && leader.CurrentHealth == 82, "A bolt must hit the first undead in its path.");
            Physics.SyncTransforms();
            screen = manager.LevelCamera.WorldToScreenPoint(zombie.transform.position + Vector3.back * 1.5f);
            Require(protection.TryCastAtScreenPosition(screen), "Protection must also be castable near the targeted zombie.");
            fired = source.ShotsFired;
            timeout = EditorApplication.timeSinceStartup + 5;
            while (source.ShotsFired == fired && EditorApplication.timeSinceStartup < timeout) yield return 0.05f;
            yield return 0.4f;
            Require(source.ShotsFired == fired + 1 && zombie.Combat.CurrentHealth == 32,
                "A real protected zombie must also receive 12 -> 6 bolt damage.");
            source.enabled = false;
            Require(!LivingTarget.FindNearest(zombie.transform.position, 7, leader.transform.position, 8),
                "Disabled ranged AI must leave the zombie's target selection, like a disabled guard.");
            source.enabled = true;
            Require(LivingTarget.FindNearest(zombie.transform.position, 7, leader.transform.position, 8) == source.Combat,
                "An enabled crossbowman must be a valid target for the horde.");
            var infection = Object.FindFirstObjectByType<InfectionAbility>();
            Physics.SyncTransforms();
            float energyBefore = leader.Energy.Current;
            screen = manager.LevelCamera.WorldToScreenPoint(source.transform.position + Vector3.up * 0.8f);
            Require(!infection.TryInfectAtScreenPosition(screen) && leader.Energy.Current == energyBefore,
                "Crossbowmen are defenders and cannot be infected by clicking them.");
            int beforeKills = manager.GuardsKilled;
            source.Combat.TakeDamage(1000, zombie.Combat);
            Require(manager.GuardsKilled == beforeKills + 1 && leader.Energy.Current == energyBefore + 8,
                "A crossbowman death must award the defender reward and count once in results.");
            yield return 1.7f;
            Require(Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 1,
                "Killing a crossbowman must not produce a civilian chain conversion.");
            Require(zombie.Combat.CurrentHealth == 32 && leader.CurrentHealth == 82,
                "A killed archer's unfinished warning cannot fire a ghost bolt.");
            Debug.Log("PASS: bolt pause/wall collision, zombie targeting, defender target lifecycle, infection rejection and one +8/statistics reward without conversion.");

            source = Object.Instantiate(prefab, new Vector3(-4, 0, -3), Quaternion.identity).GetComponent<Crossbowman>();
            zombie.transform.position = new Vector3(-5, 0, -3);
            zombie.enabled = true;
            Physics.SyncTransforms();
            beforeKills = manager.GuardsKilled;
            energyBefore = leader.Energy.Current;
            timeout = EditorApplication.timeSinceStartup + 8;
            while (source && zombie && EditorApplication.timeSinceStartup < timeout) yield return 0.1f;
            Require(!source && zombie && zombie.Combat.IsAlive && manager.GuardsKilled == beforeKills + 1
                && leader.Energy.Current == energyBefore + 8,
                "A real autonomous zombie must find, approach and kill the ranged defender, granting its reward once.");
            Debug.Log("PASS: autonomous zombie defeats a crossbowman through the real target selection, melee combat and death/reward flow.");

            leader.Combat.TakeDamage(1000);
            Require(manager.IsDefeated && manager.TryRestartLevel(), "Crossbow scenario must support the ordinary defeat/retry flow.");
            yield return 0.4f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            var freshArcher = Object.FindFirstObjectByType<Crossbowman>();
            Require(fresh && !fresh.IsFinished && fresh.GuardsKilled == 0 && fresh.Necromancer.CurrentHealth == 100
                && freshArcher && freshArcher.Combat.CurrentHealth == 60 && freshArcher.ShotsFired == 0
                && Object.FindObjectsByType<CrossbowBolt>(FindObjectsSortMode.None).Length == 0,
                "Retry must restore the authored sentry and clean counters/projectiles.");
            Debug.Log("PASS: retry restores one crossbowman at full health, zero shot/kill counters and no old bolts.");
        }

        private static void Capture(Camera source, Vector3 center)
        {
            var camera = new GameObject("CrossbowCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 4;
                camera.transform.position = center + new Vector3(0, 10, -7);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/crossbow-warning.png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
                Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
