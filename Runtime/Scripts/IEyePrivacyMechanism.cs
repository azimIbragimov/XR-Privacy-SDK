using UnityEngine;

namespace XRPrivacy
{
    // Eye-channel privacy: perturb the GAZE DIRECTION angularly (not positionally).
    // 'gaze' is a rotation whose forward (gaze * Vector3.forward) is the look direction.
    // Implementations return a privatized gaze rotation; the manager additionally
    // hard-caps the result to a small maximum angular deviation from the true gaze
    // (default 2 degrees), so mechanisms only need to aim for that budget.
    public interface IEyePrivacyMechanism
    {
        Quaternion Privatize(Quaternion gaze, float strength);
        void ResetState();
    }
}
