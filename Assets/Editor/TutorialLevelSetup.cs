using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class TutorialLevelSetup
    {
        public static string ScenePath(int level) => $"Assets/Scenes/Tutorial{level}.unity";

        [MenuItem("Necromancer Infection/Create Four Tutorial Levels")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
            for (int level = 1; level <= 4; level++)
                if (File.Exists(ScenePath(level))) throw new InvalidOperationException("Tutorial scenes already exist; edit them in place.");
            for (int level = 1; level <= 4; level++) CreateLevel(level);
            var scenes = new List<EditorBuildSettingsScene>();
            for (int level = 1; level <= 4; level++) scenes.Add(new EditorBuildSettingsScene(ScenePath(level), true));
            scenes.AddRange(EditorBuildSettings.scenes);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath(1));
            Debug.Log("Four tutorial scenes created. Tutorial1 is the build entry point.");
        }

        private static void CreateLevel(int level)
        {
            var scene = EditorSceneManager.OpenScene(JourneySetup.ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath(level));
            var game = Object.FindFirstObjectByType<GameManager>();
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) Object.DestroyImmediate(guard.gameObject);
            foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None)) Object.DestroyImmediate(archer.gameObject);
            foreach (var runner in Object.FindObjectsByType<AlarmRunner>(FindObjectsSortMode.None)) Object.DestroyImmediate(runner.gameObject);
            var bell = Object.FindFirstObjectByType<AlarmBell>();
            if (level != 4) { if (bell) Object.DestroyImmediate(bell.gameObject); bell = null; }
            var assault = game.Farm.GetComponent<FarmAssault>();
            assault.enabled = false;
            if (assault.ClosedGate) assault.ClosedGate.SetActive(false);
            var settings = new SerializedObject(game);
            settings.FindProperty("nextScenePath").stringValue = level < 4 ? ScenePath(level + 1) : "";
            settings.ApplyModifiedPropertiesWithoutUndo();
            var objective = game.gameObject.AddComponent<LevelObjective>();
            var knight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/JourneyFinalGuard.prefab").GetComponent<Guard>();
            var archerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrossbowSetup.PrefabPath).GetComponent<Crossbowman>();
            // Spawn around the arrival point, inside the open farmyard and within attack range.
            var root = new GameObject("TutorialEncounterPoints").transform;
            var points = new Transform[4];
            Vector3 arrival = game.Farm.ArrivalPoint.position;
            Vector3[] offsets = { new Vector3(-1.1f, 0, 1.2f), new Vector3(1.1f, 0, 1.2f),
                new Vector3(-2.4f, 0, 2.8f), new Vector3(2.4f, 0, 2.8f) };
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject("Arrival_" + i).transform;
                points[i].SetParent(root);
                points[i].position = arrival + offsets[i];
            }
            settings = new SerializedObject(objective);
            settings.FindProperty("tutorialLevel").intValue = level;
            settings.FindProperty("knightPrefab").objectReferenceValue = knight;
            settings.FindProperty("archerPrefab").objectReferenceValue = archerPrefab;
            settings.FindProperty("bell").objectReferenceValue = bell;
            SetPoints(settings, "encounterPoints", points[0], points[2]);
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (bell)
            {
                bell.transform.position = arrival + new Vector3(3, 0, 0);
                settings = new SerializedObject(bell);
                settings.FindProperty("tutorialTrigger").boolValue = true;
                settings.FindProperty("runner").objectReferenceValue = null;
                settings.FindProperty("guardPrefab").objectReferenceValue = knight;
                settings.FindProperty("archerPrefab").objectReferenceValue = archerPrefab;
                SetPoints(settings, "patrolPoints", points[0], points[1]);
                SetPoints(settings, "archerPoints", points[2], points[3]);
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetPoints(SerializedObject settings, string field, params Transform[] points)
        {
            var list = settings.FindProperty(field);
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        }
    }
}
