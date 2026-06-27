using UnityEngine;

namespace XRPrivacy
{
    // DMM followed by Gaussian position noise on the anonymized output.
    public class DMMGaussian : DMMCompositeBase
    {
        [Header("Gaussian (applied to DMM output)")]
        [Tooltip("Max offset in meters at 100% strength (3-sigma per axis).")]
        public float maxOffsetMeters = 0.21f;

        private readonly System.Random _rng = new System.Random();

        public override string GetMechanismName() => "DMM+Gaussian";

        protected override void ApplySecondary(ref BodyPose p, float strength)
        {
            float sigma = Mathf.Clamp01(strength / 100f) * (maxOffsetMeters / 3f);
            p.headPos += Noise(sigma);
            p.leftPos += Noise(sigma);
            p.rightPos += Noise(sigma);
        }

        Vector3 Noise(float sigma) => new Vector3(G(sigma), G(sigma), G(sigma));

        float G(float sigma)
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return (float)(sigma * System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }
    }
}
