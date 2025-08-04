using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using System;

public class MotionPrivacyManager : MonoBehaviour
{
    // --- EVENTS ---
    public static event Action<Vector3, Vector3, Quaternion> OnPrivatizedHeadUpdate;
    public static event Action<Quaternion, Quaternion> OnPrivatizedEyeGazeUpdate;
    public static event Action<Vector3, Vector3> OnPrivatizedLeftHandUpdate;
    public static event Action<Vector3, Vector3> OnPrivatizedRightHandUpdate;

    [Header("Configuration")]
    [Tooltip("Drag the LeftHand Controller GameObject from your XR Origin here.")]
    public Transform leftHandControllerTransform;
    [Tooltip("Drag the RightHand Controller GameObject from your XR Origin here.")]
    public Transform rightHandControllerTransform;

    [Header("Privacy Settings")]
    [SerializeField] private float headPositionQuantization = 0.01f;
    [SerializeField] private float handPositionQuantization = 0.01f;
    [SerializeField] private float eyeGazeJitterAmount = 0.5f;

    private Camera xrCamera;

    void Awake()
    {
        xrCamera = Camera.main;
    }

    void Update()
    {
        ProcessHeadData();
        ProcessEyeData();
        ProcessHandData();
    }

    // --- PROCESSING METHODS ---

    void ProcessHeadData()
    {
        if (xrCamera == null) return;

        Vector3 rawPos = xrCamera.transform.position;
        Quaternion rawRot = xrCamera.transform.rotation;
        Vector3 privatePos = QuantizePosition(rawPos, headPositionQuantization);

        OnPrivatizedHeadUpdate?.Invoke(rawPos, privatePos, rawRot);
    }

    void ProcessEyeData()
    {
        UnityEngine.XR.InputDevice eyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);

        if (eyeDevice.isValid && eyeDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out Quaternion rawGaze))
        {
            Quaternion privateGaze = PrivatizeGazeDirection(rawGaze, eyeGazeJitterAmount);
            OnPrivatizedEyeGazeUpdate?.Invoke(rawGaze, privateGaze);
        }
    }

    void ProcessHandData()
    {
        // CHANGED: This now checks and uses the Transform variables.
        if (leftHandControllerTransform != null)
        {
            Vector3 rawPos = leftHandControllerTransform.position;
            Vector3 privatePos = QuantizePosition(rawPos, handPositionQuantization);
            OnPrivatizedLeftHandUpdate?.Invoke(rawPos, privatePos);
        }

        if (rightHandControllerTransform != null)
        {
            Vector3 rawPos = rightHandControllerTransform.position;
            Vector3 privatePos = QuantizePosition(rawPos, handPositionQuantization);
            OnPrivatizedRightHandUpdate?.Invoke(rawPos, privatePos);
        }
    }

    // --- PRIVATIZATION TECHNIQUES ---

    Vector3 QuantizePosition(Vector3 position, float quantization)
    {
        if (quantization <= 0) return position;
        float x = Mathf.Round(position.x / quantization) * quantization;
        float y = Mathf.Round(position.y / quantization) * quantization;
        float z = Mathf.Round(position.z / quantization) * quantization;
        return new Vector3(x, y, z);
    }

    Quaternion PrivatizeGazeDirection(Quaternion direction, float jitter)
    {
        if (jitter <= 0) return direction;
        Quaternion noise = Quaternion.Euler(UnityEngine.Random.insideUnitSphere * jitter);
        return direction * noise;
    }
}