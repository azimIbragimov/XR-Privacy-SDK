using UnityEngine;

namespace XRPrivacy
{
    // Spatial quantization. Snaps each position to a regular 3D grid so that fine
    // spatial detail (which carries biometric signal) is discarded. The grid cell
    // size grows with strength, trading precision for privacy. Stateless: the
    // output depends only on the current position.
    public class SpatialNoise : MonoBehaviour, INoiseGenerator, IPositionPrivacyMechanism
    {
        [Header("Spatial Quantization")]
        [Tooltip("Grid cell size in meters at 100% strength. Larger means coarser snapping and more privacy.")]
        public float maxCellSize = 0.05f;

        public string GetMechanismName()
        {
            return "SpatialQuantization";
        }

        public Vector3 Privatize(XRChannel channel, Vector3 originalPosition, float strength)
        {
            float t = Mathf.Clamp01(strength / 100f);
            float cell = maxCellSize * t;
            if (cell <= 0f) return originalPosition;

            return new Vector3(
                Mathf.Round(originalPosition.x / cell) * cell,
                Mathf.Round(originalPosition.y / cell) * cell,
                Mathf.Round(originalPosition.z / cell) * cell
            );
        }

        public void ResetState()
        {
            // Stateless mechanism, nothing to reset.
        }

        // INoiseGenerator additive methods are unused for this mechanism because
        // snapping needs the position, not an offset. They return zero so the
        // mechanism is a no-op if ever driven through the legacy additive path.
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
