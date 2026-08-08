using System;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Presentation;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    [Serializable]
    public sealed class WeaponViewmodelSlot
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform aimAnchor;
        [SerializeField] private Transform magazine;
        [SerializeField] private RuntimeAnimatorController armsController;
        [SerializeField] private WeaponAdsProfile adsProfile;
        [SerializeField] private WeaponPresentationProfile presentationProfile;

        public Transform VisualRoot => visualRoot;
        public Animator WeaponAnimator => weaponAnimator;
        public Transform Muzzle => muzzle;
        public Transform AimAnchor => aimAnchor;
        public Transform Magazine => magazine;
        public RuntimeAnimatorController ArmsController => armsController;
        public WeaponAdsProfile AdsProfile => adsProfile;
        public WeaponPresentationProfile PresentationProfile => presentationProfile;
        public bool IsValid => visualRoot != null && muzzle != null;
        public bool IsPresentationReady => IsValid && armsController != null;

        public void Configure(Transform root, Animator animator, Transform muzzleTransform, Transform aimAnchorTransform,
            Transform magazineTransform, RuntimeAnimatorController armsAnimatorController, WeaponAdsProfile weaponAdsProfile,
            WeaponPresentationProfile weaponPresentationProfile)
        {
            visualRoot = root;
            weaponAnimator = animator;
            muzzle = muzzleTransform;
            aimAnchor = aimAnchorTransform;
            magazine = magazineTransform;
            armsController = armsAnimatorController;
            adsProfile = weaponAdsProfile;
            presentationProfile = weaponPresentationProfile;
        }

        public void SetArmsController(RuntimeAnimatorController controller) => armsController = controller;

        public void SetAimAnchor(Transform anchor) => aimAnchor = anchor;

        public void SetPresentationProfiles(WeaponAdsProfile weaponAdsProfile,
            WeaponPresentationProfile weaponPresentationProfile)
        {
            adsProfile = weaponAdsProfile;
            presentationProfile = weaponPresentationProfile;
        }
    }

    /// <summary>
    /// Owns the two runtime weapon slots. A single hitscan actor remains authoritative for firing,
    /// while this controller saves independent ammunition and swaps its active loadout and visual rig.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponInventoryController : MonoBehaviour
    {
        [SerializeField] private WeaponViewmodelSlot primaryViewmodel = new WeaponViewmodelSlot();
        [SerializeField] private WeaponViewmodelSlot secondaryViewmodel = new WeaponViewmodelSlot();
        [SerializeField, Range(0.05f, 1f)] private float holsterSeconds = 0.18f;
        [SerializeField, Range(0.05f, 1f)] private float unholsterSeconds = 0.24f;

        private HitscanWeapon weapon;
        private PlayerMatchLoadout matchLoadout;
        private FpsInput input;
        private FpsPlayerController player;
        private Camera playerCamera;
        private LowPolyShooterViewmodel viewmodel;
        private WeaponFeedbackController feedback;
        private WeaponAttachmentViewmodelPresenter attachmentPresenter;
        private int primaryAmmoInMagazine = -1;
        private int secondaryAmmoInMagazine = -1;
        private WeaponInventorySlot activeSlot;
        private bool initialized;
        private bool switching;

        public WeaponInventorySlot ActiveSlot => activeSlot;
        public bool HasSecondary => matchLoadout != null && matchLoadout.SecondaryWeapon != null && secondaryViewmodel.IsPresentationReady;
        public bool IsSwitching => switching;

        /// <summary>
        /// Provides the authored visual references for editor tooling. Runtime selection remains owned by this component.
        /// </summary>
        public bool TryGetViewmodelSlot(WeaponInventorySlot slot, out WeaponViewmodelSlot viewmodelSlot)
        {
            viewmodelSlot = slot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            return viewmodelSlot != null && viewmodelSlot.IsPresentationReady;
        }

        public void Configure(FpsPlayerInstaller installer)
        {
            if (installer == null || installer.Weapon == null || installer.MatchLoadout == null) return;
            if (matchLoadout != null) matchLoadout.Changed -= RefreshActiveAttachmentPresentation;
            weapon = installer.Weapon;
            matchLoadout = installer.MatchLoadout;
            matchLoadout.Changed += RefreshActiveAttachmentPresentation;
            input = GetComponent<FpsInput>();
            player = installer.Player;
            playerCamera = installer.PlayerCamera;
            viewmodel = GetComponent<LowPolyShooterViewmodel>();
            feedback = GetComponent<WeaponFeedbackController>();
            attachmentPresenter = GetComponent<WeaponAttachmentViewmodelPresenter>();
            if (attachmentPresenter == null) attachmentPresenter = gameObject.AddComponent<WeaponAttachmentViewmodelPresenter>();
            ResolveSourcePackViewmodels();

            primaryAmmoInMagazine = weapon.AmmoInMagazine;
            activeSlot = WeaponInventorySlot.Primary;
            initialized = ActivateSlot(WeaponInventorySlot.Primary, true);
        }

#if UNITY_EDITOR
        /// <summary>Called by the prefab setup command so the two visual slots stay inspectable rather than runtime-only.</summary>
        public void ConfigureViewmodelReferences(LowPolyShooterViewmodel sourceViewmodel)
        {
            viewmodel = sourceViewmodel;
            ResolveSourcePackViewmodels();
        }

        /// <summary>Assigned by the owned-animation integration command after it creates the clean handgun override.</summary>
        public void SetArmsController(WeaponInventorySlot slot, RuntimeAnimatorController controller)
        {
            WeaponViewmodelSlot viewmodelSlot = slot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            viewmodelSlot.SetArmsController(controller);
        }

        /// <summary>Persists a dedicated authored sight reference for a weapon visual slot.</summary>
        public void SetAimAnchor(WeaponInventorySlot slot, Transform aimAnchor)
        {
            WeaponViewmodelSlot viewmodelSlot = slot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            viewmodelSlot.SetAimAnchor(aimAnchor);
        }

        public void SetPresentationProfiles(WeaponInventorySlot slot, WeaponAdsProfile adsProfile,
            WeaponPresentationProfile presentationProfile)
        {
            WeaponViewmodelSlot viewmodelSlot = slot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            viewmodelSlot.SetPresentationProfiles(adsProfile, presentationProfile);
        }
#endif

        private void Update()
        {
            if (!initialized || weapon == null || input == null || !weapon.GameplayInputEnabled || !input.GameplayEnabled ||
                input.IsRebinding)
                return;
            if (input.WasPressed(FpsBinding.SelectPrimary)) TrySelectPrimary();
            if (input.WasPressed(FpsBinding.SelectSecondary)) TrySelectSecondary();
        }

        public bool TrySelectPrimary() => RequestSwitch(WeaponInventorySlot.Primary);
        public bool TrySelectSecondary() => RequestSwitch(WeaponInventorySlot.Secondary);

        /// <summary>Single-life rounds start from the selected primary with fresh slot ammunition.</summary>
        public void ResetForRound()
        {
            if (!initialized) return;
            StopAllCoroutines();
            switching = false;
            primaryAmmoInMagazine = -1;
            secondaryAmmoInMagazine = -1;
            ActivateSlot(WeaponInventorySlot.Primary, true, true);
        }

        private bool ActivateSlot(WeaponInventorySlot requestedSlot, bool force, bool refillMagazine = false)
        {
            if (!force && requestedSlot == activeSlot) return true;
            if (!force && weapon != null && (weapon.IsReloading || weapon.IsAiming)) return false;

            WeaponLoadout selectedLoadout = requestedSlot == WeaponInventorySlot.Primary
                ? matchLoadout != null ? matchLoadout.Primary : null
                : matchLoadout != null ? matchLoadout.Secondary : null;
            WeaponViewmodelSlot selectedViewmodel = requestedSlot == WeaponInventorySlot.Primary
                ? primaryViewmodel
                : secondaryViewmodel;
            if (selectedLoadout == null || selectedLoadout.Weapon == null || !selectedViewmodel.IsPresentationReady) return false;

            if (!refillMagazine && weapon != null)
            {
                if (activeSlot == WeaponInventorySlot.Primary) primaryAmmoInMagazine = weapon.AmmoInMagazine;
                else secondaryAmmoInMagazine = weapon.AmmoInMagazine;
            }

            int targetAmmo = refillMagazine ? -1
                : requestedSlot == WeaponInventorySlot.Primary ? primaryAmmoInMagazine : secondaryAmmoInMagazine;
            activeSlot = requestedSlot;
            SetViewmodelsActive(requestedSlot);
            weapon.ApplyRuntimeLoadout(selectedLoadout, targetAmmo);
            weapon.SetMuzzle(selectedViewmodel.Muzzle);
            Transform aimAnchor = attachmentPresenter != null
                ? attachmentPresenter.Apply(selectedLoadout, selectedViewmodel.VisualRoot, selectedViewmodel.AimAnchor)
                : selectedViewmodel.AimAnchor;
            ConfigurePresentation(selectedViewmodel, aimAnchor);
            return true;
        }

        private void ConfigurePresentation(WeaponViewmodelSlot selectedViewmodel, Transform aimAnchor)
        {
            if (viewmodel == null || viewmodel.Rig == null) return;
            if (feedback != null) feedback.SnapToHipPose();
            viewmodel.Rig.ConfigureWeaponPresentation(selectedViewmodel.ArmsController, selectedViewmodel.WeaponAnimator,
                selectedViewmodel.Muzzle,
                aimAnchor, selectedViewmodel.Magazine, selectedViewmodel.AdsProfile,
                selectedViewmodel.PresentationProfile);
            if (feedback != null)
                feedback.Configure(weapon, player, playerCamera, viewmodel.VisualRoot, selectedViewmodel.Muzzle,
                    aimAnchor, selectedViewmodel.AdsProfile, selectedViewmodel.PresentationProfile);
        }

        private void RefreshActiveAttachmentPresentation()
        {
            if (!initialized || matchLoadout == null) return;
            WeaponLoadout activeLoadout = activeSlot == WeaponInventorySlot.Primary ? matchLoadout.Primary : matchLoadout.Secondary;
            WeaponViewmodelSlot activeViewmodel = activeSlot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            if (activeLoadout == null || !activeViewmodel.IsValid) return;
            Transform aimAnchor = attachmentPresenter != null
                ? attachmentPresenter.Apply(activeLoadout, activeViewmodel.VisualRoot, activeViewmodel.AimAnchor)
                : activeViewmodel.AimAnchor;
            ConfigurePresentation(activeViewmodel, aimAnchor);
        }

        private void SetViewmodelsActive(WeaponInventorySlot selectedSlot)
        {
            if (primaryViewmodel.VisualRoot != null)
                primaryViewmodel.VisualRoot.gameObject.SetActive(selectedSlot == WeaponInventorySlot.Primary);
            if (secondaryViewmodel.VisualRoot != null)
                secondaryViewmodel.VisualRoot.gameObject.SetActive(selectedSlot == WeaponInventorySlot.Secondary);
        }

        private void ResolveSourcePackViewmodels()
        {
            if (viewmodel == null || viewmodel.Rig == null) return;
            if (!primaryViewmodel.IsValid || primaryViewmodel.ArmsController == null)
            {
                Transform root = FindDescendant(transform, "P_LPSP_WEP_AR_01");
                LowPolyShooterViewmodelRig rig = viewmodel.Rig;
                primaryViewmodel.Configure(root, root != null ? root.GetComponent<Animator>() : null, rig.Muzzle,
                    FindDescendant(root, "AimAnchor_AR4") ?? rig.AimAnchor,
                    rig.Magazine, rig.ArmsController, rig.AdsProfile, rig.PresentationProfile);
            }
            if (!secondaryViewmodel.IsValid)
            {
                Transform root = FindDescendant(transform, "P_LPSP_WEP_Handgun_03");
                secondaryViewmodel.Configure(root, root != null ? root.GetComponent<Animator>() : null,
                    FindDescendant(root, "SOCKET_Muzzle"),
                    FindDescendant(root, "AimAnchor_HG3") ?? FindDescendant(root, "SOCKET_Scope"),
                    FindDescendant(root, "magazine"), null, null, null);
            }
        }

        private bool RequestSwitch(WeaponInventorySlot requestedSlot)
        {
            if (!initialized || switching || requestedSlot == activeSlot || weapon == null || weapon.IsReloading || weapon.IsAiming)
                return false;
            WeaponViewmodelSlot requestedViewmodel = requestedSlot == WeaponInventorySlot.Primary ? primaryViewmodel : secondaryViewmodel;
            WeaponLoadout requestedLoadout = requestedSlot == WeaponInventorySlot.Primary ? matchLoadout.Primary : matchLoadout.Secondary;
            if (!requestedViewmodel.IsPresentationReady || requestedLoadout == null || requestedLoadout.Weapon == null) return false;
            StartCoroutine(SwitchRoutine(requestedSlot));
            return true;
        }

        private System.Collections.IEnumerator SwitchRoutine(WeaponInventorySlot requestedSlot)
        {
            switching = true;
            bool restoreWeaponInput = weapon.GameplayInputEnabled;
            weapon.SetGameplayInputEnabled(false);
            if (viewmodel != null && viewmodel.Rig != null) viewmodel.Rig.PlayHolster();
            yield return new WaitForSeconds(holsterSeconds);

            if (initialized)
            {
                ActivateSlot(requestedSlot, true);
                if (viewmodel != null && viewmodel.Rig != null) viewmodel.Rig.PlayUnholster();
                yield return new WaitForSeconds(unholsterSeconds);
            }

            if (restoreWeaponInput && input != null && input.GameplayEnabled)
                weapon.SetGameplayInputEnabled(true);
            switching = false;
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }

        private void OnDestroy()
        {
            if (matchLoadout != null) matchLoadout.Changed -= RefreshActiveAttachmentPresentation;
        }
    }
}
