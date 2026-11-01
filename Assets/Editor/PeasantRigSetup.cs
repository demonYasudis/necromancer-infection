using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class PeasantRigSetup
    {
        private const string PrefabPath = "Assets/Prefabs/PeasantSpriteRig.prefab";
        private const string ScenePath = "Assets/Scenes/PeasantRigPreview.unity";
        private static readonly string[] Names = { "head", "torso", "pelvis", "near_upper", "near_fore", "far_upper", "far_fore", "near_thigh", "near_shin", "far_thigh", "far_shin", "pouch" };

        [MenuItem("Necromancer Infection/Peasant Rig/Create Preview")]
        public static void CreatePreview()
        {
            if (File.Exists(ScenePath) || File.Exists(PrefabPath))
            {
                Debug.Log("Peasant preview already exists. Open " + ScenePath);
                return;
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Sprite[] sprites = new Sprite[12];
            for (int i = 0; i < Names.Length; i++)
            {
                string path = PeasantTopDownSetup.ArtPath + Names[i] + ".png";
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
                Require(sprites[i], "Missing sprite: " + path);
            }
            var source = new GameObject("PeasantSpriteRig");
            var newRig = source.AddComponent<PeasantSpriteRig>();
            newRig.Sprites = sprites;
            newRig.Pieces = JsonUtility.FromJson<PeasantSpriteRig.Definition>(File.ReadAllText(PeasantTopDownSetup.ArtPath + "rig.json")).Pieces;
            PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.transform.position = new Vector3(-0.65f, 1.15f, -10);
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.075f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var controls = new GameObject("PreviewControls").AddComponent<PeasantRigPreview>();
            controls.Rig = instance.GetComponent<PeasantSpriteRig>();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ScenePath + " and " + PrefabPath);
        }

        [MenuItem("Necromancer Infection/Peasant Rig/Run Checks and Capture")]
        public static void CheckAndCapture()
        {
            for (int i = 0; i < 240; i++)
            {
                float phase = i / 240f;
                for (int side = 0; side < 2; side++)
                {
                    Vector2 foot = PeasantSpriteRig.Foot(phase + side * 0.5f, 0.95f);
                    Vector2 endpoint = new Vector2(foot.x, -.7f + foot.y);
                    Vector2 joint = PeasantSpriteRig.SolveJoint(Vector2.zero, ref endpoint, .4f, .42f, side == 0 ? -1 : 1);
                    Require(Mathf.Abs(joint.magnitude - .4f) < .0001f, "Upper bone length");
                    Require(Mathf.Abs(Vector2.Distance(joint, endpoint) - .42f) < .0001f, "Lower bone length");
                }
                if (phase < 0.6f) Require(PeasantSpriteRig.Foot(phase, 0.95f).y == 0, "Support foot must stay grounded");
            }
            Require(PeasantSpriteRig.Foot(0, 0.65f).x > PeasantSpriteRig.Foot(0.5f, 0.65f).x, "Feet alternate");
            Require(PeasantSpriteRig.Foot(0, 0.65f) == PeasantSpriteRig.Foot(1, 0.65f), "Cycle seam");
            CreatePreview();
            EditorSceneManager.OpenScene(ScenePath);
            var rig = Object.FindFirstObjectByType<PeasantSpriteRig>();
            rig.SetPhase(0);
            Require(rig.GetComponentsInChildren<SpriteRenderer>().Length == 12, "Exactly twelve sprite parts");
            foreach (var part in rig.GetComponentsInChildren<SpriteRenderer>())
                Require(Mathf.Abs(part.transform.localScale.x - part.transform.localScale.y) < 0.0001f,
                    "Artwork must keep its aspect ratio: " + part.name);
            Directory.CreateDirectory("Logs");
            Capture(Camera.main, "Logs/peasant-unity-step-a.png");
            rig.SetPhase(0.5f);
            Capture(Camera.main, "Logs/peasant-unity-step-b.png");
            rig.SetIdlePose();
            Capture(Camera.main, "Logs/peasant-unity-idle.png");
            var renderers = rig.GetComponentsInChildren<SpriteRenderer>();
            var scales = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) scales[i] = renderers[i].transform.localScale.x;
            for (int frame = 0; frame < 240; frame++)
            {
                rig.SetPhase(frame / 240f);
                for (int i = 0; i < renderers.Length; i++)
                    Require(Mathf.Abs(renderers[i].transform.localScale.x - scales[i]) < .0001f,
                        "Animation must rotate, never stretch, the painted part: " + renderers[i].name);
            }
            rig.SetPhase(0.5f);
            var arm = rig.transform.Find("SpriteSkeleton/FarUpperArm");
            Vector3 attachment = arm.position;
            rig.DetachArm();
            Require(rig.ArmDetached && Vector3.Distance(arm.position, attachment) < 0.0001f, "Detach must preserve pose");
            Require(arm.parent.name == "DetachedArm", "Detached arm must have independent parent");
            rig.RestoreArm();
            Require(!rig.ArmDetached && arm.parent.name == "SpriteSkeleton", "Restore must reattach");
            rig.SetPouch(false);
            Require(!rig.PouchSocket.GetComponent<SpriteRenderer>().enabled, "Pouch removal");
            rig.SetPouch(true);
            Require(rig.PouchSocket.GetComponent<SpriteRenderer>().enabled, "Pouch attachment");
            // Generated pieces are runtime state, never save them into the source scene.
            Debug.Log("PASS: peasant sprite rig, 240 IK poses, support feet, cycle seam, twelve parts, arm detach/restore, pouch and two Unity renders.");
        }

        public static void RunBatch()
        {
            try { CheckAndCapture(); EditorApplication.Exit(0); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void UpgradeArtworkBatch()
        {
            try
            {
                var source = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var rig = source.GetComponent<PeasantSpriteRig>();
                    rig.Pieces = JsonUtility.FromJson<PeasantSpriteRig.Definition>(File.ReadAllText(PeasantTopDownSetup.ArtPath + "rig.json")).Pieces;
                    rig.ShowPouch = false;
                    for (int i = 0; i < Names.Length; i++)
                    {
                        string path = PeasantTopDownSetup.ArtPath + Names[i] + ".png";
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePixelsPerUnit = 100;
                        importer.alphaIsTransparency = true;
                        importer.mipmapEnabled = false;
                        importer.filterMode = FilterMode.Bilinear;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                        rig.Sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        Require(rig.Sprites[i], "Missing corrected sprite " + path);
                    }
                    PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(source); }
                EditorSceneManager.OpenScene(ScenePath);
                Camera.main.orthographicSize = 1.4f;
                Camera.main.transform.position = new Vector3(-0.55f, 1.1f, -10);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
                AssetDatabase.SaveAssets();
                CheckAndCapture();
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        public static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1000, 700, 24);
            var texture = new Texture2D(1000, 700, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
