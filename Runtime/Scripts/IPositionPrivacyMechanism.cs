using UnityEngine;

namespace XRPrivacy
{
    // Identifies which tracked object a sample belongs to. Stateful mechanisms
    // (smoothing, temporal) keep independent history per channel so the left and
    // right controllers don't share a filter.
    public enum XRChannel
    {
        Head = 0,
        LeftHand = 1,
        RightHand = 2
    }

    // Richer mechanism contract than INoiseGenerator. INoiseGenerator can only
    // return an additive offset and never sees the true position, which is fine
    // for memoryless noise (Gaussian) but cannot express mechanisms that depend
    // on the position itself or on its history: spatial quantization, low-pass
    // smoothing, temporal down-sampling, etc.
    //
    // A mechanism that implements this interface is given the true position of a
    // channel and returns the privatized position. XRPrivacyManager prefers this
    // path when available and falls back to INoiseGenerator otherwise.
    public interface IPositionPrivacyMechanism
    {
        // Map the true world-space position of a channel to its privatized
        // position. strength is the UI slider value in the range [0, 100].
        Vector3 Privatize(XRChannel channel, Vector3 originalPosition, float strength);

        // Drop any accumulated per-channel history. Called when the mechanism is
        // selected and when privacy is toggled off so re-enabling starts clean.
        void ResetState();
    }
}
