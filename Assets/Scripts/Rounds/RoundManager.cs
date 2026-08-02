using ProjectSun.FPS.AI;
using ProjectSun.FPS.Bootstrap;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSun.FPS.Rounds
{
    public enum RoundState { Preparation, Active, AttackersWin, DefendersWin, Draw, MatchComplete }

    /// <summary>
    /// Offline authority for the first Project Sun mode: single-life team elimination.
    /// Teams, score and round lifecycle deliberately remain independent of objectives so the same
    /// lifecycle can later be reused by demolition mode under server authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoundManager : MonoBehaviour
    {
        [Header("Match Rules")]
        [SerializeField, Range(1, 6)] private int maxTeamSize = 6;
        [SerializeField, Min(1)] private int roundsToWin = 7;
        [SerializeField, Min(1f)] private float preparationSeconds = 6f;
        [SerializeField, Min(10f)] private float roundSeconds = 180f;
        [SerializeField, Min(1f)] private float resultSeconds = 7f;
        [SerializeField] private bool resolveTimeoutByAliveCount = true;

        [Header("Test Controls")]
        [SerializeField] private bool allowFastRestart = true;
        [SerializeField] private KeyCode fastRestartKey = KeyCode.F8;

        [Header("Combatants")]
        [SerializeField] private FpsPlayerInstaller playerInstaller;
        [SerializeField] private CombatBotController[] attackers = System.Array.Empty<CombatBotController>();
        [SerializeField] private CombatBotController[] defenders = System.Array.Empty<CombatBotController>();

        private RoundState state;
        private float stateEndsAt;
        private string resultReason = string.Empty;
        private Health playerHealth;
        private PlayerRespawnController playerRespawn;
        private TeamCombatant playerCombatant;
        private int attackerRounds;
        private int defenderRounds;

        public RoundState State => state;
        public float TimeRemaining => Mathf.Max(0f, stateEndsAt - Time.time);
        public string ResultReason => resultReason;
        public int MaxTeamSize => maxTeamSize;
        public int RoundsToWin => roundsToWin;
        public int AttackerRounds => attackerRounds;
        public int DefenderRounds => defenderRounds;
        /// <summary>
        /// Loadout changes are a pre-round decision. Keeping this rule on the match authority makes
        /// the later networked version a matter of validating the same state on the server.
        /// </summary>
        public bool CanEditLoadout => state == RoundState.Preparation;
        public int AliveAttackerCount
        {
            get
            {
                int count = playerHealth != null && playerHealth.IsAlive ? 1 : 0;
                foreach (CombatBotController attacker in attackers)
                    if (attacker != null && attacker.IsAlive) count++;
                return count;
            }
        }
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
            RoundState.Draw => "ROUND DRAW",
            RoundState.MatchComplete => attackerRounds > defenderRounds ? "ATTACKERS WIN THE MATCH" : "DEFENDERS WIN THE MATCH",
            _ => string.Empty
        };

        public string ScoreLabel => $"ATT {attackerRounds}/{roundsToWin}  -  {defenderRounds}/{roundsToWin} DEF";

        public string ObjectiveText
        {
            get
            {
                if (state == RoundState.Preparation)
                    return "ELIMINATE THE ENEMY TEAM // ONE LIFE ONLY";
                if (state == RoundState.Active)
                    return $"ELIMINATE THE ENEMY TEAM  //  ALIVE {AliveAttackerCount} : {AliveDefenderCount}";
                if (state == RoundState.MatchComplete)
                    return $"{StateLabel}  //  PRESS [{fastRestartKey}] TO RESTART TEST MATCH";
                return resultReason;
            }
        }

        /// <summary>
        /// Compatibility entry point for the existing scene builder. Objectives are deliberately
        /// ignored by team elimination; demolition mode will own them through a dedicated rule set.
        /// </summary>
        public void SetObjectives(ObjectiveZone[] _) { }

        public void ConfigureCombatants(FpsPlayerInstaller player, CombatBotController[] defenderBots)
            => ConfigureCombatants(player, System.Array.Empty<CombatBotController>(), defenderBots);

        public void ConfigureCombatants(FpsPlayerInstaller player, CombatBotController[] attackerBots,
            CombatBotController[] defenderBots)
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            playerInstaller = player;
            attackers = attackerBots ?? System.Array.Empty<CombatBotController>();
            defenders = defenderBots ?? System.Array.Empty<CombatBotController>();
            playerHealth = playerInstaller != null ? playerInstaller.Health : null;
            playerRespawn = playerInstaller != null ? playerInstaller.GetComponent<PlayerRespawnController>() : null;
            playerCombatant = EnsureCombatant(playerInstaller != null ? playerInstaller.gameObject : null, CombatTeam.Attackers);
            ConfigureBotTeams(attackers, CombatTeam.Attackers);
            ConfigureBotTeams(defenders, CombatTeam.Defenders);
            ValidateTeamCapacity();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        /// <summary>Returns the closest living member of the opposing team for offline bot validation.</summary>
        public Transform GetTargetFor(CombatBotController requester)
        {
            if (requester == null) return null;
            TeamCombatant requesterCombatant = requester.GetComponent<TeamCombatant>();
            if (requesterCombatant == null || requesterCombatant.Team == CombatTeam.None) return null;

            CombatTeam opposingTeam = requesterCombatant.Team == CombatTeam.Attackers
                ? CombatTeam.Defenders
                : CombatTeam.Attackers;
            Transform closest = null;
            float closestDistance = float.MaxValue;

            if (opposingTeam == CombatTeam.Attackers)
            {
                ConsiderTarget(playerCombatant, requester.transform.position, ref closest, ref closestDistance);
                ConsiderTargets(attackers, requester.transform.position, ref closest, ref closestDistance);
            }
            else
            {
                ConsiderTargets(defenders, requester.transform.position, ref closest, ref closestDistance);
            }

            return closest;
        }

        private void Start()
        {
            if (playerInstaller == null)
                ConfigureCombatants(FindObjectOfType<FpsPlayerInstaller>(), System.Array.Empty<CombatBotController>(),
                    FindObjectsOfType<CombatBotController>());
            RestartMatch();
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        private void Update()
        {
            if (allowFastRestart && UnityEngine.Input.GetKeyDown(fastRestartKey))
            {
                ReloadCurrentScene();
                return;
            }

            if (state == RoundState.Active)
            {
                if (AliveAttackerCount == 0)
                {
                    FinishRound(RoundState.DefendersWin, "ATTACKERS ELIMINATED");
                    return;
                }
                if (AliveDefenderCount == 0)
                {
                    FinishRound(RoundState.AttackersWin, "DEFENDERS ELIMINATED");
                    return;
                }
            }

            if (state == RoundState.MatchComplete || Time.time < stateEndsAt) return;
            switch (state)
            {
                case RoundState.Preparation:
                    BeginRound();
                    break;
                case RoundState.Active:
                    ResolveRoundTimeout();
                    break;
                case RoundState.AttackersWin:
                case RoundState.DefendersWin:
                case RoundState.Draw:
                    if (attackerRounds >= roundsToWin || defenderRounds >= roundsToWin)
                        BeginMatchComplete();
                    else
                        BeginPreparation();
                    break;
            }
        }

        /// <summary>Starts a fresh best-of-thirteen test match without changing configured combatants.</summary>
        public void RestartMatch()
        {
            attackerRounds = 0;
            defenderRounds = 0;
            BeginPreparation();
        }

        private void BeginPreparation()
        {
            state = RoundState.Preparation;
            stateEndsAt = Time.time + preparationSeconds;
            resultReason = "ELIMINATE THE ENEMY TEAM";
            ResetCombatants();
            SetPlayerGameplayEnabled(false);
            SetPlayerLoadoutEditingEnabled(true);
        }

        private void BeginRound()
        {
            state = RoundState.Active;
            stateEndsAt = Time.time + roundSeconds;
            resultReason = string.Empty;
            if (playerRespawn != null) playerRespawn.SetRoundRespawnsEnabled(false);
            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(true);
            EnableBotsForRound(attackers);
            EnableBotsForRound(defenders);
        }

        private void ResolveRoundTimeout()
        {
            if (!resolveTimeoutByAliveCount)
            {
                FinishRound(RoundState.Draw, "TIME EXPIRED");
                return;
            }

            if (AliveAttackerCount > AliveDefenderCount)
                FinishRound(RoundState.AttackersWin, "TIME EXPIRED // ATTACKERS HAVE MORE SURVIVORS");
            else if (AliveDefenderCount > AliveAttackerCount)
                FinishRound(RoundState.DefendersWin, "TIME EXPIRED // DEFENDERS HAVE MORE SURVIVORS");
            else
                FinishRound(RoundState.Draw, "TIME EXPIRED // EQUAL SURVIVORS");
        }

        private void FinishRound(RoundState result, string reason)
        {
            if (state != RoundState.Active) return;

            state = result;
            stateEndsAt = Time.time + resultSeconds;
            resultReason = reason;
            if (result == RoundState.AttackersWin) attackerRounds++;
            else if (result == RoundState.DefendersWin) defenderRounds++;

            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(false);
            DisableBots(attackers);
            DisableBots(defenders);
        }

        private void BeginMatchComplete()
        {
            state = RoundState.MatchComplete;
            stateEndsAt = 0f;
            resultReason = attackerRounds > defenderRounds ? "ATTACKERS REACHED MATCH POINT" : "DEFENDERS REACHED MATCH POINT";
            SetPlayerLoadoutEditingEnabled(false);
            SetPlayerGameplayEnabled(false);
            DisableBots(attackers);
            DisableBots(defenders);
        }

        private void OnPlayerDied()
        {
            if (state == RoundState.Active && AliveAttackerCount == 0)
                FinishRound(RoundState.DefendersWin, "ATTACKERS ELIMINATED");
        }

        private void ResetCombatants()
        {
            if (playerRespawn != null)
            {
                playerRespawn.SetRoundRespawnsEnabled(true);
                playerRespawn.ResetForRound();
            }
            else
            {
                playerHealth?.ResetHealth();
            }

            ResetBotsForRound(attackers);
            ResetBotsForRound(defenders);
        }

        private void SetPlayerGameplayEnabled(bool enabled)
        {
            if (playerInstaller == null) return;
            if (playerInstaller.Player != null) playerInstaller.Player.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Weapon != null) playerInstaller.Weapon.SetGameplayInputEnabled(enabled);
            if (playerInstaller.Abilities != null) playerInstaller.Abilities.SetGameplayInputEnabled(enabled);
        }

        private void SetPlayerLoadoutEditingEnabled(bool enabled)
        {
            if (playerInstaller != null && playerInstaller.Weapon != null)
                playerInstaller.Weapon.SetLoadoutEditingEnabled(enabled);
        }

        private static void ReloadCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(activeScene.name))
                SceneManager.LoadScene(activeScene.name);
        }

        private static TeamCombatant EnsureCombatant(GameObject combatantObject, CombatTeam team)
        {
            if (combatantObject == null) return null;
            TeamCombatant combatant = combatantObject.GetComponent<TeamCombatant>();
            if (combatant == null) combatant = combatantObject.AddComponent<TeamCombatant>();
            combatant.SetTeam(team);
            return combatant;
        }

        private static void ConfigureBotTeams(CombatBotController[] bots, CombatTeam team)
        {
            foreach (CombatBotController bot in bots)
                if (bot != null) EnsureCombatant(bot.gameObject, team);
        }

        private void ValidateTeamCapacity()
        {
            int attackerCount = (playerInstaller != null ? 1 : 0) + attackers.Length;
            if (attackerCount > maxTeamSize)
                Debug.LogWarning($"Attackers have {attackerCount} combatants but the mode maximum is {maxTeamSize}.", this);
            if (defenders.Length > maxTeamSize)
                Debug.LogWarning($"Defenders have {defenders.Length} combatants but the mode maximum is {maxTeamSize}.", this);
        }

        private static void ConsiderTarget(TeamCombatant candidate, Vector3 origin, ref Transform closest, ref float closestDistance)
        {
            if (candidate == null || !candidate.IsAlive) return;
            float distance = (candidate.transform.position - origin).sqrMagnitude;
            if (distance >= closestDistance) return;
            closest = candidate.transform;
            closestDistance = distance;
        }

        private static void ConsiderTargets(CombatBotController[] candidates, Vector3 origin, ref Transform closest,
            ref float closestDistance)
        {
            foreach (CombatBotController candidate in candidates)
            {
                if (candidate == null) continue;
                ConsiderTarget(candidate.GetComponent<TeamCombatant>(), origin, ref closest, ref closestDistance);
            }
        }

        private static void EnableBotsForRound(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
            {
                if (bot == null) continue;
                bot.SetRoundRespawnsEnabled(false);
                bot.SetCombatEnabled(true);
            }
        }

        private static void DisableBots(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
                if (bot != null) bot.SetCombatEnabled(false);
        }

        private static void ResetBotsForRound(CombatBotController[] bots)
        {
            foreach (CombatBotController bot in bots)
            {
                if (bot == null) continue;
                bot.SetCombatEnabled(false);
                bot.SetRoundRespawnsEnabled(true);
                bot.ResetForRound();
            }
        }
    }
}
