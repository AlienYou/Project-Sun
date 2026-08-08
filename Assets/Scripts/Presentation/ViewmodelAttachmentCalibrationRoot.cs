using UnityEngine;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>
    /// Shared authored coordinate frame for one first-person attachment. Mechanical fitting moves this
    /// transform so model geometry, sight references, scope lenses and clip probes remain coherent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelAttachmentCalibrationRoot : MonoBehaviour
    {
    }
}
