using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NecromancerInfection.Editor
{
    public static class DarkEnergySetup
    {
        [MenuItem("Necromancer Infection/Add Dark Energy")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save scene changes before adding dark energy.");
            var scene = EditorSceneManager.OpenScene(DemoLevelSetup.ScenePath);
            var root = PrefabUtility.LoadPrefabContents(NecromancerSetup.PrefabPath);
            try
            {
                if (!root.GetComponent<DarkEnergy>()) root.AddComponent<DarkEnergy>();
                PrefabUtility.SaveAsPrefabAsset(root, NecromancerSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var ability = Object.FindFirstObjectByType<InfectionAbility>();
            if (!ability) throw new InvalidOperationException("DemoLevel requires InfectionAbility.");
            if (!ability.GetComponent<DarkEnergyHUD>()) ability.gameObject.AddComponent<DarkEnergyHUD>();
            // Persist the field default for scenes authored before resource costs existed.
            var settings = new SerializedObject(ability);
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ability);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Dark energy pool, paid infection and HUD added. Existing balance settings preserved.");
        }
    }
}
