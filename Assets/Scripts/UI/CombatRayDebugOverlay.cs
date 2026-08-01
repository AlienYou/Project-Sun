using System.Collections.Generic;
using ProjectSun.FPS.AI;
using ProjectSun.FPS.Core;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using ProjectSun.FPS.Rounds;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>F10-only runtime aid for validating each combat actor's latest ray. Excluded from normal presentation.</summary>
    public sealed class CombatRayDebugOverlay : MonoBehaviour
    {
        private const float RecordLifetime = 1.5f;
        private static readonly Dictionary<string, RayRecord> records = new Dictionary<string, RayRecord>();
        private FpsInput input;
        private FpsPlayerController player;
        private Health playerHealth;
        private RoundManager roundManager;
        private CombatBotController[] defenders = System.Array.Empty<CombatBotController>();

        public static bool Enabled { get; private set; }

        public void Configure(FpsPlayerController playerController, Health health = null, RoundManager rounds = null,
            CombatBotController[] defenderBots = null)
        {
            player = playerController;
            input = player != null ? player.Input : null;
            playerHealth = health;
            roundManager = rounds;
            defenders = defenderBots ?? System.Array.Empty<CombatBotController>();
        }

        private void Update()
        {
            if (!IsAvailableInBuild) return;
            if (input != null && input.WasPressed(FpsBinding.DebugCombat))
                Enabled = !Enabled;
        }

        public static void Record(string source, Ray ray, bool hasHit, RaycastHit hit, CombatRayOutcome outcome)
        {
            if (!IsAvailableInBuild || !Enabled) return;
            Vector3 end = hasHit ? hit.point : ray.GetPoint(25f);
            Color color = ColorFor(outcome);
            if (records.TryGetValue(source, out RayRecord previous) && previous.HoldUntil > Time.time && outcome != CombatRayOutcome.DamageApplied)
            {
                Debug.DrawLine(ray.origin, end, color, 0.25f, false);
                return;
            }
            string target = hasHit ? $"{hit.collider.name}  L{hit.collider.gameObject.layer}" : "no combat-layer hit";
            records[source] = new RayRecord
            {
                Message = $"{source}: {target}  {LabelFor(outcome)}",
                Color = color,
                RecordedAt = Time.time,
                HoldUntil = outcome == CombatRayOutcome.DamageApplied ? Time.time + 0.7f : 0f
            };
            Debug.DrawLine(ray.origin, end, color, 0.25f, false);
        }

        public static void MarkDamageApplied(string source)
        {
            if (!IsAvailableInBuild || !Enabled || !records.TryGetValue(source, out RayRecord record)) return;
            record.Message = record.Message.Replace("VISIBLE", "DAMAGE APPLIED");
            record.Color = ColorFor(CombatRayOutcome.DamageApplied);
            record.RecordedAt = Time.time;
            record.HoldUntil = Time.time + 0.7f;
            records[source] = record;
        }

        private void OnGUI()
        {
            if (!IsAvailableInBuild || !Enabled) return;
            Color previous = GUI.color;
            List<KeyValuePair<string, RayRecord>> activeRecords = new List<KeyValuePair<string, RayRecord>>();
            foreach (KeyValuePair<string, RayRecord> pair in records)
                if (Time.time - pair.Value.RecordedAt <= RecordLifetime)
                    activeRecords.Add(pair);
            activeRecords.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            float height = 170f + Mathf.Max(1, defenders.Length) * 20f + Mathf.Max(1, activeRecords.Count) * 20f;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            float top = Mathf.Max(24f, Screen.height - height - 24f);
            GUI.Box(new Rect(24f, top, 760f, height), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(38f, top + 10f, 730f, 20f), "COMBAT DEBUG DASHBOARD [F10]", GUI.skin.label);
            DrawDashboard(top + 34f);
            float recordTop = top + 130f + Mathf.Max(1, defenders.Length) * 20f;
            GUI.color = Color.white;
            GUI.Label(new Rect(38f, recordTop, 710f, 20f), "RAY RECORDS", GUI.skin.label);
            if (activeRecords.Count == 0)
                GUI.Label(new Rect(38f, recordTop + 20f, 710f, 20f), "No combat ray recorded.", GUI.skin.label);
            for (int i = 0; i < activeRecords.Count; i++)
            {
                RayRecord record = activeRecords[i].Value;
                GUI.color = record.Color;
                GUI.Label(new Rect(38f, recordTop + 20f + i * 20f, 710f, 20f), record.Message, GUI.skin.label);
            }
            GUI.color = previous;
        }

        private void DrawDashboard(float top)
        {
            string playerVitals = playerHealth != null ? $"{playerHealth.Current:0}/{playerHealth.Max:0}" : "UNAVAILABLE";
            string playerPosition = player != null ? FormatPosition(player.transform.position) : "UNAVAILABLE";
            string move = player != null ? $"({player.MoveInput.x:0.00}, {player.MoveInput.y:0.00})" : "UNAVAILABLE";
            string inputState = input != null && input.GameplayEnabled ? "LIVE" : "FROZEN";
            string lastDamage = playerHealth != null && playerHealth.HasLastDamage
                ? $"{playerHealth.LastDamage.Amount:0} from {playerHealth.LastDamage.Instigator?.name ?? "UNKNOWN"}"
                : "NONE";
            GUI.color = new Color(0.72f, 0.94f, 1f);
            GUI.Label(new Rect(38f, top, 710f, 20f),
                $"PLAYER  HP {playerVitals}  POS {playerPosition}  MOVE {move}  INPUT {inputState}", GUI.skin.label);
            GUI.Label(new Rect(38f, top + 20f, 710f, 20f), $"LAST DAMAGE  {lastDamage}", GUI.skin.label);

            string roundState = roundManager != null ? roundManager.StateLabel : "UNAVAILABLE";
            float roundTime = roundManager != null ? roundManager.TimeRemaining : 0f;
            string reason = roundManager != null && !string.IsNullOrEmpty(roundManager.ResultReason) ? roundManager.ResultReason : "-";
            int aliveDefenders = roundManager != null ? roundManager.AliveDefenderCount : 0;
            GUI.color = Color.white;
            GUI.Label(new Rect(38f, top + 44f, 710f, 20f),
                $"ROUND  {roundState}  {roundTime:0.0}s  DEFENDERS {aliveDefenders}/{defenders.Length}  REASON {reason}", GUI.skin.label);
            GUI.Label(new Rect(38f, top + 68f, 710f, 20f), "DEFENDERS", GUI.skin.label);
            for (int i = 0; i < defenders.Length; i++)
            {
                CombatBotController defender = defenders[i];
                if (defender == null)
                {
                    GUI.Label(new Rect(38f, top + 88f + i * 20f, 710f, 20f), $"DEFENDER {i + 1:00}  MISSING", GUI.skin.label);
                    continue;
                }
                Health health = defender.GetComponent<Health>();
                string vitals = health != null ? $"{health.Current:0}/{health.Max:0}" : "UNAVAILABLE";
                GUI.Label(new Rect(38f, top + 88f + i * 20f, 710f, 20f),
                    $"{defender.name.ToUpperInvariant()}  HP {vitals}  {defender.DebugState}  DIST {defender.TargetDistance:0.0}m  RAY {defender.LatestRayResult}", GUI.skin.label);
            }
        }

        private static string FormatPosition(Vector3 position) => $"({position.x:0.0}, {position.y:0.0}, {position.z:0.0})";

        private static bool IsAvailableInBuild
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        private static Color ColorFor(CombatRayOutcome outcome)
        {
            switch (outcome)
            {
                case CombatRayOutcome.DamageApplied: return new Color(0.3f, 1f, 0.5f);
                case CombatRayOutcome.Visible: return new Color(0.35f, 0.85f, 1f);
                case CombatRayOutcome.Blocked: return new Color(1f, 0.7f, 0.25f);
                default: return new Color(1f, 0.35f, 0.3f);
            }
        }

        private static string LabelFor(CombatRayOutcome outcome)
        {
            switch (outcome)
            {
                case CombatRayOutcome.DamageApplied: return "DAMAGE APPLIED";
                case CombatRayOutcome.Visible: return "VISIBLE";
                case CombatRayOutcome.Blocked: return "BLOCKED";
                default: return "MISS";
            }
        }

        private struct RayRecord
        {
            public string Message;
            public Color Color;
            public float RecordedAt;
            public float HoldUntil;
        }
    }

    public enum CombatRayOutcome { Miss, Blocked, Visible, DamageApplied }
}
