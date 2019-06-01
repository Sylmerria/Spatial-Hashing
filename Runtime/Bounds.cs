using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace HMH.ECS.SpatialHashing
{
    [Serializable]
    public struct Bounds : IEquatable<Bounds>
    {
        public Bounds(float3 center, float3 size)
        {
            _center  = center;
            _extents = size * 0.5f;
        }

        public void SetMinMax(float3 min, float3 max)
        {
            Extents = (max - min) * 0.5f;
            Center  = min + Extents;
        }

        public void Encapsulate(float3 point)
        {
            SetMinMax(math.min(Min, point), math.max(Max, point));
        }

        public void Encapsulate(Bounds bounds)
        {
            Encapsulate(bounds.Center - bounds.Extents);
            Encapsulate(bounds.Center + bounds.Extents);
        }

        public void Clamp(Bounds bounds)
        {
            var bMin = bounds.Min;
            var bMax = bounds.Max;
            SetMinMax(math.clamp(Min, bMin, bMax), math.clamp(Max, bMin, bMax));
        }

        public void Expand(float amount)
        {
            amount  *= 0.5f;
            Extents += new float3(amount, amount, amount);
        }

        public void Expand(float3 amount)
        {
            Extents += amount * 0.5f;
        }

        public bool Intersects(Bounds bounds)
        {
            return math.all(Min <= bounds.Max) && math.all(Max >= bounds.Min);
        }

        public int3 GetCellCount(float3 cellSize)
        {
            var min = Min;
            var max = Max;

            var diff = max - min;
            diff /= cellSize;

            return diff.CeilToInt();
        }

        public bool RayCastOBB(Ray ray, quaternion worldRotation)
        {
            return RayCastOBB(ray.origin, ray.direction, worldRotation);
        }

        public bool RayCastOBB(float3 origin, float3 directionNormalized, quaternion worldRotation, float length =1<<25)
        {
            var localRoation = math.inverse(worldRotation);
            return RayCastOBBFast(origin, directionNormalized, localRoation,length);
        }

        public bool RayCastOBBFast(float3 origin, float3 directionNormalized, quaternion localRotation,float length=1<<25)
        {
            origin              = math.mul(localRotation, origin-_center)+_center;
            directionNormalized = math.mul(localRotation, directionNormalized);

            return GetEnterPositionAABB(origin, directionNormalized, length);
        }

        public bool RayCastOBB(float3 origin, float3 directionNormalized, quaternion worldRotation, out float3 enterPoint, float length =1<<25)
        {
            var localRotation = math.inverse(worldRotation);
            origin              = math.mul(localRotation, origin-_center)+_center;
            directionNormalized = math.mul(localRotation, directionNormalized);

            bool res = GetEnterPositionAABB(origin, directionNormalized, length, out enterPoint);
            enterPoint = math.mul(worldRotation, enterPoint);

            return res;
        }

        public bool GetEnterPositionAABB(Ray ray, float length, out float3 enterPoint)
        {
            return GetEnterPositionAABB(ray.origin, ray.direction, length, out enterPoint);
        }

        /// <summary>
        /// Find the intersection of a line from v0 to v1 and an axis-aligned bounding box http://www.youtube.com/watch?v=USjbg5QXk3g
        /// <see cref="https://github.com/BSVino/MathForGameDevelopers/blob/line-box-intersection/math/collision.cpp"/>
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="length"></param>
        /// <param name="enterPoint"></param>
        /// <returns></returns>
        // 
        public bool GetEnterPositionAABB(float3 origin, float3 direction, float length)
        {
            var start = origin + direction * length;

            float low  = 0F;
            float high = 1F;

            return ClipLine(0, origin, start, ref low, ref high) && ClipLine(1, origin, start, ref low, ref high) && ClipLine(2, origin, start, ref low, ref high);
        }

        /// <summary>
        /// Find the intersection of a line from v0 to v1 and an axis-aligned bounding box http://www.youtube.com/watch?v=USjbg5QXk3g
        /// <see cref="https://github.com/BSVino/MathForGameDevelopers/blob/line-box-intersection/math/collision.cpp"/>
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="length"></param>
        /// <param name="enterPoint"></param>
        /// <returns></returns>

        // 
        public bool GetEnterPositionAABB(float3 origin, float3 direction, float length, out float3 enterPoint)
        {
            enterPoint = new float3();
            var start = origin + direction * length;

            float low  = 0F;
            float high = 1F;

            if (ClipLine(0, origin, start, ref low, ref high) == false || ClipLine(1, origin, start, ref low, ref high) == false || ClipLine(2, origin, start, ref low, ref high) == false)
                return false;

            // The formula for I: http://youtu.be/USjbg5QXk3g?t=6m24s
            var b = start - origin;
            enterPoint = origin + b * low;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
       private bool ClipLine(int d, float3 v0, float3 v1,ref float low,ref float high)
        {
            // f_low and f_high are the results from all clipping so far. We'll write our results back out to those parameters.

            // f_dim_low and f_dim_high are the results we're calculating for this current dimension.
            // Find the point of intersection in this dimension only as a fraction of the total vector http://youtu.be/USjbg5QXk3g?t=3m12s
            var dimensionLow = (Min[d] - v0[d])/(v1[d] - v0[d]);
            var dimensionHigh = (Max[d] - v0[d])/(v1[d] - v0[d]);

            // Make sure low is less than high
            if (dimensionHigh < dimensionLow)
            {
                var tmp = dimensionHigh;
                dimensionHigh = dimensionLow;
                dimensionLow = tmp;
            }

            // If this dimension's high is less than the low we got then we definitely missed. http://youtu.be/USjbg5QXk3g?t=7m16s
            if (dimensionHigh < low)
                return false;

            // Likewise if the low is less than the high.
            if (dimensionLow > high)
                return false;

            // Add the clip from this dimension to the previous results http://youtu.be/USjbg5QXk3g?t=5m32s
            low  = math.max(dimensionLow, low);
            high = math.min(dimensionHigh, high);

            if (low > high)
                return false;

            return true;
        }

        /// <summary>
        /// Return enter position of ray in this bound.Ray need to start outside of bound!
        /// <see cref="https://www.youtube.com/watch?v=4h-jlOBsndU"/>
        /// </summary>
        /// <param name="ray"></param>
        /// <returns></returns>
        public bool GetExitPosition(Ray ray, float length, out float3 exitPoint)
        {
            exitPoint = new float3();

            var minBounds = Min;
            var maxBounds = Max;

            var rayProjectionLength = ray.direction * length;

            var minProjection = (minBounds - (float3)ray.origin) / rayProjectionLength;
            var maxProjection = (maxBounds - (float3)ray.origin) / rayProjectionLength;
            var temp          = math.min(minProjection, maxProjection);
            maxProjection = math.max(minProjection, maxProjection);
            minProjection = temp;


            if (minProjection.x > maxProjection.y || minProjection.y > maxProjection.x)
                return false;

            var tMin = math.max(minProjection.x, minProjection.y); //Get Greatest Min
            var tMax = math.min(maxProjection.x, maxProjection.y); //Get Smallest Max

            if (tMin > maxProjection.z || minProjection.z > tMax)
                return false;

            tMax = math.min(maxProjection.z, tMax);

            exitPoint = ray.origin + ray.direction * length * tMax;
            return true;
        }

        public override string ToString()
        {
            return $"Center: {_center}, Extents: {_extents}";
        }

        public override int GetHashCode()
        {
            return Center.GetHashCode() ^ (Extents.GetHashCode() << 2);
        }

        public override bool Equals(object other)
        {
            if (!(other is Bounds))
                return false;
            return Equals((Bounds)other);
        }

        public bool Equals(Bounds other)
        {
            return Center.Equals(other.Center) && Extents.Equals(other.Extents);
        }

        public static bool operator ==(Bounds lhs, Bounds rhs)
        {
            return math.all(lhs.Center == rhs.Center) & math.all(lhs.Extents == rhs.Extents);
        }

        public static bool operator !=(Bounds lhs, Bounds rhs)
        {
            return !(lhs == rhs);
        }

        #region Variables

        [SerializeField]
        private float3 _center;
        [SerializeField]
        private float3 _extents;

        #endregion

        #region Properties

        public float3 Center { get { return _center; } set { _center = value; } }

        public float3 Size { get { return _extents * 2f; } set { _extents = value * 0.5f; } }

        public float3 Extents { get { return _extents; } set { _extents = value; } }

        public float3 Min { get { return Center - Extents; } set { SetMinMax(value, Max); } }

        public float3 Max { get { return Center + Extents; } set { SetMinMax(Min, value); } }

        #endregion
    }


}