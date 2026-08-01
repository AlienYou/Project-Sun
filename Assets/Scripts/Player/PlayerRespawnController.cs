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

        private void Awake()
        {
            health = GetComponent<Health>();
            characterController = GetComponent<CharacterController>();
            player = GetComponent<FpsPlayerController>();
            weapon = GetComponent<HitscanWeapon>();
            abilities = GetComponent<FpsAbilityController>();
            health.Died += OnDied;
        }

        private void Start()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (!isRespawning) StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            isRespawning = true;
            player.SetGameplayInputEnabled(false);
            if (weapon != null) weapon.SetGameplayInputEnabled(false);
            if (abilities != null) abilities.SetGameplayInputEnabled(false);
            characterController.enabled = false;
            yield return new WaitForSeconds(respawnSeconds);

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            health.ResetHealth();
            characterController.enabled = true;
            player.SetGameplayInputEnabled(true);
            if (weapon != null) weapon.SetGameplayInputEnabled(true);
            if (abilities != null) abilities.SetGameplayInputEnabled(true);
            isRespawning = false;
        }
    }
}
