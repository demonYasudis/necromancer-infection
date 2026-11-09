using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class MenuSceneSetup
    {
        private const string BackdropPath = "Assets/UI/MenuBackdrop.png";

        [MenuItem("Necromancer Infection/Install Main Menu")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the active scene first.");
            if (!File.Exists(MainMenu.ScenePath))
            {
                var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropPath);
                if (!backdrop) throw new InvalidOperationException("Menu backdrop is missing.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.035f, .031f, .024f);
                var menu = new SerializedObject(new GameObject("Main Menu", typeof(MainMenu)).GetComponent<MainMenu>());
                menu.FindProperty("backdrop").objectReferenceValue = backdrop;
                menu.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, MainMenu.ScenePath))
                    throw new InvalidOperationException("Could not save the main menu scene.");
            }
            var first = new EditorBuildSettingsScene(MainMenu.ScenePath, true);
            EditorBuildSettings.scenes = new[] { first }.Concat(
                EditorBuildSettings.scenes.Where(scene => scene.path != MainMenu.ScenePath)).ToArray();
            Debug.Log("MainMenu is now the first scene in the build.");
        }
    }
}
