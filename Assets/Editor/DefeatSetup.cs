using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class DefeatSetup
    {
        [MenuItem("Necromancer Infection/Add Defeat Screen")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding the defeat screen.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (!manager) throw new InvalidOperationException("DemoLevel requires GameManager.");
            if (!manager.GetComponent<DefeatScreen>()) manager.gameObject.AddComponent<DefeatScreen>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Defeat screen and restart are configured on DemoLevel.");
        }
    }
}
