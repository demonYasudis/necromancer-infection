using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Presentation only: unit health, horde size, rewards and capture feedback.</summary>
    [DisallowMultipleComponent]
    public sealed class GameFeedback : MonoBehaviour
    {
        private struct RewardLabel { public Vector3 Position; public float Amount; public float BornAt; }
        private readonly List<RewardLabel> rewards = new List<RewardLabel>();
        private GameManager manager;
        private DarkEnergy energy;
        private FarmAssault assault;
        private UnitCombat[] units = new UnitCombat[0];
        private GuardCorpse[] corpses = new GuardCorpse[0];
        private Grave[] graves = new Grave[0];
        private InfectionAbility infection;
        private AlarmBell alarm;
        private bool HasAlarmMessage => alarm && (alarm.IsRaised || alarm.IsPrevented || (alarm.Runner && alarm.Runner.IsRunning));
        private Rect AlarmPanel => new Rect((Screen.width / Scale - 620) * 0.5f, Screen.height / Scale - 68, 620, 48);
        private float nextRefresh;
        private GUIStyle label;
        private GUIStyle small;
        private float shakeEndsAt;
        private Vector3 cameraOffset;
        private Camera shakenCamera;
        public int HordeSize { get; private set; }
        public int RewardLabelCount => rewards.Count;
        public bool IsShaking => Time.unscaledTime < shakeEndsAt;
        private float Scale => Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        private Rect Panel => new Rect(Screen.width / Scale - 278, 18, 260, 96);

        private void OnEnable()
        {
            manager = GetComponent<GameManager>();
            infection = FindFirstObjectByType<InfectionAbility>();
            alarm = FindFirstObjectByType<AlarmBell>();
            if (!manager) return;
            energy = manager.Necromancer ? manager.Necromancer.Energy : null;
            if (energy) energy.Rewarded += OnReward;
            assault = manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            if (assault) assault.Captured += OnCaptured;
            Zombie.Raised += OnRaised;
            RefreshUnits();
        }

        private void OnDisable()
        {
            if (energy) energy.Rewarded -= OnReward;
            if (assault) assault.Captured -= OnCaptured;
            Zombie.Raised -= OnRaised;
            RestoreCamera();
            shakeEndsAt = 0;
            rewards.Clear();
        }

        private void OnReward(float amount, Vector3 position)
        {
            if (manager.IsFinished) return;
            if (rewards.Count == 24) rewards.RemoveAt(0);
            rewards.Add(new RewardLabel { Position = position + Vector3.up * 2, Amount = amount, BornAt = Time.time });
        }

        private void OnRaised(Zombie zombie)
        {
            if (manager.IsFinished || zombie.Leader != manager.Necromancer || zombie.gameObject.scene != gameObject.scene) return;
            MagicMotes.Conversion(zombie.transform.position);
            nextRefresh = 0;
        }

        private void OnCaptured()
        {
            if (manager.IsDefeated) return;
            shakeEndsAt = Time.unscaledTime + 0.45f;
        }

        private void RestoreCamera()
        {
            if (shakenCamera) shakenCamera.transform.localPosition -= cameraOffset;
            cameraOffset = Vector3.zero;
            shakenCamera = null;
        }

        private void LateUpdate()
        {
            RestoreCamera();
            if (!manager) return;
            if (Time.time >= nextRefresh)
            {
                RefreshUnits();
                nextRefresh = Time.time + 0.15f;
            }
            rewards.RemoveAll(item => Time.time - item.BornAt >= 1.2f);
            if (!IsShaking || !manager.LevelCamera) return;
            float strength = Mathf.Clamp01((shakeEndsAt - Time.unscaledTime) / 0.45f) * 0.09f;
            shakenCamera = manager.LevelCamera;
            // Unscaled time lets this short presentation finish on the frozen victory screen.
            cameraOffset = new Vector3(Mathf.Sin(Time.unscaledTime * 83), Mathf.Sin(Time.unscaledTime * 107), 0) * strength;
            shakenCamera.transform.localPosition += cameraOffset;
        }

        private void RefreshUnits()
        {
            units = FindObjectsByType<UnitCombat>(FindObjectsSortMode.None);
            corpses = FindObjectsByType<GuardCorpse>(FindObjectsSortMode.None);
            graves = FindObjectsByType<Grave>(FindObjectsSortMode.None);
            HordeSize = 0;
            foreach (var unit in units)
            {
                if (!unit || unit.gameObject.scene != gameObject.scene || !unit.IsAlive || !unit.isActiveAndEnabled) continue;
                if (unit.TryGetComponent<Zombie>(out var zombie) && zombie.Leader == manager.Necromancer) HordeSize++;
                else if (unit.GetComponent<Necromancer>() == manager.Necromancer) HordeSize++;
            }
        }

        public bool ContainsScreenPosition(Vector2 position)
        {
            if (Scale <= 0) return false;
            Vector2 point = new Vector2(position.x, Screen.height - position.y) / Scale;
            return Panel.Contains(point) || (HasAlarmMessage && AlarmPanel.Contains(point));
        }

        private void OnGUI()
        {
            if (!manager || Scale <= 0) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            }
            var matrix = GUI.matrix;
            var color = GUI.color;
            int depth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
                if (IsShaking)
                {
                    GUI.depth = -110;
                    Fill(new Rect(0, 0, Screen.width / Scale, Screen.height / Scale),
                        new Color(0.6f, 0.3f, 0.9f, (shakeEndsAt - Time.unscaledTime) * 0.3f));
                }
                if (manager.IsFinished) return;
                GUI.depth = 0;
                if (HasAlarmMessage)
                {
                    var warning = AlarmPanel;
                    Fill(warning, new Color(0.08f, 0.055f, 0.035f, 0.96f));
                    GUI.color = alarm.IsPrevented ? new Color(0.6f, 1, 0.4f) : new Color(1, 0.65f, 0.2f);
                    string message = alarm.IsRaised ? "ТРЕВОГА! Прибыл дополнительный патруль"
                        : alarm.IsPrevented ? "ТРЕВОГА ПРЕДОТВРАЩЕНА"
                        : "ЖИТЕЛЬ БЕЖИТ К КОЛОКОЛУ — ЗАРАЗИТЕ ЕГО!";
                    GUI.Label(new Rect(warning.x + 8, warning.y + 10, warning.width - 16, 28), message, label);
                }
                var panel = Panel;
                Fill(panel, new Color(0.055f, 0.06f, 0.07f, 0.96f));
                GUI.color = new Color(0.65f, 0.95f, 0.4f);
                GUI.Label(new Rect(panel.x + 8, 24, 244, 28), $"НЕЖИТЬ: {HordeSize}", label);
                GUI.color = new Color(0.8f, 0.78f, 0.84f);
                GUI.Label(new Rect(panel.x + 8, 50, 244, 22), "Орда и некромант", small);
                var leader = manager.Necromancer;
                if (leader)
                {
                    Fill(new Rect(panel.x + 16, 79, 228, 14), new Color(0.18f, 0.13f, 0.2f));
                    Fill(new Rect(panel.x + 16, 79, 228 * leader.CurrentHealth / leader.MaxHealth, 14), new Color(0.6f, 0.32f, 0.85f));
                    GUI.color = Color.white;
                    GUI.Label(new Rect(panel.x + 16, 76, 228, 22), $"Некромант: {leader.CurrentHealth:0.#} / {leader.MaxHealth} HP", small);
                }
                foreach (var unit in units)
                {
                    if (!unit || unit.gameObject.scene != gameObject.scene || !unit.isActiveAndEnabled || !unit.IsAlive) continue;
                    if (!Project(unit.transform.position + Vector3.up * 2.1f, out var point)) continue;
                    if (point.y < 150 || Panel.Contains(point) || (point.x < 365 && point.y < 195)) continue;
                    float width = unit.GetComponent<Necromancer>() ? 64 : 38;
                    Fill(new Rect(point.x - width / 2 - 1, point.y - 1, width + 2, 7), new Color(0.025f, 0.02f, 0.035f, 0.95f));
                    Color tint = unit.GetComponent<UndeadPresence>() ? new Color(0.45f, 0.9f, 0.2f)
                        : unit.GetComponent<Guard>() || unit.GetComponent<Crossbowman>()
                            ? new Color(0.75f, 0.4f, 0.3f) : new Color(0.7f, 0.66f, 0.49f);
                    Fill(new Rect(point.x - width / 2, point.y, width * unit.CurrentHealth / unit.MaxHealth, 5), tint);
                    if (unit.TryGetComponent<Crossbowman>(out var archer))
                    {
                        GUI.color = new Color(1, 0.7f, 0.25f);
                        string caption = archer.State == Crossbowman.BehaviourState.Aiming
                            ? $"Выстрел: {archer.AimRemaining:0.0} с" : "Арбалетчик";
                        GUI.Label(new Rect(point.x - 65, point.y - 23, 130, 22), caption, small);
                    }
                    if (unit.TryGetComponent<AlarmRunner>(out var runner) && runner.IsRunning)
                    {
                        GUI.color = new Color(1, 0.8f, 0.2f);
                        GUI.Label(new Rect(point.x - 100, point.y - 23, 200, 22), "БЕЖИТ К КОЛОКОЛУ", small);
                    }
                }
                foreach (var corpse in corpses)
                {
                    if (!corpse || corpse.IsConsumed || corpse.gameObject.scene != gameObject.scene || !infection
                        || !Project(corpse.transform.position + Vector3.up * 0.8f, out var point)) continue;
                    if (point.y < 195 && (point.x < 365 || Panel.Contains(point))) continue;
                    GUI.color = infection.Energy && infection.Energy.CanSpend(infection.RaiseGuardCost)
                        ? new Color(0.85f, 0.7f, 1) : new Color(1, 0.65f, 0.42f);
                    GUI.Label(new Rect(point.x - 90, point.y - 12, 180, 24),
                        $"ЛКМ · Поднять: {infection.RaiseGuardCost:0}", small);
                }
                foreach (var grave in graves)
                {
                    if (!grave || grave.gameObject.scene != gameObject.scene
                        || !Project(grave.transform.position + Vector3.up * 1.5f, out var point)) continue;
                    if (point.y < 195 && (point.x < 365 || Panel.Contains(point))) continue;
                    bool affordable = energy && energy.CanSpend(grave.Cost);
                    GUI.color = grave.IsConsumed ? Color.gray : affordable
                        ? new Color(0.85f, 0.7f, 1) : new Color(1, 0.65f, 0.42f);
                    string text = grave.IsConsumed ? "МОГИЛА ПУСТА" :
                        $"ЛКМ · {grave.SkeletonCount} скелета: {grave.Cost:0}" + (affordable ? "" : " · мало энергии");
                    GUI.Label(new Rect(point.x - 160, point.y - 12, 320, 24), text, small);
                }
                for (int i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i];
                    float age = Time.time - reward.BornAt;
                    if (!Project(reward.Position + Vector3.up * age * 0.9f, out var point)) continue;
                    GUI.color = new Color(0.66f, 1, 0.3f, Mathf.Clamp01((1.2f - age) * 3));
                    GUI.Label(new Rect(point.x - 65, point.y - i % 3 * 14, 130, 26), $"+{reward.Amount:0.#} энергии", label);
                }
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }

        private bool Project(Vector3 world, out Vector2 position)
        {
            position = default;
            if (!manager.LevelCamera) return false;
            Vector3 point = manager.LevelCamera.WorldToScreenPoint(world);
            if (point.z <= 0 || point.x < 0 || point.y < 0 || point.x > Screen.width || point.y > Screen.height) return false;
            position = new Vector2(point.x, Screen.height - point.y) / Scale;
            return true;
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }
    }
}
