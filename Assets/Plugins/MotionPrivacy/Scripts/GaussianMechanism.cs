using UnityEngine;

[CreateAssetMenu(menuName = "Privacy/Gaussian Noise")]
public class GaussianNoiseMechanism : PositionPrivacyMechanism, IAdjustablePrivacyParam
{
    [Tooltip("Std dev of Gaussian noise in meters.")]
    public float param = 0.05f;

    public override Vector3 Apply(Vector3 input)
    {
        float x = GenerateGaussianNoise(0f, param);
        float y = GenerateGaussianNoise(0f, param);
        float z = GenerateGaussianNoise(0f, param);
        return input + new Vector3(x, y, z);
    }

    float GenerateGaussianNoise(float mean, float sigma)
    {
        float u1 = 1f - Random.value;
        float u2 = 1f - Random.value;
        float z = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        return mean + sigma * z;
    }

    public void SetParam(float value)
    {
        param = Mathf.Max(0f, value);
    }
}