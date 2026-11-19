using System;
using System.Collections;
using UnityEngine;

namespace Apex.UI
{
    /// <summary>Minimal unscaled-time tweening for UI transitions (works while the game is paused).</summary>
    public static class UITween
    {
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }

        public static IEnumerator Run(float duration, Action<float> step, Func<float, float> ease = null)
        {
            ease ??= EaseOutCubic;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            step(1f);
        }

        public static IEnumerator Fade(CanvasGroup g, float to, float duration)
        {
            float from = g.alpha;
            yield return Run(duration, k => g.alpha = Mathf.Lerp(from, to, k));
        }

        public static IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float duration, Func<float, float> ease = null)
        {
            yield return Run(duration, k => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k), ease);
        }
    }
}
