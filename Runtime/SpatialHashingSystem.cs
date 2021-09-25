using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace HMH.ECS.SpatialHashing
{
    public abstract partial class SpatialHashingSystem<T, TY, TZ> : SystemBase where T : struct, ISpatialHashingItem<T>, IComponentData
                                                                               where TY : struct, ISpatialHashingItemMiror
                                                                               where TZ : struct, IComponentData
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
        protected override void OnUpdate()
        {
            //NativeHashmap can't resize when they are in concurent mode so prepare free place before
            _spatialHash.PrepareFreePlace((int)(_addGroup.CalculateEntityCount() * 1.5F)); //strangely resize just for the good length doesn't give enough space

            Dependency = new AddSpatialHashingJob { SpatialHash      = _spatialHash.ToConcurrent() }.Schedule(_addGroup, Dependency);
            Dependency = new AddSpatialHashingEndJob { CommandBuffer = CommandBuffer.AsParallelWriter() }.Schedule(_addGroup, Dependency);

            var updateRemoveJob = new UpdateSpatialHashingRemoveFastJob();
            updateRemoveJob.SetSpatialHash(ref _spatialHash);
            Dependency = updateRemoveJob.ScheduleSingle(_updateGroup, Dependency);
            Dependency = new UpdateSpatialHashingAddFastJob { SpatialHash = _spatialHash.ToConcurrent() }.Schedule(_updateGroup, Dependency);

            if (RemoveUpdateComponent)
                CommandBuffer.RemoveComponent(_updateGroup, typeof(TZ)); //Remove all component from query

            var removeJob = new RemoveSpatialHashingJob();
            removeJob.SetSpatialHash(ref _spatialHash);
            Dependency = removeJob.ScheduleSingle(_removeGroup, Dependency);

            CommandBuffer.RemoveComponent(_removeGroup, typeof(TY)); //Remove all component from query

            AddJobHandleForProducer(Dependency);
        }

        protected abstract void AddJobHandleForProducer(JobHandle inputDeps);

        #region Job

        [BurstCompile]
        public struct AddSpatialHashingJob : IJobForEachWithEntity<T>
        {
            #region Implementation of IJobProcessComponentDataWithEntity<T>

            /// <inheritdoc />
            public void Execute(Entity entity, int index, ref T item)
            {
                SpatialHash.TryAdd(ref item);
            }

            #endregion

            #region Variables

            public SpatialHash<T>.Concurrent SpatialHash;

            #endregion
        }

        internal struct AddSpatialHashingEndJob : IJobForEachWithEntity<T>
        {
            #region Implementation of IJobProcessComponentDataWithEntity<T>

            /// <inheritdoc />
            public void Execute(Entity entity, int index, ref T item)
            {
                var mirror = new TY { GetItemID = item.SpatialHashingIndex };
                CommandBuffer.AddComponent(index, entity, mirror);
            }

            #endregion

            #region Variables

            public EntityCommandBuffer.ParallelWriter CommandBuffer;

            #endregion
        }

        [BurstCompile]
        public struct RemoveSpatialHashingJob : IJobForEachWithEntity<TY>
        {
            #region Implementation of IJobProcessComponentDataWithEntity<T>

            /// <inheritdoc />
            public void Execute(Entity entity, int index, [ReadOnly] ref TY mirror)
            {
                _spatialHash.Remove(mirror.GetItemID);
            }

            #endregion

            public void SetSpatialHash(ref SpatialHash<T> spatialHash)
            {
                _spatialHash = spatialHash;
            }

            #region Variables

            private SpatialHash<T> _spatialHash;

            #endregion
        }

        [BurstCompile]
        public struct UpdateSpatialHashingRemoveFastJob : IJobForEach<T>
        {
            #region Implementation of IJobProcessComponentData<T>

            /// <inheritdoc />
            public void Execute([ReadOnly] ref T item)
            {
                _spatialHash.Remove(item.SpatialHashingIndex);
            }

            #endregion

            public void SetSpatialHash(ref SpatialHash<T> spatialHash)
            {
                _spatialHash = spatialHash;
            }

            #region Variables

            private SpatialHash<T> _spatialHash;

            #endregion
        }

        [BurstCompile]
        public struct UpdateSpatialHashingAddFastJob : IJobForEach<T>
        {
            #region Implementation of IJobProcessComponentData<T>

            /// <inheritdoc />
            public void Execute([ReadOnly] ref T item)
            {
                SpatialHash.AddFast(ref item);
            }

            #endregion

            #region Variables

            public SpatialHash<T>.Concurrent SpatialHash;

            #endregion
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
}