using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Jobs;
using Unity.Mathematics;

namespace HMH.ECS.SpatialHashing.Test
{
    public class SpatialHashingSystemTest : ECSTestsFixture
    {
        [Test]
        public void TestAdd()
        {
            var e    = _entityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            _entityManager.AddComponentData(e, item);

            EntityCommandBufferSystem barrier = World.CreateSystem<BeginInitializationEntityCommandBufferSystem>();
            var                       system  = World.CreateSystem<SystemTest>();
            system.Barrier = barrier;

            system.Update();
            _entityManager.CompleteAllJobs();
            barrier.Update();
            _entityManager.CompleteAllJobs();

            Assert.IsTrue(_entityManager.HasComponent(e, ComponentType.ReadOnly(typeof(ItemMirror))));
            Assert.AreEqual(1, _entityManager.GetComponentData<ItemMirror>(e).GetItemID);

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

            var e    = _entityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            _entityManager.AddComponentData(e, item);

            EntityCommandBufferSystem barrier = World.CreateSystem<BeginInitializationEntityCommandBufferSystem>();
            var                       system  = World.CreateSystem<SystemTest>();
            system.Barrier = barrier;

            system.Update();
            _entityManager.CompleteAllJobs();
            barrier.Update();
            _entityManager.CompleteAllJobs();

            Assert.IsTrue(_entityManager.HasComponent(e, ComponentType.ReadOnly(typeof(ItemMirror))));
            Assert.AreEqual(1, _entityManager.GetComponentData<ItemMirror>(e).GetItemID);

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

            _entityManager.RemoveComponent<Item>(e);

            system.Update();
            _entityManager.CompleteAllJobs();
            barrier.Update();
            _entityManager.CompleteAllJobs();

            Assert.IsFalse(_entityManager.HasComponent<ItemMirror>(e));

            sh = system.SpatialHash;
            Assert.AreEqual(0, sh.ItemCount);
            Assert.AreEqual(0, sh.BucketItemCount);
        }

        [Test]
        public void TestMove()
        {
            #region Add

            var e    = _entityManager.CreateEntity();
            var item = new Item { Center = new float3(50.5F), Size = new float3(1.1F) };
            _entityManager.AddComponentData(e, item);

            EntityCommandBufferSystem barrier = World.CreateSystem<BeginInitializationEntityCommandBufferSystem>();
            var                       system  = World.CreateSystem<SystemTest>();
            system.Barrier = barrier;

            system.Update();
            _entityManager.CompleteAllJobs();
            barrier.Update();
            _entityManager.CompleteAllJobs();

            Assert.IsTrue(_entityManager.HasComponent(e, ComponentType.ReadOnly(typeof(ItemMirror))));
            Assert.AreEqual(1, _entityManager.GetComponentData<ItemMirror>(e).GetItemID);

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

            _entityManager.AddComponentData(e, new EmptyData());
            item        = _entityManager.GetComponentData<Item>(e);
            item.Center = new float3(51.5F);
            _entityManager.SetComponentData(e, item);

            system.Update();
            _entityManager.CompleteAllJobs();
            barrier.Update();
            _entityManager.CompleteAllJobs();
            system.Barrier.Update();

            Assert.IsTrue(_entityManager.HasComponent(e, typeof(ItemMirror)));
            Assert.IsFalse(_entityManager.HasComponent(e, typeof(EmptyData)));
            Assert.AreEqual(1, _entityManager.GetComponentData<ItemMirror>(e).GetItemID);

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

        private class SystemTest : SpatialHashingSystem<Item, ItemMirror, EmptyData>
        {
            #region Overrides of SpatialHashingSystem<Item,ItemMirror>

            /// <inheritdoc />
            protected override void InitSpatialHashing()
            {
                _spatialHash = new SpatialHash<Item>(new Bounds(new float3(50), new float3(20)), new float3(1), Allocator.Persistent);
            }

            /// <inheritdoc />
            protected override void AddJobHandleForProducer(JobHandle inputDeps)
            { }

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

        public struct ItemMirror : ISpatialHashingItemMiror
        {
            #region Implementation of ISpatialHashingItemMiror

            /// <inheritdoc />
            public int GetItemID { get; set; }

            #endregion
        }

        public struct EmptyData : IComponentData
        { }
    }
}