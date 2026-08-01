using System.Collections.Generic;
using ProjectSun.FPS.Input;
using ProjectSun.FPS.Player;
using UnityEngine;

namespace ProjectSun.FPS.UI
{
    /// <summary>F10-only runtime aid for validating each combat actor's latest ray. Excluded from normal presentation.</summary>
    public sealed class CombatRayDebugOverlay : MonoBehaviour
    {
        private const float RecordLifetime = 1.5f;
        private static readonly Dictionary<string, RayRecord> records = new Dictionary<string, RayRecord>();
        private FpsInput input;

        public static bool Enabled { get; private set; }

        public void Configure(FpsPlayerController player) => input = player != null ? player.Input : null;

        private void Update()
        {
            if (input != null && input.WasPressed(FpsBinding.DebugCombat))
                Enabled = !Enabled;
        }

        public static void Record(string source, Ray ray, bool hasHit, RaycastHit hit, CombatRayOutcome outcome)
        {
            if (!Enabled) return;
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
            if (!Enabled || !records.TryGetValue(source, out RayRecord record)) return;
            record.Message = record.Message.Replace("VISIBLE", "DAMAGE APPLIED");
            record.Color = ColorFor(CombatRayOutcome.DamageApplied);
            record.RecordedAt = Time.time;
            record.HoldUntil = Time.time + 0.7f;
            records[source] = record;
        }

        private void OnGUI()
        {
            if (!Enabled) return;
            Color previous = GUI.color;
            List<KeyValuePair<string, RayRecord>> activeRecords = new List<KeyValuePair<string, RayRecord>>();
            foreach (KeyValuePair<string, RayRecord> pair in records)
                if (Time.time - pair.Value.RecordedAt <= RecordLifetime)
                    activeRecords.Add(pair);
            activeRecords.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            float height = 28f + Mathf.Max(1, activeRecords.Count) * 20f;
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            GUI.Box(new Rect(24f, Screen.height - height - 24f, 640f, height), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(38f, Screen.height - height - 14f, 610f, 20f), "COMBAT RAY DEBUG [F10]", GUI.skin.label);
            if (activeRecords.Count == 0)
                GUI.Label(new Rect(38f, Screen.height - 40f, 610f, 20f), "No combat ray recorded.", GUI.skin.label);
            for (int i = 0; i < activeRecords.Count; i++)
            {
                RayRecord record = activeRecords[i].Value;
                GUI.color = record.Color;
                GUI.Label(new Rect(38f, Screen.height - height + 8f + i * 20f, 610f, 20f), record.Message, GUI.skin.label);
            }
            GUI.color = previous;
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
