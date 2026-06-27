using UnityEngine;

namespace XRPrivacy
{
    // Eye channel: temporal down-sampling - hold the gaze direction for an interval
    // before refreshing, reducing temporal resolution of the gaze stream.
    public class EyeTemporal : MonoBehaviour, IEyePrivacyMechanism
    {
        [Header("Temporal down-sampling")]
        [Tooltip("Hold interval in seconds at 100% strength.")]
        public float maxHoldSeconds = 0.2f;

        private Quaternion _held;
        private bool _init;
        private float _elapsed;

        public Quaternion Privatize(Quaternion gaze, float strength)
        {
            if (!_init) { _held = gaze; _init = true; _elapsed = 0f; return gaze; }

            float interval = maxHoldSeconds * Mathf.Clamp01(strength / 100f);
            _elapsed += Time.deltaTime;
            if (interval <= 0f || _elapsed >= interval) { _held = gaze; _elapsed = 0f; }
            return _held;
        }

        public void ResetState() { _init = false; }
    }
}
