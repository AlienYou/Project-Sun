using System;
using System.Collections.Generic;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.Weapons
{
    /// <summary>
    /// Owns the player's selected tactical equipment at runtime. The loadout remains the source of truth;
    /// this component only turns the selected definition into round-scoped charges and deployable actors.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FpsTacticalEquipmentController : MonoBehaviour
    {
        private readonly List<GameObject> activeEquipmentActors = new List<GameObject>();

        private FpsPlayerController player;
        private FpsInput input;
        private Camera playerCamera;
        private PlayerMatchLoadout loadout;
        private TacticalEquipmentDefinition activeDefinition;
        private int chargesRemaining;
        private float nextUseAt;
        private bool gameplayInputEnabled = true;
        private string statusLabel = "NO EQUIPMENT";

        public TacticalEquipmentDefinition ActiveDefinition => activeDefinition;
        public int ChargesRemaining => chargesRemaining;
        public float CooldownRemaining => Mathf.Max(0f, nextUseAt - Time.time);
        public string StatusLabel => statusLabel;

        public void Configure(FpsPlayerController controller, Camera camera, PlayerMatchLoadout playerLoadout)
        {
            if (loadout != null) loadout.Changed -= OnLoadoutChanged;
            player = controller;
            input = player != null ? player.Input : GetComponent<FpsInput>();
            playerCamera = camera;
            loadout = playerLoadout;
            if (loadout != null) loadout.Changed += OnLoadoutChanged;
            RefreshPreparedEquipment(true);
        }

        public void SetGameplayInputEnabled(bool enabled) => gameplayInputEnabled = enabled;

        /// <summary>Called by the round authority. Tactical actors persist during a round but never into the next one.</summary>
        public void ResetForRound()
        {
            foreach (GameObject actor in activeEquipmentActors)
            {
                if (actor == null) continue;
                ProximityMine mine = actor.GetComponent<ProximityMine>();
                if (mine != null) mine.DetachOwner();
                FragGrenade grenade = actor.GetComponent<FragGrenade>();
                if (grenade != null) grenade.DetachOwner();
                Destroy(actor);
            }
            activeEquipmentActors.Clear();
            nextUseAt = 0f;
            RefreshPreparedEquipment(true);
        }

        private void Update()
        {
            if (!gameplayInputEnabled || input == null || !input.GameplayEnabled) return;
            if (input.WasPressed(FpsBinding.UseTactical)) TryUseSelectedEquipment();
        }

        private void OnDestroy()
        {
            if (loadout != null) loadout.Changed -= OnLoadoutChanged;
        }

        private void OnLoadoutChanged()
        {
            // Active rounds lock PlayerMatchLoadout, so a change here can only originate from preparation or
            // the unrestricted WeaponLab. Refill the newly selected equipment when no deployed actor exists.
            if (activeEquipmentActors.Count == 0) RefreshPreparedEquipment(true);
        }

        private void RefreshPreparedEquipment(bool refillCharges)
        {
            activeDefinition = loadout != null ? loadout.TacticalEquipment : null;
            if (activeDefinition == null)
            {
                chargesRemaining = 0;
                statusLabel = "NO EQUIPMENT";
                return;
            }

            if (refillCharges) chargesRemaining = Mathf.Max(1, activeDefinition.maxCharges);
            statusLabel = $"{activeDefinition.displayName.ToUpperInvariant()}  {chargesRemaining}/{activeDefinition.maxCharges}";
        }

        private void TryUseSelectedEquipment()
        {
            if (activeDefinition == null)
            {
                statusLabel = "NO EQUIPMENT SELECTED";
                return;
            }
            if (chargesRemaining <= 0)
            {
                statusLabel = $"{activeDefinition.displayName.ToUpperInvariant()}  DEPLETED";
                return;
            }
            if (Time.time < nextUseAt)
            {
                statusLabel = $"{activeDefinition.displayName.ToUpperInvariant()}  {CooldownRemaining:0.0}s";
                return;
            }
            if (activeDefinition.type == TacticalEquipmentType.Throwable)
            {
                if (!TryThrowSelectedEquipment()) return;
            }
            else if (!TryDeploySelectedEquipment())
            {
                return;
            }

            chargesRemaining--;
            nextUseAt = Time.time + activeDefinition.cooldownSeconds;
            statusLabel = activeDefinition.type == TacticalEquipmentType.Throwable
                ? $"{activeDefinition.displayName.ToUpperInvariant()}  THROWN  {chargesRemaining}/{activeDefinition.maxCharges}"
                : $"{activeDefinition.displayName.ToUpperInvariant()}  DEPLOYED  {chargesRemaining}/{activeDefinition.maxCharges}";
        }

        private bool TryDeploySelectedEquipment()
        {
            if (!TryGetDeploySurface(out RaycastHit hit))
            {
                statusLabel = "NO DEPLOY SURFACE";
                return false;
            }

            GameObject mineObject = new GameObject(activeDefinition.displayName + " (Deployed)");
            mineObject.transform.SetPositionAndRotation(hit.point + hit.normal * 0.025f,
                Quaternion.FromToRotation(Vector3.up, hit.normal));
            mineObject.layer = CombatLayers.IgnoreRaycastLayer;
            ProximityMine mine = mineObject.AddComponent<ProximityMine>();
            mine.Configure(activeDefinition, gameObject, OnMineResolved);
            activeEquipmentActors.Add(mineObject);
            return true;
        }

        private bool TryThrowSelectedEquipment()
        {
            if (playerCamera == null)
            {
                statusLabel = "NO THROW CAMERA";
                return false;
            }

            Transform cameraTransform = playerCamera.transform;
            Vector3 spawnPosition = cameraTransform.position + cameraTransform.forward * 0.45f + cameraTransform.up * -0.08f;
            GameObject grenadeObject = new GameObject(activeDefinition.displayName + " (Thrown)");
            grenadeObject.transform.SetPositionAndRotation(spawnPosition, cameraTransform.rotation);
            grenadeObject.layer = CombatLayers.IgnoreRaycastLayer;
            FragGrenade grenade = grenadeObject.AddComponent<FragGrenade>();
            Vector3 initialVelocity = cameraTransform.forward * activeDefinition.throwSpeed +
                cameraTransform.up * activeDefinition.throwUpwardSpeed;
            grenade.Configure(activeDefinition, gameObject, initialVelocity, OnGrenadeResolved);
            activeEquipmentActors.Add(grenadeObject);
            return true;
        }

        private bool TryGetDeploySurface(out RaycastHit hit)
        {
            hit = default;
            if (playerCamera == null || activeDefinition == null) return false;
            return Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit,
                activeDefinition.deployRange, CombatLayers.WallMask, QueryTriggerInteraction.Ignore);
        }

        private void OnMineResolved(ProximityMine mine, bool detonated)
        {
            OnEquipmentResolved(mine != null ? mine.gameObject : null, detonated);
        }

        private void OnGrenadeResolved(FragGrenade grenade, bool detonated)
        {
            OnEquipmentResolved(grenade != null ? grenade.gameObject : null, detonated);
        }

        private void OnEquipmentResolved(GameObject actor, bool detonated)
        {
            if (actor != null) activeEquipmentActors.Remove(actor);
            if (activeDefinition == null) return;
            statusLabel = detonated
                ? $"{activeDefinition.displayName.ToUpperInvariant()}  TRIGGERED"
                : $"{activeDefinition.displayName.ToUpperInvariant()}  EXPIRED";
        }
    }
}
