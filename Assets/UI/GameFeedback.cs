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
        public bool HasAlarmMessage => GetComponent<GameNotifications>() && GetComponent<GameNotifications>().HasMessage;
        private float nextRefresh;
        private GUIStyle label;
        private GUIStyle small;
        private float shakeEndsAt;
        private Vector3 cameraOffset;
        private Camera shakenCamera;
        public int HordeSize { get; private set; }
        public int ZombieCount { get; private set; }
        public int HeavyCount { get; private set; }
        public int SkeletonCount { get; private set; }
        public int RewardLabelCount => rewards.Count;
        public bool IsShaking => Time.unscaledTime < shakeEndsAt;
        private float Scale => new HUDLayout(Screen.width, Screen.height).Scale;
        private GameHUD hud;

        private void OnEnable()
        {
            manager = GetComponent<GameManager>();
            hud = GetComponent<GameHUD>();
            infection = FindFirstObjectByType<InfectionAbility>();

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
            ZombieCount = HeavyCount = SkeletonCount = 0;
            foreach (var unit in units)
            {
                if (!unit || unit.gameObject.scene != gameObject.scene || !unit.IsAlive || !unit.isActiveAndEnabled) continue;
                if (unit.TryGetComponent<Zombie>(out var zombie) && zombie.Leader == manager.Necromancer)
                {
                    HordeSize++;
                    if (unit.GetComponent<RaisedGuard>()) HeavyCount++;
                    else if (unit.GetComponent<Skeleton>()) SkeletonCount++;
                    else ZombieCount++;
                }
                else if (unit.GetComponent<Necromancer>() == manager.Necromancer) HordeSize++;
            }
        }

        public bool ContainsScreenPosition(Vector2 position)
        {
            if (Scale <= 0) return false;
            Vector2 point = new Vector2(position.x, Screen.height - position.y) / Scale;
            var layout = new HUDLayout(Screen.width, Screen.height);
            return (hud && hud.IsVisible && layout.Horde.Contains(layout.FromScreen(position, Screen.height)))
                || (!manager.IsFinished && HasAlarmMessage && layout.Warning.Contains(point));
        }

        private void OnGUI()
        {
            if (!manager || Scale <= 0) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
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
                if (!hud) hud = GetComponent<GameHUD>();
                GUI.depth = 0;
                foreach (var unit in units)
                {
                    if (!unit || unit.gameObject.scene != gameObject.scene || !unit.isActiveAndEnabled || !unit.IsAlive) continue;
                    if (!Project(unit.transform.position + Vector3.up * 2.1f, out var point)) continue;
                    if (HiddenByHUD(new Rect(point.x - 100, point.y - 23, 200, 31))) continue;
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
                    if (HiddenByHUD(new Rect(point.x - 90, point.y - 12, 180, 24))) continue;
                    GUI.color = infection.Energy && infection.Energy.CanSpend(infection.RaiseGuardCost)
                        ? new Color(0.85f, 0.7f, 1) : new Color(1, 0.65f, 0.42f);
                    GUI.Label(new Rect(point.x - 90, point.y - 12, 180, 24),
                        $"ЛКМ · Поднять: {infection.RaiseGuardCost:0}", small);
                }
                foreach (var grave in graves)
                {
                    if (!grave || grave.gameObject.scene != gameObject.scene
                        || !Project(grave.transform.position + Vector3.up * 1.5f, out var point)) continue;
                    if (HiddenByHUD(new Rect(point.x - 160, point.y - 12, 320, 24))) continue;
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
                    if (HiddenByHUD(new Rect(point.x - 65, point.y - i % 3 * 14, 130, 26))) continue;
                    GUI.color = new Color(0.66f, 1, 0.3f, Mathf.Clamp01((1.2f - age) * 3));
                    GUI.Label(new Rect(point.x - 65, point.y - i % 3 * 14, 130, 26), $"+{reward.Amount:0.#} энергии", label);
                }
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }

        private bool HiddenByHUD(Rect rect) => hud && hud.OverlapsWorldLabel(rect, Scale);

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
