using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class ProtectionSetup
    {
        public const string PrefabPath = "Assets/Prefabs/ProtectionArea.prefab";

        [MenuItem("Necromancer Infection/Add Protection Ability")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding protection.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            var infection = Object.FindFirstObjectByType<InfectionAbility>();
            if (!manager || !infection) throw new InvalidOperationException("DemoLevel requires GameManager and InfectionAbility.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab)
            {
                const string materialPath = "Assets/Materials/ProtectionArea.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    var shader = Shader.Find("NecromancerInfection/ProtectionArea");
                    if (!shader) throw new InvalidOperationException("ProtectionArea shader is missing.");
                    material = new Material(shader) { name = "ProtectionArea" };
                    material.SetColor("_Color", new Color(0.65f, 0.32f, 1, 0.85f));
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var root = new GameObject("ProtectionArea", typeof(ProtectionArea));
                try
                {
                    root.GetComponent<LineRenderer>().sharedMaterial = material;
                    root.GetComponent<MeshRenderer>().sharedMaterial = material;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally { Object.DestroyImmediate(root); }
            }
            var protection = infection.GetComponent<ProtectionAbility>();
            if (!protection) protection = infection.gameObject.AddComponent<ProtectionAbility>();
            var settings = new SerializedObject(protection);
            settings.FindProperty("levelCamera").objectReferenceValue = manager.LevelCamera;
            settings.FindProperty("ground").objectReferenceValue = manager.Ground;
            settings.FindProperty("necromancer").objectReferenceValue = manager.Necromancer;
            settings.FindProperty("areaPrefab").objectReferenceValue = prefab.GetComponent<ProtectionArea>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Protection area and RMB ability added. Existing balance settings preserved.");
        }
    }
}
