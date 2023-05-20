using HMH.ECS.SpatialHashing.Test;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.IncreaseSpatialHashSizeJob))]
[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.AddSpatialHashingJob))]
[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.AddSpatialHashingEndJob))]
[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.UpdateSpatialHashingRemoveFastJob))]
[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.UpdateSpatialHashingAddFastJob))]
[assembly: RegisterGenericJobType(typeof(SpatialHashingSystemTest.SystemTest.RemoveSpatialHashingJob))]

namespace HMH.ECS.SpatialHashing.Test
{
    public class SpatialHashingSystemTest : ECSTestsFixture
    {
        [Test]
        public void TestAdd()
        {
            var e    = EntityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            EntityManager.AddComponentData(e, item);

            var system = World.CreateSystemManaged<SystemTest>();

            system.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();
            EntityManager.CompleteAllTrackedJobs();

            Assert.IsTrue(EntityManager.HasComponent(e, ComponentType.ReadOnly(typeof(SpatialHashingMirror))));
            var getItemID = EntityManager.GetComponentData<SpatialHashingMirror>(e).GetItemID;
            Assert.AreEqual(1, getItemID);

            var sh = system.SpatialHash;
            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            var results = new NativeList<Item>(5, Allocator.TempJob);
            var bounds  = new Bounds(item.GetCenter(), item.GetSize());

            sh.CalculStartEndIteration(bounds, out var start, out var end);

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

                        var querryBound = new Bounds(sh.GetPositionVoxel(hashPosition, true), sh.CellSize * 0.99F);

                        results.Clear();
                        sh.Query(querryBound, results);

                        Assert.AreEqual(1, results.Length);
                        Assert.AreEqual(item, results[0]);
                    }
                }
            }

            results.Dispose();
        }

        [Test]
        public void TestRemove()
        {
            #region Add

            var e    = EntityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            EntityManager.AddComponentData(e, item);

            var system = World.CreateSystemManaged<SystemTest>();

            system.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();
            EntityManager.CompleteAllTrackedJobs();

            Assert.IsTrue(EntityManager.HasComponent(e, ComponentType.ReadOnly(typeof(SpatialHashingMirror))));
            Assert.AreEqual(1, EntityManager.GetComponentData<SpatialHashingMirror>(e).GetItemID);

            var sh = system.SpatialHash;
            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            var results = new NativeList<Item>(5, Allocator.TempJob);
            var bounds  = new Bounds(item.GetCenter(), item.GetSize());

            sh.CalculStartEndIteration(bounds, out var start, out var end);

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

                        var querryBound = new Bounds(sh.GetPositionVoxel(hashPosition, true), sh.CellSize * 0.99F);

                        results.Clear();
                        sh.Query(querryBound, results);

                        Assert.AreEqual(1, results.Length);
                        Assert.AreEqual(item, results[0]);
                    }
                }
            }

            results.Dispose();

            #endregion

            EntityManager.RemoveComponent<Item>(e);

            system.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();
            EntityManager.CompleteAllTrackedJobs();

            Assert.IsFalse(EntityManager.HasComponent<SpatialHashingMirror>(e));

            sh = system.SpatialHash;
            Assert.AreEqual(0, sh.ItemCount);
            Assert.AreEqual(0, sh.BucketItemCount);
        }

        [Test]
        public void TestMove()
        {
            #region Add

            var e    = EntityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            EntityManager.AddComponentData(e, item);

            var system = World.CreateSystemManaged<SystemTest>();

            system.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();
            EntityManager.CompleteAllTrackedJobs();

            Assert.IsTrue(EntityManager.HasComponent(e, ComponentType.ReadOnly(typeof(SpatialHashingMirror))));
            Assert.AreEqual(1, EntityManager.GetComponentData<SpatialHashingMirror>(e).GetItemID);

            var sh = system.SpatialHash;
            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            var results = new NativeList<Item>(5, Allocator.TempJob);
            var bounds  = new Bounds(item.GetCenter(), item.GetSize());

            sh.CalculStartEndIteration(bounds, out var start, out var end);

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

                        var querryBound = new Bounds(sh.GetPositionVoxel(hashPosition, true), sh.CellSize * 0.99F);

                        results.Clear();
                        sh.Query(querryBound, results);

                        Assert.AreEqual(1, results.Length);
                        Assert.AreEqual(item, results[0]);
                    }
                }
            }

            results.Dispose();

            #endregion

            EntityManager.AddComponentData(e, new EmptyData());
            item        = EntityManager.GetComponentData<Item>(e);
            item.Center = new float3(51.5F);
            EntityManager.SetComponentData(e, item);

            system.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();
            EntityManager.CompleteAllTrackedJobs();
            system.Barrier.Update();

            Assert.IsTrue(EntityManager.HasComponent(e, typeof(SpatialHashingMirror)));
            Assert.IsFalse(EntityManager.HasComponent(e, typeof(EmptyData)));
            Assert.AreEqual(1, EntityManager.GetComponentData<SpatialHashingMirror>(e).GetItemID);

            sh = system.SpatialHash;
            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            results = new NativeList<Item>(5, Allocator.TempJob);
            bounds  = new Bounds(item.GetCenter(), item.GetSize());

            sh.CalculStartEndIteration(bounds, out start, out end);

            hashPosition = new int3(0F);

            for (int x = start.x; x < end.x; ++x)
            {
                hashPosition.x = x;

                for (int y = start.y; y < end.y; ++y)
                {
                    hashPosition.y = y;

                    for (int z = start.z; z < end.z; ++z)
                    {
                        hashPosition.z = z;

                        var querryBound = new Bounds(sh.GetPositionVoxel(hashPosition, true), sh.CellSize * 0.99F);

                        results.Clear();
                        sh.Query(querryBound, results);

                        Assert.AreEqual(1, results.Length);
                        Assert.AreEqual(item, results[0]);
                    }
                }
            }

            results.Dispose();
        }

        [DisableAutoCreation]
        public partial class SystemTest : SpatialHashingSystem<Item, SpatialHashingMirror, EmptyData>
        {
            #region Overrides of SpatialHashingSystem<Item,ItemMirror>

            /// <inheritdoc />
            protected override void InitSpatialHashing()
            {
                _spatialHash = new SpatialHash<Item>(new Bounds(new float3(50), new float3(20)), new float3(1), Allocator.Persistent);
            }

            #region Overrides of SpatialHashingSystem<Item,ItemMirror,EmptyData>

            /// <inheritdoc />
            protected override void OnCreate()
            {
                Barrier = World.GetOrCreateSystemManaged<BeginInitializationEntityCommandBufferSystem>();
                base.OnCreate();
            }

            #endregion

            /// <inheritdoc />
            protected override void AddJobHandleForProducer(JobHandle inputDeps)
            {
                Barrier.AddJobHandleForProducer(inputDeps);
            }

            /// <inheritdoc />
            protected override EntityCommandBuffer CommandBuffer => Barrier.CreateCommandBuffer();

            #endregion

            #region Variables

            public EntityCommandBufferSystem Barrier;

            #endregion

            #region Properties

            public SpatialHash<Item> SpatialHash => _spatialHash;

            #endregion
        }

        public struct EmptyData : IComponentData
        { }
    }
}