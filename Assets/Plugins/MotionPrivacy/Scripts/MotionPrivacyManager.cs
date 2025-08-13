using UnityEngine;
using UnityEngine.XR;
using System;
using System.Collections.Generic;

public class MotionPrivacyManager : MonoBehaviour
{
    // Events for external systems (like analytics)
    public static event Action<Vector3, Quaternion> OnHeadUpdate;
    public static event Action<Vector3, Quaternion> OnLeftHandUpdate;
    public static event Action<Vector3, Quaternion> OnRightHandUpdate;
    public static event Action<Quaternion> OnEyeGazeUpdate;

    [Header("XR Rig References")]
    public Transform xrOrigin;
    public Camera xrCamera;
    public Transform leftHandControllerTransform;
    public Transform rightHandControllerTransform;

    [Header("Privacy Mechanisms (positions only)")]
    public PositionPrivacyMechanism headPositionMechanism;
    public PositionPrivacyMechanism leftHandMechanism;
    public PositionPrivacyMechanism rightHandMechanism;
    public PositionPrivacyMechanism eyeGazeMechanism;

    [Header("Gaze Options")]
    [Tooltip("Meters forward from head to project gaze before adding noise.")]
    public float gazeProjectDistance = 1.0f;

    [Header("Debug")]
    public bool debugLogRaw = false;
    public bool applyToRig = true;

    // Raw device poses captured this frame (world space)
    Vector3 rawHeadPosW, rawLeftPosW, rawRightPosW;
    Quaternion rawHeadRotW, rawLeftRotW, rawRightRotW;
    bool haveHead, haveLeft, haveRight;

    // Store the ORIGINAL clean positions to prevent drift
    private Vector3 originalHeadPosW, originalLeftPosW, originalRightPosW;
    private Quaternion originalHeadRotW, originalLeftRotW, originalRightRotW;
    private bool hasOriginalPoses = false;

    InputDevice eyeDevice;

    void Awake()
    {
        if (xrCamera == null) xrCamera = Camera.main;

        var eyes = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyes);
        if (eyes.Count > 0) eyeDevice = eyes[0];
    }

    void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRenderApply;
        // Reset original poses when re-enabled
        hasOriginalPoses = false;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRenderApply;
    }

    void Update()
    {
        // 1) Read raw device poses
        haveHead  = TryPose(XRNode.CenterEye,  out rawHeadPosW,  out rawHeadRotW);
        haveLeft  = TryPose(XRNode.LeftHand,   out rawLeftPosW,  out rawLeftRotW);
        haveRight = TryPose(XRNode.RightHand,  out rawRightPosW, out rawRightRotW);

        // 2) Store original poses on first frame (or when poses become available)
        if (!hasOriginalPoses && (haveHead || haveLeft || haveRight))
        {
            StoreOriginalPoses();
            hasOriginalPoses = true;
        }

        if (debugLogRaw && haveHead)
            Debug.Log($"[RAW] headW pos={rawHeadPosW} rot={rawHeadRotW.eulerAngles}");

        // 3) Compute gaze each frame
        UpdateEyeFromRaw();
    }

    void StoreOriginalPoses()
    {
        if (haveHead)
        {
            originalHeadPosW = rawHeadPosW;
            originalHeadRotW = rawHeadRotW;
        }
        if (haveLeft)
        {
            originalLeftPosW = rawLeftPosW;
            originalLeftRotW = rawLeftRotW;
        }
        if (haveRight)
        {
            originalRightPosW = rawRightPosW;
            originalRightRotW = rawRightRotW;
        }
    }

    void OnBeforeRenderApply()
    {
        if (!applyToRig || xrOrigin == null || xrCamera == null || !hasOriginalPoses) return;

        // Apply privacy using ORIGINAL poses as reference, not current noisy ones
        if (haveHead)  
            ApplyOneFromOriginal(originalHeadPosW,  originalHeadRotW,  xrCamera.transform, headPositionMechanism, OnHeadUpdate);
        if (haveLeft)  
            ApplyOneFromOriginal(originalLeftPosW,  originalLeftRotW,  leftHandControllerTransform, leftHandMechanism, OnLeftHandUpdate);
        if (haveRight) 
            ApplyOneFromOriginal(originalRightPosW, originalRightRotW, rightHandControllerTransform, rightHandMechanism, OnRightHandUpdate);
    }

    void ApplyOneFromOriginal(Vector3 originalWorldPos, Quaternion originalWorldRot,
                              Transform target, PositionPrivacyMechanism mech,
                              Action<Vector3, Quaternion> updateEvent = null)
    {
        if (target == null || xrOrigin == null) return;

        // Convert ORIGINAL world pose to rig-local
        Vector3 originalLocalPos = xrOrigin.InverseTransformPoint(originalWorldPos);
        Quaternion originalLocalRot = Quaternion.Inverse(xrOrigin.rotation) * originalWorldRot;

        // Apply privacy mechanism to ORIGINAL local position
        Vector3 privLocalPos = mech != null ? mech.Apply(originalLocalPos) : originalLocalPos;

        // Convert back to world space
        Vector3 privWorldPos = xrOrigin.TransformPoint(privLocalPos);
        Quaternion privWorldRot = xrOrigin.rotation * originalLocalRot;

        // Set the transform
        target.SetPositionAndRotation(privWorldPos, privWorldRot);

        // Trigger event if provided
        updateEvent?.Invoke(privWorldPos, privWorldRot);
    }

    void UpdateEyeFromRaw()
    {
        Quaternion gazeRot = Quaternion.identity;
        bool ok = false;

        if (eyeDevice.isValid)
        {
            Eyes eyes;
            if (eyeDevice.TryGetFeatureValue(CommonUsages.eyesData, out eyes))
                ok = eyes.TryGetLeftEyeRotation(out gazeRot) ||
                     eyes.TryGetRightEyeRotation(out gazeRot);
        }

        if (!ok)
        {
            var centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (centerEye.isValid &&
                centerEye.TryGetFeatureValue(CommonUsages.centerEyeRotation, out gazeRot))
            {
                ok = true;
            }
        }

        if (!ok && xrCamera != null)
            gazeRot = xrCamera.transform.rotation;

        // Use ORIGINAL head position for gaze calculations to prevent compound drift
        Vector3 headW = hasOriginalPoses && haveHead ? originalHeadPosW :
                        (xrCamera != null ? xrCamera.transform.position : Vector3.zero);

        Vector3 dirW = gazeRot * Vector3.forward;
        if (dirW.sqrMagnitude < 1e-8f)
            dirW = xrCamera != null ? xrCamera.transform.forward : Vector3.forward;
        dirW.Normalize();

        float d = Mathf.Max(0.01f, gazeProjectDistance);
        Vector3 gazePoint = headW + dirW * d;

        Vector3 privatizedPoint = eyeGazeMechanism != null ? eyeGazeMechanism.Apply(gazePoint) : gazePoint;
        Vector3 dirPriv = privatizedPoint - headW;
        if (dirPriv.sqrMagnitude < 1e-8f) dirPriv = dirW;
        dirPriv.Normalize();

        Quaternion privatizedGaze = Quaternion.LookRotation(dirPriv, Vector3.up);
        OnEyeGazeUpdate?.Invoke(privatizedGaze);
    }

    static bool TryPose(XRNode node, out Vector3 posW, out Quaternion rotW)
    {
        posW = Vector3.zero;
        rotW = Quaternion.identity;

        var dev = InputDevices.GetDeviceAtXRNode(node);
        bool ok = dev.isValid &&
                  dev.TryGetFeatureValue(CommonUsages.devicePosition, out posW) &&
                  dev.TryGetFeatureValue(CommonUsages.deviceRotation, out rotW);
        return ok;
    }

    // Optional: Method to reset original poses (useful for recalibration)
    [ContextMenu("Reset Original Poses")]
    public void ResetOriginalPoses()
    {
        hasOriginalPoses = false;
    }
}