using System;
using System.IO;
using NecromancerInfection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection.Editor
{
    /// <summary>Creates the initial authored scene; never runs in the player or on import.</summary>
    public static class DemoLevelSetup
    {
        public const string ScenePath = "Assets/Scenes/DemoLevel.unity";

        [MenuItem("Necromancer Infection/Create Initial Demo Level")]
        public static void Create()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("DemoLevel already exists. Open it instead of recreating it.");

            foreach (string folder in new[] { "Scenes", "Scripts", "Prefabs", "ScriptableObjects", "UI", "Materials" })
                if (!AssetDatabase.IsValidFolder("Assets/" + folder))
                    AssetDatabase.CreateFolder("Assets", folder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.5f);
            RenderSettings.fog = false;

            var earth = Material("Earth", new Color(0.19f, 0.24f, 0.19f));
            var road = Material("DirtRoad", new Color(0.36f, 0.29f, 0.22f));
            var wood = Material("WeatheredWood", new Color(0.25f, 0.20f, 0.18f));
            var roof = Material("DarkRoof", new Color(0.19f, 0.21f, 0.26f));
            var stone = Material("Stone", new Color(0.32f, 0.35f, 0.36f));
            var green = Material("StartMarker", new Color(0.34f, 0.8f, 0.28f));
            var violet = Material("TargetMarker", new Color(0.57f, 0.32f, 0.72f));

            var environment = new GameObject("Environment").transform;
            var ground = Block("Ground", new Vector3(0, -0.25f, 0), new Vector3(36, 0.5f, 22), earth, environment);
            // Flat, non-colliding road overlays keep the walking surface continuous.
            Block("DirtRoad", new Vector3(0, 0.015f, 0), new Vector3(30, 0.02f, 3.5f), road, environment, false);
            Block("FarmApproach", new Vector3(11, 0.016f, 3), new Vector3(3.5f, 0.02f, 6), road, environment, false);

            House("VillageHouse_A", new Vector3(-7, 0, 6), new Vector3(4, 2.5f, 3), wood, roof, environment);
            House("VillageHouse_B", new Vector3(0, 0, -6), new Vector3(4, 2.5f, 3), wood, roof, environment);
            House("FarmPlaceholder", new Vector3(11, 0, 7), new Vector3(6, 3.5f, 4), wood, roof, environment);
            Block("Rock_A", new Vector3(-12, 0.5f, -6), new Vector3(1.7f, 1, 1.3f), stone, environment);
            Block("Rock_B", new Vector3(6, 0.4f, -7), new Vector3(1.3f, 0.8f, 1.5f), stone, environment);

            var markers = new GameObject("LevelMarkers").transform;
            var start = Marker("StartPoint", new Vector3(-13, 0, 0), green, markers);
            var target = Marker("CaptureTarget", new Vector3(11, 0, 3.5f), violet, markers);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(new Vector3(0, 26, -26), Quaternion.Euler(45, 0, 0));
            var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 15;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.08f, 0.10f);

            var lightObject = new GameObject("Moonlight", typeof(Light));
            lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.82f, 0.87f, 1);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;

            var manager = new GameObject("GameManager", typeof(GameManager)).GetComponent<GameManager>();
            var serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("levelCamera").objectReferenceValue = camera;
            serializedManager.FindProperty("ground").objectReferenceValue = ground.GetComponent<Collider>();
            serializedManager.FindProperty("startPoint").objectReferenceValue = start;
            serializedManager.FindProperty("captureTarget").objectReferenceValue = target;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Necromancer Infection";
            PlayerSettings.productName = "Necromancer Infection";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            AssetDatabase.SaveAssets();
            Debug.Log("DemoLevel created and added to Build Settings.");
        }

        private static UnityEngine.Material Material(string name, Color color)
        {
            var material = new UnityEngine.Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0);
            AssetDatabase.CreateAsset(material, "Assets/Materials/" + name + ".mat");
            return material;
        }

        private static GameObject Block(string name, Vector3 position, Vector3 scale,
            UnityEngine.Material material, Transform parent, bool collision = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            return block;
        }

        private static void House(string name, Vector3 position, Vector3 size,
            UnityEngine.Material walls, UnityEngine.Material roof, Transform parent)
        {
            var house = new GameObject(name).transform;
            house.SetParent(parent, false);
            Block("Walls", position + Vector3.up * size.y / 2, size, walls, house);
            Block("Roof", position + Vector3.up * (size.y + 0.25f),
                new Vector3(size.x + 0.5f, 0.5f, size.z + 0.5f), roof, house);
        }

        private static Transform Marker(string name, Vector3 position, UnityEngine.Material material, Transform parent)
        {
            var marker = new GameObject(name).transform;
            marker.SetParent(parent, false);
            marker.position = position;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Visual";
            visual.transform.SetParent(marker, false);
            visual.transform.localPosition = new Vector3(0, 0.04f, 0);
            visual.transform.localScale = new Vector3(1.2f, 0.03f, 1.2f);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            return marker;
        }
    }
}
