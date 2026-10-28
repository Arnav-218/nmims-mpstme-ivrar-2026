using System;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Fallback gaze provider that approximates gaze direction using head/camera forward vector.
    /// RESEARCH INTEGRITY NOTICE: Samples produced by this provider are explicitly labelled
    /// with provenance 'HeadDirection_Fallback' and must NEVER be treated or published as true eye tracking.
    /// </summary>
    public class HeadDirectionFallbackProvider : IGazeSampleProvider
    {
        public string ProviderName => "Head Direction Fallback Provider";
        public GazeProvenance Provenance => GazeProvenance.HeadDirection_Fallback;

        private Camera _referenceCamera;
        private bool _isInitialized;

        public bool IsAvailable => _isInitialized && _referenceCamera != null;

        public void Initialize(Transform trackingOrigin, Camera referenceCamera)
        {
            _referenceCamera = referenceCamera;
            _isInitialized = true;
        }

        public bool AcquireSample(out GazeSample sample)
        {
            double now = Time.unscaledTimeAsDouble;

            if (!IsAvailable)
            {
                sample = GazeSample.Invalid(now, Provenance, ProviderName);
                return false;
            }

            Vector3 origin = _referenceCamera.transform.position;
            Vector3 direction = _referenceCamera.transform.forward;

            sample = new GazeSample(
                now,
                origin,
                direction,
                true,
                float.NaN, // Not an eye tracker: confidence is not applicable
                Provenance,
                ProviderName
            );
            return true;
        }

        public void Shutdown()
        {
            _referenceCamera = null;
            _isInitialized = false;
        }
    }
}
