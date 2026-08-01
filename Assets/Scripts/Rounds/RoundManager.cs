using ProjectSun.FPS.AI;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.Rounds
{
    public enum RoundState { Preparation, Active, AttackersWin, DefendersWin }

    /// <summary>Offline round loop. Networking will later move all state authority to the server.</summary>
    [DisallowMultipleComponent]
    public sealed class RoundManager : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float preparationSeconds = 6f;
        [SerializeField, Min(10f)] private float roundSeconds = 180f;
        [SerializeField, Min(1f)] private float resultSeconds = 7f;
        [SerializeField] private ObjectiveZone[] objectives = System.Array.Empty<ObjectiveZone>();
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [SerializeField] private CombatBotController[] defenders = System.Array.Empty<CombatBotController>();

        private RoundState state;
        private float stateEndsAt;
        private string resultReason = string.Empty;
        private Health playerHealth;
        private PlayerRespawnController playerRespawn;

        public RoundState State => state;
        public float TimeRemaining => Mathf.Max(0f, stateEndsAt - Time.time);
        public string ResultReason => resultReason;
        public int AliveDefenderCount
        {
            get
            {
                int count = 0;
                foreach (CombatBotController defender in defenders)
                    if (defender != null && defender.IsAlive) count++;
                return count;
            }
        }
        public string StateLabel => state switch
        {
            RoundState.Preparation => "PREPARE",
            RoundState.Active => "ROUND LIVE",
            RoundState.AttackersWin => "ATTACKERS WIN",
            RoundState.DefendersWin => "DEFENDERS WIN",
            _ => string.Empty
        };

        public string ObjectiveText
        {
            get
            {
                if (state == RoundState.Preparation) return "ROUND BEGINS WHEN THE TIMER EXPIRES";
                if (state == RoundState.AttackersWin || state == RoundState.DefendersWin) return resultReason;
                foreach (ObjectiveZone objective in objectives)
                {
                    if (objective != null && objective.IsPlayerInside)
                    {
                        int progress = Mathf.CeilToInt(objective.ActivationProgress);
                        int total = Mathf.CeilToInt(objective.ActivationSeconds);
                        return $"{objective.SiteLabel}: HOLD [F]  {progress}/{total}";
                    }
                }
                return "ACTIVATE OBJECTIVE A OR B";
            }
        }

        public void SetObjectives(ObjectiveZone[] objectiveZones)
        {
            UnsubscribeObjectives();
            objectives = objectiveZones ?? System.Array.Empty<ObjectiveZone>();
            SubscribeObjectives();
        }

        public void ConfigureCombatants(FpsPlayerInstaller player, CombatBotController[] defenderBots)
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            playerInstaller = player;
            defenders = defenderBots ?? System.Array.Empty<CombatBotController>();
            playerHealth = playerInstaller != null ? playerInstaller.Health : null;
            playerRespawn = playerInstaller != null ? playerInstaller.GetComponent<PlayerRespawnController>() : null;
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        private void Awake() => SubscribeObjectives();

        private void Start()
        {
            if (playerInstaller == null)
                ConfigureCombatants(FindObjectOfType<FpsPlayerInstaller>(), FindObjectsOfType<CombatBotController>());
            BeginPreparation();
        }

        private void OnDestroy()
        {
            UnsubscribeObjectives();
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (state == RoundState.Active && AreAllDefendersEliminated())
            {
                FinishRound(RoundState.AttackersWin, "DEFENDERS ELIMINATED");
                return;
            }
            if (Time.time < stateEndsAt) return;
            switch (state)
            {
                case RoundState.Preparation:
                    BeginRound();
                    break;
                case RoundState.Active:
                    FinishRound(RoundState.DefendersWin, "TIME EXPIRED");
                    break;
                case RoundState.AttackersWin:
                case RoundState.DefendersWin:
                    BeginPreparation();
                    break;
            }
        }

        private void BeginPreparation()
        {
            state = RoundState.Preparation;
            stateEndsAt = Time.time + preparationSeconds;
            resultReason = "ROUND BEGINS WHEN THE TIMER EXPIRES";
            SetObjectivesAvailable(false);
            ResetCombatants();
        }

        private void BeginRound()
        {
            state = RoundState.Active;
            stateEndsAt = Time.time + roundSeconds;
            resultReason = string.Empty;
            SetObjectivesAvailable(true);
            if (playerRespawn != null) playerRespawn.SetRoundRespawnsEnabled(false);
            foreach (CombatBotController defender in defenders)
            {
                if (defender == null) continue;
                defender.SetRoundRespawnsEnabled(false);
                defender.SetCombatEnabled(true);
            }
        }

        private void FinishRound(RoundState result, string reason)
        {
            state = result;
            stateEndsAt = Time.time + resultSeconds;
            resultReason = reason;
            SetObjectivesAvailable(false);
            SetPlayerGameplayEnabled(false);
            foreach (CombatBotController defender in defenders)
                if (defender != null) defender.SetCombatEnabled(false);
        }

        private void OnObjectiveActivated(ObjectiveZone _)
        {
            if (state == RoundState.Active)
                FinishRound(RoundState.AttackersWin, "OBJECTIVE ACTIVATED");
        }

        private void SetObjectivesAvailable(bool available)
        {
            foreach (ObjectiveZone objective in objectives)
                if (objective != null)
                    objective.SetAvailable(available);
        }

        private void SubscribeObjectives()
        {
            foreach (ObjectiveZone objective in objectives)
                if (objective != null)
                    objective.Activated += OnObjectiveActivated;
        }

        private void UnsubscribeObjectives()
        {
            foreach (ObjectiveZone objective in objectives)
                if (objective != null)
                    objective.Activated -= OnObjectiveActivated;
        }

        private void OnPlayerDied()
        {
            if (state == RoundState.Active)
                FinishRound(RoundState.DefendersWin, "ATTACKER ELIMINATED");
        }

        private bool AreAllDefendersEliminated()
        {
            bool hasDefender = false;
            foreach (CombatBotController defender in defenders)
            {
                if (defender == null) continue;
                hasDefender = true;
                if (defender.IsAlive) return false;
            }
            return hasDefender;
        }

        private void ResetCombatants()
        {
            if (playerRespawn != null)
            {
                playerRespawn.SetRoundRespawnsEnabled(true);
                playerRespawn.ResetForRound();
            }
            else SetPlayerGameplayEnabled(true);
            foreach (CombatBotController defender in defenders)
            {
                if (defender == null) continue;
                defender.SetCombatEnabled(false);
                defender.SetRoundRespawnsEnabled(true);
                defender.ResetForRound();
            }
        }

        private void SetPlayerGameplayEnabled(bool enabled)
        {
            if (playerInstaller == null) return;
            if (playerInstaller.Player != null) playerInstaller.Player.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Weapon != null) playerInstaller.Weapon.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Abilities != null) playerInstaller.Abilities.SetGameplayInputEnabled(enabled);
        }
    }
}
