using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    /// <summary>Exercises real movement and combat with a crowded horde, walls and scene reloads.</summary>
    public static class CrowdChecks
    {
        public static IEnumerator Checks()
        {
            SceneManager.LoadSceneAsync(DemoLevelSetup.ScenePath);
            yield return 0.5f;
            var manager = Object.FindFirstObjectByType<GameManager>();
            CrossbowChecks.DisableSceneArchers();
            var leader = manager.Necromancer;
            leader.enabled = false;
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var human in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) human.gameObject.SetActive(false);
            // Isolate the crowd fixture from the village buildings, retaining the real ground.
            foreach (var body in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (body != manager.Ground && !body.GetComponentInParent<UnitCombat>())
                {
                    body.enabled = false;
                    if (body.TryGetComponent<Renderer>(out var visual)) visual.enabled = false;
                }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InfectionSetup.PrefabPath).GetComponent<Zombie>();
            var waypoint = new GameObject("CrowdCheckDestination").transform;
            var corner = new GameObject("CrowdCheckCorner").transform;
            Vector3 start = new Vector3(-9, 0, -3);
            foreach (int size in new[] { 5, 20, 50 })
            {
                leader.enabled = false;
                waypoint.position = start + Vector3.right * 3;
                leader.BeginRoute(start, new[] { waypoint });
                var group = Spawn(prefab, leader, size);
                yield return 5f;
                RequireFront(group, leader, Vector3.right, 1f);
                var settled = new Vector3[size];
                for (int i = 0; i < size; i++) settled[i] = group[i].transform.position;
                yield return .5f;
                for (int i = 0; i < size; i++)
                    Require(Vector3.Distance(settled[i], group[i].transform.position) < .04f,
                        $"{size} zombies must settle without oscillating around their places.");
                leader.enabled = true;
                yield return 1f;
                Require(!leader.HasArrived, "Check the front while the leader is moving.");
                RequireFront(group, leader, Vector3.right, .8f);
                if (size == 50)
                {
                    corner.position = waypoint.position + Vector3.forward * 4;
                    leader.BeginRoute(leader.transform.position, new[] { waypoint, corner });
                    yield return 5f;
                    Require(leader.HasArrived, "The leader must complete the right-angle route.");
                    RequireFront(group, leader, Vector3.forward, 1f);
                    Capture(manager.LevelCamera, leader.transform.position + Vector3.forward * 3, "crowd-following.png");
                }
                foreach (var zombie in group) Object.Destroy(zombie.gameObject);
                yield return .1f;
            }
            Debug.Log("PASS: 5, 20 and 50 overlapping spawns form a stable moving front; 50 reform after a right-angle turn.");

            leader.enabled = false;
            start = new Vector3(-11, 0, -6);
            waypoint.position = new Vector3(7, 0, -6);
            leader.BeginRoute(start, new[] { waypoint });
            var horde = Spawn(prefab, leader, 30);
            yield return 4f;

            var positions = new Vector3[horde.Length];
            for (int i = 0; i < horde.Length; i++) positions[i] = horde[i].transform.position;
            Time.timeScale = 0;
            yield return 0.2f;
            for (int i = 0; i < horde.Length; i++)
                Require(horde[i].transform.position == positions[i], "Pause must also stop separation of crowded units.");
            Time.timeScale = 1;

            // A 2.2-unit passage is much narrower than the settled 30-member formation.
            var walls = new GameObject[2];
            for (int i = 0; i < walls.Length; i++)
            {
                walls[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                walls[i].name = "CrowdCheckWall";
                walls[i].transform.position = new Vector3(0, 0.8f, i == 0 ? -7.6f : -4.4f);
                walls[i].transform.localScale = new Vector3(6, 1.6f, 1);
            }
            Physics.SyncTransforms();
            leader.enabled = true;
            double timeout = EditorApplication.timeSinceStartup + 15;
            while (EditorApplication.timeSinceStartup < timeout)
            {
                bool crossed = leader.HasArrived;
                foreach (var zombie in horde)
                {
                    crossed &= zombie.transform.position.x > 3.3f;
                    foreach (var wall in walls)
                    {
                        Vector3 center = zombie.transform.position + Vector3.up * 0.7f;
                        float clearance = Vector3.Distance(wall.GetComponent<Collider>().ClosestPoint(center), center);
                        Require(clearance >= 0.27f, $"Crowd movement must not cross a solid wall; clearance {clearance:F3}.");
                    }
                    Require(Vector3.Distance(zombie.transform.position, leader.transform.position) <= zombie.MaxHordeDistance + 0.1f,
                        "Separation must respect the horde leash.");
                }
                if (crossed) break;
                yield return 0.1f;
            }
            int through = 0;
            var behind = new List<Vector3>();
            foreach (var zombie in horde)
                if (zombie.transform.position.x > 3.3f) through++;
                else behind.Add(zombie.transform.position);
            Require(leader.HasArrived && through == 30,
                $"The whole horde must pass the narrow opening; passed {through}/30. Behind: {string.Join(", ", behind)}.");
            yield return 3.5f;
            RequireFront(horde, leader, Vector3.right, .9f);
            float gap = MinimumGap(horde);
            Debug.Log($"PASS: 30 zombies follow an autonomous leader through a 2.2-unit passage without crossing walls or the leash; final gap {gap:F3}.");
            foreach (var wall in walls) Object.Destroy(wall);
            foreach (var zombie in horde) Object.Destroy(zombie.gameObject);
            Object.Destroy(waypoint.gameObject);
            Object.Destroy(corner.gameObject);
            yield return 0.1f;

            leader.enabled = false;
            leader.transform.position = new Vector3(-6, 0, -6);
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuardSetup.PrefabPath);
            var defender = Object.Instantiate(guardPrefab, new Vector3(-1, 0, -6), Quaternion.identity).GetComponent<Guard>();
            // Keep the real target alive long enough to inspect several attack cooldowns.
            var settings = new SerializedObject(defender.Combat);
            settings.FindProperty("currentHealth").floatValue = 1000;
            settings.ApplyModifiedPropertiesWithoutUndo();
            settings = new SerializedObject(defender);
            settings.FindProperty("moveSpeed").floatValue = 0;
            settings.FindProperty("necromancerPriorityRadius").floatValue = 0.1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            horde = new Zombie[12];
            for (int i = 0; i < horde.Length; i++)
            {
                horde[i] = Object.Instantiate(prefab, new Vector3(-3 - (i / 4) * 0.7f, 0, -7 + (i % 4) * 0.7f), Quaternion.identity);
                horde[i].Initialize(leader);
            }
            yield return 4f;
            int attackers = 0;
            bool front = false, back = false;
            Zombie frontliner = null;
            var waiting = new List<Zombie>();
            foreach (var zombie in horde)
            {
                if (!zombie) continue;
                if (zombie.State != Zombie.BehaviourState.Attacking)
                {
                    waiting.Add(zombie);
                    continue;
                }
                attackers++;
                front |= zombie.transform.position.x < defender.transform.position.x;
                back |= zombie.transform.position.x > defender.transform.position.x;
                frontliner = zombie;
                Require(Vector3.Distance(zombie.transform.position, defender.transform.position) <= zombie.Combat.AttackRange,
                    "A melee attacker must stay within the real attack range.");
            }
            Require(attackers >= 4 && front && back, $"Zombies must surround the target from both sides; attackers {attackers}.");
            Require(MinimumGap(horde) > 0.4f, "Both attacking and waiting ranks must remain distinguishable.");
            Capture(manager.LevelCamera, defender.transform.position, "crowd-combat.png");
            frontliner.Combat.TakeDamage(1000);
            yield return 1f;
            int replacements = 0;
            foreach (var zombie in horde) if (zombie && zombie.State == Zombie.BehaviourState.Attacking) replacements++;
            Require(replacements >= attackers - 1, "Waiting zombies must fill a dead front-line fighter's place.");
            Require(waiting.Exists(zombie => zombie && zombie.State == Zombie.BehaviourState.Attacking),
                "At least one waiting fighter must actually advance into the vacated attack rank.");
            defender.Combat.TakeDamage(10000);
            yield return 3f;
            foreach (var zombie in horde)
                if (zombie) Require(!zombie.CurrentTarget && zombie.State == Zombie.BehaviourState.Following,
                    "After a target dies, all ranks must release their reservations and return to the leader.");
            Debug.Log("PASS: attack positions surround a real guard, melee range/cooldowns still apply, waiting ranks advance after death and return after combat.");

            var chainChecks = InfectionHordeChecks.ChainInfectionChecks(leader, prefab);
            while (chainChecks.MoveNext()) yield return chainChecks.Current;

            SceneManager.LoadSceneAsync(JourneySetup.ScenePath);
            yield return 0.5f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Require(manager && !manager.IsFinished && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                "Reload must remove all old horde members and slot reservations.");
            foreach (var target in Object.FindObjectsByType<LivingTarget>(FindObjectsSortMode.None))
                target.gameObject.SetActive(false);
            leader = manager.Necromancer;
            Transform arrival = manager.Farm.ArrivalPoint;
            leader.transform.rotation = Quaternion.identity;
            leader.BeginRoute(arrival.position, new[] { arrival });
            horde = Spawn(prefab, leader, 5);
            Require(manager.Farm.TryBeginFinalPhase(leader), "Start the real farm assault.");
            yield return 2.65f;
            Vector3 cameraForward = Vector3.ProjectOnPlane(manager.LevelCamera.transform.forward, Vector3.up).normalized;
            Require(Vector3.Dot(cameraForward, Vector3.back) > .98f, "The finale camera looks toward the road.");
            RequireFront(horde, leader, cameraForward, .85f);
            leader.Combat.TakeDamage(10000);
            Require(manager.TryRestartLevel(), "Retry the level after the finale.");
            yield return .6f;
            manager = Object.FindFirstObjectByType<GameManager>();
            Require(manager && !manager.FinalPhase && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0,
                "Retry must reset the encounter and formation.");
            Debug.Log("PASS: the horde turns with the camera for the farm assault; pause and retry reset verified.");
        }

        private static Zombie[] Spawn(Zombie prefab, Necromancer leader, int count)
        {
            var units = new Zombie[count];
            for (int i = 0; i < units.Length; i++)
            {
                units[i] = Object.Instantiate(prefab, leader.transform.position, Quaternion.identity);
                units[i].Initialize(leader);
            }
            return units;
        }

        private static void RequireFront(Zombie[] units, Necromancer leader, Vector3 forward, float minimumLead)
        {
            float minSide = float.PositiveInfinity, maxSide = float.NegativeInfinity;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (var unit in units)
            {
                Vector3 offset = unit.transform.position - leader.transform.position;
                float lead = Vector3.Dot(offset, forward);
                Require(lead >= minimumLead && offset.magnitude < unit.MaxHordeDistance,
                    $"{units.Length} zombies must hold the front inside the leash; lead {lead:F2}, offset {offset}.");
                float side = Vector3.Dot(offset, right);
                minSide = Mathf.Min(minSide, side);
                maxSide = Mathf.Max(maxSide, side);
            }
            float gap = MinimumGap(units);
            Require(gap > .48f, $"{units.Length} zombies must separate; closest pair {gap:F3}.");
            Require(maxSide - minSide > (units.Length >= 20 ? 5 : 2.4f),
                $"{units.Length} zombies must spread into a broad front; width {maxSide - minSide:F2}.");
        }

        private static float MinimumGap(Zombie[] horde)
        {
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < horde.Length; i++)
                for (int j = i + 1; j < horde.Length; j++)
                    if (horde[i] && horde[j]) minimum = Mathf.Min(minimum,
                        Vector3.Distance(horde[i].transform.position, horde[j].transform.position));
            return minimum;
        }

        private static void Capture(Camera source, Vector3 center, string filename)
        {
            var camera = new GameObject("CrowdCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographicSize = 5;
                camera.transform.position = center + new Vector3(0, 10, -7);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(Path.Combine("Logs", filename), image.EncodeToPNG());
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
