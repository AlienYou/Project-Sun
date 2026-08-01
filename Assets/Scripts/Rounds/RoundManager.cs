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

        private RoundState state;
        private float stateEndsAt;

        public RoundState State => state;
        public float TimeRemaining => Mathf.Max(0f, stateEndsAt - Time.time);
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
                if (state == RoundState.AttackersWin) return "OBJECTIVE ACTIVATED";
                if (state == RoundState.DefendersWin) return "TIME EXPIRED";
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

        private void Awake() => SubscribeObjectives();

        private void Start() => BeginPreparation();

        private void OnDestroy() => UnsubscribeObjectives();

        private void Update()
        {
            if (Time.time < stateEndsAt) return;
            switch (state)
            {
                case RoundState.Preparation:
                    BeginRound();
                    break;
                case RoundState.Active:
                    FinishRound(RoundState.DefendersWin);
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
            SetObjectivesAvailable(false);
        }

        private void BeginRound()
        {
            state = RoundState.Active;
            stateEndsAt = Time.time + roundSeconds;
            SetObjectivesAvailable(true);
        }

        private void FinishRound(RoundState result)
        {
            state = result;
            stateEndsAt = Time.time + resultSeconds;
            SetObjectivesAvailable(false);
        }

        private void OnObjectiveActivated(ObjectiveZone _)
        {
            if (state == RoundState.Active)
                FinishRound(RoundState.AttackersWin);
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
    }
}
