using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection.Editor
{
    public static class CharacterArtSetup
    {
        public static readonly string[] Prefabs = {
            "Assets/Prefabs/Necromancer.prefab", "Assets/Prefabs/Zombie.prefab",
            "Assets/Prefabs/Guard.prefab", "Assets/Prefabs/FinalGuard.prefab",
            "Assets/Prefabs/JourneyFinalGuard.prefab"
        };

        [MenuItem("Necromancer Infection/Install Approved Character Art")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing artwork.");
            AssetDatabase.Refresh();
            var necromancer = Import("NecromancerTopDown/necromancer-concept-v1.png");
            var zombie = Import("ZombiePeasantTopDown/zombie-peasant-concept-v1.png");
            var guard = Import("GuardTopDown/guard-concept-v1.png");
            for (int i = 0; i < Prefabs.Length; i++)
            {
                var root = PrefabUtility.LoadPrefabContents(Prefabs[i]);
                try
                {
                    // Preserve named anchors used by CombatMotion, along with all gameplay components.
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        renderer.enabled = false;
                    var visual = root.transform.Find("CharacterVisual");
                    if (!visual)
                    {
                        visual = new GameObject("CharacterVisual").transform;
                        visual.SetParent(root.transform, false);
                    }
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = Vector3.one * (i == 0 ? 1.05f : i == 1 ? .9f : 1);
                    var image = visual.GetComponent<SpriteRenderer>();
                    if (!image) image = visual.gameObject.AddComponent<SpriteRenderer>();
                    image.sprite = i == 0 ? necromancer : i == 1 ? zombie : guard;
                    image.shadowCastingMode = ShadowCastingMode.Off;
                    image.receiveShadows = false;
                    image.spriteSortPoint = SpriteSortPoint.Pivot;
                    var controller = visual.GetComponent<CharacterSpriteVisual>();
                    if (!controller) controller = visual.gameObject.AddComponent<CharacterSpriteVisual>();
                    controller.Image = image;
                    PrefabUtility.SaveAsPrefabAsset(root, Prefabs[i]);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Installed approved necromancer, zombie peasant and guard artwork, including both reinforcement prefabs.");
        }

        private static Sprite Import(string relativePath)
        {
            string path = "Assets/Art/" + relativePath;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing approved artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1000;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, .035f);
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new InvalidOperationException("Sprite import failed: " + path);
            return sprite;
        }

        public static void RunBatch()
        {
            try { Install(); CharacterArtChecks.RunBatch(); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
    }
}
