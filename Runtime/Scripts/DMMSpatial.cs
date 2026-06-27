using UnityEngine;

namespace XRPrivacy
{
    // DMM followed by spatial quantization (grid snapping) of the anonymized output.
    public class DMMSpatial : DMMCompositeBase
    {
        [Header("Spatial quantization (applied to DMM output)")]
        [Tooltip("Grid cell size in meters at 100% strength. Operates in play space.")]
        public float maxCellSize = 0.05f;

        public override string GetMechanismName() => "DMM+Spatial";

        protected override void ApplySecondary(ref BodyPose p, float strength)
        {
            float cell = maxCellSize * Mathf.Clamp01(strength / 100f);
            if (cell <= 0f) return;
            p.headPos = Snap(p.headPos, cell);
            p.leftPos = Snap(p.leftPos, cell);
            p.rightPos = Snap(p.rightPos, cell);
        }

        static Vector3 Snap(Vector3 v, float c) =>
            new Vector3(Mathf.Round(v.x / c) * c, Mathf.Round(v.y / c) * c, Mathf.Round(v.z / c) * c);
    }
}
