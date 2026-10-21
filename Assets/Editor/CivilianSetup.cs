using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class CivilianSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Civilian.prefab";

        [MenuItem("Necromancer Infection/Add Village Civilians")]
        public static void Create()
        {
            if (File.Exists(PrefabPath))
                throw new InvalidOperationException("Civilian prefab already exists; edit it instead of recreating it.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (!manager || !manager.Necromancer)
                throw new InvalidOperationException("DemoLevel requires its necromancer before adding villagers.");

            var shirt = MakeMaterial("CivilianShirt", new Color(0.53f, 0.46f, 0.33f));
            var skin = MakeMaterial("CivilianSkin", new Color(0.66f, 0.55f, 0.43f));
            var trousers = MakeMaterial("CivilianTrousers", new Color(0.24f, 0.28f, 0.32f));
            var unit = new GameObject("Civilian", typeof(Civilian));
            var collider = unit.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0, 0.75f, 0);
            collider.height = 1.5f;
            collider.radius = 0.3f;
            Part(unit.transform, "Shirt", PrimitiveType.Capsule, new Vector3(0, 0.72f, 0), new Vector3(0.55f, 0.42f, 0.4f), shirt);
            Part(unit.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 1.35f, 0), Vector3.one * 0.4f, skin);
            Part(unit.transform, "Leg_L", PrimitiveType.Cube, new Vector3(-0.14f, 0.22f, 0), new Vector3(0.18f, 0.44f, 0.23f), trousers);
            Part(unit.transform, "Leg_R", PrimitiveType.Cube, new Vector3(0.14f, 0.22f, 0), new Vector3(0.18f, 0.44f, 0.23f), trousers);
            var prefab = PrefabUtility.SaveAsPrefabAsset(unit, PrefabPath);
            UnityEngine.Object.DestroyImmediate(unit);
            var villagers = new GameObject("Villagers").transform;
            Vector3[] positions = {
                new Vector3(-11, 0, 2.5f), new Vector3(-9, 0, -2.5f), new Vector3(-7, 0, 1),
                new Vector3(-5, 0, -2.5f), new Vector3(-3, 0, 2.8f), new Vector3(-1, 0, 1),
                new Vector3(1, 0, 2.8f), new Vector3(3, 0, -2.5f), new Vector3(5, 0, 1),
                new Vector3(7, 0, -2.5f), new Vector3(9, 0, 2), new Vector3(13, 0, 2),
                new Vector3(-12, 0, 7), new Vector3(-3, 0, 7), new Vector3(3, 0, 7),
                new Vector3(6, 0, 5), new Vector3(-7, 0, -6), new Vector3(10, 0, -6)
            };
            for (int i = 0; i < positions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Civilian_" + (i + 1).ToString("00");
                instance.transform.SetParent(villagers, false);
                instance.transform.position = positions[i];
                instance.transform.rotation = Quaternion.Euler(0, i * 137.5f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            }

            // The shared marker will also be usable on the Zombie prefab in task 4.
            var necromancerRoot = PrefabUtility.LoadPrefabContents(NecromancerSetup.PrefabPath);
            if (!necromancerRoot.GetComponent<UndeadPresence>()) necromancerRoot.AddComponent<UndeadPresence>();
            PrefabUtility.SaveAsPrefabAsset(necromancerRoot, NecromancerSetup.PrefabPath);
            PrefabUtility.UnloadPrefabContents(necromancerRoot);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added 18 civilians and undead detection to DemoLevel.");
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
