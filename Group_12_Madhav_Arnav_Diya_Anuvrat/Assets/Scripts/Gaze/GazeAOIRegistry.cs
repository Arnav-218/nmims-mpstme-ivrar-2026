using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.CybersecurityVR
{
    /// <summary>
    /// Runtime registry tracking all active AOI targets in the scene.
    /// Provides O(1) lookup from raycast Colliders to GazeAOITarget components,
    /// avoiding expensive GetComponent or GameObject.Find calls in hot paths.
    /// </summary>
    public static class GazeAOIRegistry
    {
        private static readonly Dictionary<Collider, GazeAOITarget> _colliderMap = new Dictionary<Collider, GazeAOITarget>();
        private static readonly Dictionary<string, GazeAOITarget> _idMap = new Dictionary<string, GazeAOITarget>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<GazeAOITarget> _allTargets = new List<GazeAOITarget>();

        public static int ActiveCount => _allTargets.Count;

        public static void Register(GazeAOITarget target)
        {
            if (target == null) return;

            if (target.TargetCollider != null)
            {
                _colliderMap[target.TargetCollider] = target;
            }

            if (!string.IsNullOrEmpty(target.AoiId))
            {
                _idMap[target.AoiId] = target;
            }

            if (!_allTargets.Contains(target))
            {
                _allTargets.Add(target);
            }
        }

        public static void Unregister(GazeAOITarget target)
        {
            if (target == null) return;

            if (target.TargetCollider != null)
            {
                _colliderMap.Remove(target.TargetCollider);
            }

            if (!string.IsNullOrEmpty(target.AoiId))
            {
                _idMap.Remove(target.AoiId);
            }

            _allTargets.Remove(target);
        }

        public static bool TryGetTarget(Collider collider, out GazeAOITarget target)
        {
            if (collider != null && _colliderMap.TryGetValue(collider, out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        public static bool TryGetTargetById(string aoiId, out GazeAOITarget target)
        {
            if (!string.IsNullOrEmpty(aoiId) && _idMap.TryGetValue(aoiId, out target))
            {
                return true;
            }

            target = null;
            return false;
        }

        public static Vector3? GetTargetWorldPosition(string aoiId)
        {
            if (TryGetTargetById(aoiId, out var target))
            {
                if (target.TargetCollider != null)
                {
                    return target.TargetCollider.bounds.center;
                }
                return target.transform.position;
            }
            return null;
        }

        public static IReadOnlyList<GazeAOITarget> GetAllTargets()
        {
            return _allTargets;
        }

        public static void Clear()
        {
            _colliderMap.Clear();
            _idMap.Clear();
            _allTargets.Clear();
        }
    }
}
