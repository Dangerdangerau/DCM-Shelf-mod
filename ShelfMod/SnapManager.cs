#pragma warning disable CS8632
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelfMod
{
    public class SnapManager : MonoBehaviour
    {
        public SnapManager(IntPtr ptr) : base(ptr) { }

        public static SnapManager Instance;

        private readonly HashSet<ShelfSnapZone> _zones = new HashSet<ShelfSnapZone>();
        private static readonly float SEARCH_RADIUS = 2.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterZone(ShelfSnapZone zone)
        {
            _zones.Add(zone);
        }

        public void UnregisterZone(ShelfSnapZone zone)
        {
            _zones.Remove(zone);
        }

        public ShelfSnapZone FindNearestZoneForItem(Vector3 worldPos, GameObject item)
        {
            bool small = ShelfSnapZone.IsSmallItem(item);
            ShelfSnapZone best = null;
            float bestDist = SEARCH_RADIUS;

            foreach (var zone in _zones)
            {
                if (zone == null || zone.IsOccupied)
                    continue;

                if (!small && zone.DepthRowIndex > 0)
                    continue;
                if (!small && zone.StackIndex != zone.TotalStackLevels / 2)
                    continue;

                Vector3 zonePos = zone.SlotCenter != null
                    ? zone.SlotCenter.position
                    : zone.transform.position;

                float dist = Vector3.Distance(worldPos, zonePos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = zone;
                }
            }

            return best;
        }

        public ShelfSnapZone FindNearestOccupiedZone(Vector3 worldPos)
        {
            ShelfSnapZone closest = null;
            float closestDist = SEARCH_RADIUS * 3f;

            foreach (var zone in _zones)
            {
                if (zone == null || !zone.IsOccupied)
                    continue;

                Vector3 zonePos = zone.SlotCenter != null
                    ? zone.SlotCenter.position
                    : zone.transform.position;

                float dist = Vector3.Distance(worldPos, zonePos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }

            return closest;
        }

        public GameObject ReleaseNearestItem(Vector3 worldPos)
        {
            ShelfSnapZone closest = null;
            float closestDist = SEARCH_RADIUS;

            foreach (var zone in _zones)
            {
                if (zone == null || !zone.IsOccupied)
                    continue;

                Vector3 zonePos = zone.SlotCenter != null
                    ? zone.SlotCenter.position
                    : zone.transform.position;

                float dist = Vector3.Distance(worldPos, zonePos);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }

            if (closest != null)
                return closest.ReleaseItem();
            return null;
        }
    }
}
