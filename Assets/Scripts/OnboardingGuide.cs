using UnityEngine;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class OnboardingGuide : MonoBehaviour
    {
        private GameManager manager;
        private InfectionAbility infection;
        private ProtectionAbility protection;
        private float explanationTime;
        private bool protectionLearned;
        public int Step { get; private set; }
        public bool IsComplete => Step >= 3;
        public bool IsVisible { get; private set; }
        public string Message { get; private set; }
        private void Awake() => manager = GetComponent<GameManager>();
        private void OnEnable()
        {
            infection = FindFirstObjectByType<InfectionAbility>();
            protection = FindFirstObjectByType<ProtectionAbility>();
            if (infection) infection.InfectedByPlayer += OnInfection;
            if (protection) protection.CastByPlayer += OnProtection;
        }
        private void OnDisable()
        {
            if (infection) infection.InfectedByPlayer -= OnInfection;
            if (protection) protection.CastByPlayer -= OnProtection;
            IsVisible = false;
        }
        private void OnInfection() { if (Step == 0) Step = 1; }
        private void OnProtection() { protectionLearned = true; if (Step == 2) Skip(); }
        public void Skip() { Step = 3; IsVisible = false; }
        private void LateUpdate()
        {
            IsVisible = false;
            if (!manager || manager.IsFinished || manager.IsRestarting || IsComplete) return;
            var progression = GetComponent<SoulProgression>();
            var notices = GetComponent<GameNotifications>();
            if ((progression && progression.IsChoosing) || (notices && notices.HasMessage)) return;
            if (Step == 0)
            {
                foreach (var civilian in FindObjectsByType<Civilian>(FindObjectsSortMode.None))
                {
                    if (!civilian.IsAvailableTarget || civilian.gameObject.scene != gameObject.scene || !manager.LevelCamera) continue;
                    Vector3 p = manager.LevelCamera.WorldToViewportPoint(civilian.transform.position);
                    if (p.z > 0 && p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1)
                    {
                        IsVisible = true;
                        Message = $"ЛКМ по жителю — заразить и собрать орду. Цена: {(infection ? infection.InfectionCost : 15):0.#} энергии.";
                        break;
                    }
                }
            }
            else if (Step == 1)
            {
                IsVisible = true;
                Message = "Нежить сама следует за некромантом и атакует людей. Энергия восстанавливается со временем.";
                explanationTime += Time.deltaTime;
                if (explanationTime >= 6) { Step = protectionLearned ? 3 : 2; IsVisible = false; }
            }
            else
            {
                foreach (var guard in FindObjectsByType<Guard>(FindObjectsSortMode.None))
                    if (guard.gameObject.scene == gameObject.scene && guard.Combat.IsAlive
                        && Vector3.Distance(guard.transform.position, manager.Necromancer.transform.position) < 18)
                    {
                        IsVisible = true;
                        Message = $"Впереди стража! ПКМ по земле — защита орды на 5 секунд. Цена: {(protection ? protection.EnergyCost : 25):0.#}.";
                        break;
                    }
            }
        }
    }
}
