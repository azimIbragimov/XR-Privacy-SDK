using UnityEngine;

namespace XRPrivacy
{
    // Temporal down-sampling. Reduces the effective update rate by holding the
    // last reported position for a fixed interval before refreshing it. This
    // strips fine temporal structure (velocity/timing signatures) from the motion
    // stream. Higher strength means a longer hold and a lower update rate.
    public class TemporalNoise : MonoBehaviour, INoiseGenerator, IPositionPrivacyMechanism
    {
        [Header("Temporal Down-sampling")]
        [Tooltip("Hold interval in seconds at 100% strength. Larger means a lower effective update rate.")]
        public float maxHoldSeconds = 0.2f;

        // Per-channel held value and elapsed time since last refresh, indexed by (int)XRChannel.
        private readonly Vector3[] _held = new Vector3[3];
        private readonly float[] _elapsed = new float[3];
        private readonly bool[] _initialized = new bool[3];

        public string GetMechanismName()
        {
            return "TemporalDownsampling";
        }

        public Vector3 Privatize(XRChannel channel, Vector3 originalPosition, float strength)
        {
            int i = (int)channel;

            if (!_initialized[i])
            {
                _held[i] = originalPosition;
                _elapsed[i] = 0f;
                _initialized[i] = true;
                return originalPosition;
            }

            float t = Mathf.Clamp01(strength / 100f);
            float interval = maxHoldSeconds * t;

            _elapsed[i] += Time.deltaTime;
            if (interval <= 0f || _elapsed[i] >= interval)
            {
                _held[i] = originalPosition;
                _elapsed[i] = 0f;
            }

            return _held[i];
        }

        public void ResetState()
        {
            for (int i = 0; i < _initialized.Length; i++)
                _initialized[i] = false;
        }

        // Unused additive path (see SpatialNoise for rationale).
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
