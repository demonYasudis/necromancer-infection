using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class InfectionSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Zombie.prefab";

        [MenuItem("Necromancer Infection/Add Infection Ability")]
        public static void Create()
        {
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Zombie prefab already exists; edit the existing assets instead.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (!manager || !manager.Necromancer || !manager.LevelCamera)
                throw new InvalidOperationException("DemoLevel requires its manager, camera and necromancer.");

            var skin = Material("ZombieSkin", new Color(0.35f, 0.52f, 0.28f));
            var clothes = Material("ZombieClothes", new Color(0.21f, 0.30f, 0.22f));
            var eyes = Material("ZombieEyes", new Color(0.45f, 1, 0.1f));
            eyes.EnableKeyword("_EMISSION");
            eyes.SetColor("_EmissionColor", new Color(0.25f, 0.8f, 0.05f));

            var unit = new GameObject("Zombie", typeof(UndeadPresence), typeof(Zombie));
            var body = unit.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0, 0.7f, 0);
            body.height = 1.4f;
            body.radius = 0.3f;
            Part(unit.transform, "TornShirt", PrimitiveType.Capsule, new Vector3(0, 0.66f, 0), new Vector3(0.65f, 0.4f, 0.44f), clothes);
            Part(unit.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.22f, 0.14f), Vector3.one * 0.43f, skin);
            Part(unit.transform, "Eyes", PrimitiveType.Cube, new Vector3(0, 1.24f, 0.34f), new Vector3(0.27f, 0.06f, 0.06f), eyes);
            Part(unit.transform, "Arm_L", PrimitiveType.Cube, new Vector3(-0.37f, 0.75f, 0.25f), new Vector3(0.15f, 0.15f, 0.65f), skin);
            Part(unit.transform, "Arm_R", PrimitiveType.Cube, new Vector3(0.37f, 0.68f, 0.2f), new Vector3(0.15f, 0.15f, 0.55f), skin);
            Part(unit.transform, "Leg_L", PrimitiveType.Cube, new Vector3(-0.14f, 0.22f, 0), new Vector3(0.18f, 0.44f, 0.23f), clothes);
            Part(unit.transform, "Leg_R", PrimitiveType.Cube, new Vector3(0.14f, 0.22f, 0), new Vector3(0.18f, 0.44f, 0.23f), clothes);
            var prefab = PrefabUtility.SaveAsPrefabAsset(unit, PrefabPath);
            UnityEngine.Object.DestroyImmediate(unit);

            var ability = new GameObject("PlayerAbilities", typeof(InfectionAbility)).GetComponent<InfectionAbility>();
            var settings = new SerializedObject(ability);
            settings.FindProperty("levelCamera").objectReferenceValue = manager.LevelCamera;
            settings.FindProperty("necromancer").objectReferenceValue = manager.Necromancer;
            settings.FindProperty("zombiePrefab").objectReferenceValue = prefab.GetComponent<Zombie>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Free LMB infection and Zombie prefab added to DemoLevel.");
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
