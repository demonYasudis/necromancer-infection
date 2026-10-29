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
        public string Caption { get; private set; }
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

        private void LateUpdate() => Refresh(Input.mousePosition);

        public void Refresh(Vector2 pointer)
        {
            Caption = null;
            IsAvailable = false;
            ring.enabled = false;
            PreviewRadius = 0;
            if (!isActiveAndEnabled || !manager || manager.IsFinished || manager.IsRestarting
                || Time.timeScale <= 0 || !Application.isFocused || AbilityPointer.IsBlocked(pointer)) return;
            if (infection && infection.TryGetTarget(pointer, out var target, out var action, out var cost, out var reason))
            {
                IsAvailable = reason == null;
                Caption = $"ЛКМ · {action} · {cost:0.##} энергии\n" + (reason ?? "Нажмите, чтобы применить");
                DrawRing(target.position, target.GetComponent<Grave>() ? 1.2f : 0.65f,
                    IsAvailable ? new Color(0.6f, 1, 0.3f) : new Color(1, 0.55f, 0.25f));
                return;
            }
            if (!protection || !protection.AreaPrefab) return;
            if (protection.TryGetPlacement(pointer, out var point, out var unavailable))
            {
                IsAvailable = unavailable == null;
                Caption = $"ПКМ · Защита · {protection.EnergyCost:0.##} энергии\n" + (unavailable ??
                    $"−{protection.AreaPrefab.DamageReduction * 100:0}% урона · {protection.AreaPrefab.Duration:0.#} сек");
                DrawRing(point, protection.AreaPrefab.Radius,
                    IsAvailable ? new Color(0.8f, 0.5f, 1) : new Color(1, 0.55f, 0.25f));
            }
            else Caption = $"ПКМ · Защита · {protection.EnergyCost:0.##} энергии\nВыберите свободную землю";
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
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
        }

        private void OnGUI()
        {
            if (Caption == null || !manager || manager.IsFinished || Time.timeScale <= 0) return;
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
                GUI.color = new Color(0.045f, 0.04f, 0.06f, 0.96f);
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                GUI.color = IsAvailable ? new Color(0.8f, 1, 0.7f) : new Color(1, 0.75f, 0.5f);
                GUI.Label(new Rect(box.x + 10, box.y + 4, box.width - 20, box.height - 8), Caption, style);
            }
            finally { GUI.matrix = matrix; GUI.color = color; GUI.depth = depth; }
        }
    }
}
