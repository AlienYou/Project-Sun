using UnityEngine;

namespace ProjectSun.FPS.Presentation
{
    /// <summary>Receives third-party animation events without loading the third-party gameplay framework.</summary>
    [DisallowMultipleComponent]
    public sealed class LowPolyShooterAnimationEvents : MonoBehaviour
    {
        [SerializeField] private LowPolyShooterViewmodelRig rig;

        public void Configure(LowPolyShooterViewmodelRig viewmodelRig) => rig = viewmodelRig;

        public void OnEjectCasing() { }
        public void OnAmmunitionFill(int amount) { }
        public void OnSetActiveKnife(int active) { }
        public void OnGrenade() { }
        public void OnSetActiveMagazine(int active) => rig?.SetMagazineActive(active);
        public void OnAnimationEndedBolt() { }
        public void OnAnimationEndedReload() { }
        public void OnAnimationEndedGrenadeThrow() { }
        public void OnAnimationEndedMelee() { }
        public void OnAnimationEndedInspect() { }
        public void OnAnimationEndedHolster() { }
        public void OnSlideBack(int back) { }
    }
}
