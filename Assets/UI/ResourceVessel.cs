using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Procedural glass and metal, with a bottom-up liquid level.</summary>
    internal static class ResourceVessel
    {
        private static Texture2D frame, liquid, glass;

        internal static void Draw(Rect bounds, float amount, Color pigment, float pulse)
        {
            if (!frame) Build();
            amount = Mathf.Clamp01(amount);
            GUI.color = Color.white;
            GUI.DrawTexture(bounds, frame);
            var bowl = new Rect(bounds.x + bounds.width * .04f, bounds.y + bounds.height * .04f,
                bounds.width * .92f, bounds.height * .92f);
            if (amount > 0)
            {
                var fill = new Rect(bowl.x, bowl.yMax - bowl.height * amount, bowl.width, bowl.height * amount);
                GUI.color = Color.Lerp(pigment, Color.white, pulse * .25f);
                GUI.DrawTextureWithTexCoords(fill, liquid, new Rect(0, 0, 1, amount));
                // A thin meniscus makes the remaining amount readable at a glance.
                float y = 2 * amount - 1;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0, 1 - y * y)) * bowl.width * .5f;
                GUI.color = new Color(pigment.r, pigment.g, pigment.b, .85f);
                GUI.DrawTexture(new Rect(bowl.center.x - halfWidth, fill.y, halfWidth * 2, 1.5f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            GUI.DrawTexture(bounds, glass);
        }

        private static void Build()
        {
            const int size = 256;
            frame = Make("Vessel transparent shell", size);
            liquid = Make("Vessel liquid", size);
            glass = Make("Vessel glass", size);
            var metalPixels = new Color[size * size];
            var liquidPixels = new Color[size * size];
            var glassPixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                float radius = Mathf.Sqrt(u * u + v * v);
                int i = y * size + x;
                if (radius < .98f)
                {
                    float light = Mathf.Clamp01(.5f + (v - u) * .35f);
                    Color c = new Color(.06f, .12f, .16f, .20f);
                    if (radius > .92f)
                    {
                        float edge = Mathf.Sin((radius - .92f) / .06f * Mathf.PI);
                        c = new Color(.65f + light * .25f, .8f + light * .15f, 1, .25f + edge * light * .6f);
                    }
                    c.a *= Mathf.Clamp01((.98f - radius) * size);
                    metalPixels[i] = c;
                }
                if (radius < 1)
                {
                    float glow = Mathf.Sqrt(1 - radius * radius);
                    float ripple = Mathf.Sin(u * 5 + v * 3) * .025f;
                    float shade = Mathf.Clamp01(.24f + glow * .68f + v * .14f + ripple);
                    liquidPixels[i] = new Color(shade, shade, shade, .88f * Mathf.Clamp01((1 - radius) * size));
                }
                if (radius < .94f)
                {
                    float soft = Mathf.Exp(-((u + .30f) * (u + .30f) / .025f + (v - .42f) * (v - .42f) / .1f));
                    float glint = Mathf.Exp(-((u + .36f) * (u + .36f) / .003f + (v - .49f) * (v - .49f) / .024f));
                    float rim = Mathf.Exp(-Mathf.Pow((radius - .87f) / .018f, 2));
                    float crescent = rim * Mathf.Clamp01((v - u - .25f) * 1.2f);
                    float reflection = Mathf.Exp(-((u - .5f) * (u - .5f) / .008f + (v + .38f) * (v + .38f) / .055f));
                    glassPixels[i] = new Color(.83f, .95f, 1, Mathf.Clamp01(soft * .3f + glint * .7f + crescent * .65f + reflection * .35f));
                }
            }
            frame.SetPixels(metalPixels); frame.Apply(false, true);
            liquid.SetPixels(liquidPixels); liquid.Apply(false, true);
            glass.SetPixels(glassPixels); glass.Apply(false, true);
        }

        private static Texture2D Make(string name, int size) => new Texture2D(size, size, TextureFormat.RGBA32, false)
        { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
    }
}
