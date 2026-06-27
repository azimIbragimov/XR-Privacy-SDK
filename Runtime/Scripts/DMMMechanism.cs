
using UnityEngine;

namespace XRPrivacy
{
    // Deep Motion Masking (Nair et al. 2024), ported from metaguard/metaguardplus.
    // Runs the trained anonymizer + normalizer networks (converted to ONNX, run via
    // Unity Sentis) over a rolling 30 s / 900-frame @ 30 Hz window of head+hand pose.
    //
    // Pipeline per inference (matches evaluation/common/metaguardplus.py):
    //   noisy = anonymizer([window(1,900,21), seed(1,32)])
    //   recenter: per-feature (noisy - anonMean)/anonStd * dataStd + dataMean   (over the window)
    //   anons = normalizer(noisy)
    //   clip quaternion columns [3:7],[10:14],[17:21] to [-1,1]
    //   output = anons[last frame]  -> privatized current pose
    //
    // Feature layout per frame (21): head[x,y,z,qx,qy,qz,qw], left[...], right[...].
    // Poses are in tracking/play space (see IBodyPrivacyMechanism). The identity seed
    // is sampled once per session, so the user is consistently mapped to one fake body.
    public class DMMMechanism : MonoBehaviour, INoiseGenerator, IBodyPrivacyMechanism
    {
        [Header("Models (assign imported ONNX)")]
        public Unity.InferenceEngine.ModelAsset anonymizerModel;
        public Unity.InferenceEngine.ModelAsset normalizerModel;

        [Header("Inference")]
        [Tooltip("Network is trained at 30 Hz. This caps how often inference runs; lower it if the " +
                 "frame rate suffers (the 900-step LSTM is the heavy part on standalone headsets).")]
        public float inferenceHz = 30f;
        [Tooltip("GPUCompute is fastest. Use CPU only if compute shaders are unavailable.")]
        public Unity.InferenceEngine.BackendType backend = Unity.InferenceEngine.BackendType.GPUCompute;

        const int WINDOW = 900;        // timesteps the model expects
        const int FEAT = 21;           // features per frame
        const int SEED = 32;           // identity seed dim
        const int N = WINDOW * FEAT;
        const float SAMPLE_DT = 1f / 30f;

        Unity.InferenceEngine.Worker _anon, _norm;
        readonly float[] _ring = new float[N];   // oldest..newest, (900,21) flattened
        bool _filled;
        float[] _seed;
        float _accum;
        BodyPose _lastOut;
        bool _haveOut;
        bool _warned;
        readonly System.Random _rng = new System.Random();

        public string GetMechanismName() => "DMM";

        void OnDisable() => DisposeWorkers();
        void OnDestroy() => DisposeWorkers();

        void EnsureInit()
        {
            if (_anon != null) return;
            if (anonymizerModel == null || normalizerModel == null)
            {
                if (!_warned) { Debug.LogError("DMM: anonymizer/normalizer ModelAsset not assigned."); _warned = true; }
                return;
            }
            _anon = new Unity.InferenceEngine.Worker(Unity.InferenceEngine.ModelLoader.Load(anonymizerModel), backend);
            _norm = new Unity.InferenceEngine.Worker(Unity.InferenceEngine.ModelLoader.Load(normalizerModel), backend);
            SampleSeed();
        }

        void SampleSeed()
        {
            _seed = new float[SEED];
            for (int i = 0; i < SEED; i++)
                _seed[i] = Gaussian(); // training used np.random.normal
        }

        public BodyPose Privatize(BodyPose localPose, float strength)
        {
            EnsureInit();
            if (_anon == null) return localPose;

            // Prime the whole window with the first observed pose so the window is valid
            // immediately instead of after a 30 s warm-up.
            if (!_filled) { for (int k = 0; k < WINDOW; k++) WriteFrame(k, localPose); _filled = true; }

            // Advance the ring + run inference at the (capped) sample rate.
            _accum += Time.deltaTime;
            float dt = 1f / Mathf.Max(1f, inferenceHz);
            if (!_haveOut || _accum >= dt)
            {
                _accum = 0f;
                PushFrame(localPose);
                RunInference();
            }

            // Blend: 0% = original, 100% = full DMM.
            float t = Mathf.Clamp01(strength / 100f);
            return LerpPose(localPose, _lastOut, t);
        }

        void PushFrame(BodyPose p)
        {
            // shift left by one frame, append newest at the end
            System.Array.Copy(_ring, FEAT, _ring, 0, (WINDOW - 1) * FEAT);
            WriteFrame(WINDOW - 1, p);
        }

        void WriteFrame(int t, BodyPose p)
        {
            int o = t * FEAT;
            _ring[o + 0] = p.headPos.x; _ring[o + 1] = p.headPos.y; _ring[o + 2] = p.headPos.z;
            _ring[o + 3] = p.headRot.x; _ring[o + 4] = p.headRot.y; _ring[o + 5] = p.headRot.z; _ring[o + 6] = p.headRot.w;
            _ring[o + 7] = p.leftPos.x; _ring[o + 8] = p.leftPos.y; _ring[o + 9] = p.leftPos.z;
            _ring[o + 10] = p.leftRot.x; _ring[o + 11] = p.leftRot.y; _ring[o + 12] = p.leftRot.z; _ring[o + 13] = p.leftRot.w;
            _ring[o + 14] = p.rightPos.x; _ring[o + 15] = p.rightPos.y; _ring[o + 16] = p.rightPos.z;
            _ring[o + 17] = p.rightRot.x; _ring[o + 18] = p.rightRot.y; _ring[o + 19] = p.rightRot.z; _ring[o + 20] = p.rightRot.w;
        }

        void RunInference()
        {
            // --- anonymizer ---
            float[] anon;
            using (var x = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, WINDOW, FEAT), _ring))
            using (var r = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, SEED), _seed))
            {
                _anon.Schedule(x, r);
                using var outT = (_anon.PeekOutput() as Unity.InferenceEngine.Tensor<float>).ReadbackAndClone();
                anon = outT.DownloadToArray();
            }

            // --- recenter to the input window's per-feature mean/std ---
            Recenter(anon);

            // --- normalizer ---
            float[] norm;
            using (var nx = new Unity.InferenceEngine.Tensor<float>(new Unity.InferenceEngine.TensorShape(1, WINDOW, FEAT), anon))
            {
                _norm.Schedule(nx);
                using var outT = (_norm.PeekOutput() as Unity.InferenceEngine.Tensor<float>).ReadbackAndClone();
                norm = outT.DownloadToArray();
            }

            // --- take the last (causal) frame, clip quats, build pose ---
            int last = (WINDOW - 1) * FEAT;
            _lastOut = ReadFrame(norm, last);
            _haveOut = true;
        }

        // Per-feature: noisy = (noisy - anonMean)/anonStd * dataStd + dataMean,
        // with statistics taken over the 900-frame window (matching the reference).
        void Recenter(float[] anon)
        {
            for (int f = 0; f < FEAT; f++)
            {
                double dm = 0, am = 0;
                for (int t = 0; t < WINDOW; t++) { dm += _ring[t * FEAT + f]; am += anon[t * FEAT + f]; }
                dm /= WINDOW; am /= WINDOW;

                double dv = 0, av = 0;
                for (int t = 0; t < WINDOW; t++)
                {
                    double d = _ring[t * FEAT + f] - dm; dv += d * d;
                    double a = anon[t * FEAT + f] - am; av += a * a;
                }
                double ds = System.Math.Sqrt(dv / WINDOW);
                double as_ = System.Math.Sqrt(av / WINDOW);
                if (as_ < 1e-8) as_ = 1e-8;

                for (int t = 0; t < WINDOW; t++)
                {
                    int i = t * FEAT + f;
                    anon[i] = (float)(((anon[i] - am) / as_) * ds + dm);
                }
            }
        }

        BodyPose ReadFrame(float[] a, int o)
        {
            BodyPose p;
            p.headPos = new Vector3(a[o + 0], a[o + 1], a[o + 2]);
            p.headRot = ClipQuat(a[o + 3], a[o + 4], a[o + 5], a[o + 6]);
            p.leftPos = new Vector3(a[o + 7], a[o + 8], a[o + 9]);
            p.leftRot = ClipQuat(a[o + 10], a[o + 11], a[o + 12], a[o + 13]);
            p.rightPos = new Vector3(a[o + 14], a[o + 15], a[o + 16]);
            p.rightRot = ClipQuat(a[o + 17], a[o + 18], a[o + 19], a[o + 20]);
            return p;
        }

        // Clip each component to [-1,1] (as the reference does) then normalize to a unit quaternion.
        static Quaternion ClipQuat(float x, float y, float z, float w)
        {
            var q = new Quaternion(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1), Mathf.Clamp(z, -1, 1), Mathf.Clamp(w, -1, 1));
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (m < 1e-6f) return Quaternion.identity;
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }

        static BodyPose LerpPose(BodyPose a, BodyPose b, float t)
        {
            BodyPose p;
            p.headPos = Vector3.Lerp(a.headPos, b.headPos, t);
            p.headRot = Quaternion.Slerp(a.headRot, b.headRot, t);
            p.leftPos = Vector3.Lerp(a.leftPos, b.leftPos, t);
            p.leftRot = Quaternion.Slerp(a.leftRot, b.leftRot, t);
            p.rightPos = Vector3.Lerp(a.rightPos, b.rightPos, t);
            p.rightRot = Quaternion.Slerp(a.rightRot, b.rightRot, t);
            return p;
        }

        float Gaussian()
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }

        void DisposeWorkers()
        {
            _anon?.Dispose(); _anon = null;
            _norm?.Dispose(); _norm = null;
        }

        public void ResetState()
        {
            _filled = false;
            _haveOut = false;
            _accum = 0f;
            SampleSeed(); // new fake identity per session
        }

        // Unused additive path (DMM is whole-body, see IBodyPrivacyMechanism).
        public Vector3 GenerateEyeNoise(float strength) => Vector3.zero;
        public Vector3 GenerateHandNoise(float strength) => Vector3.zero;
        public Vector3 GenerateBodyNoise(float strength) => Vector3.zero;
    }
}
