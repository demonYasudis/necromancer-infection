using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class FarmSetup
    {
        [MenuItem("Necromancer Infection/Add Farm Objective")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding the farm.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager || !manager.CaptureTarget) throw new InvalidOperationException("DemoLevel requires its manager and destination.");
            var target = manager.CaptureTarget;
            if (!target.GetComponent<CaptureTarget>()) target.gameObject.AddComponent<CaptureTarget>();
            if (!manager.GetComponent<FarmObjectiveHUD>()) manager.gameObject.AddComponent<FarmObjectiveHUD>();
            var building = GameObject.Find("FarmPlaceholder");
            if (building)
            {
                building.name = "Farmhouse";
                building.transform.SetParent(target, true);
            }
            if (!target.Find("Farmyard"))
            {
                var yard = new GameObject("Farmyard").transform;
                yard.SetParent(target, false);
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeatheredWood.mat");
                var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stone.mat");
                Fence(yard, new Vector3(-3.8f, 0, 2.6f), 6.8f, true, wood);
                Fence(yard, new Vector3(3.8f, 0, 2.6f), 6.8f, true, wood);
                Fence(yard, new Vector3(0, 0, 6), 7.6f, false, wood);
                Fence(yard, new Vector3(-2.8f, 0, -0.8f), 2, false, wood);
                Fence(yard, new Vector3(2.8f, 0, -0.8f), 2, false, wood);
                Block(yard, "GatePost_L", new Vector3(-1.8f, 0.7f, -0.8f), new Vector3(0.22f, 1.4f, 0.22f), wood);
                Block(yard, "GatePost_R", new Vector3(1.8f, 0.7f, -0.8f), new Vector3(0.22f, 1.4f, 0.22f), wood);
                Block(yard, "Trough", new Vector3(2.8f, 0.25f, 0.6f), new Vector3(0.8f, 0.5f, 1.4f), stone);
            }
            var markers = manager.StartPoint.parent;
            var route = new Transform[4];
            string[] names = { "VillageRoad", "VillageSquare", "PatrolApproach", "RoadCorner" };
            float[] x = { -7, -1, 5, 11 };
            for (int i = 0; i < route.Length; i++)
            {
                route[i] = markers.Find(names[i]);
                if (!route[i])
                {
                    route[i] = new GameObject(names[i]).transform;
                    route[i].SetParent(markers, false);
                    route[i].position = new Vector3(x[i], 0, 0);
                }
            }
            var settings = new SerializedObject(manager);
            var waypoints = settings.FindProperty("intermediateWaypoints");
            // Preserve a route that has already been authored for this stage.
            if (waypoints.arraySize <= 1)
            {
                waypoints.arraySize = route.Length;
                for (int i = 0; i < route.Length; i++) waypoints.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None))
            {
                if (guard.transform.position.x > 5) continue;
                var patrol = new SerializedObject(guard);
                var offsets = patrol.FindProperty("patrolOffsets");
                if (offsets.arraySize != 0) continue;
                offsets.arraySize = 2;
                offsets.GetArrayElementAtIndex(0).vector3Value = new Vector3(0, 0, -1.1f);
                offsets.GetArrayElementAtIndex(1).vector3Value = new Vector3(0, 0, 1.1f);
                patrol.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Farm objective, farmyard and village route configured. Entering the farm radius starts FinalPhase.");
        }

        private static void Fence(Transform yard, Vector3 center, float length, bool alongZ, Material wood)
        {
            var axis = alongZ ? Vector3.forward : Vector3.right;
            int posts = Mathf.CeilToInt(length / 1.5f);
            for (int i = 0; i <= posts; i++)
                Block(yard, "FencePost", center + axis * (length * i / posts - length * 0.5f) + Vector3.up * 0.45f,
                    new Vector3(0.14f, 0.9f, 0.14f), wood);
            foreach (float y in new[] { 0.3f, 0.65f })
                Block(yard, "FenceRail", center + Vector3.up * y,
                    alongZ ? new Vector3(0.1f, 0.12f, length) : new Vector3(length, 0.12f, 0.1f), wood);
        }

        private static void Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
