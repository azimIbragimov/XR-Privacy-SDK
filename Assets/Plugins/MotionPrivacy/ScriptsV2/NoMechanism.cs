using UnityEngine;

namespace XRPrivacy
{
    public class NoMechanism : MonoBehaviour, INoiseGenerator
    {
        private System.Random random = new System.Random();

        public string GetMechanismName()
        {
            return "NoMechanism";
        }

        public Vector3 GenerateEyeNoise(float strength)
        {
            // Eye/head tracking noise - smaller scale for subtle privacy
            float scale = 0.005f;
            return new Vector3(
                0,
                0,
                0
            );
        }

        public Vector3 GenerateHandNoise(float strength)
        {
            // Hand tracking noise - medium scale for natural movement
            float scale = 0.01f;
            return new Vector3(
                0,
                0,
                0
            );
        }

        public Vector3 GenerateBodyNoise(float strength)
        {
            // Body tracking noise - larger scale for full body movement
            float scale = 0.015f;
            return new Vector3(
                0,
                0,
                0
            );
        }

    }
}