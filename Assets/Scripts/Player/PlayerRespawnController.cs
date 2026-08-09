using System.Collections;
using ProjectSun.FPS.Abilities;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.Player
{
    /// <summary>Offline training respawn. Competitive multiplayer will replace this with server-controlled respawns.</summary>
    [RequireComponent(typeof(Health), typeof(CharacterController), typeof(FpsPlayerController))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField, Min(0.5f)]
        [Tooltip("训练模式死亡后的自动复活等待时间，单位为秒；竞技回合会关闭局中复活，因此该值不影响团队歼灭。")]
        private float respawnSeconds = 3f;
        private Health health;
        private CharacterController characterController;
        private FpsPlayerController player;
        private HitscanWeapon weapon;
        private FpsAbilityController abilities;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private bool isRespawning;
        private bool roundRespawnsEnabled = true;

        public bool IsRespawning => isRespawning;

        private void Awake()
        {
            health = GetComponent<Health>();
            characterController = GetComponent<CharacterController>();
            player = GetComponent<FpsPlayerController>();
            weapon = GetComponent<HitscanWeapon>();
            abilities = GetComponent<FpsAbilityController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (isRespawning) return;
            SetGameplayEnabled(false);
            if (roundRespawnsEnabled) StartCoroutine(RespawnRoutine());
        }

        /// <summary>设置是否允许训练模式的局中自动复活；团队歼灭在回合开始时关闭它。</summary>
        /// <param name="enabled">true 允许按等待时间复活；false 时死亡后保持淘汰直到下一回合重置。</param>
        public void SetRoundRespawnsEnabled(bool enabled) => roundRespawnsEnabled = enabled;

        /// <summary>
        /// 更新后续回合与训练复活使用的权威出生姿态。这里只保存数据，实际移动统一发生在重置流程中，
        /// 避免配置阶段绕过 CharacterController 的安全开关。
        /// </summary>
        /// <param name="spawnPose">世界空间出生位置和朝向，由阵营槽位对应的 TeamSpawnGroup 提供。</param>
        public void SetRoundSpawn(Pose spawnPose)
        {
            spawnPosition = spawnPose.position;
            spawnRotation = spawnPose.rotation;
        }

        public void ResetForRound()
        {
            StopAllCoroutines();
            isRespawning = false;
            characterController.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            health.ResetHealth();
            characterController.enabled = true;
            SetGameplayEnabled(true);
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            SetGameplayEnabled(false);
            characterController.enabled = false;
            yield return new WaitForSeconds(respawnSeconds);

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            health.ResetHealth();
            characterController.enabled = true;
            SetGameplayEnabled(true);
            isRespawning = false;
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (player != null) player.SetGameplayInputEnabled(enabled);
            if (weapon != null) weapon.SetGameplayInputEnabled(enabled);
            if (abilities != null) abilities.SetGameplayInputEnabled(enabled);
        }
    }
}
