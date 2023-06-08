using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HMH.ECS.SpatialHashing
{
    [BurstCompile]
    public abstract partial class SpatialHashingSystem<T, TY, TZ> : SystemBase where T : unmanaged, ISpatialHashingItem<T>, IComponentData
                                                                               where TY : unmanaged, ISpatialHashingItemMiror
                                                                               where TZ : unmanaged, IComponentData
    {
        #region Variables

        protected EntityQuery _addGroup;
        protected EntityQuery _removeGroup;
        protected EntityQuery _updateGroup;

        protected SpatialHash<T> _spatialHash;

        protected bool      _cleanSpatialHashmapAtStopRunning = true;
        protected JobHandle _lastDependency;

        private CountNewAdditionJob _countNewAdditionJob;
        #endregion

        #region Properties

        protected abstract EntityCommandBuffer CommandBuffer { get; }
        public bool RemoveUpdateComponent { get; set; } = true;

        #endregion

        /// <inheritdoc />
        protected override void OnCreate()
        {
            InitSpatialHashing();

            var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
            _addGroup = entityQueryBuilder.WithAllRW<T>().WithNone<TY>().Build(this);

            entityQueryBuilder.Reset();
            _updateGroup = entityQueryBuilder.WithAllRW<T>().WithAll<TY, TZ>().Build(this);

            entityQueryBuilder.Reset();
            _removeGroup         = entityQueryBuilder.WithNone<T>().WithAll<TY>().Build(this);

            _countNewAdditionJob = new CountNewAdditionJob { Counter = new NativeReference<int>(0, Allocator.Persistent) };
        }

        /// <inheritdoc />
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var entityQueryBuilder   = new EntityQueryBuilder(Allocator.Temp);
            var alreadyExistingGroup = entityQueryBuilder.WithAllRW<T, TY>().Build(this);

            if (alreadyExistingGroup.IsEmptyIgnoreFilter == false)
            {
                var items = alreadyExistingGroup.ToComponentDataArray<T>(World.UpdateAllocator.ToAllocator);

                for (var i = 0; i < items.Length; i++)
                {
                    ref var item = ref items.GetElementAsRef(i);
                    _spatialHash.Add(ref item);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            if (_spatialHash.IsCreated && _cleanSpatialHashmapAtStopRunning)
            {
                _lastDependency.Complete();
                _spatialHash.Clear();
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            base.OnDestroy();

            _countNewAdditionJob.Dispose();

            if (_spatialHash.IsCreated)
            {
                _lastDependency.Complete();
                _spatialHash.Dispose();
            }
        }

        protected abstract void InitSpatialHashing();

        /// <inheritdoc />
        protected sealed override void OnUpdate()
        {
            OnPreUpdate();

            _countNewAdditionJob.Counter.Value = 0;

            Dependency = _countNewAdditionJob.Schedule(_addGroup, Dependency);

            var increaseSpatialHashSizeJob = new IncreaseSpatialHashSizeJob { Counter = _countNewAdditionJob.Counter, SpatialHash = _spatialHash };

            Dependency = increaseSpatialHashSizeJob.Schedule(Dependency);

            var addSpatialHashingJob = new AddSpatialHashingJob { ComponentTTypeHandle = GetComponentTypeHandle<T>(), SpatialHash = _spatialHash.ToConcurrent() };

            Dependency = addSpatialHashingJob.Schedule(_addGroup, Dependency);

            var addSpatialHashingEndJob = new AddSpatialHashingEndJob
            {
                ComponentTTypeHandle = GetComponentTypeHandle<T>(true), EntityTypeHandle = GetEntityTypeHandle(), CommandBuffer = CommandBuffer.AsParallelWriter()
            };

            Dependency = addSpatialHashingEndJob.ScheduleParallel(_addGroup, Dependency);

            var updateRemoveJob = new UpdateSpatialHashingRemoveFastJob { ComponentTTypeHandle = GetComponentTypeHandle<T>(true), SpatialHash = _spatialHash };
            Dependency = updateRemoveJob.Schedule(_updateGroup, Dependency);

            var updateSpatialHashingAddFastJob = new UpdateSpatialHashingAddFastJob { ComponentTTypeHandle = GetComponentTypeHandle<T>(true), SpatialHash = _spatialHash.ToConcurrent() };
            Dependency = updateSpatialHashingAddFastJob.Schedule(_updateGroup, Dependency);

            if (RemoveUpdateComponent)
                CommandBuffer.RemoveComponent(_updateGroup, typeof(TZ));

            var removeSpatialHashingJob = new RemoveSpatialHashingJob { ComponentMirrorTypeHandle = GetComponentTypeHandle<TY>(), SpatialHash = _spatialHash };
            Dependency = removeSpatialHashingJob.Schedule(_removeGroup, Dependency);

            CommandBuffer.RemoveComponent(_removeGroup, typeof(TY));

            OnPostUpdate();

            AddJobHandleForProducer(Dependency);
            _lastDependency = Dependency;
        }

        protected virtual void OnPreUpdate()
        { }

        protected virtual void OnPostUpdate()
        { }

        protected abstract void AddJobHandleForProducer(JobHandle inputDeps);

        #region Job

        [BurstCompile]
        public struct CountNewAdditionJob : IJobChunk, IDisposable
        {
            public NativeReference<int> Counter;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Counter.Value += chunk.Count;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                Counter.Dispose();
            }
        }

        [BurstCompile]
        public struct IncreaseSpatialHashSizeJob : IJob
        {
            public NativeReference<int> Counter;
            public SpatialHash<T>       SpatialHash;

            [BurstCompile]
            public void Execute()
            {
                //strangely resizing just for the truth length doesn't give enough space
                SpatialHash.PrepareFreePlace((int)(Counter.Value * 1.5F));
            }
        }

        [BurstCompile]
        public struct AddSpatialHashingJob : IJobChunk
        {
            public ComponentTypeHandle<T>    ComponentTTypeHandle;
            public SpatialHash<T>.Concurrent SpatialHash;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<T> itemArray = chunk.GetNativeArray(ref ComponentTTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref var item = ref itemArray.GetElementAsRef(i);
                    SpatialHash.TryAdd(ref item);
                }
            }
        }

        [BurstCompile]
        public struct AddSpatialHashingEndJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<T>      itemArray   = chunk.GetNativeArray(ref ComponentTTypeHandle);
                NativeArray<Entity> entityArray = chunk.GetNativeArray(EntityTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref T   item   = ref itemArray.GetElementAsRefReadOnly(i);
                    var mirror = new TY { GetItemID = item.SpatialHashingIndex };
                    CommandBuffer.AddComponent(unfilteredChunkIndex, entityArray[i], mirror);
                }
            }
        }

        [BurstCompile]
        public struct RemoveSpatialHashingJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<TY> ComponentMirrorTypeHandle;
            public SpatialHash<T> SpatialHash;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<TY> mirrorArray = chunk.GetNativeArray(ref ComponentMirrorTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref var item = ref mirrorArray.GetElementAsRefReadOnly(i);
                    SpatialHash.Remove(item.GetItemID);
                }
            }
        }

        [BurstCompile]
        public struct UpdateSpatialHashingRemoveFastJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            public SpatialHash<T> SpatialHash;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<T> mirrorArray = chunk.GetNativeArray(ref ComponentTTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref var item = ref mirrorArray.GetElementAsRefReadOnly(i);
                    SpatialHash.Remove(item.SpatialHashingIndex);
                }
            }
        }

        [BurstCompile]
        public struct UpdateSpatialHashingAddFastJob : IJobChunk
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            public SpatialHash<T>.Concurrent SpatialHash;

            [BurstCompile]
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<T> mirrorArray = chunk.GetNativeArray(ref ComponentTTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while (enumerator.NextEntityIndex(out var i))
                {
                    ref var item = ref mirrorArray.GetElementAsRefReadOnly(i);
                    SpatialHash.AddFast(in item);
                }
            }
        }

        #endregion
    }

    public interface ISpatialHashingItemMiror : ICleanupComponentData
    {
        int GetItemID { get; set; }
    }

    public struct SpatialHashingMirror : ISpatialHashingItemMiror
    {
        public int GetItemID { get; set; }
    }
}