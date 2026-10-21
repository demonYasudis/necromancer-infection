using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class GuardSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Guard.prefab";

        [MenuItem("Necromancer Infection/Add Guards")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save changes before adding guards.");
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Guard prefab already exists; edit existing guards instead.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var civilian = PrefabUtility.LoadPrefabContents(CivilianSetup.PrefabPath);
            try
            {
                if (!civilian.GetComponent<LivingTarget>()) civilian.AddComponent<LivingTarget>();
                PrefabUtility.SaveAsPrefabAsset(civilian, CivilianSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(civilian); }

            var metal = Material("GuardMetal", new Color(0.30f, 0.34f, 0.40f));
            var cloth = Material("GuardTabard", new Color(0.17f, 0.22f, 0.36f));
            var skin = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Materials/CivilianSkin.mat");
            var wood = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Materials/WeatheredWood.mat");
            var root = new GameObject("Guard", typeof(Guard));
            var body = root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0, 0.9f, 0);
            body.height = 1.8f;
            body.radius = 0.35f;
            var stats = new SerializedObject(root.GetComponent<UnitCombat>());
            stats.FindProperty("maxHealth").intValue = 100;
            stats.FindProperty("damage").intValue = 15;
            stats.FindProperty("attackRange").floatValue = 0.9f;
            stats.FindProperty("attackCooldown").floatValue = 1;
            stats.FindProperty("setupVersion").intValue = 1;
            stats.ApplyModifiedPropertiesWithoutUndo();
            Part(root.transform, "Armor", PrimitiveType.Capsule, new Vector3(0, 0.9f, 0), new Vector3(0.75f, 0.55f, 0.55f), metal);
            Part(root.transform, "Tabard", PrimitiveType.Cube, new Vector3(0, 0.95f, 0.29f), new Vector3(0.48f, 0.72f, 0.08f), cloth);
            Part(root.transform, "Face", PrimitiveType.Sphere, new Vector3(0, 1.65f, 0), Vector3.one * 0.43f, skin);
            Part(root.transform, "Helmet", PrimitiveType.Sphere, new Vector3(0, 1.8f, 0), new Vector3(0.6f, 0.35f, 0.6f), metal);
            Part(root.transform, "Shield", PrimitiveType.Cube, new Vector3(-0.5f, 0.95f, 0.3f), new Vector3(0.65f, 0.9f, 0.16f), cloth);
            Part(root.transform, "ShieldBoss", PrimitiveType.Sphere, new Vector3(-0.5f, 0.95f, 0.4f), Vector3.one * 0.22f, metal);
            Part(root.transform, "Sword", PrimitiveType.Cube, new Vector3(0.55f, 0.9f, 0.25f), new Vector3(0.1f, 0.1f, 1), metal);
            Part(root.transform, "SwordGrip", PrimitiveType.Cube, new Vector3(0.55f, 0.9f, -0.3f), new Vector3(0.13f, 0.13f, 0.22f), wood);
            Part(root.transform, "Leg_L", PrimitiveType.Cube, new Vector3(-0.18f, 0.25f, 0), new Vector3(0.23f, 0.5f, 0.28f), metal);
            Part(root.transform, "Leg_R", PrimitiveType.Cube, new Vector3(0.18f, 0.25f, 0), new Vector3(0.23f, 0.5f, 0.28f), metal);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            var group = new GameObject("Guards").transform;
            foreach (Vector3 position in new[] { new Vector3(1, 0, 0), new Vector3(11, 0, 3) })
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(group, false);
                instance.transform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added two guards with 100 HP / 15 damage and enabled zombie targeting of defenders.");
        }

        private static UnityEngine.Material Material(string name, Color color)
        {
            var material = new UnityEngine.Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0);
            AssetDatabase.CreateAsset(material, "Assets/Materials/" + name + ".mat");
            return material;
        }

        private static void Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, UnityEngine.Material material)
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
