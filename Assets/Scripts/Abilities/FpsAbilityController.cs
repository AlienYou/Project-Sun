using System.Collections;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.Abilities
{
    /// <summary>Two reusable tactical abilities: short mobility burst and a temporary precision/damage boost.</summary>
    public sealed class FpsAbilityController : MonoBehaviour
    {
        [SerializeField] private float dashDistance = 7f;
        [SerializeField] private float dashDuration = 0.16f;
        [SerializeField] private float dashCooldown = 6f;
        [SerializeField] private float focusDuration = 5f;
        [SerializeField] private float focusCooldown = 18f;
        [SerializeField] private float focusDamageMultiplier = 1.25f;
        [SerializeField] private float focusSpreadMultiplier = 0.45f;

        private FpsPlayerController player;
        private FpsInput input;
        private HitscanWeapon weapon;
        private float dashReadyAt;
        private float focusReadyAt;
        private bool isDashing;
        private bool isFocused;
        private bool gameplayInputEnabled = true;

        public float DashCooldownRemaining => Mathf.Max(0f, dashReadyAt - Time.time);
        public float FocusCooldownRemaining => Mathf.Max(0f, focusReadyAt - Time.time);
        public bool IsFocused => isFocused;

        public void Configure(FpsPlayerController controller, HitscanWeapon hitscanWeapon)
        {
            player = controller;
            input = player != null ? player.Input : null;
            weapon = hitscanWeapon;
        }

        public void SetGameplayInputEnabled(bool enabled) => gameplayInputEnabled = enabled;

        private void Update()
        {
            if (!gameplayInputEnabled || input == null) return;
            if (input.WasPressed(FpsBinding.Dash)) TryDash();
            if (input.WasPressed(FpsBinding.Focus)) TryFocus();
        }

        private void TryDash()
        {
            if (player == null || isDashing || Time.time < dashReadyAt) return;
            StartCoroutine(DashRoutine());
        }

        private IEnumerator DashRoutine()
        {
            isDashing = true;
            dashReadyAt = Time.time + dashCooldown;
            float elapsed = 0f;
            Vector3 direction = player.FlatForward;
            CharacterController controller = player.GetComponent<CharacterController>();
            while (elapsed < dashDuration)
            {
                elapsed += Time.deltaTime;
                controller.Move(direction * (dashDistance / dashDuration) * Time.deltaTime);
                yield return null;
            }
            isDashing = false;
        }

        private void TryFocus()
        {
            if (weapon == null || isFocused || Time.time < focusReadyAt) return;
            StartCoroutine(FocusRoutine());
        }

        private IEnumerator FocusRoutine()
        {
            isFocused = true;
            focusReadyAt = Time.time + focusCooldown;
            weapon.SetAbilityModifiers(focusDamageMultiplier, focusSpreadMultiplier);
            yield return new WaitForSeconds(focusDuration);
            weapon.SetAbilityModifiers(1f, 1f);
            isFocused = false;
        }
    }
}
