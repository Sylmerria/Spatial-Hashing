using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace HMH.ECS.SpatialHashing
{
    /// <summary>
    /// Spatial hashing logic. Have to be assign by ref !
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public unsafe struct SpatialHash<T> : IDisposable, IRay where T : struct, ISpatialHashingItem<T>
    {
        public SpatialHash(Bounds worldBounds, float3 cellSize, Allocator label)
            : this(worldBounds, cellSize, worldBounds.GetCellCount(cellSize).Mul() * 3, label)
        { }

        public SpatialHash(Bounds worldBounds, float3 cellSize, int startSize, Allocator label)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Create(out _safety, out _disposeSentinel, 0, label);
#endif
            _allocatorLabel         = label;
            _data                   = (SpatialHashData*)UnsafeUtility.Malloc(sizeof(SpatialHashData), UnsafeUtility.AlignOf<SpatialHashData>(), label);
            _data -> WorldBounds    = worldBounds;
            _data -> WorldBoundsMin = worldBounds.Min;
            _data -> CellSize       = cellSize;
            _data -> CellCount      = worldBounds.GetCellCount(cellSize);
            _data -> RayCastBound   = new Bounds();
            _data -> HasHit         = false;
            _data -> Counter        = 0;
            _data -> RayOrigin      = float3.zero;
            _data -> RayDirection   = float3.zero;

            _buckets            = new NativeMultiHashMap<uint, int>(startSize, label);
            _itemIDToBounds     = new NativeHashMap<int, Bounds>(startSize >> 1, label);
            _itemIDToItem       = new NativeHashMap<int, T>(startSize >> 1, label);
            _helpMoveHashMapOld = new NativeHashMap<int3, byte>(128, _allocatorLabel);
            _helpMoveHashMapNew = new NativeHashMap<int3, byte>(128, _allocatorLabel);

            _voxelRay    = new VoxelRay<SpatialHash<T>>();
            _rayHitValue = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref _safety, ref _disposeSentinel);
#endif
            UnsafeUtility.Free(_data, _allocatorLabel);

            _buckets.Dispose();
            _itemIDToBounds.Dispose();
            _itemIDToItem.Dispose();
            _helpMoveHashMapOld.Dispose();
            _helpMoveHashMapNew.Dispose();
        }

        #region I/O

        public void Add(ref T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif

            var bounds = new Bounds(item.GetCenter(), item.GetSize());

            bounds.Clamp(_data -> WorldBounds);

            var itemID = ++_data -> Counter;

            item.SpatialHashingIndex = itemID;
            _itemIDToBounds.TryAdd(itemID, bounds);
            _itemIDToItem.TryAdd(itemID, item);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        AddInternal(hashPosition, itemID);
                    }
                }
            }
        }

        public void AddFast(ref T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif

            var itemID = item.SpatialHashingIndex;
            var bounds = new Bounds(item.GetCenter(), item.GetSize());

            bounds.Clamp(_data -> WorldBounds);

            _itemIDToBounds.Remove(itemID); //TODO Replace when hashmap will have value override
            _itemIDToBounds.TryAdd(itemID, bounds);
            _itemIDToItem.Remove(itemID); //TODO Replace when hashmap will have value override
            _itemIDToItem.TryAdd(itemID, item);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        AddInternal(hashPosition, itemID);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddInternal(int3 position, int itemID)
        {
            _buckets.Add(Hash(position), itemID);
        }

        public void Remove(int itemID)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif
            var success = _itemIDToBounds.TryGetValue(itemID, out var bounds);

            Assert.IsTrue(success);

            _itemIDToBounds.Remove(itemID);
            _itemIDToItem.Remove(itemID);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        RemoveInternal(hashPosition, itemID);
                    }
                }
            }
        }

        /// <summary>
        /// Remove method used for move or scale an item; 
        /// </summary>
        /// <param name="itemID"></param>
        public void RemoveFast(int itemID)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif
            var success = _itemIDToBounds.TryGetValue(itemID, out var bounds);

            Assert.IsTrue(success);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        RemoveInternal(hashPosition, itemID);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveInternal(int3 voxelPosition, int itemID)
        {
            var success = _buckets.TryRemove(Hash(voxelPosition), itemID);

            Assert.IsTrue(success);
        }

        public void Move(T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif
            var itemID  = item.SpatialHashingIndex;
            var success = _itemIDToBounds.TryGetValue(itemID, out var oldBounds);
            Assert.IsTrue(success);

            var newBounds = new Bounds(item.GetCenter(), item.GetSize());
            newBounds.Clamp(_data -> WorldBounds);
            _itemIDToBounds.Remove(itemID);
            _itemIDToBounds.TryAdd(itemID, newBounds);
            _itemIDToItem.Remove(itemID);
            _itemIDToItem.TryAdd(itemID, item);

            _helpMoveHashMapOld.Clear();
            SetVoxelIndexForBounds(_data, oldBounds, _helpMoveHashMapOld);
            _helpMoveHashMapNew.Clear();
            SetVoxelIndexForBounds(_data, newBounds, _helpMoveHashMapNew);

            var oldVoxel = _helpMoveHashMapOld.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < oldVoxel.Length; i++)
            {
                if (_helpMoveHashMapNew.TryGetValue(oldVoxel[i], out _) == false)
                    RemoveInternal(oldVoxel[i], itemID);
            }

            var newVoxel = _helpMoveHashMapNew.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < newVoxel.Length; i++)
            {
                if (_helpMoveHashMapOld.TryGetValue(newVoxel[i], out _) == false)
                    AddInternal(newVoxel[i], itemID);
            }
        }

        public void Resize(T item)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif

            Move(item);
        }

        public void Clear()
        {
            _itemIDToBounds.Clear();
            _buckets.Clear();
            _itemIDToBounds.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetVoxelIndexForBounds<TY>(SpatialHashData* data, Bounds bounds, NativeHashMap<int3, TY> collection) where TY : struct
        {
            CalculStartEndIterationInternal(data, bounds, out var start, out var end);

            var position = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                position.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    position.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        position.z = z;
                        collection.TryAdd(position, default(TY));
                    }
                }
            }
        }

        public void CalculStartEndIteration(Bounds bounds, out int3 start, out int3 end)
        {
            CalculStartEndIterationInternal(_data, bounds, out start, out end);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculStartEndIterationInternal(SpatialHashData* data, Bounds bounds, out int3 start, out int3 end)
        {
            start = ((bounds.Min - data -> WorldBoundsMin) / data -> CellSize).FloorToInt();
            end   = ((bounds.Max - data -> WorldBoundsMin) / data -> CellSize).CeilToInt();
        }

        public T GetObject(int index)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            if (_itemIDToItem.TryGetValue(index, out var value))
                return value;

            Assert.IsTrue(false);

            return default(T);
        }

        public void PrepareFreePlace(int count)
        {
            while (_buckets.Capacity - _buckets.Length < count)
                _buckets.Capacity *= 2;

            while (_itemIDToBounds.Capacity - _itemIDToBounds.Length < count)
                _itemIDToBounds.Capacity *= 2;

            while (_itemIDToItem.Capacity - _itemIDToItem.Length < count)
                _itemIDToItem.Capacity *= 2;
        }

        #endregion

        #region Query

        public void Query(int3 chunkIndex, NativeList<T> resultList)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            var hashMapUnic = new NativeHashMap<int, byte>(64, Allocator.Temp);
            var hash        = Hash(chunkIndex);

            if (_buckets.TryGetFirstValue(hash, out var item, out var it) == false)
                return;

            do
                if (hashMapUnic.TryAdd(item, 0))
                    resultList.Add(_itemIDToItem[item]);
            while (_buckets.TryGetNextValue(out item, ref it));
        }

        /// <summary>
        /// Query system to find object in <paramref name="bounds"/>.
        /// </summary>
        public void Query(Bounds bounds, NativeList<T> resultList)
        {
            Assert.IsTrue(resultList.IsCreated);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
            bounds.Clamp(_data -> WorldBounds);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashMapUnic  = new NativeHashMap<int, byte>(64, Allocator.Temp);
            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        var hash = Hash(hashPosition);

                        if (_buckets.TryGetFirstValue(hash, out var item, out var it))
                            do
                                hashMapUnic.TryAdd(item, 0);
                            while (_buckets.TryGetNextValue(out item, ref it));
                    }
                }
            }

            ExtractValueFromHashMap(hashMapUnic, bounds, resultList);
        }

        public void Query(Bounds bounds, NativeList<int3> voxelIndexes)
        {
            Assert.IsTrue(voxelIndexes.IsCreated);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
            bounds.Clamp(_data -> WorldBounds);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        voxelIndexes.Add(hashPosition);
                    }
                }
            }
        }

        public void Query(Bounds obbBounds, quaternion rotation, NativeList<T> resultList)
        {
            Assert.IsTrue(resultList.IsCreated);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            var bounds = TransformBounds(obbBounds, rotation);
            bounds.Clamp(_data -> WorldBounds);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashMapUnic = new NativeHashMap<int, byte>(64, Allocator.Temp);

            var hashPosition = new int3(0F);

            //add offset for simplify logic and allowed pruning
            obbBounds.Size += _data -> CellSize * 1F;

            var inverseRotation = math.inverse(rotation);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        var pos = GetPositionVoxel(hashPosition, true);

                        if (obbBounds.RayCastOBBFast(pos - new float3(_data -> CellSize.x * 0.5F, 0F, 0F), _right, inverseRotation, _data -> CellSize.x) ||
                            obbBounds.RayCastOBBFast(pos - new float3(0F, _data -> CellSize.y * 0.5F, 0F), _up, inverseRotation, _data -> CellSize.y) ||
                            obbBounds.RayCastOBBFast(pos - new float3(0F, 0F, _data -> CellSize.z * 0.5F), _forward, inverseRotation, _data -> CellSize.z))
                        {
                            var hash = Hash(hashPosition);

                            if (_buckets.TryGetFirstValue(hash, out var item, out var it))
                                do
                                    hashMapUnic.TryAdd(item, 0);
                                while (_buckets.TryGetNextValue(out item, ref it));
                        }
                    }
                }
            }

            ExtractValueFromHashMap(hashMapUnic, bounds, resultList);
        }

        public void Query(Bounds obbBounds, quaternion rotation, NativeList<int3> voxelIndexes)
        {
            Assert.IsTrue(voxelIndexes.IsCreated);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            var bounds = TransformBounds(obbBounds, rotation);
            bounds.Clamp(_data -> WorldBounds);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            //add offset for simplify logic and allowed pruning
            obbBounds.Size += _data -> CellSize * 1F;

            var inverseRotation = math.inverse(rotation);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        var pos = GetPositionVoxel(hashPosition, true);

                        if (obbBounds.RayCastOBBFast(pos - new float3(_data -> CellSize.x * 0.5F, 0F, 0F), _right, inverseRotation, _data -> CellSize.x) ||
                            obbBounds.RayCastOBBFast(pos - new float3(0F, _data -> CellSize.y * 0.5F, 0F), _up, inverseRotation, _data -> CellSize.y) ||
                            obbBounds.RayCastOBBFast(pos - new float3(0F, 0F, _data -> CellSize.z * 0.5F), _forward, inverseRotation, _data -> CellSize.z))
                            voxelIndexes.Add(hashPosition);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bounds TransformBounds(Bounds boundTarget, quaternion rotation)
        {
            float3 x    = math.mul(rotation, new float3(boundTarget.Size.x, 0, 0));
            float3 y    = math.mul(rotation, new float3(0, boundTarget.Size.y, 0));
            float3 z    = math.mul(rotation, new float3(0, 0, boundTarget.Size.z));
            float3 size = math.abs(x) + math.abs(y) + math.abs(z);

            var b = new Bounds(boundTarget.Center, size);
            return b;
        }

        private void ExtractValueFromHashMap(NativeHashMap<int, byte> hashMapUnic, Bounds bounds, NativeList<T> resultList)
        {
            var datas = hashMapUnic.GetKeyArray(Allocator.Temp);

            for (int i = 0; i < datas.Length; i++)
            {
                _itemIDToBounds.TryGetValue(datas[i], out var b);

                if (bounds.Intersects(b) == false)
                    continue;

                resultList.Add(_itemIDToItem[datas[i]]);
            }
        }

        public bool RayCast(Ray ray, ref T item, float length = 99999F)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            _data -> HasHit = false;

            _data -> RayOrigin    = ray.origin;
            _data -> RayDirection = ray.direction;

            _voxelRay.RayCast(ref this, _data -> RayOrigin, _data -> RayDirection, length);

            if (_data -> HasHit == false)
                return false;

            item = _itemIDToItem[_rayHitValue];
            return true;
        }

        public int QueryCount(int3 chunkIndex)
        {
            var hash = Hash(chunkIndex);

            int counter = 0;

            if (_buckets.TryGetFirstValue(hash, out _, out var it))
            {
                ++counter;

                while (_buckets.TryGetNextValue(out _, ref it))
                    ++counter;
            }

            return counter;
        }

        #endregion

        #region Hashing

        public static uint Hash(int3 cellIndex)
        {
            return math.hash(cellIndex);
        }

        private int GetCellCountFromSize(float3 size)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif

            var deltaSize = size / _data -> CellSize;

            return (int)math.ceil(deltaSize.x) * (int)math.ceil(deltaSize.y) * (int)math.ceil(deltaSize.z);
        }

        public float3 GetPositionVoxel(int3 index, bool center)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
            var pos = index * _data -> CellSize + _data -> WorldBoundsMin;

            if (center)
                pos += _data -> CellSize * 0.5F;

            return pos;
        }

        public int3 GetIndexVoxel(float3 position)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
            position -= _data -> WorldBoundsMin;

            position /= _data -> CellSize;

            return position.FloorToInt();
        }

        #endregion

        #region Implementation of IRay

        /// <inheritdoc />
        public bool OnTraversingVoxel(int3 voxelIndex)
        {
            if (math.any(voxelIndex > _data -> CellCount)) //if voxel still in world
                return true;

            var hash = Hash(voxelIndex);

            if (_buckets.TryGetFirstValue(hash, out var itemID, out var it))
                do
                {
                    _itemIDToBounds.TryGetValue(itemID, out var b);

                    if (b.GetEnterPositionAABB(_data -> RayOrigin, _data -> RayDirection, 1 << 25, out _) == false)
                        continue;

                    _data -> HasHit = true;
                    _rayHitValue     = itemID;

                    return true;
                } while (_buckets.TryGetNextValue(out itemID, ref it));

            return false;
        }

        /// <inheritdoc />
        public void GetIndexiesVoxel(T item, NativeList<int3> results)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
            var bounds = new Bounds(item.GetCenter(), item.GetSize());

            bounds.Clamp(_data -> WorldBounds);

            CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

            var hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        results.Add(hashPosition);
                    }
                }
            }
        }

        #endregion

        public Concurrent ToConcurrent()
        {
            return new Concurrent
            {
                _data           = _data,
                _safety         = _safety,
                _itemIDToBounds = _itemIDToBounds.ToConcurrent(),
                _itemIDToItem   = _itemIDToItem.ToConcurrent(),
                _buckets        = _buckets.ToConcurrent()
            };
        }

        #region Variables

        private static float3 _forward = new float3(0F, 0F, 1F);
        private static float3 _up      = new float3(0F, 1F, 0F);
        private static float3 _right   = new float3(1F, 0F, 0F);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle _safety;
        [NativeSetClassTypeToNullOnSchedule]
        private DisposeSentinel _disposeSentinel;
#endif

        private Allocator _allocatorLabel;

        [NativeDisableUnsafePtrRestriction]
        private SpatialHashData* _data;
        private NativeMultiHashMap<uint, int> _buckets;        //4
        private NativeHashMap<int, Bounds>    _itemIDToBounds; //4
        private NativeHashMap<int, T>         _itemIDToItem;   //4

        private NativeHashMap<int3, byte> _helpMoveHashMapOld;
        private NativeHashMap<int3, byte> _helpMoveHashMapNew;
        private VoxelRay<SpatialHash<T>>  _voxelRay;
        private int                       _rayHitValue;

        #endregion

        #region Proprieties

        public bool IsCreated => _data != null;

        public int ItemCount
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
                return _itemIDToBounds.Length;
            }
        }

        public int BucketItemCount
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
                return _buckets.Length;
            }
        }

        public float3 CellSize
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
                return _data -> CellSize;
            }
        }

        public Bounds WorldBounds
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
                return _data -> WorldBounds;
            }
        }

        public int3 CellCount
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(_safety);
#endif
                return _data -> CellCount;
            }
        }

#if UNITY_EDITOR
        public NativeMultiHashMap<uint, int> DebugBuckets => _buckets;
        public NativeHashMap<int, T> DebugIDToItem => _itemIDToItem;
        public Bounds DebugRayCastBounds => _data -> RayCastBound;
        public VoxelRay<SpatialHash<T>> DebugVoxelRay => _voxelRay;
#endif

        #endregion

        public struct Concurrent
        {
            public bool TryAdd(ref T item)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif

                var bounds = new Bounds(item.GetCenter(), item.GetSize());

                bounds.Clamp(_data -> WorldBounds);

                var itemID = Interlocked.Increment(ref _data -> Counter);
                item.SpatialHashingIndex = itemID;

                if (_itemIDToBounds.TryAdd(itemID, bounds) == false || _itemIDToItem.TryAdd(itemID, item) == false)
                    return false;

                CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

                var hashPosition = new int3(0F);

                for (int x = start.x; x < end.x; ++x)
                {
                    hashPosition.x = x;

                    for (int y = start.y; y < end.y; ++y)
                    {
                        hashPosition.y = y;

                        for (int z = start.z; z < end.z; ++z)
                        {
                            hashPosition.z = z;

                            var hash = Hash(hashPosition);
                            _buckets.Add(hash, itemID);
                        }
                    }
                }

                return true;
            }

            /// <summary>
            /// Add fast after a remove for moving or scaling item
            /// <para>DOESN'T WORK YET WAIT UNITY OVERRIDE VALUE IN HASHMAP</para>
            /// </summary>
            /// <param name="item"></param>
            /// <returns></returns>
            public void AddFast(ref T item)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(_safety);
#endif

                var itemID = item.SpatialHashingIndex;
                var bounds = new Bounds(item.GetCenter(), item.GetSize());

                bounds.Clamp(_data -> WorldBounds);

                //TODO Replace with Override
                if (_itemIDToBounds.TryAdd(itemID, bounds) == false || _itemIDToItem.TryAdd(itemID, item) == false)
                    return;

                CalculStartEndIterationInternal(_data, bounds, out var start, out var end);

                var hashPosition = new int3(0F);

                for (int x = start.x; x < end.x; ++x)
                {
                    hashPosition.x = x;

                    for (int y = start.y; y < end.y; ++y)
                    {
                        hashPosition.y = y;

                        for (int z = start.z; z < end.z; ++z)
                        {
                            hashPosition.z = z;

                            var hash = Hash(hashPosition);
                            _buckets.Add(hash, itemID);
                        }
                    }
                }
            }

            #region Variables

            [NativeDisableUnsafePtrRestriction]
            internal SpatialHashData* _data;

            internal NativeMultiHashMap<uint, int>.Concurrent _buckets;        //4
            internal NativeHashMap<int, Bounds>.Concurrent    _itemIDToBounds; //4
            internal NativeHashMap<int, T>.Concurrent         _itemIDToItem;   //4


#if ENABLE_UNITY_COLLECTIONS_CHECKS
            internal AtomicSafetyHandle _safety;
#endif

            [NativeSetThreadIndex]
            #pragma warning disable 649
            internal int _threadIndex;
            #pragma warning restore 649

            #endregion

            #region Properties


            #endregion
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpatialHashData
    {
        public Bounds WorldBounds;    //24
        public float3 WorldBoundsMin; //12

        public Bounds RayCastBound; //24
        public float3 CellSize;     //12

        public float3 RayOrigin;    //12
        public float3 RayDirection; //12
        public int3   CellCount;    //12

        public int  Counter; //4
        public bool HasHit;  //1
    }

    public interface ISpatialHashingItem<T> : IEquatable<T>
    {
        float3 GetCenter();
        float3 GetSize();

        int SpatialHashingIndex { get; set; }
    }
}