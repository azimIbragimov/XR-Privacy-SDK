using UnityEngine;

namespace XRPrivacy
{
    public class GaussianNoise : MonoBehaviour, INoiseGenerator
    {
        private System.Random random = new System.Random();

        [Header("Gaussian Noise")]
        [Tooltip("Maximum offset in meters at 100% strength, interpreted as the 3-sigma reach per axis. " +
                 "Noise is normally distributed and centered on the true pose, so the displacement spans " +
                 "roughly 0..this value and (with the manager's Max Displacement set to the same number) is " +
                 "hard-limited to it. Scales linearly with the strength slider, so 0% = no noise.")]
        public float maxOffsetMeters = 0.21f;

        public string GetMechanismName()
        {
            return "GaussianNoise";
        }

        // Per-axis standard deviation. Scales linearly from 0 (at 0% strength) up to
        // maxOffsetMeters / 3 (at 100%), so the 3-sigma reach equals maxOffsetMeters.
        private float SigmaForStrength(float strength)
        {
            float t = Mathf.Clamp01(strength / 100f);
            return t * (maxOffsetMeters / 3f);
        }

        public Vector3 GenerateEyeNoise(float strength)
        {
            return GenerateNoise(strength);
        }

        public Vector3 GenerateHandNoise(float strength)
        {
            return GenerateNoise(strength);
        }

        public Vector3 GenerateBodyNoise(float strength)
        {
            return GenerateNoise(strength);
        }

        private Vector3 GenerateNoise(float strength)
        {
            float sigma = SigmaForStrength(strength);
            return new Vector3(
                GenerateGaussian(0f, sigma),
                GenerateGaussian(0f, sigma),
                GenerateGaussian(0f, sigma)
            );
        }

        private float GenerateGaussian(float mean, float stdDev)
        {
            // Box-Muller transform for Gaussian distribution
            float u1 = 1f - (float)random.NextDouble();
            float u2 = 1f - (float)random.NextDouble();
            float randStdNormal = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
            return mean + stdDev * randStdNormal;
        }
    }
}
