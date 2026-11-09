using UnityEngine;

namespace NecromancerInfection
{
    [RequireComponent(typeof(UnitCombat), typeof(Guard))]
    [DisallowMultipleComponent]
    public sealed class GuardRemains : MonoBehaviour
    {
        // Older scene/prefab references supply the undead asset without creating a corpse.
        [SerializeField] private GuardCorpse corpsePrefab;
        [SerializeField] private Zombie raisedGuardPrefab;
        private UnitCombat combat;
        private bool created;

        private void OnEnable()
        {
            combat = GetComponent<UnitCombat>();
            combat.Died += OnDeath;
        }

        private void OnDisable()
        {
            if (combat) combat.Died -= OnDeath;
        }

        private void OnDeath(UnitCombat attacker)
        {
            var prefab = raisedGuardPrefab ? raisedGuardPrefab : corpsePrefab ? corpsePrefab.RaisedGuardPrefab : null;
            if (created || !prefab) return;
            created = true;
            foreach (var manager in FindObjectsByType<GameManager>(FindObjectsSortMode.None))
            {
                if (manager.gameObject.scene != gameObject.scene) continue;
                var leader = manager.Necromancer;
                if (manager.IsFinished || manager.IsRestarting || !leader || !leader.gameObject.activeInHierarchy
                    || !leader.Combat.IsAlive) return;
                // Spawn synchronously so a last boss joins the victory count before time freezes.
                var undead = Instantiate(prefab, transform.position, transform.rotation);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(undead.gameObject, gameObject.scene);
                undead.name = "RaisedGuard";
                undead.GetComponent<CombatMotion>().BeginRising();
                undead.Initialize(leader);
                return;
            }
        }
    }
}
