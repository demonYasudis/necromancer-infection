using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    // Moves only test fixtures; normal gameplay still uses the real route and horde AI.
    public sealed class WalkCheckDriver : MonoBehaviour
    {
        public Vector3 Velocity;
        private void Update() => transform.position += Velocity * Time.deltaTime;
    }

    public static class WalkChecks
    {
        public static IEnumerator Checks()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager.IsFinished) manager.Necromancer.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Walk checks need a fresh attempt.");
            yield return 0.35f;
            manager = Object.FindFirstObjectByType<GameManager>();
            var leader = manager.Necromancer;
            leader.enabled = false;
            leader.transform.position = new Vector3(-4, 0, 0);
            CrossbowChecks.DisableSceneArchers();
            foreach (var unit in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) unit.gameObject.SetActive(false);
            foreach (var unit in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None)) unit.gameObject.SetActive(false);
            var names = new[] { "Zombie", "Zombie", "RaisedGuard", "Skeleton" };
            var units = new GameObject[5];
            units[0] = leader.gameObject;
            for (int i = 1; i < units.Length; i++)
            {
                units[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + names[i - 1] + ".prefab"),
                    new Vector3(-4 + i * 1.8f, 0, 0), Quaternion.identity);
                units[i].GetComponent<Zombie>().enabled = false;
            }
            yield return 0.1f;
            var motions = new CombatMotion[5];
            var drivers = new WalkCheckDriver[5];
            var limbs = new Transform[5];
            var resting = new Vector3[5];
            var rotations = new Quaternion[5];
            for (int i = 0; i < units.Length; i++)
            {
                motions[i] = units[i].GetComponent<CombatMotion>();
                drivers[i] = units[i].AddComponent<WalkCheckDriver>();
                limbs[i] = units[i].transform.Find(i == 0 ? "Staff" : i == 4 ? "Leg_1" : "Leg_L");
                resting[i] = limbs[i].localPosition;
                rotations[i] = limbs[i].localRotation;
                Require(motions[i] && motions[i].GaitWeight == 0, "Stationary new units must have no walking motion.");
            }
            Require(Mathf.Abs(Mathf.DeltaAngle(motions[1].GaitPhase * Mathf.Rad2Deg,
                motions[2].GaitPhase * Mathf.Rad2Deg)) > 2, "Two zombies must start at different gait phases.");
            Require(motions[3].GaitStrideLength > motions[1].GaitStrideLength, "Heavy gait must have a slower cycle at the same speed.");
            var crystal = leader.transform.Find("StaffCrystal");
            var robe = leader.transform.Find("Robe");
            Quaternion restingRobe = robe.localRotation;
            float attachment = Vector3.Distance(crystal.localPosition, limbs[0].localPosition);
            float phase = motions[1].GaitPhase;
            Vector3 start = units[1].transform.position;
            foreach (var driver in drivers) driver.Velocity = Vector3.forward * 2;
            yield return 0.45f;
            float distance = Vector3.Distance(start, units[1].transform.position);
            float expected = Mathf.Repeat(phase + distance / motions[1].GaitStrideLength * Mathf.PI * 2, Mathf.PI * 2);
            Require(Mathf.Abs(Mathf.DeltaAngle(expected * Mathf.Rad2Deg, motions[1].GaitPhase * Mathf.Rad2Deg)) < 3,
                "Step phase must follow travelled distance, independent of frame rate.");
            for (int i = 0; i < units.Length; i++)
                Require(motions[i].GaitWeight > 0.5f && (limbs[i].localPosition != resting[i] || limbs[i].localRotation != rotations[i]),
                    "Moving necromancer, zombies, heavy and skeleton must all animate.");
            Require(Mathf.Abs(Vector3.Distance(crystal.localPosition, limbs[0].localPosition) - attachment) < 0.001f,
                "Staff crystal must remain attached while the staff sways.");
            Require(robe.localRotation != restingRobe, "The necromancer robe must sway with its own delayed phase.");
            Capture(manager.LevelCamera, units, "walk-phase-a");
            yield return 0.18f;
            Capture(manager.LevelCamera, units, "walk-phase-b");
            Time.timeScale = 0;
            phase = motions[1].GaitPhase;
            Vector3 frozen = limbs[1].localPosition;
            Quaternion frozenRotation = limbs[1].localRotation;
            Vector3 frozenRoot = units[1].transform.position;
            yield return 0.3f;
            Require(motions[1].GaitPhase == phase && limbs[1].localPosition == frozen
                && limbs[1].localRotation == frozenRotation && units[1].transform.position == frozenRoot,
                "Pause must freeze root, walking phase and model pose exactly.");
            Time.timeScale = 1;
            yield return 0.1f;
            Require(motions[1].GaitPhase != phase, "Walking must resume after pause.");
            foreach (var driver in drivers) driver.Velocity = Vector3.zero;
            yield return 0.25f;
            phase = motions[1].GaitPhase;
            for (int i = 0; i < units.Length; i++)
                Require(motions[i].GaitWeight == 0 && limbs[i].localPosition == resting[i] && limbs[i].localRotation == rotations[i],
                    "Stopping must restore each authored pose without marching in place.");
            yield return 0.15f;
            Require(motions[1].GaitPhase == phase, "A stopped unit must not advance its stride.");

            drivers[1].Velocity = Vector3.forward;
            yield return 0.3f;
            float slowWeight = motions[1].GaitWeight;
            drivers[1].Velocity = Vector3.forward * 3.2f;
            yield return 0.3f;
            Require(motions[1].GaitWeight > slowWeight * 2, "Slow motion must reduce the gait amplitude.");
            drivers[1].Velocity = Vector3.zero;
            phase = motions[1].GaitPhase;
            units[1].transform.position += Vector3.right * 10;
            yield return 0.1f;
            Require(motions[1].GaitPhase == phase && motions[1].GaitWeight == 0, "Teleport must not generate a giant stride.");
            units[1].SetActive(false);
            units[1].transform.position -= Vector3.right * 10;
            units[1].SetActive(true);
            yield return 0.1f;
            Require(motions[1].GaitWeight == 0, "Reactivation must not animate the distance moved while inactive.");
            for (int i = 0; i < units.Length; i++) Object.Destroy(drivers[i]);
            for (int i = 1; i < units.Length; i++) Object.Destroy(units[i]);
            yield return 0.1f;
            // Check real navigation, beyond the controlled movement fixtures.
            var waypoint = new GameObject("WalkCheckWaypoint");
            waypoint.transform.position = leader.transform.position + Vector3.forward * 2;
            leader.BeginRoute(leader.transform.position, new[] { waypoint.transform });
            leader.enabled = true;
            var follower = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Zombie>(InfectionSetup.PrefabPath),
                leader.transform.position + Vector3.back * 3, Quaternion.identity);
            follower.Initialize(leader);
            yield return 0.4f;
            Require(motions[0].GaitWeight > 0 && follower.GetComponent<CombatMotion>().GaitWeight > 0,
                "Real autonomous route and horde following must drive walking.");
            yield return 1.0f;
            Require(leader.HasArrived && motions[0].GaitWeight == 0, "Route arrival must stop the necromancer animation.");
            Object.Destroy(waypoint);
            leader.Combat.TakeDamage(1000);
            Require(manager.TryRestartLevel(), "Walk checks must support defeat/retry.");
            yield return 0.35f;
            var fresh = Object.FindFirstObjectByType<GameManager>();
            Require(fresh && fresh != manager && Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None).Length == 0
                && fresh.Necromancer.GetComponents<CombatMotion>().Length == 1,
                "Retry must remove old walkers and initialize a single clean motion component.");
            Debug.Log("PASS: distance/speed-driven walking, distinct horde phases, necromancer staff/robe, heavy stride, skeleton, pause, stop, teleport, reactivation, real route/following and retry.");
        }

        private static void Capture(Camera source, GameObject[] units, string filename)
        {
            var camera = new GameObject("WalkCheckCamera").AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var target = new RenderTexture(1440, 810, 24);
            var texture = new Texture2D(1440, 810, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Vector3 center = (units[0].transform.position + units[4].transform.position) * 0.5f + Vector3.up;
                camera.orthographicSize = 3.2f;
                camera.transform.position = center + new Vector3(0, 8, 9);
                camera.transform.LookAt(center);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1440, 810), 0, 0); texture.Apply();
                File.WriteAllBytes("Logs/" + filename + ".png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                Object.Destroy(target); Object.Destroy(texture); Object.Destroy(camera.gameObject);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
