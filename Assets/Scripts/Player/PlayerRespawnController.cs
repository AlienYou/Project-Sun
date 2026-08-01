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
        [SerializeField, Min(0.5f)] private float respawnSeconds = 3f;
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

        /// <summary>Round mode disables mid-round respawns while preserving this component for training mode.</summary>
        public void SetRoundRespawnsEnabled(bool enabled) => roundRespawnsEnabled = enabled;

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
