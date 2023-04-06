
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using UnityEngine;
using UnityObject=UnityEngine.Object;
namespace CellularSim {

    public readonly ref struct ReversedSpan<T> {
        public readonly Span<T> Span;
        public ReversedSpan(Span<T> span) {
            Span = span;
        }
        public ref T this[int index] => ref Span[Span.Length-index-1];
        public Enumerator GetEnumerator() => new Enumerator(Span);
        
        public ref struct Enumerator {
            private readonly Span<T> _span;
            private int index;

            public Enumerator(Span<T> span) {
                _span = span;
                index = span.Length;
               
            }

            public bool MoveNext() {
                return 0 <= --index;
            }
            public ref T Current =>ref  _span[index];
        }
        
    }
    public static class SpanUtil {
        public static float2 Encode(float v) {
            var enc =math.float2(1f,65025f) *v;
            enc = math.frac(enc);
            enc -= enc * math.float2(1,1/65025f);
            return enc;
        }
        
        public static float Decode(float2 v) {
            return math.dot(v, math.float2(1f, 1 / 65025f));
        }
        
        public static float f2_f(float2 c)
        {
            
            return math.round(c.x*65025f)+c.y;
        }
        public static float2 f_f2(float f) {
            var u = asuint(f);
            var upper = u >> 8;
            var lower = u & 0xffff;
            return float2(asfloat(upper), asfloat(lower));
        }
        
        public static float ushort_f_5e11s(ushort i) {
            if (i == 0) return 0;
            var exp = (i >> 11)+111;
            exp <<=23;
            var frac = (i&0x7ff)<<12;
            return asfloat(exp|frac);
        }
        public static ushort f_ushort_5e11s(float f) {
            var i = asint(f)>>12;
            var exp = (i >> 11);
            if (exp <111) return 0;
            if (142 < exp) return   (0xffff);
            exp = (((exp - 111) & 0x1f) << 11);
            var frac =  (i )& 0x7ff;
            return (ushort)(exp | frac);
        }
        public static float ushort_f_6e10s(ushort i) {
            if (i == 0) return 0;
            var exp = (i >> 10)+96;
            exp <<=23;
            var frac = (i&0x3ff)<<13;
            return asfloat(exp|frac);
        }
        public static ushort f_ushort_6e10s(float f) {
            var i = asint(f)>>13;
            var exp = (i >> 10);
            if (158 < exp) return   (0xffff);
            if (exp <96) return 0;
            exp = ((exp - 96) & 0x3f) << 10;
            var frac =  (i)& 0x3ff;
            return (ushort)(exp | frac);
        }
        public static byte get_exp(float f) {
            return (byte) ((asint(f) >> 23));
        }
        public static string get_significand(float f) {
            return  Convert.ToString(((asuint(f)>>12) & 0x1fff), 2);
        }
        
        
        private sealed class DummyList<T> {
            public T[] _items;
            public int _size;
            public int _version;
        }
        public static ReversedSpan<T> AsReversed<T>(this Span<T> span) {
            return new ReversedSpan<T>(span);
        }
        public static Span<T> AsSpan<T>(this List<T> list) {
          
            ref var dummyList =ref  UnsafeUtility.As<List<T>,DummyList<T>>(ref list);
            return dummyList._items.AsSpan(0, list.Count);
        }
        public static T[] GetArray<T>(this List<T> list) {
          
            ref var dummyList =ref  UnsafeUtility.As<List<T>,DummyList<T>>(ref list);
            return dummyList._items;
        }
        public static ref T[] GetArrayRef<T>(this List<T> list) {
          
            ref var dummyList =ref  UnsafeUtility.As<List<T>,DummyList<T>>(ref list);
            return ref dummyList._items;
        }
        public static ref int GetSizeRef<T>(this List<T> list) {
          
            ref var dummyList =ref  UnsafeUtility.As<List<T>,DummyList<T>>(ref list);
            return ref dummyList._size;
        }
        public static unsafe Span<T> AsSpan<T>(this NativeArray<T> array) where T:unmanaged {
            var ptr = array.GetUnsafePtr();
            return new Span<T>(ptr, array.Length);
        }

        public static unsafe  void SwapLast<T>(ref this NativeList<T> list, int index) where T : unmanaged {
            var unsafeList = list.GetUnsafeList();
            int copyFrom = math.max(unsafeList->m_length - 1, index + 1);
            if (index == copyFrom) return;
            var sizeOf = sizeof(T);
            void* dst = (byte*)unsafeList->Ptr + index * sizeOf;
            var value = *(T*) dst;
            void* src = (byte*)unsafeList->Ptr + copyFrom * sizeOf;
            UnsafeUtility.MemCpy(dst, src, sizeOf);
            *(T*) src = value;
        }

        public static void CheckIndexInRange(int index, int length) {
            if (index < 0)
                throw new IndexOutOfRangeException($"Index {index} must be positive.");

            if (index >= length)
                throw new IndexOutOfRangeException($"Index {index} is out of range in container of '{length}' Length.");
        }
        public static unsafe  void Swap<T>(ref this NativeList<T> list, int index1,int index2) where T : unmanaged {
            if (index1 == index2) return;
            var unsafeList = list.GetUnsafeList();
            CheckIndexInRange(index1, unsafeList->m_length);
            CheckIndexInRange(index2, unsafeList->m_length);
            var sizeOf = sizeof(T);
            void* dst = (byte*)unsafeList->Ptr + index1 * sizeOf;
            var value = *(T*) dst;
            void* src = (byte*)unsafeList->Ptr + index2 * sizeOf;
            UnsafeUtility.MemCpy(dst, src, sizeOf);
            *(T*) src = value;
        }
        public static unsafe  void Copy<T>(ref this NativeList<T> list, int dstIndex,int srcIndex) where T : unmanaged {
            if (dstIndex == srcIndex) return;
            var unsafeList = list.GetUnsafeList();
            CheckIndexInRange(dstIndex, unsafeList->m_length);
            CheckIndexInRange(srcIndex, unsafeList->m_length);
            var sizeOf = sizeof(T);
            void* dst = (byte*)unsafeList->Ptr + dstIndex * sizeOf;
            void* src = (byte*)unsafeList->Ptr + srcIndex * sizeOf;
            UnsafeUtility.MemCpy(dst, src, sizeOf);
        }

        public static void ResizePowerTwo<T>(ref T[] array, int minimumCapacity) {
            var current =Math.Max(array.Length,1) ;
            while (current<minimumCapacity) {
                current *= 2;
            }
            if (array.Length != current) {
                Array.Resize(ref array,current);
            }
        }

        public static void Mul(ref this byte left, float right) {
            left = (byte) (left * right);
        }
         public static void Mul(ref this byte left, byte right) {
            left = (byte) (left * right/255);
        }
         
        
        public static void Sub(ref this byte left, byte right) {
            left = (byte)Math.Max(0,left - right);
        }
        public static unsafe ref T ElementAt<T>(in this NativeArray<T> array,int index)where T :unmanaged {

            var ptr=(T*)array.GetUnsafePtr();
            return ref ptr[index];
        }
        public static unsafe  T* GetPtr<T>(in this NativeArray<T> array,int index)where T :unmanaged {
            var ptr=(T*)array.GetUnsafePtr();
            return  ptr+index;
        } public static unsafe  T* GetPtrWithoutChecks<T>(in this NativeArray<T> array,int index)where T :unmanaged {
            var ptr=(T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            return  ptr+index;
        }
        public static unsafe  T* GetReadOnlyPtr<T>(in this NativeArray<T> array,int index)where T :unmanaged {
            var ptr=(T*)array.GetUnsafeReadOnlyPtr();
            return  ptr+index;
        }
        public static void DisposeIfCreated<T>(ref this NativeArray<T> nativeArray)where T :struct {
            if (nativeArray.IsCreated) nativeArray.Dispose();
        }
        public static void DisposeIfCreated<T>(ref this NativeList<T> nativeList)where T :unmanaged {
            if (nativeList.IsCreated) nativeList.Dispose();
        }
         public static unsafe void SetAll<T>(this NativeArray<T> nativeArray,T value)where T :unmanaged {
             if (nativeArray.IsCreated) {
                 UnsafeUtility.MemCpyReplicate(nativeArray.GetUnsafePtr(),&value,sizeof(T),nativeArray.Length);
             }
        } 
         
         public static unsafe void Clear<T>(this NativeArray<T> nativeArray)where T :unmanaged {
             if (nativeArray.IsCreated) {
                 UnsafeUtility.MemClear(nativeArray.GetUnsafePtr(),sizeof(T)*nativeArray.Length);
             }
        }
         public static unsafe  NativeArray<T> PtrToNativeArray<T>(T* ptr, int length)where T:unmanaged
         {
             var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(ptr, length, Allocator.Invalid);

             // これをやらないとNativeArrayのインデクサアクセス時に死ぬ
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, AtomicSafetyHandle.Create());
#endif

             return arr;
         } 
         public static unsafe  NativeArray<T> AsArray<T>(this GraphicsBuffer buffer)where T:unmanaged
         {
             var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((T*)buffer.GetNativeBufferPtr(), buffer.count, Allocator.Invalid);

             // これをやらないとNativeArrayのインデクサアクセス時に死ぬ
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, AtomicSafetyHandle.Create());
#endif
             return arr;
         }public static unsafe  NativeArray<T> AsArray<T>(this GraphicsBuffer buffer,int length)where T:unmanaged
         {
             var arr = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>((T*)buffer.GetNativeBufferPtr(), length, Allocator.Invalid);

             // これをやらないとNativeArrayのインデクサアクセス時に死ぬ
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref arr, AtomicSafetyHandle.Create());
#endif
             return arr;
         }
         public static byte ClampToByte(this int value) => (byte)Math.Clamp(value, 0, 255);
         public static sbyte ClampToSbyte(this int value) => (sbyte)Math.Clamp(value, sbyte.MinValue, sbyte.MaxValue);
         public static short ClampToShort(this int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        
      
        public static T TryGetObjectFromLastNotNull<T>(this List<T> list) where T: UnityObject {
            var tail = list.Count - 1;
            while (0<=tail) {
                var t = list[tail--];
                if (t == null) {
                    list.RemoveAt(tail+1);
                }
                else return t;
            }
            return null;
        }
    }

    
}