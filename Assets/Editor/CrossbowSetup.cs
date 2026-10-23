using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class CrossbowSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Crossbowman.prefab";

        [MenuItem("Necromancer Infection/Add Crossbowman")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save changes before adding the crossbowman.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = CreatePrefab();
            if (!Object.FindFirstObjectByType<Crossbowman>())
            {
                var group = GameObject.Find("Crossbowmen");
                if (!group) group = new GameObject("Crossbowmen");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(group.transform, false);
                // Behind the road patrol, outside the autonomous route and village houses.
                instance.transform.SetPositionAndRotation(new Vector3(3.5f, 0, -2.8f), Quaternion.Euler(0, -90, 0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Crossbowman configured behind the patrol: 60 HP, 12 damage, range 7, 1.5-second warning and 1.8-second reload.");
        }

        private static GameObject CreatePrefab()
        {
            var cloth = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CrossbowCloth.mat");
            if (!cloth)
            {
                cloth = new Material(Shader.Find("Standard")) { name = "CrossbowCloth", color = new Color(0.46f, 0.31f, 0.17f) };
                cloth.SetFloat("_Glossiness", 0);
                AssetDatabase.CreateAsset(cloth, "Assets/Materials/CrossbowCloth.mat");
            }
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GuardMetal.mat");
            var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CivilianSkin.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WeatheredWood.mat");
            var root = new GameObject("Crossbowman", typeof(Crossbowman));
            try
            {
                var body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0, 0.8f, 0);
                body.height = 1.6f;
                body.radius = 0.3f;
                var settings = new SerializedObject(root.GetComponent<UnitCombat>());
                settings.FindProperty("maxHealth").intValue = 60;
                settings.FindProperty("damage").intValue = 12;
                settings.FindProperty("attackRange").floatValue = 7;
                settings.FindProperty("attackCooldown").floatValue = 1.8f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Part(root.transform, "LeatherCoat", PrimitiveType.Capsule, new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.48f, 0.46f), cloth);
                Part(root.transform, "Hood", PrimitiveType.Sphere, new Vector3(0, 1.53f, 0), new Vector3(0.5f, 0.55f, 0.5f), cloth);
                Part(root.transform, "Face", PrimitiveType.Sphere, new Vector3(0, 1.5f, 0.2f), Vector3.one * 0.29f, skin);
                Part(root.transform, "Arm_L", PrimitiveType.Cube, new Vector3(-0.3f, 1.05f, 0.25f), new Vector3(0.18f, 0.18f, 0.55f), cloth);
                Part(root.transform, "Arm_R", PrimitiveType.Cube, new Vector3(0.3f, 1.05f, 0.25f), new Vector3(0.18f, 0.18f, 0.55f), cloth);
                Part(root.transform, "CrossbowStock", PrimitiveType.Cube, new Vector3(0, 1.12f, 0.5f), new Vector3(0.15f, 0.15f, 0.9f), wood);
                Part(root.transform, "CrossbowBow", PrimitiveType.Cube, new Vector3(0, 1.12f, 0.7f), new Vector3(1.2f, 0.13f, 0.13f), metal);
                Part(root.transform, "BowString", PrimitiveType.Cube, new Vector3(0, 1.12f, 0.48f), new Vector3(1.08f, 0.035f, 0.035f), skin);
                Part(root.transform, "LoadedBolt", PrimitiveType.Cube, new Vector3(0, 1.22f, 0.57f), new Vector3(0.045f, 0.045f, 0.75f), metal);
                Part(root.transform, "Leg_L", PrimitiveType.Cube, new Vector3(-0.16f, 0.25f, 0), new Vector3(0.2f, 0.5f, 0.24f), cloth);
                Part(root.transform, "Leg_R", PrimitiveType.Cube, new Vector3(0.16f, 0.25f, 0), new Vector3(0.2f, 0.5f, 0.24f), cloth);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0, 1.2f, 1);
                settings = new SerializedObject(root.GetComponent<Crossbowman>());
                settings.FindProperty("muzzle").objectReferenceValue = muzzle;
                settings.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void Part(Transform root, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
