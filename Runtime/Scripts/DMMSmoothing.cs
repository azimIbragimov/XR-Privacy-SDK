using System.Collections.Generic;
using UnityEngine;

namespace XRPrivacy
{
    // DMM followed by weighted time-window smoothing of the anonymized output.
    public class DMMSmoothing : DMMCompositeBase
    {
        [Header("Weighted smoothing (applied to DMM output)")]
        [Tooltip("Window length in seconds at 100% strength. Recency-weighted moving average.")]
        public float maxWindowSeconds = 3f;

        private readonly List<Vector3>[] _pos =
        {
            new List<Vector3>(), new List<Vector3>(), new List<Vector3>()
        };
        private readonly List<float>[] _time =
        {
            new List<float>(), new List<float>(), new List<float>()
        };

        public override string GetMechanismName() => "DMM+Smoothing";

        protected override void ApplySecondary(ref BodyPose p, float strength)
        {
            p.headPos = Smooth(0, p.headPos, strength);
            p.leftPos = Smooth(1, p.leftPos, strength);
            p.rightPos = Smooth(2, p.rightPos, strength);
        }

        Vector3 Smooth(int c, Vector3 pos, float strength)
        {
            List<Vector3> P = _pos[c];
            List<float> T = _time[c];
            float now = Time.time;
            float maxWin = Mathf.Max(0f, maxWindowSeconds);

            P.Add(pos); T.Add(now);
            while (T.Count > 1 && now - T[0] > maxWin) { T.RemoveAt(0); P.RemoveAt(0); }

            float windowSeconds = Mathf.Clamp01(strength / 100f) * maxWin;
            float cutoff = now - windowSeconds;

            Vector3 sum = Vector3.zero;
            float ws = 0f;
            for (int i = P.Count - 1; i >= 0; i--)
            {
                if (T[i] < cutoff) break;
                float w = T[i] - cutoff;
                sum += P[i] * w;
                ws += w;
            }
            return ws > 0f ? sum / ws : pos;
        }

        protected override void ResetSecondary()
        {
            for (int i = 0; i < 3; i++) { _pos[i].Clear(); _time[i].Clear(); }
        }
    }
}
