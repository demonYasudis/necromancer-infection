using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class FeedbackSetup
    {
        [MenuItem("Necromancer Infection/Add Feedback UI")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding feedback UI.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var manager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (!manager) throw new InvalidOperationException("DemoLevel requires GameManager.");
            if (!manager.GetComponent<GameFeedback>()) manager.gameObject.AddComponent<GameFeedback>();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Health bars, horde counter, energy rewards and capture feedback configured.");
        }
    }
}
