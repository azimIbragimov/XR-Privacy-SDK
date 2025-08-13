using UnityEngine;

namespace XRPrivacy
{
    public class GaussianNoise : MonoBehaviour, INoiseGenerator
    {
        private System.Random random = new System.Random();

        public string GetMechanismName()
        {
            return "GaussianNoise";
        }

        public Vector3 GenerateEyeNoise(float strength)
        {
            // Eye/head tracking noise - smaller scale for subtle privacy
            float scale = 0.005f;
            return new Vector3(
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale)
            );
        }

        public Vector3 GenerateHandNoise(float strength)
        {
            // Hand tracking noise - medium scale for natural movement
            float scale = 0.01f;
            return new Vector3(
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale)
            );
        }

        public Vector3 GenerateBodyNoise(float strength)
        {
            // Body tracking noise - larger scale for full body movement
            float scale = 0.015f;
            return new Vector3(
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale),
                GenerateGaussian(0f, strength * scale)
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