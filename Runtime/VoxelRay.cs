using System;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace HMH.ECS.SpatialHashing
{
    /// <summary>
    /// Ray for ray casting inside a voxel world. Each voxel is considered as a cube within this ray. A ray consists of a starting position, a direction and a length.
    /// Adaptation from https://www.gamedev.net/blogs/entry/2265248-voxel-traversal-algorithm-ray-casting/
    /// </summary>
    public struct VoxelRay<T> where T : IRay
    {
        /**
         * Casts the ray from its starting position towards its direction whilst keeping in mind its length. A lambda parameter is supplied and called each time a voxel is traversed.
         * This allows the lambda to stop anytime the algorithm to continue its loop.
         *
         * This method is local because the parameter voxelIndex is locally changed to avoid creating a new instance of {@link Vector3i}.
         *
         * @param voxelHalfExtent   The half extent (radius) of a voxel.
         * @param onTraversingVoxel The operation to execute when traversing a voxel. This method called the same number of times as the value of {@link #getVoxelDistance()}. The
         *                          supplied {@link Vector3i} parameter is not a new instance but a local instance, so it is a reference. The return value {@link Boolean} defines if
         *                          the algorithm should stop.
         * @param voxelIndex        The voxel index to locally modify in order to traverse voxels. This parameter exists simply to avoid creating a new {@link Vector3i} instance.
         *
         * @see <a href="http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.42.3443&rep=rep1&type=pdf">Axel Traversal Algorithm</a>
         */
        public bool RayCast(ref T asker, float3 start, float3 direction, float length)
        {
            if (math.any(math.isnan(direction)))
                return true;

            Assert.IsTrue(Math.Abs(math.length(direction) - 1F) < 0.00001F);

            var currentVoxel = asker.GetIndexVoxel(start);

            var voxelDistance = ComputeVoxelDistance(ref asker, start + direction * length, currentVoxel);

            // In which direction the voxel ids are incremented.
            var directionStep = GetSignZeroPositive(direction);

            // Distance along the ray to the next voxel border from the current position (max.x, max.y, max.z).
            var nextVoxelBoundary = asker.GetPositionVoxel(currentVoxel + GetNegativeSign(directionStep) + 1, false);

            // distance until next intersection with voxel-border
            // the value of t at which the ray crosses the first vertical voxel boundary
            var max = new float3
            {
                x = math.abs(direction.x) > 0.00001F ? (nextVoxelBoundary.x - start.x) / direction.x : float.MaxValue,
                y = math.abs(direction.y) > 0.00001F ? (nextVoxelBoundary.y - start.y) / direction.y : float.MaxValue,
                z = math.abs(direction.z) > 0.00001F ? (nextVoxelBoundary.z - start.z) / direction.z : float.MaxValue
            };

            // how far along the ray we must move for the horizontal component to equal the width of a voxel
            // the direction in which we traverse the grid
            // can only be FLT_MAX if we never go in that direction
            var delta = new float3
            {
                x = math.abs(direction.x) > 0.00001F ? directionStep.x * asker.CellSize.x / direction.x : float.MaxValue,
                y = math.abs(direction.y) > 0.00001F ? directionStep.y * asker.CellSize.y / direction.y : float.MaxValue,
                z = math.abs(direction.z) > 0.00001F ? directionStep.z * asker.CellSize.z / direction.z : float.MaxValue
            };

            if (asker.OnTraversingVoxel(currentVoxel))
                return true;

            int traversedVoxelCount = 0;

            while (++traversedVoxelCount < voxelDistance)
            {
                if (max.x < max.y && max.x < max.z)
                {
                    currentVoxel.x += directionStep.x;
                    max.x          += delta.x;
                }
                else if (max.y < max.z)
                {
                    currentVoxel.y += directionStep.y;
                    max.y          += delta.y;
                }
                else
                {
                    currentVoxel.z += directionStep.z;
                    max.z          += delta.z;
                }

                if (asker.OnTraversingVoxel(currentVoxel))
                    return true;
            }

            return false;
        }

        /**
         * Computes the voxel distance, a.k.a. the number of voxel to traverse, for the ray cast.
         *
         * @param voxelExtent The extent of a voxel, which is the equivalent for a cube of a sphere's radius.
         * @param startIndex The starting position's index.
         */
        private int ComputeVoxelDistance(ref T asker, float3 end, int3 startIndex)
        {
            return 1 + math.abs(asker.GetIndexVoxel(end) - startIndex).Sum();
        }

        /// <summary>
        /// Gets the sign of the supplied number. The method being "zero position" means that the sign of zero is 1.
        /// </summary>
        public static int3 GetSignZeroPositive(float3 number)
        {
            return GetNegativeSign(number) | 1;
        }

        /// <summary>
        /// Gets the negative sign of the supplied number. So, in other words, if the number is negative, -1 is returned but if the number is positive or zero, then zero is returned.
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int3 GetNegativeSign(float3 number)
        {
            return new int3(math.asint(number.x) >> (32 - 1),
                            math.asint(number.y) >> (32 - 1),
                            math.asint(number.z) >> (32 - 1)); //float are always 32bit in c# and -1 for sign bit which is at position 31
        }

        /// <summary>
        /// Gets the negative sign of the supplied number. So, in other words, if the number is negative, -1 is returned but if the number is positive or zero, then zero is returned.
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static int3 GetNegativeSign(int3 number)
        {
            return new int3(math.asint(number.x) >> (32 - 1),
                            math.asint(number.y) >> (32 - 1),
                            math.asint(number.z) >> (32 - 1)); //int are always 32bit in c# and -1 for sign bit which is at position 31
        }

    }

    public interface IRay
    {
        /// <summary>
        /// The operation to execute when traversing a voxel.The return value defines if the algorithm should stop.
        /// </summary>
        /// <param name="voxelIndex"></param>
        /// <returns></returns>
        bool OnTraversingVoxel(int3 voxelIndex);

        int3 GetIndexVoxel(float3    position);
        float3 GetPositionVoxel(int3 index, bool center);
        float3 CellSize { get; }
    }
}