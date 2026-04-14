using UnityEngine;

namespace MassCableRemover.UI;

/// <summary>
/// IMGUI dashed ring similar to vanilla hold-to-second-action feedback (screen center).
/// </summary>
internal static class MassRemoveChargeRing
{
    private static Texture2D _pixel;

    private static void EnsurePixel()
    {
        if (_pixel != null)
            return;

        _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _pixel.SetPixel(0, 0, Color.white);
        _pixel.wrapMode = TextureWrapMode.Clamp;
        _pixel.Apply(false, true);
    }

    public static void Draw(float centerX, float centerY, float radius, float fill01)
    {
        EnsurePixel();
        fill01 = Mathf.Clamp01(fill01);

        var trackColor = new Color(1f, 1f, 1f, 0.22f);
        var fillColor = new Color(1f, 1f, 1f, 0.92f);
        const int segments = 72;
        const float dot = 3.2f;

        for (var i = 0; i < segments; i++)
        {
            var t = (i / (float)segments) * Mathf.PI * 2f;
            var x = centerX + Mathf.Cos(t) * radius;
            var y = centerY - Mathf.Sin(t) * radius;
            var isFill = (i + 1) / (float)segments <= fill01;
            GUI.color = isFill ? fillColor : trackColor;
            GUI.DrawTexture(new Rect(x - dot * 0.5f, y - dot * 0.5f, dot, dot), _pixel);
        }

        GUI.color = Color.white;
    }
}
