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

            GameObject mineObject = TryInstantiateEquipmentActor("Deployed", hit.point + hit.normal * 0.025f,
                Quaternion.FromToRotation(Vector3.up, hit.normal));
            if (mineObject == null) return false;

            ProximityMine mine = mineObject.GetComponent<ProximityMine>();
            if (mine == null || !mine.Configure(activeDefinition, gameObject, OnMineResolved))
            {
                ReportInvalidActor(mineObject, "ProximityMine");
                Destroy(mineObject);
                return false;
            }
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
            GameObject grenadeObject = TryInstantiateEquipmentActor("Thrown", spawnPosition, cameraTransform.rotation);
            if (grenadeObject == null) return false;

            FragGrenade grenade = grenadeObject.GetComponent<FragGrenade>();
            Vector3 initialVelocity = cameraTransform.forward * activeDefinition.throwSpeed +
                cameraTransform.up * activeDefinition.throwUpwardSpeed;
            if (grenade == null || !grenade.Configure(activeDefinition, gameObject, initialVelocity, OnGrenadeResolved))
            {
                ReportInvalidActor(grenadeObject, "FragGrenade");
                Destroy(grenadeObject);
                return false;
            }
            activeEquipmentActors.Add(grenadeObject);
            return true;
        }

        /// <summary>
        /// 实例化当前装备声明的项目 Prefab，并统一设为不参与玩家瞄准查询的运行时层。
        /// </summary>
        /// <param name="stateSuffix">附加在实例名称末尾的状态文本，仅用于 Hierarchy 与调试定位。</param>
        /// <param name="worldPosition">Actor 初始世界坐标，单位米。</param>
        /// <param name="worldRotation">Actor 初始世界旋转；部署物会对齐命中表面，投掷物对齐相机方向。</param>
        /// <returns>成功创建的 Actor；定义或 Prefab 缺失时返回空且更新 HUD 状态。</returns>
        private GameObject TryInstantiateEquipmentActor(string stateSuffix, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (activeDefinition == null || activeDefinition.worldPrefab == null)
            {
                statusLabel = "MISSING TACTICAL PREFAB";
                Debug.LogError($"{name} cannot use tactical equipment because its project worldPrefab is missing.", activeDefinition);
                return null;
            }

            GameObject actor = Instantiate(activeDefinition.worldPrefab, worldPosition, worldRotation);
            actor.name = $"{activeDefinition.displayName} ({stateSuffix})";
            // 战术 Actor 仍可与世界碰撞，但不应被武器瞄准射线当作角色或墙体命中目标。
            CombatLayers.SetLayerRecursively(actor, CombatLayers.IgnoreRaycastLayer);
            return actor;
        }

        /// <summary>
        /// 报告 Prefab 与装备类型不匹配的配置错误。
        /// </summary>
        /// <param name="actor">已实例化但无法配置的 Actor；用于在 Unity Console 中定位 Prefab 来源。</param>
        /// <param name="requiredComponentName">当前投放路径必需的组件名称，例如 FragGrenade。</param>
        private void ReportInvalidActor(GameObject actor, string requiredComponentName)
        {
            statusLabel = "INVALID TACTICAL PREFAB";
            Debug.LogError($"{name} requires {activeDefinition.displayName} worldPrefab to contain {requiredComponentName} and valid presentation components.",
                actor != null ? actor : activeDefinition);
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
