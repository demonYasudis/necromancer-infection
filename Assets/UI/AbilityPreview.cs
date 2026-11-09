using UnityEngine;
using UnityEngine.Rendering;

namespace NecromancerInfection
{
    [DisallowMultipleComponent]
    public sealed class AbilityPreview : MonoBehaviour
    {
        private GameManager manager;
        private InfectionAbility infection;
        private ProtectionAbility protection;
        private LineRenderer ring;
        private Material material;
        private GUIStyle style;
        private string previousCaption;
        private float hintElapsed;
        public bool IsHintVisible => Caption != null && hintElapsed < 3;
        public string Caption { get; private set; }
        public string UnavailableReason { get; private set; }
        internal Texture2D Icon { get; private set; }
        internal float Cost { get; private set; }
        public bool IsAvailable { get; private set; }
        public bool HasPreview => ring && ring.enabled;
        public float PreviewRadius { get; private set; }

        private void Awake()
        {
            manager = GetComponent<GameManager>();
            infection = FindFirstObjectByType<InfectionAbility>();
            protection = FindFirstObjectByType<ProtectionAbility>();
            var visual = new GameObject("AbilityPreviewRing");
            visual.transform.SetParent(transform, false);
            ring = visual.AddComponent<LineRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            ring.sharedMaterial = material;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 64;
            ring.widthMultiplier = 0.065f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.enabled = false;
        }

        private void LateUpdate()
        {
            Refresh(Input.mousePosition);
            if (Caption == null) { previousCaption = null; hintElapsed = 0; return; }
            if (Caption != previousCaption) { previousCaption = Caption; hintElapsed = 0; }
            hintElapsed += Time.deltaTime;
        }

        public void Refresh(Vector2 pointer)
        {
            Caption = null;
            UnavailableReason = null;
            IsAvailable = false;
            ring.enabled = false;
            PreviewRadius = 0;
            if (!isActiveAndEnabled || !manager || manager.IsFinished || manager.IsRestarting
                || Time.timeScale <= 0 || !Application.isFocused || AbilityPointer.IsBlocked(pointer)) return;
            if (infection && infection.TryGetTarget(pointer, out var target, out var action, out var cost, out var reason))
            {
                Icon = target.GetComponent<Civilian>() ? HUDIcons.Infection : HUDIcons.Raise;
                Cost = cost;
                IsAvailable = reason == null;
                UnavailableReason = reason;
                Caption = $"ЛКМ · {action} · {cost:0.##} энергии\n" + (reason ?? "Нажмите, чтобы применить");
                if (target.TryGetComponent<DefenderInfection>(out var defender))
                {
                    float total = target.GetComponent<Crossbowman>() ? infection.ArcherInfectionCost : infection.SoldierInfectionCost;
                    Caption = $"ЛКМ · {action}\n" + (reason ?? $"{total:0.##} энергии на последнем клике");
                }
                DrawRing(target.position, target.GetComponent<Grave>() ? 1.2f : 0.65f,
                    IsAvailable ? new Color(0.6f, 1, 0.3f) : new Color(1, 0.55f, 0.25f));
                return;
            }
            if (!protection || !protection.AreaPrefab) return;
            Icon = HUDIcons.Protection;
            Cost = protection.EnergyCost;
            if (protection.TryGetPlacement(pointer, out var point, out var unavailable))
            {
                IsAvailable = unavailable == null;
                UnavailableReason = unavailable;
                Caption = $"ПКМ · Защита · {protection.EnergyCost:0.##} энергии\n" + (unavailable ??
                    $"−{protection.AreaPrefab.DamageReduction * 100:0}% урона · {protection.AreaPrefab.Duration:0.#} сек");
                DrawRing(point, protection.AreaPrefab.Radius,
                    IsAvailable ? new Color(0.8f, 0.5f, 1) : new Color(1, 0.55f, 0.25f));
            }
            else
            {
                UnavailableReason = "Выберите свободную землю";
                Caption = $"ПКМ · Защита · {protection.EnergyCost:0.##} энергии\n{UnavailableReason}";
            }
        }

        private void DrawRing(Vector3 center, float radius, Color color)
        {
            PreviewRadius = radius;
            ring.enabled = true;
            ring.startColor = ring.endColor = color;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / ring.positionCount;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0.09f, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDisable()
        {
            if (ring) ring.enabled = false;
            Caption = null;
            UnavailableReason = null;
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
        }

        private void OnGUI()
        {
            if (!IsHintVisible || !manager || manager.IsFinished || Time.timeScale <= 0) return;
            var layout = new HUDLayout(Screen.width, Screen.height);
            float scale = layout.Scale;
            if (scale <= 0) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            var matrix = GUI.matrix;
            var color = GUI.color;
            var depth = GUI.depth;
            try
            {
                GUI.depth = -10;
                GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
                float width = Screen.width / scale;
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / scale;
                // Keep the hint in the play area between the top HUD and alarm banner.
                float hintWidth = Mathf.Min(340, width - 16);
                float top = Mathf.Min(layout.PlayTop, layout.PlayBottom - 68);
                var box = new Rect(Mathf.Clamp(mouse.x + 20, 8, width - hintWidth - 8),
                    Mathf.Clamp(mouse.y + 22, top, layout.PlayBottom - 68), hintWidth, 68);
                Color accent = Icon == HUDIcons.Protection ? InterfaceTheme.Purple
                    : Icon == HUDIcons.Raise ? InterfaceTheme.Brass : InterfaceTheme.Green;
                InterfaceTheme.Panel(box, IsAvailable ? accent : new Color(1, 0.65f, 0.38f));
                HUDIcons.Draw(new Rect(box.x + 9, box.center.y - 15, 30, 30), Icon, accent, !IsAvailable);
                GUI.color = InterfaceTheme.Ink;
                GUI.Label(new Rect(box.x + 44, box.y + 4, box.width - 56, box.height - 8), Caption, style);
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
    }
}
