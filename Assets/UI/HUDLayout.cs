using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>One layout for drawing, pointer rejection and world-label occlusion.</summary>
    public readonly struct HUDLayout
    {
        public readonly float Scale, Width, Height;
        public readonly Rect Status, EnergyOrb, Experience, Village, Objective, Abilities, Warning;
        public float PlayTop => Warning.yMax + 12;
        public float PlayBottom => Status.yMin - 12;

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
            float diameter = Mathf.Min(156, Width * .27f);
            Status = new Rect(-diameter * .18f, Height - diameter * .82f, diameter, diameter);
            EnergyOrb = new Rect(Width - diameter * .82f, Status.y, diameter, diameter);
            Experience = new Rect(0, Height - 12, Width, 12);
            // Empty compatibility slots do not intercept clicks.
            Village = Abilities = Rect.zero;
            Objective = Rect.zero;
            float warningWidth = Mathf.Min(640, Width - 36);
            Warning = new Rect((Width - warningWidth) * .5f, 18, warningWidth, 48);
        }

        public bool Contains(Vector2 point, bool warning = false) => Status.Contains(point) || EnergyOrb.Contains(point) || Experience.Contains(point) || Objective.Contains(point) || (warning && Warning.Contains(point));
        public bool Overlaps(Rect rect, bool warning = false) => Status.Overlaps(rect) || EnergyOrb.Overlaps(rect) || Experience.Overlaps(rect) || Objective.Overlaps(rect) || (warning && Warning.Overlaps(rect));
        public Vector2 FromScreen(Vector2 point, float screenHeight) => new Vector2(point.x, screenHeight - point.y) / Scale;
    }
}
