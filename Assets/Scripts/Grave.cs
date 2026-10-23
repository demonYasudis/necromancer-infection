using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class Grave : MonoBehaviour
    {
        [SerializeField] private Zombie skeletonPrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField, Min(0)] private float cost = 30;
        [SerializeField] private GameObject availableMarker;
        public float Cost => cost;
        public int SkeletonCount => spawnPoints == null ? 0 : spawnPoints.Length;
        public bool IsConsumed { get; private set; }

        public bool TryRaise(Necromancer leader)
        {
            if (!isActiveAndEnabled || IsConsumed || Time.timeScale <= 0 || !skeletonPrefab
                || !skeletonPrefab.GetComponent<Skeleton>() || SkeletonCount == 0
                || !leader || !leader.gameObject.activeInHierarchy || !leader.Combat.IsAlive
                || leader.gameObject.scene != gameObject.scene || !leader.Energy.CanSpend(cost)) return false;
            foreach (var point in spawnPoints) if (!point) return false;
            IsConsumed = true;
            if (!leader.Energy.TrySpend(cost)) { IsConsumed = false; return false; }
            if (availableMarker) availableMarker.SetActive(false);
            foreach (var point in spawnPoints)
            {
                var unit = Instantiate(skeletonPrefab, point.position, point.rotation);
                unit.name = "Skeleton";
                unit.Initialize(leader);
            }
            return true;
        }
    }
}
