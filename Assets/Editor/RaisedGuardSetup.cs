using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class RaisedGuardSetup
    {
        public const string UndeadPath = "Assets/Prefabs/RaisedGuard.prefab";
        public const string CorpsePath = "Assets/Prefabs/GuardCorpse.prefab";

        [MenuItem("Necromancer Infection/Add Raised Guards")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save changes before configuring raised guards.");
            var undead = AssetDatabase.LoadAssetAtPath<GameObject>(UndeadPath);
            if (!undead) undead = CreateUndead();
            var corpse = AssetDatabase.LoadAssetAtPath<GameObject>(CorpsePath);
            if (!corpse) corpse = CreateCorpse(undead.GetComponent<Zombie>());
            foreach (string path in new[] { GuardSetup.PrefabPath, FarmAssaultSetup.GuardPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var remains = root.GetComponent<GuardRemains>();
                    if (!remains) remains = root.AddComponent<GuardRemains>();
                    var settings = new SerializedObject(remains);
                    if (!settings.FindProperty("corpsePrefab").objectReferenceValue)
                    {
                        settings.FindProperty("corpsePrefab").objectReferenceValue = corpse.GetComponent<GuardCorpse>();
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Normal/final guards leave single-use remains; LMB raising costs 35 energy. Raised guard: 150 HP, 12 damage, 1.2-second cooldown, speed 2.2.");
        }

        private static GameObject CreateUndead()
        {
            var root = PrefabUtility.LoadPrefabContents(GuardSetup.PrefabPath);
            try
            {
                RemoveLiving(root);
                root.name = "RaisedGuard";
                root.AddComponent<RaisedGuard>();
                var stats = new SerializedObject(root.GetComponent<UnitCombat>());
                stats.FindProperty("maxHealth").intValue = 150;
                stats.FindProperty("damage").intValue = 12;
                stats.FindProperty("attackCooldown").floatValue = 1.2f;
                stats.ApplyModifiedPropertiesWithoutUndo();
                stats = new SerializedObject(root.GetComponent<Zombie>());
                stats.FindProperty("moveSpeed").floatValue = 2.2f;
                stats.FindProperty("crowdSpacing").floatValue = 0.82f;
                stats.ApplyModifiedPropertiesWithoutUndo();
                var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZombieSkin.mat");
                var cloth = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZombieClothes.mat");
                var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ZombieEyes.mat");
                root.transform.Find("Face").GetComponent<Renderer>().sharedMaterial = skin;
                root.transform.Find("Tabard").GetComponent<Renderer>().sharedMaterial = cloth;
                root.transform.Find("Shield").localScale = new Vector3(0.65f, 0.62f, 0.16f);
                root.transform.Find("Shield").localRotation = Quaternion.Euler(0, 0, -16);
                root.transform.Find("Helmet").localRotation = Quaternion.Euler(0, 0, 12);
                Part(root.transform, "NecroticEyes", new Vector3(0, 1.66f, 0.22f), new Vector3(0.3f, 0.07f, 0.05f), glow);
                Part(root.transform, "ShieldRune", new Vector3(-0.5f, 0.95f, 0.42f), new Vector3(0.08f, 0.45f, 0.04f), glow);
                return PrefabUtility.SaveAsPrefabAsset(root, UndeadPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject CreateCorpse(Zombie undead)
        {
            var root = PrefabUtility.LoadPrefabContents(GuardSetup.PrefabPath);
            try
            {
                RemoveLiving(root);
                Object.DestroyImmediate(root.GetComponent<UnitCombat>());
                Object.DestroyImmediate(root.GetComponent<Collider>());
                root.name = "GuardCorpse";
                var body = new GameObject("FallenGuard").transform;
                var parts = new Transform[root.transform.childCount];
                for (int i = 0; i < parts.Length; i++) parts[i] = root.transform.GetChild(i);
                body.SetParent(root.transform, false);
                foreach (var part in parts) part.SetParent(body, false);
                body.localPosition = new Vector3(0, 0.25f, 0.8f);
                body.localRotation = Quaternion.Euler(-90, 0, 0);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 0.3f, 0);
                collider.size = new Vector3(1.5f, 0.6f, 2.1f);
                collider.isTrigger = true;
                var corpse = root.AddComponent<GuardCorpse>();
                var settings = new SerializedObject(corpse);
                settings.FindProperty("raisedGuardPrefab").objectReferenceValue = undead;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var ring = new GameObject("RaiseMarker").AddComponent<LineRenderer>();
                ring.transform.SetParent(root.transform, false);
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 40;
                ring.widthMultiplier = 0.065f;
                ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/ProtectionArea.mat");
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2 / ring.positionCount;
                    ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.95f, 0.06f, Mathf.Sin(angle) * 0.95f));
                }
                return PrefabUtility.SaveAsPrefabAsset(root, CorpsePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RemoveLiving(GameObject root)
        {
            if (root.GetComponent<GuardRemains>()) Object.DestroyImmediate(root.GetComponent<GuardRemains>());
            Object.DestroyImmediate(root.GetComponent<Guard>());
            Object.DestroyImmediate(root.GetComponent<LivingTarget>());
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
