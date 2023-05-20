using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace HMH.ECS
{
    public static class NativeArrayExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetElementAsRef<T>(this ref NativeArray<T> array, int index) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            // Make sure we have the right to read & write on the array. Depending of the job, min & max are not necessarily 0 & Length
            T value = array[index];
            array[index] = default;
            array[index] = value;
#endif

            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetElementAsRefReadOnly<T>(this ref NativeArray<T> array, int index) where T : struct
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            // Make sure we have the right to read & write on the array. Depending of the job, min & max are not necessarily 0 & Length
            T value = array[index];
            value = default;
#endif

            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafeReadOnlyPtr(), index);
        }
    }
}