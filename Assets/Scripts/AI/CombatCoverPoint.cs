using UnityEngine;

namespace ProjectSun.FPS.AI
{
    /// <summary>Authored tactical anchor: defenders hide at CoverPosition and move to PeekPosition before firing.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatCoverPoint : MonoBehaviour
    {
        [SerializeField] private Vector3 peekPosition;
        private CombatBotController occupant;

        public Vector3 CoverPosition => transform.position;
        public Vector3 PeekPosition => peekPosition;
        public bool IsOccupied => occupant != null;

        public void SetPositions(Vector3 coverPosition, Vector3 peekWorldPosition)
        {
            transform.position = coverPosition;
            peekPosition = peekWorldPosition;
        }

        public bool TryClaim(CombatBotController bot)
        {
            if (bot == null || (occupant != null && occupant != bot)) return false;
            occupant = bot;
            return true;
        }

        public void Release(CombatBotController bot)
        {
            if (occupant == bot) occupant = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsOccupied ? new Color(1f, 0.55f, 0.2f) : new Color(0.2f, 0.85f, 1f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawLine(transform.position, peekPosition);
            Gizmos.DrawWireCube(peekPosition, Vector3.one * 0.18f);
        }
    }
}
