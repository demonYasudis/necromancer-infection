using System.Collections.Generic;
using UnityEngine;

namespace NecromancerInfection
{
    /// <summary>One set of reserved places and clearance measurements per leader.</summary>
    internal sealed class HordeFormation
    {
        private const int OpenColumns = 9;
        private const float Spacing = .9f;
        private const float NarrowSpacing = .75f;
        private const float FrontDepth = 6.3f;
        private readonly Necromancer leader;
        private readonly List<Zombie> members = new List<Zombie>();
        private readonly List<Vector3> trail = new List<Vector3>();
        private readonly RaycastHit[] obstacles = new RaycastHit[64];
        private Quaternion heading;
        private float yaw;
        private Vector3 encounterDirection;
        private int frame = -1;
        private int columns = OpenColumns;
        private int frontRows;
        private float lateralSpacing;
        private float widenAt;

        public HordeFormation(Necromancer leader) => this.leader = leader;

        public Vector3 Forward => heading * Vector3.forward;

        public void FaceEncounter(Vector3 direction) => encounterDirection = direction;

        public void Join(Zombie unit)
        {
            unit.FormationSlot = members.Count;
            members.Add(unit);
        }

        public void Leave(Zombie unit)
        {
            int index = unit.FormationSlot;
            if (index < 0) return;
            int last = members.Count - 1;
            // Fill a vacancy with the last rank so the rest keep their places.
            members[index] = members[last];
            members[index].FormationSlot = index;
            members.RemoveAt(last);
            unit.FormationSlot = -1;
        }

        public Vector3 Destination(Zombie unit, float leadDistance, Rect area)
        {
            if (frame != Time.frameCount) Refresh(leadDistance, area);
            int row = unit.FormationSlot / columns;
            int column = unit.FormationSlot % columns;
            int side = column % 2 == 0 ? -1 : 1;
            float across = side * ((column + 1) / 2) * lateralSpacing;
            // Ranks that cannot fit ahead queue behind the leader while the passage is narrow.
            float along = row < frontRows ? leadDistance + row * Spacing
                : -leadDistance - (row - frontRows) * Spacing;
            Vector3 position = leader.transform.position + heading * new Vector3(across, 0, along);
            position.x = Mathf.Clamp(position.x, area.xMin + .05f, area.xMax - .05f);
            position.z = Mathf.Clamp(position.z, area.yMin + .05f, area.yMax - .05f);
            return position;
        }

        public bool TryDetour(Vector3 origin, Vector3 destination, RaycastHit[] hits, out Vector3 detour)
        {
            detour = default;
            if (HasClearPath(origin, destination, hits)) return false;
            // A rear rank can use the leader's route through an opening even after the front has widened.
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                Vector3 point = trail[i];
                if ((origin - point).sqrMagnitude < .36f || !HasClearPath(origin, point, hits)) continue;
                detour = point;
                return true;
            }
            return false;
        }

        private static bool HasClearPath(Vector3 origin, Vector3 destination, RaycastHit[] hits)
        {
            Vector3 offset = destination - origin;
            offset.y = 0;
            float distance = Mathf.Max(0, offset.magnitude - .15f);
            return Zombie.ObstacleClearance(origin, offset.normalized, distance, hits) >= distance;
        }

        private void Refresh(float leadDistance, Rect area)
        {
            Vector3 direction = encounterDirection.sqrMagnitude > .0001f ? encounterDirection : leader.TravelDirection;
            float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            yaw = frame < 0 ? desiredYaw : Mathf.MoveTowardsAngle(yaw, desiredYaw, 180 * Time.deltaTime);
            heading = Quaternion.Euler(0, yaw, 0);
            frame = Time.frameCount;

            Vector3 origin = leader.transform.position;
            RecordTrail(origin);
            Vector3 forward = Forward;
            Vector3 right = heading * Vector3.right;
            float ahead = Clearance(origin, forward, FrontDepth, area);
            float halfWidth = (OpenColumns / 2) * Spacing;
            float room = SideRoom(origin, right, halfWidth, area);
            float probeDepth = Mathf.Min(ahead, members.Count <= OpenColumns ? leadDistance : 4.2f);
            room = Mathf.Min(room, SideRoom(origin + forward * probeDepth, right, halfWidth, area));
            int fit = Mathf.Min(OpenColumns, 1 + 2 * Mathf.FloorToInt((room + .01f) / NarrowSpacing));
            if (fit <= columns) widenAt = Time.time + .3f;
            if (fit < columns || Time.time >= widenAt) columns = fit;
            lateralSpacing = columns > 1 ? Mathf.Min(Spacing, (room - .04f) / (columns / 2)) : Spacing;
            frontRows = Mathf.Max(0, 1 + Mathf.FloorToInt((ahead - leadDistance) / Spacing));
        }

        private void RecordTrail(Vector3 position)
        {
            if (trail.Count > 0)
            {
                float distance = (position - trail[trail.Count - 1]).sqrMagnitude;
                if (distance < .36f) return;
                if (distance > 25) trail.Clear();
            }
            if (trail.Count == 48) trail.RemoveAt(0);
            trail.Add(position);
        }

        private float SideRoom(Vector3 origin, Vector3 right, float width, Rect area)
        {
            return Mathf.Min(Clearance(origin, right, width, area), Clearance(origin, -right, width, area));
        }

        private float Clearance(Vector3 origin, Vector3 direction, float distance, Rect area)
        {
            if (Mathf.Abs(direction.x) > .001f)
                distance = Mathf.Min(distance, ((direction.x > 0 ? area.xMax : area.xMin) - origin.x) / direction.x);
            if (Mathf.Abs(direction.z) > .001f)
                distance = Mathf.Min(distance, ((direction.z > 0 ? area.yMax : area.yMin) - origin.z) / direction.z);
            return Zombie.ObstacleClearance(origin, direction, Mathf.Max(0, distance), obstacles);
        }
    }
}
