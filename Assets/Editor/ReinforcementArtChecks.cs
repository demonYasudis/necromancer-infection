using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    [InitializeOnLoad]
    public static class ReinforcementArtChecks
    {
        private const string Key = "NecromancerInfection.ReinforcementArtChecks";
        private static IEnumerator routine;
        private static double next, deadline;
        private static string error;
        static ReinforcementArtChecks() => EditorApplication.update += Tick;

        public static void RunBatch()
        {
            foreach (string path in ReinforcementArtSetup.Prefabs)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(root);
                Require(root.GetComponent<CapsuleCollider>(), "Keep original collider: " + path);
                Require(root.GetComponent<UnitCombat>(), "Keep original combat: " + path);
            }
            foreach (string scene in new[] { DemoLevelSetup.ScenePath, JourneySetup.ScenePath })
            {
                EditorSceneManager.OpenScene(scene);
                foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None))
                    Check(archer.gameObject);
            }
            EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
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
                    deadline = EditorApplication.timeSinceStartup + 150;
                    Application.logMessageReceived += Watch;
                }
                Require(error == null, error);
                Require(EditorApplication.timeSinceStartup < deadline, "Reinforcement artwork checks timed out");
                if (EditorApplication.timeSinceStartup < next) return;
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + Convert.ToDouble(routine.Current);
                else
                {
                    SessionState.SetBool(Key, false);
                    Debug.Log("PASS: reinforcement artwork on three prefabs and both levels; real crossbow combat, paid resurrection and grave spawning.");
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
            CaptureLineup();

            // Reuse the gameplay regression suites rather than duplicate their assertions.
            var crossbow = CrossbowChecks.Checks();
            while (crossbow.MoveNext()) yield return crossbow.Current;
            var raised = RaisedGuardChecks.Checks();
            while (raised.MoveNext()) yield return raised.Current;
            var grave = GraveyardChecks.Checks();
            while (grave.MoveNext()) yield return grave.Current;
        }

        public static void CapturePreviewBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
                CaptureLineup();
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        private static void CaptureLineup()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            manager.Necromancer.enabled = false;
            // Hide the scene's other figures and buildings for an unobstructed comparison.
            // This scene is temporary and is never saved by the capture.
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (renderer.gameObject != manager.Ground.gameObject) renderer.enabled = false;
            var camera = new GameObject("ReinforcementArtCapture").AddComponent<Camera>();
            camera.CopyFrom(manager.LevelCamera);
            camera.enabled = false;
            camera.orthographicSize = 1.7f;
            camera.transform.rotation = manager.LevelCamera.transform.rotation;
            Vector3 center = Vector3.zero;
            camera.transform.position = center + Vector3.up * .65f - camera.transform.forward * 9;
            var copies = new GameObject[3];
            for (int i = 0; i < copies.Length; i++)
            {
                copies[i] = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ReinforcementArtSetup.Prefabs[i]),
                    center + camera.transform.right * ((i - 1) * 1.7f), Quaternion.identity);
                foreach (var behaviour in copies[i].GetComponents<MonoBehaviour>()) behaviour.enabled = false;
                var visual = copies[i].GetComponentInChildren<CharacterSpriteVisual>();
                visual.transform.rotation = camera.transform.rotation;
                Check(copies[i]);
            }
            Directory.CreateDirectory("Previews");
            PeasantRigSetup.Capture(camera, "Previews/reinforcement-characters-in-game.png");
            foreach (var copy in copies) Object.DestroyImmediate(copy);
            Object.DestroyImmediate(camera.gameObject);
        }

        private static void Check(GameObject root)
        {
            var visuals = root.GetComponentsInChildren<CharacterSpriteVisual>(true);
            Require(visuals.Length == 1 && visuals[0].Image && visuals[0].Image.sprite,
                "Exactly one assigned sprite: " + root.name);
            foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
                Require(!mesh.enabled, "Hide placeholder mesh: " + root.name);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
