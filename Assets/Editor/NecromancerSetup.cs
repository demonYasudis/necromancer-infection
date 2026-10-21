using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class NecromancerSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Necromancer.prefab";

        [MenuItem("Necromancer Infection/Add Autonomous Necromancer")]
        public static void Create()
        {
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Necromancer prefab already exists; edit it instead of recreating it.");

            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (manager == null || manager.StartPoint == null || manager.CaptureTarget == null)
                throw new InvalidOperationException("DemoLevel requires a GameManager with start and target references.");

            var robe = MakeMaterial("NecromancerRobe", new Color(0.19f, 0.10f, 0.27f));
            var staff = MakeMaterial("NecromancerStaff", new Color(0.24f, 0.19f, 0.14f));
            var magic = MakeMaterial("NecromancerMagic", new Color(0.35f, 1, 0.18f));
            magic.EnableKeyword("_EMISSION");
            magic.SetColor("_EmissionColor", new Color(0.2f, 0.8f, 0.08f));

            var unit = new GameObject("Necromancer", typeof(Necromancer));
            var body = unit.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0, 1, 0);
            body.height = 2;
            body.radius = 0.35f;
            Part(unit.transform, "Robe", PrimitiveType.Capsule, new Vector3(0, 0.85f, 0), new Vector3(0.65f, 0.85f, 0.65f), robe);
            Part(unit.transform, "Hood", PrimitiveType.Sphere, new Vector3(0, 1.75f, 0), Vector3.one * 0.65f, robe);
            Part(unit.transform, "FaceGlow", PrimitiveType.Cube, new Vector3(0, 1.75f, 0.29f), new Vector3(0.26f, 0.08f, 0.08f), magic);
            Part(unit.transform, "Staff", PrimitiveType.Cylinder, new Vector3(0.6f, 1.1f, 0.15f), new Vector3(0.09f, 1.1f, 0.09f), staff);
            Part(unit.transform, "StaffCrystal", PrimitiveType.Sphere, new Vector3(0.6f, 2.25f, 0.15f), Vector3.one * 0.28f, magic);
            var prefab = PrefabUtility.SaveAsPrefabAsset(unit, PrefabPath);
            UnityEngine.Object.DestroyImmediate(unit);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = manager.StartPoint.position;

            var corner = new GameObject("RoadCorner").transform;
            corner.SetParent(manager.StartPoint.parent, false);
            corner.position = new Vector3(manager.CaptureTarget.position.x, manager.StartPoint.position.y, manager.StartPoint.position.z);
            var settings = new SerializedObject(manager);
            settings.FindProperty("necromancer").objectReferenceValue = instance.GetComponent<Necromancer>();
            var waypoints = settings.FindProperty("intermediateWaypoints");
            waypoints.arraySize = 1;
            waypoints.GetArrayElementAtIndex(0).objectReferenceValue = corner;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Autonomous Necromancer prefab and route added to DemoLevel.");
        }

        private static Material MakeMaterial(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0);
            AssetDatabase.CreateAsset(material, "Assets/Materials/" + name + ".mat");
            return material;
        }

        private static void Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
