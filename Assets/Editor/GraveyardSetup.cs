using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class GraveyardSetup
    {
        public const string SkeletonPath = "Assets/Prefabs/Skeleton.prefab";

        [MenuItem("Necromancer Infection/Add Optional Graveyard")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the scene before adding the graveyard.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            if (Object.FindFirstObjectByType<Grave>()) return;
            var bone = Material("SkeletonBone", new Color(0.8f, 0.78f, 0.63f));
            var stone = Material("GraveStone", new Color(0.3f, 0.32f, 0.36f));
            var dirt = Material("GraveSoil", new Color(0.2f, 0.15f, 0.12f));
            var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZombieEyes.mat");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonPath);
            if (!prefab)
            {
                var unit = new GameObject("Skeleton", typeof(Skeleton));
                var collider = unit.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0, 0.7f, 0);
                collider.height = 1.4f;
                collider.radius = 0.25f;
                var stats = new SerializedObject(unit.GetComponent<UnitCombat>());
                stats.FindProperty("maxHealth").intValue = 25;
                stats.FindProperty("damage").intValue = 5;
                stats.ApplyModifiedPropertiesWithoutUndo();
                Part(unit.transform, "Skull", new Vector3(0, 1.25f, 0), new Vector3(0.34f, 0.35f, 0.3f), bone);
                Part(unit.transform, "Eyes", new Vector3(0, 1.28f, 0.16f), new Vector3(0.25f, 0.07f, 0.025f), glow);
                Part(unit.transform, "Spine", new Vector3(0, 0.78f, 0), new Vector3(0.08f, 0.65f, 0.12f), bone);
                for (int i = 0; i < 3; i++)
                    Part(unit.transform, "Rib_" + i, new Vector3(0, 0.76f + i * 0.13f, 0), new Vector3(0.42f, 0.065f, 0.2f), bone);
                for (int i = -1; i <= 1; i += 2)
                {
                    Part(unit.transform, "Leg_" + i, new Vector3(i * 0.13f, 0.25f, 0), new Vector3(0.09f, 0.5f, 0.1f), bone);
                    Part(unit.transform, "Arm_" + i, new Vector3(i * 0.29f, 0.85f, 0.13f), new Vector3(0.08f, 0.1f, 0.5f), bone);
                }
                prefab = PrefabUtility.SaveAsPrefabAsset(unit, SkeletonPath);
                Object.DestroyImmediate(unit);
            }
            var root = new GameObject("OptionalGraveyard", typeof(Grave));
            root.transform.position = new Vector3(-8, 0, -3);
            Part(root.transform, "FreshEarth", new Vector3(0, 0.035f, 0), new Vector3(2, 0.07f, 2.5f), dirt);
            Part(root.transform, "Headstone", new Vector3(0, 0.6f, 0.8f), new Vector3(0.8f, 1.2f, 0.25f), stone);
            Part(root.transform, "CrossVertical", new Vector3(0, 0.75f, 0.655f), new Vector3(0.07f, 0.5f, 0.025f), bone);
            Part(root.transform, "CrossHorizontal", new Vector3(0, 0.85f, 0.65f), new Vector3(0.35f, 0.07f, 0.025f), bone);
            for (int i = -1; i <= 1; i += 2)
                Part(root.transform, "OldHeadstone_" + i, new Vector3(i * 1.5f, 0.35f, 0.85f), new Vector3(0.5f, 0.7f, 0.2f), stone);
            var hit = root.AddComponent<BoxCollider>();
            hit.center = new Vector3(0, 0.6f, 0);
            hit.size = new Vector3(2, 1.2f, 2.5f);
            hit.isTrigger = true;
            var ring = new GameObject("AvailableMarker").AddComponent<LineRenderer>();
            ring.transform.SetParent(root.transform, false);
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.07f;
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProtectionArea.mat");
            for (int i = 0; i < ring.positionCount; i++)
            {
                float a = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 1.35f, 0.1f, Mathf.Sin(a) * 1.35f));
            }
            var settings = new SerializedObject(root.GetComponent<Grave>());
            settings.FindProperty("skeletonPrefab").objectReferenceValue = prefab.GetComponent<Zombie>();
            settings.FindProperty("availableMarker").objectReferenceValue = ring.gameObject;
            var points = settings.FindProperty("spawnPoints");
            points.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var point = new GameObject("SkeletonSpawn_" + i).transform;
                point.SetParent(root.transform, false);
                point.localPosition = new Vector3((i - 1) * 0.8f, 0, -0.6f);
                points.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Optional graveyard added: 30 energy, three 25-HP / 5-damage skeletons, once per attempt.");
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Materials/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result) return result;
            result = new Material(Shader.Find("Standard")) { name = name, color = color };
            result.SetFloat("_Glossiness", 0);
            AssetDatabase.CreateAsset(result, path);
            return result;
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
