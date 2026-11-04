using System;
using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Attempt-local experience. Modal selection owns only the pause it creates.</summary>
    [DisallowMultipleComponent]
    public sealed class SoulProgression : MonoBehaviour
    {
        public enum Upgrade { EnergyCapacity, Regeneration, InfectionCost, ProtectionCost }
        private const int CapacityBonus = 20;
        private const float RegenerationBonus = .5f;
        private GameManager manager;
        private readonly List<Upgrade> offers = new List<Upgrade>();
        private readonly int[] ranks = new int[4];
        private System.Random random;
        private bool ownsPause;
        private float previousScale;
        private int unlockFrame;
        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int RequiredExperience => 4 + (Level - 1) * 3;
        public bool IsChoosing => isActiveAndEnabled && manager && !manager.IsFinished && offers.Count == 3;
        public IReadOnlyList<Upgrade> Offers => offers;
        public int Rank(Upgrade upgrade) => ranks[(int)upgrade];

        private void Awake() { manager = GetComponent<GameManager>(); random = new System.Random(); }
        private void OnEnable() { Zombie.Raised += OnRaised; UnitCombat.AnyDied += OnDied; }
        private void OnDisable()
        {
            Zombie.Raised -= OnRaised; UnitCombat.AnyDied -= OnDied;
            ReleasePause();
        }
        private void OnRaised(Zombie zombie)
        {
            if (manager && zombie.gameObject.scene == gameObject.scene && zombie.Leader == manager.Necromancer
                && !zombie.GetComponent<RaisedGuard>() && !zombie.GetComponent<Skeleton>()) AddExperience(1);
        }
        private void OnDied(UnitCombat victim)
        {
            if (victim.gameObject.scene == gameObject.scene && (victim.GetComponent<Guard>() || victim.GetComponent<Crossbowman>()))
                AddExperience(3);
        }
        public void AddExperience(int amount)
        {
            if (!isActiveAndEnabled || !manager || manager.IsFinished || manager.IsRestarting
                || !manager.Necromancer || !manager.Necromancer.Combat.IsAlive || amount <= 0) return;
            Experience = (int)Math.Min((long)Experience + amount, 1000000);
            // Open in LateUpdate so a kill's other rewards and victory callbacks finish first.
        }
        private void LateUpdate()
        {
            if (!manager || manager.IsFinished || manager.IsRestarting)
            { offers.Clear(); ReleasePause(); return; }
            if (IsChoosing && !ownsPause && Time.timeScale > 0)
            {
                previousScale = Time.timeScale; ownsPause = true; Time.timeScale = 0;
                unlockFrame = Time.frameCount + 1;
            }
            if (!IsChoosing && Experience >= RequiredExperience && Time.timeScale > 0)
            {
                Experience -= RequiredExperience; Level++;
                var pool = new List<Upgrade>((Upgrade[])Enum.GetValues(typeof(Upgrade)));
                for (int i = 0; i < 3; i++)
                {
                    int index = random.Next(pool.Count); offers.Add(pool[index]); pool.RemoveAt(index);
                }
                previousScale = Time.timeScale; ownsPause = true; Time.timeScale = 0;
                unlockFrame = Time.frameCount + 1;
            }
        }
        private void ReleasePause()
        {
            if (!ownsPause) return;
            if (Time.timeScale == 0 && (!manager || !manager.IsFinished)) Time.timeScale = previousScale;
            ownsPause = false;
        }
        public bool TryChoose(int index)
        {
            if (!IsChoosing || !ownsPause || Time.frameCount <= unlockFrame || index < 0 || index >= offers.Count) return false;
            var upgrade = offers[index];
            var infection = FindFirstObjectByType<InfectionAbility>();
            var protection = FindFirstObjectByType<ProtectionAbility>();
            switch (upgrade)
            {
                case Upgrade.EnergyCapacity: manager.Necromancer.Energy.IncreaseCapacity(CapacityBonus); break;
                case Upgrade.Regeneration: manager.Necromancer.Energy.IncreaseRegeneration(RegenerationBonus); break;
                case Upgrade.InfectionCost: if (!infection) return false; infection.ReduceCost(); break;
                case Upgrade.ProtectionCost: if (!protection) return false; protection.ReduceCost(); break;
            }
            ranks[(int)upgrade]++;
            offers.Clear(); ReleasePause();
            return true;
        }
        public static string Title(Upgrade upgrade)
        {
            switch (upgrade)
            {
                case Upgrade.EnergyCapacity: return "СОСУД ДУШ";
                case Upgrade.Regeneration: return "ТЁМНЫЙ ИСТОЧНИК";
                case Upgrade.InfectionCost: return "ЗАРАЗНОЕ ПРИКОСНОВЕНИЕ";
                default: return "БЕРЕЖНЫЙ РИТУАЛ";
            }
        }
        public static string Description(Upgrade upgrade) => upgrade switch
        {
            Upgrade.EnergyCapacity => $"Больше тёмной энергии. Сразу +{CapacityBonus}.",
            Upgrade.Regeneration => "Энергия восстанавливается быстрее.",
            Upgrade.InfectionCost => "Заражение стоит на 20% меньше.",
            _ => "Защита стоит на 20% меньше."
        };

        public string StatChange(Upgrade upgrade)
        {
            switch (upgrade)
            {
                case Upgrade.EnergyCapacity:
                    float maximum = manager.Necromancer.Energy.Maximum;
                    return $"{maximum:0} → {maximum + CapacityBonus:0} эн.";
                case Upgrade.Regeneration:
                    float regeneration = manager.Necromancer.Energy.Regeneration;
                    return $"{regeneration:0.#} → {regeneration + RegenerationBonus:0.#} эн./с";
                case Upgrade.InfectionCost:
                    var infection = FindFirstObjectByType<InfectionAbility>();
                    float cost = infection ? infection.InfectionCost : 15;
                    return $"{cost:0.##} → {cost * .8f:0.##} эн.";
                default:
                    var protection = FindFirstObjectByType<ProtectionAbility>();
                    float price = protection ? protection.EnergyCost : 25;
                    return $"{price:0.##} → {price * .8f:0.##} эн.";
            }
        }
    }
}
