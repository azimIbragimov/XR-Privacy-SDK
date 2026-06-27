using System.Collections.Generic;
using UnityEngine;

namespace XRPrivacy
{
    // Eye channel: weighted time-window smoothing of the gaze direction (low-pass).
    public class EyeSmoothing : MonoBehaviour, IEyePrivacyMechanism
    {
        [Header("Weighted gaze smoothing")]
        [Tooltip("Window length in seconds at 100% strength. Recency-weighted average of the gaze direction.")]
        public float maxWindowSeconds = 0.5f;

        private readonly List<Vector3> _dir = new List<Vector3>();
        private readonly List<float> _time = new List<float>();

        public Quaternion Privatize(Quaternion gaze, float strength)
        {
            Vector3 fwd = gaze * Vector3.forward;
            float now = Time.time;
            float maxWin = Mathf.Max(0f, maxWindowSeconds);

            _dir.Add(fwd); _time.Add(now);
            while (_time.Count > 1 && now - _time[0] > maxWin) { _time.RemoveAt(0); _dir.RemoveAt(0); }

            float windowSeconds = Mathf.Clamp01(strength / 100f) * maxWin;
            float cutoff = now - windowSeconds;

            Vector3 sum = Vector3.zero;
            float ws = 0f;
            for (int i = _dir.Count - 1; i >= 0; i--)
            {
                if (_time[i] < cutoff) break;
                float w = _time[i] - cutoff;
                sum += _dir[i] * w;
                ws += w;
            }

            Vector3 avg = ws > 0f ? sum.normalized : fwd;
            if (avg.sqrMagnitude < 1e-6f) avg = fwd;
            return Quaternion.LookRotation(avg, gaze * Vector3.up);
        }

        public void ResetState() { _dir.Clear(); _time.Clear(); }
    }
}
