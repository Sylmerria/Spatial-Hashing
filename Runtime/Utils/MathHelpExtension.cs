using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace HMH.ECS
{
    public static class MathHelpExtension
    {
        /// <summary>Returns the result of rounding each component of a float2 vector value up to the nearest value greater or equal to the original value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 CeilToInt(this float2 x)
        {
            return new int2(math.ceil(x));
        }

        /// <summary>Returns the result of rounding each component of a float3 vector value up to the nearest value greater or equal to the original value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 CeilToInt(this float3 x)
        {
            return new int3(math.ceil(x));
        }

        /// <summary>Returns the result of rounding each component of a float2 vector value up to the nearest value lower or equal to the original value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 FloorToInt(this float2 x)
        {
            return new int2(math.floor(x));
        }

        /// <summary>Returns the result of rounding each component of a float3 vector value up to the nearest value lower or equal to the original value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 FloorToInt(this float3 x)
        {
            return new int3(math.floor(x));
        }

        /// <summary>Returns the result of adding each component of a int2 vector value </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(this int2 x)
        {
            return math.csum(x);
        }

        /// <summary>Returns the result of adding each component of a int3 vector value </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sum(this int3 x)
        {
            return math.csum(x);
        }

        /// <summary>Returns the result of multiplying each component with others</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Mul(this float2 x)
        {
            return x.x * x.y;
        }

        /// <summary>Returns the result of multiplying each component with others</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Mul(this float3 x)
        {
            return x.x * x.y * x.z;
        }

        /// <summary>Returns the result of multiplying each component with others</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mul(this int2 x)
        {
            return x.x * x.y;
        }

        /// <summary>Returns the result of multiplying each component with others</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Mul(this int3 x)
        {
            return x.x * x.y * x.z;
        }
    }
}