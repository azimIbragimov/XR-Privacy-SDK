using UnityEngine;

namespace XRPrivacy
{
    // Base for the paired "DMM + X" mechanisms. Runs the shared DMM whole-body
    // anonymizer first, then lets the subclass layer a secondary perturbation on top of
    // the DMM output (per channel, in play space). Subclasses: DMMGaussian, DMMSpatial,
    // DMMSmoothing, DMMTemporal.
    //
    // The DMM network itself lives on a single DMMMechanism component (one Sentis
    // worker). These composites reference it - leave 'dmm' empty to auto-find the one in
    // the scene, so a composite is usually just "add component + set one parameter".
    public abstract class DMMCompositeBase : MonoBehaviour, INoiseGenerator, IBodyPrivacyMechanism
    {
        [Header("DMM")]
        [Tooltip("Shared DMM mechanism (the Sentis anonymizer). If left empty, the first " +
                 "DMMMechanism found in the scene is used.")]
        public DMMMechanism dmm;

        DMMMechanism Dmm()
        {
            if (dmm == null) dmm = FindAnyObjectByType<DMMMechanism>();
            return dmm;
        }

        public BodyPose Privatize(BodyPose localPose, float strength)
        {
            DMMMechanism d = Dmm();
            BodyPose p = d != null ? d.Privatize(localPose, strength) : localPose;
            ApplySecondary(ref p, strength);
            return p;
        }

        // Apply the subclass's secondary perturbation to the (local-space) DMM output.
        protected abstract void ApplySecondary(ref BodyPose pose, float strength);

        // Reset any per-channel state the secondary keeps. Default: nothing.
        protected virtual void ResetSecondary() { }

        public void ResetState()
        {
            Dmm()?.ResetState();
            ResetSecondary();
        }

        public abstract string GetMechanismName();

        // Unused additive path (these are whole-body, see IBodyPrivacyMechanism).
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
