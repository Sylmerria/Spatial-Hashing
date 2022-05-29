using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HMH.ECS.SpatialHashing
{
    public abstract partial class SpatialHashingSystem<T, TY, TZ> : SystemBase where T : unmanaged, ISpatialHashingItem<T>, IComponentData
                                                                               where TY : unmanaged, ISpatialHashingItemMiror
                                                                               where TZ : unmanaged, IComponentData
    {
        /// <inheritdoc />
        protected override void OnCreate()
        {
            InitSpatialHashing();

            _addGroup    = GetEntityQuery(ComponentType.ReadWrite<T>(), ComponentType.Exclude<TY>());
            _updateGroup = GetEntityQuery(ComponentType.ReadWrite<T>(), ComponentType.ReadOnly<TY>(), ComponentType.ReadOnly<TZ>());
            _removeGroup = GetEntityQuery(ComponentType.Exclude<T>(), ComponentType.ReadOnly<TY>());
        }

        /// <inheritdoc />
        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            var alreadyExistingGroup = GetEntityQuery(ComponentType.ReadWrite<T>(), ComponentType.ReadWrite<TY>());

            if (alreadyExistingGroup.CalculateEntityCount() > 0)
            {
                var items = alreadyExistingGroup.ToComponentDataArray<T>(Allocator.Temp);

                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    _spatialHash.Add(ref item);
                    items[i] = item;
                }
            }
        }

        /// <inheritdoc />
        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            if (_spatialHash.IsCreated)
            {
                World.EntityManager.CompleteAllJobs(); //need to avoid error spawn
                _spatialHash.Clear();
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_spatialHash.IsCreated)
                _spatialHash.Dispose();
        }

        protected abstract void InitSpatialHashing();

        /// <inheritdoc />
        protected sealed override void OnUpdate()
        {
            //NativeHashmap can't resize when they are in concurent mode so prepare free place before
            _spatialHash.PrepareFreePlace((int)(_addGroup.CalculateEntityCount() * 1.5F)); //strangely resize just for the good length doesn't give enough space

            OnPreUpdate();

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
                CommandBuffer.RemoveComponentForEntityQuery(_updateGroup, typeof(TZ)); //Remove all component from query

            var removeSpatialHashingJob = new RemoveSpatialHashingJob { ComponentMirrorTypeHandle = GetComponentTypeHandle<TY>(), SpatialHash = _spatialHash };
            Dependency = removeSpatialHashingJob.Schedule(_removeGroup, Dependency);

            CommandBuffer.RemoveComponentForEntityQuery(_removeGroup, typeof(TY)); //Remove all component from query

            OnPostUpdate();

            AddJobHandleForProducer(Dependency);
        }

        protected virtual void OnPreUpdate()
        { }

        protected virtual void OnPostUpdate()
        { }

        protected abstract void AddJobHandleForProducer(JobHandle inputDeps);

        #region Job

        [BurstCompile]
        public struct AddSpatialHashingJob : IJobEntityBatch
        {
            public ComponentTypeHandle<T>    ComponentTTypeHandle;
            public SpatialHash<T>.Concurrent SpatialHash;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<T> itemArray = batchInChunk.GetNativeArray(ComponentTTypeHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    T item = itemArray[i];
                    SpatialHash.TryAdd(ref item);
                    itemArray[i] = item;
                }
            }
        }

        [BurstCompile]
        public struct AddSpatialHashingEndJob : IJobEntityBatch
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            [ReadOnly]
            public EntityTypeHandle EntityTypeHandle;
            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            /// <inheritdoc />
            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<T>      itemArray   = batchInChunk.GetNativeArray(ComponentTTypeHandle);
                NativeArray<Entity> entityArray = batchInChunk.GetNativeArray(EntityTypeHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    T   item   = itemArray[i];
                    var mirror = new TY { GetItemID = item.SpatialHashingIndex };
                    CommandBuffer.AddComponent(batchIndex, entityArray[i], mirror);
                }
            }
        }

        [BurstCompile]
        public struct RemoveSpatialHashingJob : IJobEntityBatch
        {
            [ReadOnly]
            public ComponentTypeHandle<TY> ComponentMirrorTypeHandle;
            public SpatialHash<T> SpatialHash;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<TY> mirrorArray = batchInChunk.GetNativeArray(ComponentMirrorTypeHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    SpatialHash.Remove(mirrorArray[i].GetItemID);
                }
            }
        }

        [BurstCompile]
        public struct UpdateSpatialHashingRemoveFastJob : IJobEntityBatch
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            public SpatialHash<T> SpatialHash;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<T> itemArray = batchInChunk.GetNativeArray(ComponentTTypeHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    SpatialHash.Remove(itemArray[i].SpatialHashingIndex);
                }
            }
        }

        [BurstCompile]
        public struct UpdateSpatialHashingAddFastJob : IJobEntityBatch
        {
            [ReadOnly]
            public ComponentTypeHandle<T> ComponentTTypeHandle;
            public SpatialHash<T>.Concurrent SpatialHash;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                NativeArray<T> itemArray = batchInChunk.GetNativeArray(ComponentTTypeHandle);

                for (int i = 0; i < batchInChunk.Count; i++)
                {
                    T item = itemArray[i];
                    SpatialHash.AddFast(in item);
                }
            }
        }

        #endregion

        #region Variables

        protected EntityQuery _addGroup;
        protected EntityQuery _removeGroup;
        protected EntityQuery _updateGroup;

        protected SpatialHash<T> _spatialHash;

        #endregion

        #region Properties

        protected abstract EntityCommandBuffer CommandBuffer { get; }
        public bool RemoveUpdateComponent { get; set; } = true;

        #endregion
    }

    public interface ISpatialHashingItemMiror : ISystemStateComponentData
    {
        int GetItemID { get; set; }
    }

    public struct SpatialHashingMirror : ISpatialHashingItemMiror
    {
        public int GetItemID { get; set; }
    }
}