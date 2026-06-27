using UnityEngine;

namespace XRPrivacy
{
    // DMM followed by temporal down-sampling (hold) of the anonymized output.
    public class DMMTemporal : DMMCompositeBase
    {
        [Header("Temporal down-sampling (applied to DMM output)")]
        [Tooltip("Hold interval in seconds at 100% strength.")]
        public float maxHoldSeconds = 0.2f;

        private readonly Vector3[] _held = new Vector3[3];
        private readonly float[] _elapsed = new float[3];
        private readonly bool[] _init = new bool[3];

        public override string GetMechanismName() => "DMM+Temporal";

        protected override void ApplySecondary(ref BodyPose p, float strength)
        {
            float interval = maxHoldSeconds * Mathf.Clamp01(strength / 100f);
            p.headPos = Hold(0, p.headPos, interval);
            p.leftPos = Hold(1, p.leftPos, interval);
            p.rightPos = Hold(2, p.rightPos, interval);
        }

        Vector3 Hold(int i, Vector3 pos, float interval)
        {
            if (!_init[i]) { _held[i] = pos; _elapsed[i] = 0f; _init[i] = true; return pos; }
            _elapsed[i] += Time.deltaTime;
            if (interval <= 0f || _elapsed[i] >= interval) { _held[i] = pos; _elapsed[i] = 0f; }
            return _held[i];
        }

        protected override void ResetSecondary()
        {
            for (int i = 0; i < 3; i++) _init[i] = false;
        }
    }
}
