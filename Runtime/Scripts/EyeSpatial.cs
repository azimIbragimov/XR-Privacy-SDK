using UnityEngine;

namespace XRPrivacy
{
    // Eye channel: spatial (angular) quantization - snap the gaze pitch/yaw to a grid,
    // reducing angular resolution.
    public class EyeSpatial : MonoBehaviour, IEyePrivacyMechanism
    {
        [Header("Angular quantization")]
        [Tooltip("Angular grid cell in degrees at 100% strength. Snap error is at most half a cell.")]
        public float maxCellDegrees = 2f;

        public Quaternion Privatize(Quaternion gaze, float strength)
        {
            float cell = maxCellDegrees * Mathf.Clamp01(strength / 100f);
            if (cell <= 0f) return gaze;

            Vector3 e = gaze.eulerAngles;
            float pitch = Mathf.Round(e.x / cell) * cell;
            float yaw = Mathf.Round(e.y / cell) * cell;
            return Quaternion.Euler(pitch, yaw, e.z);
        }

        public void ResetState() { }
    }
}
