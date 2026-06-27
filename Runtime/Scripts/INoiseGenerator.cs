using UnityEngine;

namespace XRPrivacy
{
    // Interface that all noise mechanisms must implement
    public interface INoiseGenerator
    {
        Vector3 GenerateEyeNoise(float strength);
        Vector3 GenerateHandNoise(float strength);
        Vector3 GenerateBodyNoise(float strength);
        string GetMechanismName();
    }
}