using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NecromancerInfection.Editor
{
    public static class CombatSetup
    {
        [MenuItem("Necromancer Infection/Add Melee Combat")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before configuring combat.");
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene changes before configuring combat.");
            AddStats(NecromancerSetup.PrefabPath, 100, 0);
            AddStats(CivilianSetup.PrefabPath, 30, 0);
            AddStats(InfectionSetup.PrefabPath, 50, 10);
            var civilian = PrefabUtility.LoadPrefabContents(CivilianSetup.PrefabPath);
            try
            {
                var settings = new SerializedObject(civilian.GetComponent<Civilian>());
                if (!settings.FindProperty("conversionPrefab").objectReferenceValue)
                {
                    settings.FindProperty("conversionPrefab").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(InfectionSetup.PrefabPath).GetComponent<Zombie>();
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(civilian, CivilianSetup.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(civilian);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Combat configured: Human 30 HP, Zombie 50 HP / 10 damage, Necromancer 100 HP. Existing combat stats preserved.");
        }

        private static void AddStats(string path, int health, int damage)
        {
            var unit = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var combat = unit.GetComponent<UnitCombat>();
                if (!combat) combat = unit.AddComponent<UnitCombat>();
                var settings = new SerializedObject(combat);
                // Unity may have auto-created the required component during import.
                if (settings.FindProperty("setupVersion").intValue == 0)
                {
                    settings.FindProperty("maxHealth").intValue = health;
                    settings.FindProperty("damage").intValue = damage;
                    settings.FindProperty("attackRange").floatValue = 0.9f;
                    settings.FindProperty("attackCooldown").floatValue = 1;
                    settings.FindProperty("setupVersion").intValue = 1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(unit, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(unit);
            }
        }
    }
}
