using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class PeasantTopDownSetup
    {
        public const string ArtPath = "Assets/Art/PeasantTopDown/";
        private const string RigPath = "Assets/Prefabs/PeasantSpriteRig.prefab";

        [MenuItem("Necromancer Infection/Peasant Rig/Install Top Down Villager")]
        public static void Install()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var definition = JsonUtility.FromJson<PeasantSpriteRig.Definition>(File.ReadAllText(ArtPath + "rig.json"));
            var sprites = new Sprite[12];
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = ArtPath + definition.Pieces[i].Name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprites[i]) throw new InvalidOperationException("Missing peasant part: " + path);
            }
            var source = PrefabUtility.LoadPrefabContents(RigPath);
            try
            {
                var rig = source.GetComponent<PeasantSpriteRig>();
                rig.Sprites = sprites;
                rig.Pieces = definition.Pieces;
                rig.PreviewWalking = true;
                rig.ShowPouch = false;
                PrefabUtility.SaveAsPrefabAsset(source, RigPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(source); }

            var human = PrefabUtility.LoadPrefabContents(CivilianSetup.PrefabPath);
            try
            {
                // Keep the old named anchors for CombatMotion and existing scene references.
                foreach (Transform child in human.transform)
                    if (child.TryGetComponent<MeshRenderer>(out var renderer)) renderer.enabled = false;
                var visual = human.transform.Find("PeasantVisual");
                if (!visual)
                {
                    visual = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath))).transform;
                    visual.name = "PeasantVisual";
                    visual.SetParent(human.transform, false);
                }
                visual.localPosition = Vector3.zero;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one * .55f;
                var rig = visual.GetComponent<PeasantSpriteRig>();
                rig.PreviewWalking = false;
                if (!visual.GetComponent<CivilianSpriteVisual>()) visual.gameObject.AddComponent<CivilianSpriteVisual>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
                PrefabUtility.SaveAsPrefabAsset(human, CivilianSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(human); }
            AssetDatabase.SaveAssets();
            foreach (string scene in new[] { "Assets/Scenes/DemoLevel.unity", "Assets/Scenes/JourneyLevel.unity" })
            {
                EditorSceneManager.OpenScene(scene);
                foreach (var unit in Object.FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                    if (!unit.GetComponentInChildren<PeasantSpriteRig>())
                        throw new InvalidOperationException("Civilian did not inherit its sprite: " + unit.name);
            }
            Debug.Log("Installed approved 3/4 top-down peasant on Civilian prefab in DemoLevel and JourneyLevel.");
        }

        public static void RunBatch()
        {
            try { Install(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
