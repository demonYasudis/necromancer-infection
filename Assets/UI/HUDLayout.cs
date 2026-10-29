using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>One layout for drawing, pointer rejection and world-label occlusion.</summary>
    public readonly struct HUDLayout
    {
        public readonly float Scale, Width, Height;
        public readonly Rect Status, Horde, Objective, Abilities, Warning;
        public float PlayTop => Mathf.Max(Status.yMax, Horde.y < Height * 0.5f ? Horde.yMax : 0, Objective.yMax) + 12;
        public float PlayBottom => Mathf.Min(Warning.yMin, Horde.y > Height * 0.5f ? Horde.yMin : Height) - 12;

        public HUDLayout(float width, float height)
        {
            width = Mathf.Max(1, width); height = Mathf.Max(1, height);
            float scale = Mathf.Max(0.65f, Mathf.Min(width / 1280, height / 720));
            for (int i = 0; i < 3; i++)
            {
                float minimumHeight = width / scale < 738 ? 760 : 560;
                scale = Mathf.Min(scale, height / minimumHeight);
            }
            Scale = scale; Width = width / scale; Height = height / scale;
            bool stacked = Width < 738;
            bool twoRows = Width < 1140;
            Status = new Rect(18, 18, stacked ? Width - 36 : 324, 148);
            Horde = stacked ? new Rect(18, Height - 324, Width - 36, 148)
                : new Rect(Width - 302, twoRows ? Height - 166 : 18, 284, 148);
            Objective = stacked ? new Rect(18, 182, Width - 36, 128)
                : twoRows ? new Rect(Width - 378, 18, 360, 128)
                : new Rect((Width - 360) * 0.5f, 18, 360, 128);
            float controlsWidth = Mathf.Min(640, Width - 36);
            Abilities = twoRows && !stacked ? new Rect(18, Height - 98, Width - 356, 80)
                : new Rect((Width - controlsWidth) * 0.5f, Height - 98, controlsWidth, 80);
            Warning = twoRows && !stacked ? new Rect(18, Height - 226, Width - 36, 48)
                : new Rect((Width - controlsWidth) * 0.5f, Height - 158, controlsWidth, 48);
        }

        public bool Contains(Vector2 point, bool warning = false) => Status.Contains(point) || Horde.Contains(point)
            || Objective.Contains(point) || Abilities.Contains(point) || (warning && Warning.Contains(point));
        public bool Overlaps(Rect rect, bool warning = false) => Status.Overlaps(rect) || Horde.Overlaps(rect)
            || Objective.Overlaps(rect) || Abilities.Overlaps(rect) || (warning && Warning.Overlaps(rect));
        public Vector2 FromScreen(Vector2 point, float screenHeight) => new Vector2(point.x, screenHeight - point.y) / Scale;
    }
}
