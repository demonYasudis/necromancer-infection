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
    public static class CharacterArtChecks
    {
        private const string Key = "CharacterArtChecks";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static CharacterArtChecks() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            foreach (string path in CharacterArtSetup.Prefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(root.GetComponent<UnitCombat>());
                Require(root.GetComponent<CapsuleCollider>(), "Keep original collider: " + path);
            }
            EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Watch(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = message; }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (routine == null)
                {
                    routine = Checks();
                    deadline = EditorApplication.timeSinceStartup + 75;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Character artwork checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else
                {
                    SessionState.SetBool(Key, false);
                    Debug.Log("PASS: approved artwork on all five prefabs, both levels, spawned zombie conversion, movement, billboard, mirroring, pause and damage feedback.");
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Key, false);
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerator Checks()
        {
            yield return .4;
            var manager = Object.FindFirstObjectByType<GameManager>();
            var leader = manager.Necromancer;
            Check(leader.Combat);
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) Check(guard.GetComponent<UnitCombat>());
            var start = leader.transform.position;
            yield return .25;
            Require(leader.transform.position != start, "Artwork must preserve autonomous movement");
            leader.enabled = false;
            var human = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Civilian>(CivilianSetup.PrefabPath),
                leader.transform.position + Vector3.back * 2, Quaternion.identity);
            Require(human.TryInfect(AssetDatabase.LoadAssetAtPath<Zombie>("Assets/Prefabs/Zombie.prefab"), leader, .2f),
                "Existing infection must still work");
            yield return .4;
            Require(!human, "Infected civilian must convert");
            var zombies = Object.FindObjectsByType<Zombie>(FindObjectsSortMode.None);
            Require(zombies.Length == 1, "Exactly one new zombie");
            var zombie = zombies[0];
            Check(zombie.Combat);
            zombie.enabled = false;
            zombie.Combat.enabled = false; // Cancel an in-flight attack before testing navigation facing.
            var visual = zombie.GetComponentInChildren<CharacterSpriteVisual>();
            yield return .2;
            zombie.transform.position -= manager.LevelCamera.transform.right * .03f;
            yield return .2;
            Require(visual.Image.flipX, "Left movement mirrors artwork");
            Require(Vector3.Dot(visual.transform.forward, manager.LevelCamera.transform.forward) > .999f,
                "Artwork must face level camera while its pose can roll in the image plane");
            zombie.Combat.enabled = true;
            var restingTint = visual.Image.color;
            zombie.Combat.TakeDamage(1);
            yield return .04;
            Require(visual.Image.color.g < .9f, "Visible damage feedback");
            Time.timeScale = 0;
            var tint = visual.Image.color;
            yield return .15;
            Require(visual.Image.color == tint, "Pause freezes feedback");
            Time.timeScale = 1;
            yield return .25;
            Require(visual.Image.color == restingTint, "Hit tint settles to the character palette");

            // Temporary lineup in the actual level, never saved into either gameplay scene.
            Vector3 center = leader.transform.position;
            Vector3 right = manager.LevelCamera.transform.right;
            zombie.transform.position = center - right * 1.1f;
            var guardCopy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Guard.prefab"),
                center + right * 1.2f, Quaternion.identity);
            guardCopy.GetComponent<Guard>().enabled = false;
            yield return .8;
            Directory.CreateDirectory("Previews");
            var camera = new GameObject("CharacterArtCapture").AddComponent<Camera>();
            camera.CopyFrom(manager.LevelCamera);
            camera.enabled = false;
            camera.transform.rotation = manager.LevelCamera.transform.rotation;
            camera.transform.position = center + Vector3.up * .6f - camera.transform.forward * 8;
            camera.orthographicSize = 1.7f;
            PeasantRigSetup.Capture(camera, "Previews/approved-characters-in-game.png");
            Object.Destroy(camera.gameObject);
            SceneManager.LoadScene(DemoLevelSetup.ScenePath);
            yield return .4;
            Check(Object.FindFirstObjectByType<Necromancer>().Combat);
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) Check(guard.GetComponent<UnitCombat>());
        }

        private static void Check(UnitCombat unit)
        {
            var visuals = unit.GetComponentsInChildren<CharacterSpriteVisual>(true);
            Require(visuals.Length == 1 && visuals[0].Image && visuals[0].Image.sprite, "Exactly one assigned character sprite: " + unit.name);
            foreach (var mesh in unit.GetComponentsInChildren<MeshRenderer>(true))
                Require(!mesh.enabled, "Old geometry must be hidden: " + unit.name);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
