using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class VillageArtChecks
    {
        private const string Key = "VillageArtChecks.Running";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;

        static VillageArtChecks() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            foreach (string path in VillageArtSetup.Scenes)
            {
                EditorSceneManager.OpenScene(path);
                CheckScenery(path);
            }
            EditorSceneManager.OpenScene(TutorialLevelSetup.ScenePath(1));
            Directory.CreateDirectory("Previews");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void CheckScenery(string path)
        {
            var game = Object.FindFirstObjectByType<GameManager>();
            var scenery = GameObject.Find(VillageArtSetup.SceneryName);
            Require(scenery && scenery.transform.Find("Farmhouse") && scenery.transform.Find("Farmyard Floor"),
                path + ": farm and yard artwork installed");
            Require(game.Ground.GetComponent<Renderer>().sharedMaterial.shader.name == "Necromancer/Village Surface",
                path + ": ground uses the pixel atlas");
            var waypoints = new SerializedObject(game).FindProperty("intermediateWaypoints");
            Vector3 last = game.StartPoint.position;
            Physics.SyncTransforms();
            for (int i = 0; i <= waypoints.arraySize; i++)
            {
                var end = i == waypoints.arraySize ? game.Farm.ArrivalPoint.position
                    : ((Transform)waypoints.GetArrayElementAtIndex(i).objectReferenceValue).position;
                Vector3 delta = end - last;
                foreach (var hit in Physics.SphereCastAll(last + Vector3.up, 3.85f, delta.normalized, delta.magnitude))
                    Require(!hit.transform.IsChildOf(scenery.transform) && hit.transform.name != "House_2",
                        path + ": new buildings must clear the wide formation near " + hit.transform.name);
                last = end;
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (routine == null)
                {
                    routine = Checks();
                    deadline = EditorApplication.timeSinceStartup + 100;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Village checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) { next = EditorApplication.timeSinceStartup + (double)routine.Current; return; }
                Finish(0, "PASS: six decorated scenes, clear routes, full tutorial, character poses, live horde battle, farm finale and three gameplay captures.");
            }
            catch (Exception exception) { Finish(1, exception.ToString()); }
        }

        private static void Watch(string message, string stack, LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message;
        }

        private static void Finish(int result, string message)
        {
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= Watch;
            Time.timeScale = 1;
            if (result == 0) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(result);
        }

        private static IEnumerator Checks()
        {
            yield return .4;
            var game = Object.FindFirstObjectByType<GameManager>();
            game.GetComponent<OnboardingGuide>().Skip();
            Time.timeScale = 8;
            double arrivalDeadline = EditorApplication.timeSinceStartup + 50;
            while (!game.IsFinished && EditorApplication.timeSinceStartup < arrivalDeadline) yield return .2;
            Require(game.IsVictorious && game.ElapsedSeconds > 110
                && Vector3.Distance(game.Necromancer.transform.position, game.Farm.transform.position) <= game.Farm.ActivationRadius,
                "Complete the entire decorated Tutorial1 route via normal movement.");
            yield return .2;
            var windows = GameObject.Find("Village Scenery/Farmhouse/Lamplight").GetComponent<Renderer>();
            var emission = new MaterialPropertyBlock();
            windows.GetPropertyBlock(emission);
            Color glow = emission.GetColor("_EmissionColor");
            Require(glow.g > glow.r, "Farm windows turn green on real victory.");
            Debug.Log("PASS: full tutorial traversal and corrupted farm windows.");

            SceneManager.LoadScene(JourneySetup.ScenePath);
            yield return .5;
            game = Object.FindFirstObjectByType<GameManager>();
            game.GetComponent<SoulProgression>().enabled = false;
            game.GetComponent<OnboardingGuide>().Skip();
            var leader = game.Necromancer;
            var camera = game.LevelCamera;
            camera.aspect = 1280f / 720;
            var waypoint = new GameObject("Village art check waypoint").transform;
            Vector3 district = new Vector3(-21.45f, 0, 0);
            waypoint.position = district + Vector3.right * 8;
            leader.BeginRoute(district, new[] { waypoint });
            leader.enabled = false;
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) guard.gameObject.SetActive(false);
            foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None)) archer.gameObject.SetActive(false);
            yield return .7;
            Capture(camera, "village-road.png");

            Zombie check = null;
            for (int i = 0; i < 32; i++)
            {
                string name = i == 5 || i == 7 ? "RaisedGuard" : i == 13 || i == 17 ? "Skeleton" : "Zombie";
                Vector3 position = district + new Vector3(2.3f + i / 8 * .81f, 0, (i % 8 - 3.5f) * .78f);
                var zombie = Spawn<Zombie>(name, position);
                zombie.Initialize(leader);
                if (i == 15) check = zombie;
            }
            var defender = Spawn<Guard>("JourneyFinalGuard", district + new Vector3(7, 0, .8f));
            Spawn<Crossbowman>("Crossbowman", district + new Vector3(8.3f, 0, -2.65f));
            yield return .8;
            var image = check.GetComponentInChildren<CharacterSpriteVisual>();
            var direction = new SerializedObject(image).FindProperty("rear").objectReferenceValue as Sprite;
            Require(direction && image.Image.sprite, "Directional sprite is assigned in the actual prefab.");
            check.enabled = false;
            check.Combat.enabled = false;
            Vector3 travel = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized * .13f;
            check.transform.position += travel;
            yield return .1;
            Require(image.Image.sprite == direction, "A unit heading away shows the painted rear direction.");
            check.transform.position -= travel;
            yield return .1;
            Require(image.Image.sprite != direction, "A unit turning toward the camera restores the approved front art.");
            check.Combat.enabled = true;
            check.enabled = true;
            double battleDeadline = EditorApplication.timeSinceStartup + 3;
            while (defender && !defender.Combat.IsMeleeAttacking && EditorApplication.timeSinceStartup < battleDeadline)
                yield return .06;
            Require(defender && defender.Combat.IsMeleeAttacking, "Capture a real melee windup in the village.");
            Capture(camera, "village-horde-battle.png");

            var visual = defender.GetComponentInChildren<CharacterSpriteVisual>();
            var dying = visual.gameObject;
            defender.Combat.TakeDamage(10000);
            yield return .13;
            Require(!defender && dying && dying.transform.parent == null && dying.name == "Falling character",
                "The visual falls while the defeated gameplay object is gone.");
            yield return .5;
            Require(!dying, "Death animation is removed after fading.");

            foreach (var zombie in Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None)) Object.Destroy(zombie.gameObject);
            foreach (var living in Object.FindObjectsByType<LivingTarget>(FindObjectsSortMode.None)) living.gameObject.SetActive(false);
            leader.Energy.Restore(100);
            leader.transform.rotation = Quaternion.identity;
            Transform arrival = game.Farm.ArrivalPoint;
            leader.BeginRoute(arrival.position, new[] { arrival });
            leader.enabled = true;
            Require(game.Farm.TryBeginFinalPhase(leader), "Trigger the authored farm encounter.");
            for (int i = 0; i < 6; i++)
                Spawn<Zombie>("Zombie", arrival.position + new Vector3((i % 3 - 1) * .7f, 0, -1.5f - i / 3)).Initialize(leader);
            var assault = game.Farm.GetComponent<FarmAssault>();
            double waveDeadline = EditorApplication.timeSinceStartup + 6;
            while (assault.WavesSpawned == 0 && EditorApplication.timeSinceStartup < waveDeadline) yield return .12;
            Require(assault.WavesSpawned == 1 && Vector3.Dot(camera.transform.forward, Vector3.back) > .45f,
                "First real reinforcement wave appears in the reversed farm view.");
            Capture(camera, "village-farm-finale.png");
        }

        private static T Spawn<T>(string name, Vector3 position) where T : Component =>
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>("Assets/Prefabs/" + name + ".prefab"),
                position, Quaternion.identity);

        private static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine("Previews", name), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
