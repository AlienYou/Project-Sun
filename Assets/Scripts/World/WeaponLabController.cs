using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Weapons;
using UnityEngine;

namespace ProjectSun.FPS.World
{
    /// <summary>
    /// Owns test-only controls for the WeaponLab scene. It intentionally resets the same runtime
    /// Player, inventory and TargetDummy components used by gameplay instead of simulating weapons separately.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponLabController : MonoBehaviour
    {
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [SerializeField] private TargetDummy[] targets = System.Array.Empty<TargetDummy>();
        [SerializeField] private KeyCode resetLabKey = KeyCode.F6;
        [SerializeField] private bool resetOnStart = true;

        public KeyCode ResetLabKey => resetLabKey;

        public void Configure(FpsPlayerInstaller player, TargetDummy[] trainingTargets)
        {
            playerInstaller = player;
            targets = trainingTargets ?? System.Array.Empty<TargetDummy>();
        }

        private void Start()
        {
            if (resetOnStart) ResetLab();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(resetLabKey)) ResetLab();
        }

        public void ResetLab()
        {
            if (playerInstaller != null)
            {
                WeaponInventoryController inventory = playerInstaller.WeaponInventory;
                if (inventory != null) inventory.ResetForRound();

                PlayerRespawnController respawn = playerInstaller.GetComponent<PlayerRespawnController>();
                if (respawn != null)
                {
                    respawn.SetRoundRespawnsEnabled(true);
                    respawn.ResetForRound();
                }
                else if (playerInstaller.Health != null)
                {
                    playerInstaller.Health.ResetHealth();
                }
            }

            foreach (TargetDummy target in targets)
                if (target != null) target.ResetTarget();
        }
    }
}
