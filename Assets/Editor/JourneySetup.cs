using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    /// <summary>Authors a separate playable level; keeps the compact regression scene intact.</summary>
    public static class JourneySetup
    {
        public const string ScenePath = "Assets/Scenes/JourneyLevel.unity";
        private static readonly Vector3[] Corners = {
            new Vector3(-189, 0, 0), new Vector3(11, 0, 0), new Vector3(11, 0, 40),
            new Vector3(-189, 0, 40), new Vector3(-189, 0, 80), new Vector3(11, 0, 80)
        };

        public static Vector3 Point(float distance, float lateral = 0)
        {
            for (int i = 1; i < Corners.Length; i++)
            {
                Vector3 direction = (Corners[i] - Corners[i - 1]).normalized;
                float length = Vector3.Distance(Corners[i], Corners[i - 1]);
                if (distance <= length) return Corners[i - 1] + direction * distance + Vector3.Cross(direction, Vector3.up) * lateral;
                distance -= length;
            }
            return Corners[Corners.Length - 1];
        }

        [MenuItem("Necromancer Infection/Create Extended Journey Level")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the scene first.");
            if (File.Exists(ScenePath)) throw new InvalidOperationException("JourneyLevel already exists; edit the authored scene in place.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            foreach (var civilian in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                if (!civilian.GetComponent<AlarmRunner>()) Object.DestroyImmediate(civilian.gameObject);
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None)) Object.DestroyImmediate(guard.gameObject);
            foreach (var archer in Object.FindObjectsByType<Crossbowman>(FindObjectsSortMode.None)) Object.DestroyImmediate(archer.gameObject);
            foreach (string name in new[] { "DirtRoad", "FarmApproach", "VillageHouse_A", "VillageHouse_B", "Rock_A", "Rock_B" })
            {
                var old = GameObject.Find(name);
                if (old) Object.DestroyImmediate(old);
            }
            manager.Ground.transform.position = new Vector3(-89, -0.25f, 40);
            manager.Ground.transform.localScale = new Vector3(240, 0.5f, 140);
            manager.StartPoint.position = Corners[0];
            manager.Necromancer.transform.position = Corners[0];
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager.Necromancer.transform);
            manager.CaptureTarget.position += Vector3.forward * 80;
            var environment = new GameObject("JourneyEnvironment").transform;
            var road = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DirtRoad.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeatheredWood.mat");
            var roof = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DarkRoof.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Stone.mat");
            for (int i = 1; i < Corners.Length; i++)
            {
                Vector3 delta = Corners[i] - Corners[i - 1];
                Block(environment, "Road_" + i, (Corners[i] + Corners[i - 1]) * 0.5f + Vector3.up * 0.015f,
                    new Vector3(Mathf.Abs(delta.x) + 5, 0.02f, Mathf.Abs(delta.z) + 5), road, false);
            }
            var routeRoot = new GameObject("JourneyWaypoints").transform;
            var points = new Transform[Corners.Length + 1];
            points[0] = manager.StartPoint;
            for (int i = 1; i < Corners.Length; i++)
            {
                points[i] = new GameObject("RouteCorner_" + i).transform;
                points[i].SetParent(routeRoot);
                points[i].position = Corners[i];
            }
            points[points.Length - 1] = manager.Farm.ArrivalPoint;
            var settings = new SerializedObject(manager);
            var waypoints = settings.FindProperty("intermediateWaypoints");
            waypoints.arraySize = Corners.Length - 1;
            for (int i = 1; i < Corners.Length; i++) waypoints.GetArrayElementAtIndex(i - 1).objectReferenceValue = points[i];
            settings.ApplyModifiedPropertiesWithoutUndo();

            var civilians = new GameObject("JourneyCivilians").transform;
            for (int group = 0; group < 23; group++)
            {
                float distance = 8 + group * 29;
                for (int i = 0; i < 4; i++)
                    Spawn(CivilianSetup.PrefabPath, civilians, $"Resident_{group + 1:00}_{i + 1}", Point(distance + (i % 2) * 2, i < 2 ? 2 : -2));
                // Buildings sit beyond both the road and the horde's formation.
                if (NearCorner(distance)) continue;
                Vector3 house = Point(distance, group % 2 == 0 ? 9 : -9);
                Block(environment, "House_" + group, house + Vector3.up * 1.25f, new Vector3(4, 2.5f, 4), wood, true);
                Block(environment, "Roof_" + group, house + Vector3.up * 2.7f, new Vector3(4.6f, 0.4f, 4.6f), roof, false);
                if (group % 3 == 0)
                    Block(environment, "Well_" + group, Point(distance + 7, -7) + Vector3.up * 0.4f, new Vector3(1.5f, 0.8f, 1.5f), stone, true);
            }
            var enemies = new GameObject("JourneyPatrols").transform;
            float[] battles = { 105, 185, 285, 385, 475, 555, 635 };
            int[] counts = { 1, 2, 2, 3, 3, 3, 4 };
            for (int battle = 0; battle < battles.Length; battle++)
            {
                for (int i = 0; i < counts[battle]; i++)
                    Spawn(GuardSetup.PrefabPath, enemies, $"Patrol_{battle + 1}_{i + 1}", Point(battles[battle] + i * 0.9f, (i % 2 == 0 ? 1 : -1) * 1.5f));
                if (battle >= 4)
                    Spawn(CrossbowSetup.PrefabPath, enemies, "CrossbowPost_" + battle, Point(battles[battle] + 4, -3));
            }
            var runner = Object.FindFirstObjectByType<AlarmRunner>();
            runner.transform.position = Point(160, 2);
            PrefabUtility.RecordPrefabInstancePropertyModifications(runner.transform);
            var bell = Object.FindFirstObjectByType<AlarmBell>();
            bell.transform.position = Point(170, 4);
            GameObject.Find("OptionalGraveyard").transform.position = Point(47, -3);

            var rig = new GameObject("JourneyCameraRig").transform;
            rig.position = manager.StartPoint.position;
            manager.LevelCamera.transform.SetParent(rig, false);
            manager.LevelCamera.transform.localPosition = new Vector3(3, 22, -22);
            manager.LevelCamera.orthographicSize = 12;
            var journey = manager.gameObject.AddComponent<JourneyRoute>();
            settings = new SerializedObject(journey);
            settings.FindProperty("cameraRig").objectReferenceValue = rig;
            var route = settings.FindProperty("route");
            route.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) route.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            ConfigureJourneyGuards();
            EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(DemoLevelSetup.ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("JourneyLevel authored: 681.5m, three districts, seven patrols, three crossbows, 93 residents, alarm and optional graveyard.");
        }

        private static void Spawn(string path, Transform parent, string name, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            instance.name = name;
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        }

        private static bool NearCorner(float distance)
        {
            foreach (float corner in new[] { 200f, 240f, 440f, 480f, 680f })
                if (Mathf.Abs(distance - corner) < 14) return true;
            return false;
        }

        private static void ConfigureJourneyGuards()
        {
            // On a long level, defenders engage the front rank instead of always
            // ignoring an entire army to focus the irreplaceable leader at 5m.
            foreach (var guard in Object.FindObjectsByType<Guard>(FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(guard);
                settings.FindProperty("necromancerPriorityRadius").floatValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(guard);
            }
            const string path = "Assets/Prefabs/JourneyFinalGuard.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                var source = PrefabUtility.LoadPrefabContents(FarmAssaultSetup.GuardPath);
                try
                {
                    var settings = new SerializedObject(source.GetComponent<Guard>());
                    settings.FindProperty("necromancerPriorityRadius").floatValue = 1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(source); }
            }
            foreach (var component in new Component[] { Object.FindFirstObjectByType<FarmAssault>(), Object.FindFirstObjectByType<AlarmBell>() })
            {
                var settings = new SerializedObject(component);
                settings.FindProperty("guardPrefab").objectReferenceValue = prefab.GetComponent<Guard>();
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Block(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(block.GetComponent<Collider>());
        }
    }
}
