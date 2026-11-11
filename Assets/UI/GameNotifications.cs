using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>One priority banner and passive direction markers for live threats.</summary>
    [DisallowMultipleComponent]
    public sealed class GameNotifications : MonoBehaviour
    {
        private sealed class Notice
        {
            public string Key, Text;
            public int Priority, Count = 1;
            public float Remaining;
            public bool Persistent;
        }
        public struct Indicator
        {
            public Rect Bounds;
            public Vector2 Direction;
            public string Text;
            public int Count;
            internal Texture2D Icon;
        }
        private readonly List<Notice> notices = new List<Notice>();
        private readonly List<Indicator> indicators = new List<Indicator>();
        private readonly List<UnitCombat> defenders = new List<UnitCombat>();
        private GameManager manager;
        private AlarmBell bell;
        private FarmAssault assault;
        private SurvivalWaves survival;
        private bool raised, prevented, started;
        private int arrived;
        private Notice current;
        private GUIStyle title, detail;
        private static readonly Color Urgent = new Color32(255, 166, 96, 255);
        public bool IsVisible => isActiveAndEnabled && manager && !manager.IsFinished && !manager.IsRestarting;
        public bool HasMessage => IsVisible && current != null && current.Remaining > 0;
        public string CurrentKey => HasMessage ? current.Key : null;
        public string CurrentText => HasMessage ? current.Text : null;
        public int CurrentCount => HasMessage ? current.Count : 0;
        public int PendingCount => IsVisible ? notices.Count : 0;
        public float CurrentRemaining => HasMessage ? current.Remaining : 0;

        private void OnEnable()
        {
            manager = GetComponent<GameManager>();
            bell = FindFirstObjectByType<AlarmBell>();
            if (bell && bell.gameObject.scene != gameObject.scene) bell = null;
            assault = manager && manager.Farm ? manager.Farm.GetComponent<FarmAssault>() : null;
            survival = GetComponent<SurvivalWaves>();
            // Re-enabling recovers live warnings without replaying historical events.
            raised = bell && bell.IsRaised; prevented = bell && bell.IsPrevented;
            started = assault && assault.HasStarted; arrived = assault ? assault.WavesSpawned : 0;
        }

        private void OnDisable() { notices.Clear(); indicators.Clear(); current = null; }

        private Notice Put(string key, string text, int priority, bool persistent, int occurrences = 0)
        {
            var notice = notices.Find(item => item.Key == key);
            if (notice == null)
            {
                notice = new Notice { Key = key, Count = 0, Remaining = 4 };
                notices.Add(notice);
            }
            notice.Text = text; notice.Priority = priority; notice.Persistent = persistent;
            if (occurrences > 0) { notice.Count += occurrences; notice.Remaining = 4; }
            else notice.Count = 1;
            return notice;
        }

        private void Remove(string key) => notices.RemoveAll(item => item.Key == key);

        private void LateUpdate()
        {
            if (!IsVisible) { notices.Clear(); current = null; return; }
            if (current != null && Time.deltaTime > 0)
            {
                current.Remaining -= Time.deltaTime;
                if (current.Remaining <= 0 && !current.Persistent) notices.Remove(current);
            }
            bool running = bell && bell.isActiveAndEnabled && bell.CanRun && bell.Runner && bell.Runner.IsRunning;
            if (running) Put("runner", "ПОСЫЛЬНЫЙ БЕЖИТ К КОЛОКОЛУ · Заразите его!", 100, true);
            else Remove("runner");
            if (bell && bell.IsRaised && !raised) Put("alarm", "ТРЕВОГА! · Прибыл дополнительный патруль", 95, false, 1);
            if (bell && bell.IsPrevented && !prevented) Put("prevented", "ТРЕВОГА ПРЕДОТВРАЩЕНА", 65, false, 1);
            raised = bell && bell.IsRaised; prevented = bell && bell.IsPrevented;
            bool activeAssault = assault && assault.isActiveAndEnabled && manager.Farm && manager.Farm.isActiveAndEnabled;
            if (activeAssault && assault.HasStarted && !started) Put("assault", "НАЧАЛСЯ ШТУРМ · Защищайте некроманта у фермы", 85, false, 1);
            if (activeAssault && assault.WavesSpawned > arrived)
                Put("arrival", $"ПОДКРЕПЛЕНИЕ ПРИБЫЛО · Волна {assault.WavesSpawned}/{assault.WaveCount}",
                    80, false, assault.WavesSpawned - arrived);
            if (activeAssault && assault.WavesSpawned != arrived) Remove("wave");
            if (activeAssault && assault.HasPendingWave)
                Put("wave", $"ВОЛНА {assault.WavesSpawned + 1}/{assault.WaveCount} · {assault.NextWaveDirection} · {assault.NextWaveSize} бойца · {assault.NextWaveSeconds:0.0} с",
                    assault.NextWaveSeconds <= 3 ? 90 : 50, true);
            else Remove("wave");
            if (survival && survival.HasWarning)
                Put("survival-wave", survival.WarningText, 90, true).Remaining = 4;
            else Remove("survival-wave");
            started = assault && assault.HasStarted; arrived = assault ? assault.WavesSpawned : 0;
            current = null;
            foreach (var notice in notices)
                if (notice.Remaining > 0 && (current == null || notice.Priority > current.Priority)) current = notice;
            GetIndicators(manager.LevelCamera, new HUDLayout(Screen.width, Screen.height), indicators);
        }

        public static bool TryPlaceIndicator(Camera camera, Vector3 world, HUDLayout layout, out Indicator indicator)
        {
            indicator = default;
            if (!camera) return false;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            if (viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1) return false;
            var direction = new Vector2(viewport.x - 0.5f, 0.5f - viewport.y);
            if (viewport.z < 0) direction = -direction;
            if (direction.sqrMagnitude < 0.00001f) direction = Vector2.down;
            direction.Normalize();
            const float width = 156, height = 40;
            var center = new Vector2(layout.Width * 0.5f, (layout.PlayTop + layout.PlayBottom) * 0.5f);
            var extent = new Vector2(Mathf.Max(1, layout.Width * 0.5f - width * 0.5f - 12),
                Mathf.Max(1, (layout.PlayBottom - layout.PlayTop - height) * 0.5f));
            float distance = Mathf.Min(extent.x / Mathf.Max(0.0001f, Mathf.Abs(direction.x)),
                extent.y / Mathf.Max(0.0001f, Mathf.Abs(direction.y)));
            Vector2 position = center + direction * distance;
            indicator = new Indicator { Bounds = new Rect(position.x - width * 0.5f, position.y - height * 0.5f, width, height),
                Direction = direction, Count = 1 };
            return true;
        }

        public void GetIndicators(Camera camera, HUDLayout layout, List<Indicator> result)
        {
            result.Clear();
            if (!IsVisible) return;
            if (bell && bell.isActiveAndEnabled)
            {
                if (bell.CanRun && bell.Runner && bell.Runner.IsRunning)
                    AddIndicator(camera, bell.Runner.transform.position + Vector3.up, "Посыльный", HUDIcons.Bell, layout, result);
                AddGroup(camera, bell.Patrol, "Патруль", HUDIcons.Swords, layout, result);
            }
            if (assault && assault.isActiveAndEnabled && manager.Farm && manager.Farm.isActiveAndEnabled)
            {
                if (assault.HasPendingWave) AddIndicator(camera, assault.NextWavePosition, "Новая волна", HUDIcons.Swords, layout, result);
                AddGroup(camera, assault.Reinforcements, "Стража фермы", HUDIcons.Farm, layout, result);
            }
            if (survival && survival.isActiveAndEnabled)
            {
                if (survival.HasWarning)
                    AddIndicator(camera, survival.WarningPosition, "Новая волна", HUDIcons.Swords, layout, result);
                AddGroup(camera, survival.Reinforcements, "Враги арены", HUDIcons.Swords, layout, result);
            }
        }

        private void AddGroup(Camera camera, Transform group, string caption, Texture2D icon, HUDLayout layout, List<Indicator> result)
        {
            if (!group || !group.gameObject.activeInHierarchy) return;
            // Pick the nearest live offscreen defender; a visible member needs no pointer.
            UnitCombat nearest = null; float distance = float.PositiveInfinity;
            group.GetComponentsInChildren(false, defenders);
            foreach (var unit in defenders)
            {
                if (!unit.IsAlive || !unit.isActiveAndEnabled || !TryPlaceIndicator(camera, unit.transform.position, layout, out _)) continue;
                float candidate = manager.Necromancer ? (unit.transform.position - manager.Necromancer.transform.position).sqrMagnitude : 0;
                if (candidate >= distance) continue;
                nearest = unit; distance = candidate;
            }
            if (nearest) AddIndicator(camera, nearest.transform.position, caption, icon, layout, result);
        }

        private static void AddIndicator(Camera camera, Vector3 position, string caption, Texture2D icon, HUDLayout layout, List<Indicator> result)
        {
            if (!TryPlaceIndicator(camera, position, layout, out var value)) return;
            value.Text = caption;
            value.Icon = icon;
            for (int i = 0; i < result.Count; i++)
            {
                var other = result[i];
                if (!other.Bounds.Overlaps(value.Bounds)) continue;
                other.Count++; other.Text = "Угрозы рядом"; other.Icon = HUDIcons.Swords; result[i] = other; return;
            }
            result.Add(value);
        }

        public bool OverlapsWorldLabel(Rect rect)
        {
            if (!IsVisible) return false;
            var layout = new HUDLayout(Screen.width, Screen.height);
            if (HasMessage && layout.Warning.Overlaps(rect)) return true;
            foreach (var item in indicators) if (item.Bounds.Overlaps(rect)) return true;
            return false;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            if (GetComponent<SoulProgression>() is SoulProgression progression && progression.IsChoosing) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true,
                    normal = { textColor = Color.white }
                };
                detail = new GUIStyle(title) { fontSize = 12, fontStyle = FontStyle.Normal };
            }
            var layout = new HUDLayout(Screen.width, Screen.height);
            var matrix = GUI.matrix; var color = GUI.color; int depth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.Scale(new Vector3(layout.Scale, layout.Scale, 1)); GUI.depth = -30;
                if (HasMessage) DrawNotice(layout.Warning);
                foreach (var item in indicators)
                {
                    var r = item.Bounds;
                    InterfaceTheme.Panel(r, InterfaceTheme.Brass);
                    GUI.color = Urgent;
                    var pivot = new Vector2(r.x + 15, r.center.y);
                    var side = new Vector2(-item.Direction.y, item.Direction.x);
                    Vector2 tip = pivot + item.Direction * 8;
                    DrawLine(pivot - item.Direction * 8, tip);
                    DrawLine(tip, pivot + item.Direction + side * 7);
                    DrawLine(tip, pivot + item.Direction - side * 7);
                    GUI.DrawTexture(new Rect(r.x + 28, r.center.y - 8, 16, 16), item.Icon);
                    GUI.color = InterfaceTheme.Ink;
                    GUI.Label(new Rect(r.x + 49, r.y + 3, r.width - 57, r.height - 6),
                        item.Text + (item.Count > 1 ? $" ×{item.Count}" : ""), detail);
                }
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
        private void DrawNotice(Rect rect)
        {
            bool urgent = current.Priority >= 90;
            Color accent = urgent ? Urgent : current.Key == "prevented" ? InterfaceTheme.Green : InterfaceTheme.Brass;
            var icon = current.Key switch
            {
                "runner" or "alarm" or "prevented" => HUDIcons.Bell,
                "assault" => HUDIcons.Farm,
                _ => HUDIcons.Swords
            };
            InterfaceTheme.Panel(rect, accent);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x + 15, rect.y + 12, 24, 24), icon);
            if (urgent)
            {
                DrawLine(new Vector2(rect.x + 8, rect.y + 16), new Vector2(rect.x + 8, rect.y + 25));
                InterfaceTheme.Fill(new Rect(rect.x + 6.5f, rect.y + 28, 3, 3), accent);
            }

            int separator = current.Text.IndexOf(" · ", System.StringComparison.Ordinal);
            string headline = separator >= 0 ? current.Text.Substring(0, separator) : current.Text;
            string context = separator >= 0 ? current.Text.Substring(separator + 3) : "Дополнительный патруль не придёт";
            if (current.Key == "runner") context = "ЛКМ по посыльному остановит тревогу";
            if (current.Count > 1) headline += $" · ×{current.Count}";
            if (notices.Count > 1) headline += $" · +{notices.Count - 1}";
            var nextWave = notices.Find(item => item.Key == "wave");
            string secondary = nextWave != null && current != nextWave ? nextWave.Text : context;
            GUI.color = urgent ? accent : InterfaceTheme.Ink;
            GUI.Label(new Rect(rect.x + 46, rect.y + 5, rect.width - 57, 19), headline, title);
            GUI.color = nextWave != null && nextWave.Priority >= 90 ? Urgent : InterfaceTheme.Ink;
            GUI.Label(new Rect(rect.x + 46, rect.y + 24, rect.width - 57, 18), secondary, detail);
        }
        private static void DrawLine(Vector2 from, Vector2 to)
        {
            var matrix = GUI.matrix;
            Vector2 delta = to - from;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(from.x, from.y, 0),
                Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg), Vector3.one);
            GUI.DrawTexture(new Rect(0, -1.5f, delta.magnitude, 3), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
