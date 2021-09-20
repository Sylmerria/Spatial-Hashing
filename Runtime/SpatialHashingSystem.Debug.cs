using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace HMH.ECS.SpatialHashing
{
    public abstract partial class SpatialHashingSystem<T, TY, TZ>
    {
#if UNITY_EDITOR

        #region Variables

        public float RefreshTime = 0.2F;
        public bool  ShowRay;
        public Ray   DebugRay;
        public bool  ShowLink;

        private float                     _timeLastRefresh = -99F;
        private Dictionary<int3, List<T>> _links           = new Dictionary<int3, List<T>>();
        private List<int3>                _voxelTraversed  = new List<int3>();
        private VoxelRay<RayDebug>        _voxelRay;
        private RayDebug                  _rayDebug;

        #endregion

        public void EnableDebug()
        {
            _rayDebug.SpatialHash    = _spatialHash;
            _rayDebug.VoxelTraversed = _voxelTraversed;
        }

        public void OnDrawGizmos()
        {
            if (Application.isPlaying == false || _spatialHash.IsCreated == false)
                return;

            if (UnityEngine.Time.realtimeSinceStartup - _timeLastRefresh > RefreshTime)
                RefreshLinks();

            foreach (var l in _links)
            {
                DrawCell(l.Key);

                if (ShowLink)
                    foreach (var item in l.Value)
                        DrawLink(l.Key, item);
            }

            if (ShowRay)
            {
                var startRayPosition = DebugRay.origin;
                var startCellBound   = new Bounds(_spatialHash.GetPositionVoxel(_spatialHash.GetIndexVoxel(startRayPosition), true), _spatialHash.CellSize);

                var raySize = math.max(_spatialHash.WorldBounds.Size.x, math.max(_spatialHash.WorldBounds.Size.y, _spatialHash.WorldBounds.Size.z));

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(startRayPosition, DebugRay.GetPoint(raySize));

                T hit = default;

                if (_spatialHash.RayCast(DebugRay, ref hit))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(hit.GetCenter(), hit.GetSize());
                }

                _voxelTraversed.Clear();

                _voxelRay.RayCast(ref _rayDebug, DebugRay.origin, DebugRay.direction, raySize);
                Gizmos.color = new Color(0.88F, 0.6F, 0.1F, 0.4F);

                foreach (var voxel in _voxelTraversed)
                {
                    var position = _spatialHash.GetPositionVoxel(voxel, true);
                    Gizmos.DrawCube(position, _spatialHash.CellSize);
                }

                var rayOffsetted = new Ray(DebugRay.origin - (Vector3)(DebugRay.direction * _spatialHash.CellSize), DebugRay.direction);
                startCellBound.GetEnterPositionAABB(rayOffsetted, 1 << 25, out var enterPoint);
                Gizmos.color = Color.white;
                Gizmos.DrawCube(enterPoint, Vector3.one * 0.3F);

                startCellBound.GetExitPosition(rayOffsetted, 1 << 25, out var exitPoint);
                Gizmos.color = Color.white;
                Gizmos.DrawCube(exitPoint, Vector3.one * 0.3F);
            }
        }

        private void RefreshLinks()
        {
            _timeLastRefresh = UnityEngine.Time.realtimeSinceStartup;
            _links.Clear();

            var unic   = new HashSet<T>();
            var values = _spatialHash.DebugBuckets.GetValueArray(Allocator.TempJob);

            foreach (var itemTest in values)
                unic.Add(_spatialHash.DebugIDToItem[itemTest]);
            values.Dispose();

            var list = new NativeList<int3>(Allocator.Temp);

            foreach (var item in unic)
            {
                list.Clear();
                _spatialHash.GetIndexiesVoxel(item, list);

                foreach (var index in list)
                {
                    if (_links.TryGetValue(index, out var l) == false)
                    {
                        l = new List<T>();
                        _links.Add(index, l);
                    }

                    l.Add(item);
                }
            }

            list.Dispose();
        }

        private void DrawCell(int3 index)
        {
            var position = _spatialHash.GetPositionVoxel(index, true);
            Gizmos.color = new Color(0F, 1F, 0F, 0.3F);
            Gizmos.DrawCube(position, _spatialHash.CellSize);
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(position, _spatialHash.CellSize);
        }

        private void DrawLink(int3 cellIndex, T target)
        {
            var position = _spatialHash.GetPositionVoxel(cellIndex, true);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, target.GetCenter());
        }

#endif

        private struct RayDebug : IRay
        {
            public SpatialHash<T> SpatialHash;
            public List<int3>     VoxelTraversed;

            #region Implementation of IRay<ItemTest>

            /// <inheritdoc />
            public bool OnTraversingVoxel(int3 voxelIndex)
            {
                VoxelTraversed.Add(voxelIndex);

                return false;
            }

            /// <inheritdoc />
            public int3 GetIndexVoxel(float3 position)
            {
                return SpatialHash.GetIndexVoxel(position);
            }

            /// <inheritdoc />
            public float3 GetPositionVoxel(int3 index, bool center)
            {
                return SpatialHash.GetPositionVoxel(index, center);
            }

            /// <inheritdoc />
            public float3 CellSize => SpatialHash.CellSize;

            #endregion
        }
    }
}