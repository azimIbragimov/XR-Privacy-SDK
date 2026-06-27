using System.Collections.Generic;
using UnityEngine;

namespace XRPrivacy
{
    // Weighted moving-average smoothing over a sliding TIME window. Each frame the
    // newest position is pushed into a per-channel history buffer (with its
    // timestamp), and the output is a recency-weighted average of all samples within
    // the active window (newest weighted highest). The window length in seconds is
    // driven by the strength slider, so higher strength => longer window => heavier
    // smoothing and more visible lag. Time-based, so it is frame-rate independent.
    public class SmoothingMechanism : MonoBehaviour, INoiseGenerator, IPositionPrivacyMechanism
    {
        [Header("Weighted Window Smoothing")]
        [Tooltip("Window length in SECONDS at 100% strength. The slider scales the window from 0 " +
                 "(no smoothing) up to this many seconds of history. Frame-rate independent.")]
        public float maxWindowSeconds = 3f;

        // Per-channel history of recent positions and their timestamps, indexed by (int)XRChannel.
        private readonly List<Vector3>[] _pos =
        {
            new List<Vector3>(), new List<Vector3>(), new List<Vector3>()
        };
        private readonly List<float>[] _time =
        {
            new List<float>(), new List<float>(), new List<float>()
        };

        public string GetMechanismName()
        {
            return "WeightedSmoothing";
        }

        public Vector3 Privatize(XRChannel channel, Vector3 originalPosition, float strength)
        {
            int c = (int)channel;
            List<Vector3> pos = _pos[c];
            List<float> time = _time[c];
            float now = Time.time;
            float maxWin = Mathf.Max(0f, maxWindowSeconds);

            // Append newest sample.
            pos.Add(originalPosition);
            time.Add(now);

            // Drop samples older than the largest window we might ever use.
            while (time.Count > 1 && now - time[0] > maxWin)
            {
                time.RemoveAt(0);
                pos.RemoveAt(0);
            }

            // Active window grows with strength: 0 s at 0%, maxWindowSeconds at 100%.
            float windowSeconds = Mathf.Clamp01(strength / 100f) * maxWin;
            float cutoff = now - windowSeconds;

            // Recency-weighted average of samples within the window. Weight is the
            // sample's distance past the cutoff, so the newest sample weighs the most
            // (windowSeconds) and a sample right at the window edge weighs ~0.
            Vector3 sum = Vector3.zero;
            float weightSum = 0f;
            for (int i = pos.Count - 1; i >= 0; i--)
            {
                if (time[i] < cutoff) break; // earlier samples are all older too
                float w = time[i] - cutoff;
                sum += pos[i] * w;
                weightSum += w;
            }

            return weightSum > 0f ? sum / weightSum : originalPosition;
        }

        public void ResetState()
        {
            for (int i = 0; i < 3; i++)
            {
                _pos[i].Clear();
                _time[i].Clear();
            }
        }

        // Unused additive path (see SpatialNoise for rationale).
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
