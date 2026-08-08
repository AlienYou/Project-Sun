using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Marks an Aim Anchor whose transform rotation is an authored optical frame. Position represents
    /// the ideal ADS eye reference; local +Z points toward the target and local +Y defines sight up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AdsSightReference : MonoBehaviour
    {
        [SerializeField] private bool orientationAuthored;

        public bool OrientationAuthored => orientationAuthored;

        public void SetOrientationAuthored(bool authored)
        {
            orientationAuthored = authored;
        }
    }
}
