using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection.Editor
{
    /// <summary>Installs the remaining unit portraits without rebuilding gameplay prefabs.</summary>
    public static class ReinforcementArtSetup
    {
        public static readonly string[] Prefabs = {
            "Assets/Prefabs/Skeleton.prefab", "Assets/Prefabs/Crossbowman.prefab",
            "Assets/Prefabs/RaisedGuard.prefab"
        };
        public static readonly string[] Artwork = {
            "Assets/Art/SkeletonTopDown/skeleton-v1.png",
            "Assets/Art/CrossbowmanTopDown/crossbowman-v1.png",
            "Assets/Art/RaisedGuardTopDown/raised-guard-v1.png"
        };

        [MenuItem("Necromancer Infection/Install Reinforcement Character Art")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before installing artwork.");
            AssetDatabase.Refresh();
            for (int i = 0; i < Prefabs.Length; i++)
            {
                var sprite = Import(Artwork[i]);
                var root = PrefabUtility.LoadPrefabContents(Prefabs[i]);
                try
                {
                    // Keep the old transforms: combat, projectile muzzle and resurrection use them.
                    foreach (var mesh in root.GetComponentsInChildren<MeshRenderer>(true))
                        mesh.enabled = false;
                    var visual = root.transform.Find("CharacterVisual");
                    if (!visual)
                    {
                        visual = new GameObject("CharacterVisual").transform;
                        visual.SetParent(root.transform, false);
                    }
                    visual.localPosition = Vector3.zero;
                    visual.localRotation = Quaternion.identity;
                    // Match the existing guard's 1.5 m artwork; skeletons are a little shorter.
                    float height = i == 0 ? 1.4f : i == 1 ? 1.55f : 1.65f;
                    visual.localScale = Vector3.one * height / sprite.bounds.size.y;
                    var image = visual.GetComponent<SpriteRenderer>();
                    if (!image) image = visual.gameObject.AddComponent<SpriteRenderer>();
                    image.sprite = sprite;
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
            Debug.Log("Installed skeleton, crossbowman and raised guard sprites on the original gameplay prefabs.");
        }

        private static Sprite Import(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing artwork: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1000;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, .02f);
            importer.SetTextureSettings(settings);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new InvalidOperationException("Failed to import sprite: " + path);
            return sprite;
        }

        public static void RunBatch()
        {
            try { Install(); ReinforcementArtChecks.RunBatch(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
