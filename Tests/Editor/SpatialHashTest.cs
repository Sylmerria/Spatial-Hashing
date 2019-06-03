using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HMH.ECS.SpatialHashing.Test
{
    public class SpatialHashTest
    {
        [Test]
        public void SpatialHashSimpleAdd()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(1, sh.BucketItemCount);
            sh.Dispose();
        }

        [Test]
        public void SpatialHashAdd()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1.1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);
            sh.Dispose();
        }

        [Test]
        public void SpatialHashAddOverWorld()
        {
            var               worldSize = new float3(30);
            SpatialHash<Item> sh        = new SpatialHash<Item>(new Bounds(new float3(15F), worldSize), new float3(1F), 1, Allocator.Temp);

            var item = new Item { Center = new float3(15F), Size = worldSize + new float3(10F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(worldSize.x * worldSize.y * worldSize.z, sh.BucketItemCount);
            sh.Dispose();
        }

        [Test]
        public void SpatialHashSimpleRemove()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(1, sh.BucketItemCount);

            sh.Remove(item.SpatianHashingIndex);

            Assert.AreEqual(0, sh.ItemCount);
            Assert.AreEqual(0, sh.BucketItemCount);

            sh.Dispose();
        }

        [Test]
        public void SpatialHashRemove()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1.1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            sh.Remove(item.SpatianHashingIndex);

            Assert.AreEqual(0, sh.ItemCount);
            Assert.AreEqual(0, sh.BucketItemCount);

            sh.Dispose();
        }

        [Test]
        public void SpatialHashSimpleQuerry()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1.1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            var querryBound = new Bounds(5.5F, 0.1F);
            var results     = new NativeList<Item>(5, Allocator.TempJob);
            sh.Query(querryBound, results);

            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(item, results[0]);

            //check clear result
            results.Dispose();
            sh.Dispose();
        }

        [Test]
        public void SpatialHashOverWorldQuerry()
        {
            SpatialHash<Item> sh = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), new float3(1F), 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1.1F) };
            sh.Add(ref item);

            Assert.AreEqual(1, sh.ItemCount);
            Assert.AreEqual(3 * 3 * 3, sh.BucketItemCount);

            var querryBound = new Bounds(15F, 50F);
            var results     = new NativeList<Item>(5, Allocator.TempJob);
            sh.Query(querryBound, results);

            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(item, results[0]);

            //check clear result
            results.Dispose();
            sh.Dispose();
        }

        [Test]
        public void SpatialHashQuerry()
        {
            var               cellSize = new float3(1F);
            SpatialHash<Item> sh       = new SpatialHash<Item>(new Bounds(new float3(15F), new float3(30F)), cellSize, 15, Allocator.Temp);

            var item = new Item { Center = new float3(5.5F), Size = new float3(1.1F) };
            sh.Add(ref item);

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

                        var querryBound = new Bounds(sh.GetPositionVoxel(hashPosition, true), cellSize * 0.95F);

                        results.Clear();
                        sh.Query(querryBound, results);

                        Assert.AreEqual(1, results.Length);
                        Assert.AreEqual(item, results[0]);
                    }
                }
            }

            //check clear result
            results.Dispose();
            sh.Dispose();
        }
    }

    public struct Item : ISpatialHashingItem<Item>, IComponentData
    {
        public float3 Center;
        public float3 Size;

        #region Implementation of IEquatable<Item>

        /// <inheritdoc />
        public bool Equals(Item other)
        {
            return math.all(Center == other.Center) && math.all(Size == other.Size);
        }

        #region Overrides of ValueType

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Center.GetHashCode();
        }

        #endregion

        #endregion

        #region Implementation of ISpacialHasingItem<Item>

        public float3 GetCenter()
        {
            return Center;
        }

        public float3 GetSize()
        {
            return Size;
        }

        /// <inheritdoc />
        public int SpatianHashingIndex { get; set; }

        #endregion
    }
}