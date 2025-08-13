using UnityEngine;

public class ExampleAnalytics : MonoBehaviour
{
    void OnEnable()
    {
        MotionPrivacyManager.OnHeadUpdate      += OnHead;
        MotionPrivacyManager.OnLeftHandUpdate  += OnLeft;
        MotionPrivacyManager.OnRightHandUpdate += OnRight;
        MotionPrivacyManager.OnEyeGazeUpdate   += OnGaze;
    }
    void OnDisable()
    {
        MotionPrivacyManager.OnHeadUpdate      -= OnHead;
        MotionPrivacyManager.OnLeftHandUpdate  -= OnLeft;
        MotionPrivacyManager.OnRightHandUpdate -= OnRight;
        MotionPrivacyManager.OnEyeGazeUpdate   -= OnGaze;
    }
    void OnHead(Vector3 p, Quaternion r) {}
    void OnLeft(Vector3 p, Quaternion r) {}
    void OnRight(Vector3 p, Quaternion r) {}
    void OnGaze(Quaternion r) {}
}