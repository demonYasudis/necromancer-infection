using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class VictorySetup
    {
        [MenuItem("Necromancer Infection/Add Victory Screen")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding victory UI.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (!manager) throw new InvalidOperationException("DemoLevel requires GameManager.");
            if (!manager.GetComponent<VictoryScreen>()) manager.gameObject.AddComponent<VictoryScreen>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Victory screen configured on DemoLevel.");
        }
    }
}
