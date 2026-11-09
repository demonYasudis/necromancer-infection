using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>Lets a visual fall and fade after its gameplay object has died.</summary>
    internal sealed class SpriteFall : MonoBehaviour
    {
        private const float Duration = .58f;
        private Vector3 origin, scale;
        private SpriteRenderer[] images;
        private Color[] colors;
        private float age;

        public static void Begin(Transform visual)
        {
            visual.SetParent(null, true);
            visual.name = "Falling character";
            var fall = visual.gameObject.AddComponent<SpriteFall>();
            fall.origin = visual.position;
            fall.scale = visual.localScale;
            fall.images = visual.GetComponentsInChildren<SpriteRenderer>();
            fall.colors = new Color[fall.images.Length];
            for (int i = 0; i < fall.images.Length; i++) fall.colors[i] = fall.images[i].color;
        }

        private void LateUpdate()
        {
            age += Time.unscaledDeltaTime;
            float fall = Mathf.SmoothStep(0, 1, age / .29f);
            var view = Camera.main;
            if (view)
            {
                transform.rotation = view.transform.rotation * Quaternion.Euler(0, 0, -74 * fall);
                transform.position = origin + view.transform.right * (.27f * fall) - Vector3.up * (.035f * fall);
            }
            transform.localScale = Vector3.Scale(scale, new Vector3(1, Mathf.Lerp(1, .57f, fall), 1));
            float alpha = 1 - Mathf.InverseLerp(.22f, Duration, age);
            for (int i = 0; i < images.Length; i++)
            {
                Color color = colors[i];
                color.a *= alpha;
                images[i].color = color;
            }
            if (age >= Duration) Destroy(gameObject);
        }
    }
}
