using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class FarmAssaultSetup
    {
        public const string GuardPath = "Assets/Prefabs/FinalGuard.prefab";

        [MenuItem("Necromancer Infection/Add Farm Assault")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save changes before adding the farm assault.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager || !manager.Farm) throw new InvalidOperationException("DemoLevel requires the farm objective.");
            var farm = manager.Farm;
            var finalGuard = AssetDatabase.LoadAssetAtPath<GameObject>(GuardPath);
            if (!finalGuard)
            {
                var source = PrefabUtility.LoadPrefabContents(GuardSetup.PrefabPath);
                try
                {
                    source.name = "FinalGuard";
                    var stats = new SerializedObject(source.GetComponent<Guard>());
                    stats.FindProperty("detectionRadius").floatValue = 12;
                    stats.FindProperty("chaseRadius").floatValue = 14;
                    stats.ApplyModifiedPropertiesWithoutUndo();
                    finalGuard = PrefabUtility.SaveAsPrefabAsset(source, GuardPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(source); }
            }
            var arrival = farm.transform.Find("ArrivalPoint");
            if (!arrival)
            {
                arrival = new GameObject("ArrivalPoint").transform;
                arrival.SetParent(farm.transform, false);
                // Hold outside the closed gate; the autonomous route never crosses its collider.
                arrival.localPosition = new Vector3(0, 0, -2);
                var visual = farm.transform.Find("Visual");
                if (visual) visual.position += Vector3.back * 2;
            }
            var objective = new SerializedObject(farm);
            objective.FindProperty("arrivalPoint").objectReferenceValue = arrival;
            objective.ApplyModifiedPropertiesWithoutUndo();
            var gate = farm.transform.Find("ClosedGate");
            if (!gate)
            {
                gate = new GameObject("ClosedGate").transform;
                gate.SetParent(farm.transform, false);
                gate.localPosition = new Vector3(0, 0, -0.8f);
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeatheredWood.mat");
                for (int i = 0; i < 9; i++)
                    Part(gate, "GateSlat", new Vector3(-1.6f + i * 0.4f, 0.7f, 0), new Vector3(0.3f, 1.4f, 0.16f), wood);
                Part(gate, "GateBrace", new Vector3(0, 0.8f, -0.1f), new Vector3(3.5f, 0.16f, 0.14f), wood);
                var collider = gate.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 0.7f, 0);
                collider.size = new Vector3(3.5f, 1.4f, 0.25f);
                gate.gameObject.SetActive(false);
            }
            var pointsRoot = farm.transform.Find("ReinforcementPoints");
            if (!pointsRoot)
            {
                pointsRoot = new GameObject("ReinforcementPoints").transform;
                pointsRoot.SetParent(farm.transform, false);
                for (int i = 0; i < 8; i++)
                {
                    var point = new GameObject($"FinalGuard_{i + 1:00}").transform;
                    point.SetParent(pointsRoot, false);
                    point.localPosition = new Vector3(new[] { -4f, -2f, 2f, 4f }[i % 4], 0, i < 4 ? -8.5f : -10);
                }
            }
            var assault = farm.GetComponent<FarmAssault>();
            if (!assault) assault = farm.gameObject.AddComponent<FarmAssault>();
            var settings = new SerializedObject(assault);
            settings.FindProperty("manager").objectReferenceValue = manager;
            settings.FindProperty("closedGate").objectReferenceValue = gate.gameObject;
            settings.FindProperty("guardPrefab").objectReferenceValue = finalGuard.GetComponent<Guard>();
            var points = settings.FindProperty("reinforcementPoints");
            if (points.arraySize == 0)
            {
                points.arraySize = pointsRoot.childCount;
                for (int i = 0; i < pointsRoot.childCount; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = pointsRoot.GetChild(i);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Farm assault configured: closed gate, eight final guards and 20 seconds of corruption outside the gate.");
        }

        private static void Part(Transform root, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
