using UnityEngine;

namespace XRPrivacy
{
    // MetaGuard (Nair et al., "Going Incognito in the Metaverse"), ported from the
    // reference implementation in metaguard/metaguardplus (evaluation/common/metaguard.py).
    //
    // Instead of per-frame noise, MetaGuard perturbs the user's apparent ANTHROPOMETRICS
    // once per session using a bounded-Laplacian mechanism and applies the result as a
    // constant multiplicative SCALING of play-space coordinates:
    //   - height   -> scales head Y           (eps 3,  bounds [1.496, 1.826] m)
    //   - wingspan -> scales left/right hand X (eps 1,  bounds [1.556, 1.899] m)
    //   - room W   -> scales head X            (eps 1,  bounds [0.1, 1])
    //   - room D   -> scales head Z            (eps 1,  bounds [0.1, 1])
    // sensitivity = upper - lower; Laplace scale b = sensitivity / epsilon.
    //
    // The reference works on a whole recorded replay (median/max over all frames). For
    // real-time use we build running estimates of the same quantities and sample the
    // per-session scale factors once, after both hands have been observed.
    public class MetaGuardMechanism : MonoBehaviour, INoiseGenerator, IPositionPrivacyMechanism
    {
        [Header("References")]
        [Tooltip("Tracking-space origin (the XR Origin / floor object). Positions are converted into this " +
                 "local space so 'height' is metres above the floor and room extents are play-space relative. " +
                 "Required: MetaGuard scales play-space anthropometrics, not world coordinates.")]
        public Transform trackingOrigin;

        // ---- per-session sampled scale factors (1 = no change) ----
        private bool _sampled;
        private float _heightScale = 1f, _wingspanScale = 1f, _widthScale = 1f, _depthScale = 1f;

        // ---- running estimates of the user's real dimensions (play space) ----
        private float _heightEst, _wingspanEst, _widthEst, _depthEst;
        private float _leftXLocal, _rightXLocal;
        private bool _haveLeft, _haveRight;

        private readonly System.Random _rng = new System.Random();

        public string GetMechanismName()
        {
            return "MetaGuard";
        }

        public Vector3 Privatize(XRChannel channel, Vector3 originalWorldPosition, float strength)
        {
            if (trackingOrigin == null) return originalWorldPosition; // misconfigured -> passthrough

            // World -> play-space local.
            Vector3 local = trackingOrigin.InverseTransformPoint(originalWorldPosition);

            UpdateEstimates(channel, local);

            // Sample the per-session scale factors once we have seen both hands so the
            // wingspan estimate is meaningful.
            if (!_sampled && _haveLeft && _haveRight)
                SampleScales();

            // Strength blends from identity (0%) to full MetaGuard scaling (100%).
            float t = Mathf.Clamp01(strength / 100f);

            Vector3 outLocal = local;
            switch (channel)
            {
                case XRChannel.Head:
                    outLocal.x = local.x * Mathf.Lerp(1f, _widthScale, t);
                    outLocal.y = local.y * Mathf.Lerp(1f, _heightScale, t);
                    outLocal.z = local.z * Mathf.Lerp(1f, _depthScale, t);
                    break;
                case XRChannel.LeftHand:
                case XRChannel.RightHand:
                    outLocal.x = local.x * Mathf.Lerp(1f, _wingspanScale, t);
                    break;
            }

            return trackingOrigin.TransformPoint(outLocal);
        }

        private void UpdateEstimates(XRChannel channel, Vector3 local)
        {
            if (channel == XRChannel.Head)
            {
                _heightEst = Mathf.Max(_heightEst, local.y);           // standing height above floor
                _widthEst = Mathf.Max(_widthEst, Mathf.Abs(local.x));  // room half-extent X
                _depthEst = Mathf.Max(_depthEst, Mathf.Abs(local.z));  // room half-extent Z
            }
            else if (channel == XRChannel.LeftHand) { _leftXLocal = local.x; _haveLeft = true; }
            else if (channel == XRChannel.RightHand) { _rightXLocal = local.x; _haveRight = true; }

            if (_haveLeft && _haveRight)
                _wingspanEst = Mathf.Max(_wingspanEst, Mathf.Abs(_rightXLocal - _leftXLocal));
        }

        private void SampleScales()
        {
            float height = Mathf.Clamp(_heightEst, 1.496f, 1.826f);
            float wingspan = Mathf.Clamp(_wingspanEst, 1.556f, 1.899f);
            float width = Mathf.Clamp(_widthEst, 0.1f, 1f);
            float depth = Mathf.Clamp(_depthEst, 0.1f, 1f);

            float heightF = BoundedLaplace(height, 3f, 1.496f, 1.826f);
            float wingspanF = BoundedLaplace(wingspan, 1f, 1.556f, 1.899f);
            float widthF = BoundedLaplace(width, 1f, 0.1f, 1f);
            float depthF = BoundedLaplace(depth, 1f, 0.1f, 1f);

            _heightScale = heightF / Mathf.Max(height, 1e-4f);
            _wingspanScale = wingspanF / Mathf.Max(wingspan, 1e-4f);
            _widthScale = widthF / Mathf.Max(width, 1e-4f);
            _depthScale = depthF / Mathf.Max(depth, 1e-4f);
            _sampled = true;
        }

        // Bounded-domain Laplace mechanism: sample Laplace(x, b = (upper-lower)/epsilon)
        // via inverse-CDF, truncated to [lower, upper] by rejection (matches the bounded
        // domain behavior of diffprivlib's LaplaceBoundedDomain closely enough for runtime).
        private float BoundedLaplace(float x, float epsilon, float lower, float upper)
        {
            float b = (upper - lower) / epsilon;
            for (int i = 0; i < 100; i++)
            {
                float u = (float)_rng.NextDouble() - 0.5f;            // (-0.5, 0.5]
                float lap = x - b * Mathf.Sign(u) * Mathf.Log(1f - 2f * Mathf.Abs(u));
                if (lap >= lower && lap <= upper) return lap;
            }
            return Mathf.Clamp(x, lower, upper);
        }

        public void ResetState()
        {
            _sampled = false;
            _heightEst = _wingspanEst = _widthEst = _depthEst = 0f;
            _haveLeft = _haveRight = false;
            _heightScale = _wingspanScale = _widthScale = _depthScale = 1f;
        }

        // Unused additive path (see SpatialNoise for rationale).
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
