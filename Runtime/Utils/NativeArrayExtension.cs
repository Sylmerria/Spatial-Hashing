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
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetElementAsRefReadOnly<T>(this ref NativeArray<T> array, int index) where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafeReadOnlyPtr(), index);
        }
    }
}