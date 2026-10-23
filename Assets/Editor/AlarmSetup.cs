using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class AlarmSetup
    {
        [MenuItem("Necromancer Infection/Add Village Alarm")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save changes before adding the alarm.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            if (Object.FindFirstObjectByType<AlarmBell>())
            {
                Debug.Log("Village alarm already exists; its authored settings were preserved.");
                return;
            }
            var manager = Object.FindFirstObjectByType<GameManager>();
            var civilian = GameObject.Find("Civilian_01").GetComponent<Civilian>();
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeatheredWood.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/AlarmGold.mat");
            if (!gold)
            {
                gold = new Material(Shader.Find("Standard")) { name = "AlarmGold", color = new Color(0.95f, 0.65f, 0.15f) };
                gold.SetFloat("_Glossiness", 0.25f);
                AssetDatabase.CreateAsset(gold, "Assets/Materials/AlarmGold.mat");
            }
            var root = new GameObject("VillageAlarm", typeof(AlarmBell));
            root.transform.position = new Vector3(-2.5f, 0, 3.5f);
            Part(root.transform, "Post_L", PrimitiveType.Cube, new Vector3(-0.65f, 1.3f, 0), new Vector3(0.18f, 2.6f, 0.18f), wood, true);
            Part(root.transform, "Post_R", PrimitiveType.Cube, new Vector3(0.65f, 1.3f, 0), new Vector3(0.18f, 2.6f, 0.18f), wood, true);
            Part(root.transform, "Beam", PrimitiveType.Cube, new Vector3(0, 2.6f, 0), new Vector3(1.6f, 0.2f, 0.25f), wood, true);
            var bellVisual = Part(root.transform, "Bell", PrimitiveType.Sphere, new Vector3(0, 2.15f, 0), new Vector3(0.8f, 0.75f, 0.65f), gold, false);
            Part(root.transform, "BellLip", PrimitiveType.Cylinder, new Vector3(0, 1.9f, 0), new Vector3(0.9f, 0.07f, 0.7f), gold, false);
            Part(root.transform, "Rope", PrimitiveType.Cylinder, new Vector3(0, 1.15f, 0), new Vector3(0.035f, 0.7f, 0.035f), wood, false);
            var arrival = new GameObject("BellArrival").transform;
            arrival.SetParent(root.transform, false);
            arrival.localPosition = Vector3.back * 1.2f;
            var points = new Transform[3];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GameObject($"PatrolArrival_{i + 1}").transform;
                points[i].SetParent(root.transform, false);
                points[i].position = new Vector3(i * 1.3f, 0, 2);
            }
            var runner = civilian.gameObject.AddComponent<AlarmRunner>();
            var marker = Part(civilian.transform, "AlarmWarning", PrimitiveType.Cube, new Vector3(0, 2.15f, 0), Vector3.one * 0.35f, gold, false);
            marker.transform.localRotation = Quaternion.Euler(0, 0, 45);
            marker.SetActive(false);
            var settings = new SerializedObject(runner);
            settings.FindProperty("bell").objectReferenceValue = root.GetComponent<AlarmBell>();
            settings.FindProperty("warningMarker").objectReferenceValue = marker;
            settings.ApplyModifiedPropertiesWithoutUndo();
            settings = new SerializedObject(root.GetComponent<AlarmBell>());
            settings.FindProperty("manager").objectReferenceValue = manager;
            settings.FindProperty("runner").objectReferenceValue = runner;
            settings.FindProperty("arrivalPoint").objectReferenceValue = arrival;
            settings.FindProperty("guardPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FarmAssaultSetup.GuardPath).GetComponent<Guard>();
            settings.FindProperty("bellVisual").objectReferenceValue = bellVisual.GetComponent<Renderer>();
            var list = settings.FindProperty("patrolPoints");
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Village alarm configured: existing Civilian_01 runs to a bell; reaching it calls three additional guards once.");
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }
    }
}
