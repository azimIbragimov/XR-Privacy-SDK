using UnityEngine;

namespace XRPrivacy
{
    // Eye channel: Gaussian angular jitter of the gaze direction.
    public class EyeGaussian : MonoBehaviour, IEyePrivacyMechanism
    {
        [Header("Gaussian gaze jitter")]
        [Tooltip("Per-axis angular std-dev in degrees at 100% strength (~3-sigma reaches 3x this). " +
                 "The manager caps total deviation to its Max Eye Angle (2 deg).")]
        public float maxStdDegrees = 0.7f;

        private readonly System.Random _rng = new System.Random();

        public Quaternion Privatize(Quaternion gaze, float strength)
        {
            float sigma = Mathf.Clamp01(strength / 100f) * maxStdDegrees;
            float pitch = G(sigma);
            float yaw = G(sigma);
            return gaze * Quaternion.Euler(pitch, yaw, 0f);
        }

        float G(float sigma)
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return (float)(sigma * System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }

        public void ResetState() { }
    }
}
