using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Random = UnityEngine.Random;

namespace HMH.ECS.SpatialHashing.Debug
{
    public class HashMapVisualDebug : MonoBehaviour, IRay
    {
        private void Start()
        {
            Random.InitState(123456789);

            var copy = new Bounds(_worldBounds.Center, _worldBounds.Size * 4F);
            _spatialHashing = new SpatialHash<ItemTest>(copy, new float3(10F), Allocator.Persistent);

            for (int i = 0; i < _spawnCount; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);

                if (_useRaycast == false)
                    g.AddComponent<HashTeleport>();
                Destroy(g.GetComponent<BoxCollider>());

                g.transform.position = new Vector3(Random.Range(_worldBounds.Min.x + 1, _worldBounds.Max.x),
                                                   Random.Range(_worldBounds.Min.y + 1, _worldBounds.Max.y),
                                                   Random.Range(_worldBounds.Min.z + 1, _worldBounds.Max.z));

                var item = new ItemTest() { ID = i, Position = g.transform.position };

                Profiler.BeginSample("SpatialHasing Add");
                _spatialHashing.Add(ref item);
                Profiler.EndSample();

                _listItemGameobject.Add(g);
                _listItem.Add(item);
            }
        }

        [BurstCompile]
        private struct MoveItemTestJob : IJob
        {
            #region Implementation of IJob

            /// <inheritdoc />
            public void Execute()
            {
                for (int i = 0; i < ItemList.Length; i++)
                    SpatialHash.Move(ItemList[i]);
            }

            public NativeList<ItemTest>  ItemList;
            public SpatialHash<ItemTest> SpatialHash;

            #endregion
        }

        
        [BurstCompile]
        private struct AddItemTestJob : IJobParallelFor
        {
            #region Implementation of IJob

            /// <inheritdoc />
            public void Execute(int index)
            {
                var item = ItemList[index];
                    SpatialHash.TryAdd(ref item);
                    ItemList[index] = item;
            }
            [NativeDisableParallelForRestriction]
            public NativeList<ItemTest>  ItemList;
            public SpatialHash<ItemTest>.Concurrent SpatialHash;

            #endregion
        }
        [BurstCompile]
        private struct RemoveItemTestJob : IJob
        {
            #region Implementation of IJob

            /// <inheritdoc />
            public void Execute()
            {
                for (int i = 0; i < ItemList.Length; i++)
                    SpatialHash.Remove(ItemList[i].SpatianHashingIndex);
            }

            public NativeList<ItemTest>  ItemList;
            public SpatialHash<ItemTest> SpatialHash;

            #endregion
        }

        private void FixedUpdate()
        {
            World.Active.EntityManager.CompleteAllJobs();

            NativeList<ItemTest> itemList = new NativeList<ItemTest>(_spawnCount, Allocator.TempJob);

            for (var i = 0; i < _listItem.Count; i++)
            {
                if (math.any(_listItem[i].Position != (float3)_listItemGameobject[i].transform.position))
                {
                    var item = _listItem[i];
                    Profiler.BeginSample("SpatialHasing Move");
                    itemList.Add(item);
                    Profiler.EndSample();
                }
            }
            //   new MoveItemTestJob() { SpatialHash = _spatialHashing, ItemList = itemList }.Schedule().Complete();
            int length = itemList.Length;
            var inputDep = new JobHandle();
            inputDep= new RemoveItemTestJob() { SpatialHash = _spatialHashing, ItemList = itemList }.Schedule(inputDep);
            inputDep = new AddItemTestJob() { SpatialHash = _spatialHashing.ToConcurrent(), ItemList = itemList }.Schedule(length, 32, inputDep);
            inputDep.Complete();

            int delta = 0;
            for (var i = 0; i < _listItem.Count; i++)
            {
                if (math.any(_listItem[i].Position != (float3)_listItemGameobject[i].transform.position))
                {
                    var item =itemList[delta++];
                    item.Position = _listItemGameobject[i].transform.position;
                    _listItem[i]  = item;
                }
            }
            itemList.Dispose();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying == false)
                return;

            if (Time.realtimeSinceStartup - _timeLastRefresh > _refreshTime)
                RefreshLinks();

            foreach (var l in _links)
            {
                DrawCell(l.Key);

                foreach (var item in l.Value)
                    DrawLink(l.Key, item);
            }

            if (_useRaycast)
            {
                var startRayPosition = _startRay.position;
                var startCellBound   = new Bounds(_spatialHashing.GetPositionVoxel(_spatialHashing.GetIndexVoxel(startRayPosition), true), _spatialHashing.CellSize);

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(startRayPosition, _endRay.position);

                ItemTest hit = new ItemTest();
                var      ray = new Ray(startRayPosition, _endRay.position - startRayPosition);

                if (_spatialHashing.RayCast(ray, ref hit, (_endRay.position - _startRay.position).magnitude))
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawCube(hit.GetCenter(), hit.GetSize() * 3F);
                }

                _voxelTraversed.Clear();

                var me = this;
                _voxelRay.RayCast(ref me, ray.origin, ray.direction, (_endRay.position - _startRay.position).magnitude);
                Gizmos.color = new Color(0.88F, 0.6F, 0.1F, 0.4F);

                foreach (var voxel in _voxelTraversed)
                {
                    var position = _spatialHashing.GetPositionVoxel(voxel, true);
                    Gizmos.DrawCube(position, _spatialHashing.CellSize);
                }

                var rayOffsetted = new Ray(ray.origin - (Vector3)(ray.direction * _spatialHashing.CellSize), ray.direction);
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
            _timeLastRefresh = Time.realtimeSinceStartup;
            _links.Clear();

            var unic   = new HashSet<ItemTest>();
            var values = _spatialHashing.DebugBuckets.GetValueArray(Allocator.TempJob);

            foreach (var itemTest in values)
                unic.Add(_spatialHashing.DebugIDToItem[itemTest]);
            values.Dispose();

            var list = new NativeList<int3>(Allocator.Temp);

            foreach (var item in unic)
            {
                list.Clear();
                _spatialHashing.GetIndexiesVoxel(item, list);

                for (int i = 0; i < list.Length; i++)
                {
                    var index = list[i];

                    if (_links.TryGetValue(index, out var l) == false)
                    {
                        l = new List<ItemTest>();
                        _links.Add(index, l);
                    }

                    l.Add(item);
                }
            }

            list.Dispose();
        }

        private void DrawCell(int3 index)
        {
            var position = _spatialHashing.GetPositionVoxel(index, true);
            Gizmos.color = new Color(0F, 1F, 0F, 0.3F);
            Gizmos.DrawCube(position, _spatialHashing.CellSize);
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(position, _spatialHashing.CellSize);
        }

        private void DrawLink(int3 cellIndex, ItemTest target)
        {
            var position = _spatialHashing.GetPositionVoxel(cellIndex, true);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(position, target.Position);
        }

        #region Implementation of IRay<ItemTest>

        /// <inheritdoc />
        public bool OnTraversingVoxel(int3 voxelIndex)
        {
            _voxelTraversed.Add(voxelIndex);

            return false;
        }

        /// <inheritdoc />
        public int3 GetIndexVoxel(float3 position)
        {
            return _spatialHashing.GetIndexVoxel(position);
        }

        /// <inheritdoc />
        public float3 GetPositionVoxel(int3 index, bool center)
        {
            return _spatialHashing.GetPositionVoxel(index, center);
        }

        /// <inheritdoc />
        public float3 CellSize { get { return _spatialHashing.CellSize; } }

        #endregion

        #region Variables

        [SerializeField, Range(0F, 1F)]
        private float _refreshTime = 0.2F;
        [SerializeField]
        private Bounds _worldBounds;
        [SerializeField]
        private int _spawnCount;
        [SerializeField]
        private bool _useRaycast;
        [SerializeField]
        private Transform _startRay;
        [SerializeField]
        private Transform _endRay;

        private List<ItemTest>                   _listItem           = new List<ItemTest>();
        private List<GameObject>                 _listItemGameobject = new List<GameObject>();
        private SpatialHash<ItemTest>            _spatialHashing;
        private float                            _timeLastRefresh = -99F;
        private Dictionary<int3, List<ItemTest>> _links           = new Dictionary<int3, List<ItemTest>>();
        private List<int3>                       _voxelTraversed  = new List<int3>();
        private VoxelRay<HashMapVisualDebug>     _voxelRay        = new VoxelRay<HashMapVisualDebug>();

        #endregion

        public struct ItemTest : ISpatialHashingItem<ItemTest>, IComponentData
        {
            public int    ID;
            public float3 Position;

            #region Implementation of IEquatable<ItemTest>

            /// <inheritdoc />
            public bool Equals(ItemTest other)
            {
                return ID == other.ID;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return ID;
            }

            #region Overrides of ValueType

            /// <inheritdoc />
            public override string ToString()
            {
                return "Item " + ID;
            }

            #endregion

            #endregion

            #region Implementation of ISpatialHashingItem<ItemTest>

            /// <inheritdoc />
            public float3 GetCenter()
            {
                return Position;
            }

            /// <inheritdoc />
            public float3 GetSize()
            {
                return new float3(1F);
            }

            /// <inheritdoc />
            public int SpatianHashingIndex { get; set; }

            #endregion
        }
    }
}