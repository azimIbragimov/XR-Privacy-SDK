using UnityEngine;

namespace XRPrivacy
{
    // Full upper-body pose (head + both controllers), position and rotation.
    public struct BodyPose
    {
        public Vector3 headPos;
        public Quaternion headRot;
        public Vector3 leftPos;
        public Quaternion leftRot;
        public Vector3 rightPos;
        public Quaternion rightRot;
    }

    // Whole-body privacy contract for mechanisms that need all three trackers at once
    // and/or must privatize ROTATION as well as position - e.g. learned models (DMM)
    // that consume the full 21-feature frame, or anthropometric transforms that couple
    // the hands (wingspan). Poses are passed in TRACKING/LOCAL space (relative to the
    // manager's tracking origin) so they match play-space training data; the mechanism
    // returns privatized poses in the same local space.
    //
    // Mechanisms that only perturb a single channel's position should implement
    // IPositionPrivacyMechanism instead. The manager prefers IBodyPrivacyMechanism when
    // a mechanism implements it.
    public interface IBodyPrivacyMechanism
    {
        // Map true local-space poses to privatized local-space poses. strength is the
        // UI slider value in [0, 100]. Implementations may run asynchronously / at a
        // fixed internal rate and return the latest available result each call.
        BodyPose Privatize(BodyPose localPose, float strength);

        // Drop any accumulated history/state. Called when selected and on disable.
        void ResetState();
    }
}
