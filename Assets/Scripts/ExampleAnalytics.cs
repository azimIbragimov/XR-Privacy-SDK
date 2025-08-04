using UnityEngine;

public class ExampleAnalytics : MonoBehaviour
{
    void OnEnable()
    {
        // Subscribe to all the events
        MotionPrivacyManager.OnPrivatizedHeadUpdate += LogHeadData;
        MotionPrivacyManager.OnPrivatizedEyeGazeUpdate += LogEyeData;
        MotionPrivacyManager.OnPrivatizedLeftHandUpdate += LogLeftHandData;
        MotionPrivacyManager.OnPrivatizedRightHandUpdate += LogRightHandData;
    }

    void OnDisable()
    {
        // Unsubscribe from all the events
        MotionPrivacyManager.OnPrivatizedHeadUpdate -= LogHeadData;
        MotionPrivacyManager.OnPrivatizedEyeGazeUpdate -= LogEyeData;
        MotionPrivacyManager.OnPrivatizedLeftHandUpdate -= LogLeftHandData;
        MotionPrivacyManager.OnPrivatizedRightHandUpdate -= LogRightHandData;
    }

    // --- HANDLER METHODS ---

    private void LogHeadData(Vector3 rawPos, Vector3 privatePos, Quaternion rotation)
    {
        Debug.Log($"HEAD - Raw: {rawPos.ToString("F4")} | Privatized: {privatePos.ToString("F4")}");
    }

    private void LogEyeData(Quaternion rawRot, Quaternion privateRot)
    {
        Debug.Log($"EYE - Raw: {rawRot.eulerAngles.ToString("F2")} | Privatized: {privateRot.eulerAngles.ToString("F2")}");
    }

    private void LogLeftHandData(Vector3 rawPos, Vector3 privatePos)
    {
        Debug.Log($"LEFT HAND - Raw: {rawPos.ToString("F4")} | Privatized: {privatePos.ToString("F4")}");
    }

    private void LogRightHandData(Vector3 rawPos, Vector3 privatePos)
    {
        Debug.Log($"RIGHT HAND - Raw: {rawPos.ToString("F4")} | Privatized: {privatePos.ToString("F4")}");
    }
}