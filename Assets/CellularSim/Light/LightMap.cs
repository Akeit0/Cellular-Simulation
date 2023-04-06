using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
namespace CellularSim.Light {
[StructLayout(LayoutKind.Explicit)]
    public struct ColorUnion {
        [FieldOffset(0)] public uint Value;
        [FieldOffset(0)] public Color24 Color24;
        [FieldOffset(0)] public byte r;
        [FieldOffset(1)] public byte g;
        [FieldOffset(2)] public byte b;
    }
    [Serializable]
    public struct Color24 : IEquatable<Color24> {
        public byte r;
        public byte g;
        public byte b;

        public Color24(Vector3 vector3) {
            r = (byte) (vector3.x * 255);
            g = (byte) (vector3.y * 255);
            b = (byte) (vector3.z * 255);
        }

        public Color24(Color color) {
            r = (byte) (color.r *color.a* 255);
            g = (byte) (color.g *color.a* 255);
            b = (byte) (color.b *color.a* 255);
        }

        public Color24(byte r, byte g, byte b) {
            this.r = r;
            this.g = g;
            this.b = b;
        }
         public Color24(byte rgb) {
            this.r = (byte) ((rgb&0x0700) >>1);
            this.g = (byte) (rgb&0x0070) ;
            this.b = (byte) ((rgb&0x0007) <<1);
        }
      
        

        public void SetMax(Color24 rhs) {
            r = Math.Max(r, rhs.r);
            g = Math.Max(g, rhs.g);
            b = Math.Max(b, rhs.b);
        }
        
        public static bool operator ==(Color24 left, Color24 right) {
            return left.r == right.r && left.g == right.g && left.b == right.b;
        }

        public static bool operator !=(Color24 left, Color24 right) {
            return !(left == right);
        }
 public static Color24 operator *(Color24 left, float right) {
            return new Color24((byte) (left.r * right),(byte) (left.g * right),(byte) (left.b * right));
        }

        public bool Equals(Color24 other) {
            return r == other.r && g == other.g && b == other.b;
        }
        

        public override bool Equals(object obj) {
            return obj is Color24 other && Equals(other);
        }

        public override int GetHashCode() {
            return HashCode.Combine(r, g, b);
        }

        public override string ToString() {
            return $"({r},{g},{b})";
        }
    }

    [BurstCompatible]
    public unsafe class LightMap : IDisposable {
        NativeArray<Color24> _colors;
        public NativeArray<Color24> Colors => _colors;
        public void CopyTo(NativeArray<Color24> array) {
            _colors.CopyTo(array);
        }

        NativeArray<Color24> _medium;
        public NativeArray<Color24> Medium => _medium;

        public int Width { get; set; }

        public int Height { get; set; }
        public bool IsCreated => _colors.IsCreated;

        public unsafe ref Color24 this[int x, int y] => ref ((Color24*) _colors.GetUnsafePtr())[IndexOf(x, y)];

        public LightMap(int width, int height) {
            this.Width = width;
            this.Height = height;
            this._colors = new NativeArray<Color24>(width * height, Allocator.Persistent);
            this._medium = new NativeArray<Color24>(width * height, Allocator.Persistent);
        }

        

        public void Clear() {
            _colors.Clear();
        }

        
        public void Blur() {
            BlurHV(BlurHV(default)).Complete();
           // BlurD(BlurU(BlurR(BlurL(BlurD(BlurU(BlurR(BlurL(default)))))))).Complete();
        }
      
      

        JobHandle BlurL(JobHandle jobHandle) {
            var job = new BlurJob(_colors.GetPtr(Width - 1), _medium.GetPtr(Width - 1), Width, Width, -1);
            return job.Schedule(Height, 0, jobHandle);
        }
        JobHandle BlurR(JobHandle jobHandle) {
            var job = new BlurJob(_colors, _medium, Width, Width, 1);
             return job.Schedule(Height, 1, jobHandle);
        }
       
        
        JobHandle BlurU(JobHandle jobHandle) {
            var job = new BlurJob(_colors, _medium, 1, Height, Width);
           return job.Schedule(Width, 1, jobHandle);
        }
        JobHandle BlurD(JobHandle jobHandle) {
            var down = new BlurJob(_colors.GetPtr(Width * (Height - 1)), _medium.GetPtr(Width * (Height - 1)), 1, Height,
                -Width);
           return down.Schedule(Width, 1, jobHandle);
        }
        JobHandle BlurHV(JobHandle jobHandle) {
            var job = new BlurLineJob(_colors, _medium, Width, Width, 1);
            jobHandle=job.Schedule(Height, 1, jobHandle);
            job = new BlurLineJob(_colors, _medium, 1, Height, Width);
            return job.Schedule(Width,1,jobHandle);
        }


        [BurstCompile(CompileSynchronously = true)]
        struct BlurJob : IJobParallelFor {
            [NativeDisableParallelForRestriction] [NativeDisableUnsafePtrRestriction]
            Color24* colorArray;

            [NativeDisableParallelForRestriction] [NativeDisableUnsafePtrRestriction] [ReadOnly]
            Color24* mediumArray;

            [ReadOnly] int StartIndexFactor;
            [ReadOnly] int PerIterationCount;
            [ReadOnly] int Stride;

            public BlurJob(NativeArray<Color24> colorArray, NativeArray<Color24> mediumArray,
                int startIndexFactor, int perIterationCount, int stride) {
                this.colorArray = colorArray.GetPtr(0);
                this.mediumArray = mediumArray.GetPtr(0);
                StartIndexFactor = startIndexFactor;
                PerIterationCount = perIterationCount;
                Stride = stride;
            }

            public BlurJob(Color24* colorArray, Color24* mediumArray,
                int startIndexFactor, int perIterationCount, int stride) {
                this.colorArray = colorArray;
                this.mediumArray = mediumArray;
                StartIndexFactor = startIndexFactor;
                PerIterationCount = perIterationCount;
                Stride = stride;
            }

            public void Execute(int index) {
                ColorUnion lastColor = default;
                var nextIsBrighter = false;
                Color24* nextColorPtr =colorArray+ index * StartIndexFactor;
                for (int i = 0; i < PerIterationCount-1; ++i) {
                    var colorPtr =nextColorPtr;
                     nextColorPtr = colorPtr + Stride;
                    var currentColor = new ColorUnion {
                        Color24 = *colorPtr
                    };
                    if (currentColor.Value==0&&lastColor.Value==0) {
                        nextIsBrighter = true;
                        continue;
                    }
                    if (nextIsBrighter) {
                        lastColor = currentColor;
                    }
                    else {
                        if (lastColor.r < currentColor.r) {
                            lastColor.r = currentColor.r;
                        }
                        else  {
                            colorPtr->r = lastColor.r;
                        }
                        if (lastColor.g < currentColor.g) {
                            lastColor.g = currentColor.g;
                        }
                        else {
                            colorPtr->g = lastColor.g;
                        }
                        if (lastColor.b < currentColor.b) {
                            lastColor.b = currentColor.b;
                        }
                        else {
                            colorPtr->b = lastColor.b;
                        }
                    }
                    var  nextColor =  new ColorUnion { Color24 =*nextColorPtr};
                    nextIsBrighter =lastColor.Value==nextColor.Value|| (lastColor.r <= nextColor.r &&
                                                                        lastColor.g <= nextColor.g &&
                                                                        lastColor.b <= nextColor.b);
                    if (!nextIsBrighter) {
                        var medium = mediumArray[index * StartIndexFactor + i * Stride];
                        lastColor.r = (byte) ((lastColor.r * medium.r) >> 8);
                        lastColor.g = (byte) ((lastColor.g * medium.g) >> 8);
                        lastColor.b = (byte) ((lastColor.b * medium.b) >> 8);
                    }
                }
                {
                    if (lastColor.r >= nextColorPtr->r) {
                            nextColorPtr->r = lastColor.r;
                    }
                    if (lastColor.g >= nextColorPtr->g) {
                            nextColorPtr->g = lastColor.g;
                    }
                    if (lastColor.b >= nextColorPtr->b) {
                            nextColorPtr->b = lastColor.b;
                    }

                }
                
            }
        } 
        [BurstCompile(CompileSynchronously = true)]
        struct BlurLineJob : IJobParallelFor {
           [NativeDisableParallelForRestriction] [NativeDisableUnsafePtrRestriction]
            Color24* colorArray;

            [NativeDisableParallelForRestriction] [NativeDisableUnsafePtrRestriction] [ReadOnly]
            Color24* mediumArray;

            [ReadOnly] int StartIndexFactor;
            [ReadOnly] int PerIterationCount;
            [ReadOnly] int Stride;

            public BlurLineJob(NativeArray<Color24> colorArray, NativeArray<Color24> mediumArray,
                int startIndexFactor, int perIterationCount, int stride) {
                this.colorArray = colorArray.GetPtr(0);
                this.mediumArray = mediumArray.GetPtr(0);
                StartIndexFactor = startIndexFactor;
                PerIterationCount = perIterationCount;
                Stride = stride;
            }
    public BlurLineJob(Color24 *colorArray, NativeArray<Color24> mediumArray,
                int startIndexFactor, int perIterationCount, int stride) {
                this.colorArray = colorArray;
                this.mediumArray = mediumArray.GetPtr(0);
                StartIndexFactor = startIndexFactor;
                PerIterationCount = perIterationCount;
                Stride = stride;
            }

            
            public void Execute(int index) {
                ColorUnion lastColor = default;
                var nextIsBrighter = false;
                Color24* nextColorPtr =colorArray+ index * StartIndexFactor;
                for (int i = 0; i < PerIterationCount-1; ++i) {
                    var colorPtr =nextColorPtr;
                     nextColorPtr = colorPtr + Stride;
                    var currentColor = new ColorUnion {
                        Color24 = *colorPtr
                    };
                    if (currentColor.Value==0&&lastColor.Value==0) {
                        nextIsBrighter = true;
                        continue;
                    }
                    if (nextIsBrighter) {
                        lastColor = currentColor;
                    }
                    else {
                        if (lastColor.r <= currentColor.r) {
                            lastColor.r = currentColor.r;
                        }
                        else  {
                            colorPtr->r = lastColor.r;
                        }
                        if (lastColor.g <= currentColor.g) {
                            lastColor.g = currentColor.g;
                        }
                        else {
                            colorPtr->g = lastColor.g;
                        }
                        if (lastColor.b <= currentColor.b) {
                            lastColor.b = currentColor.b;
                        }
                        else {
                            colorPtr->b = lastColor.b;
                        }
                    }
                    var  nextColor =  new ColorUnion { Color24 =*nextColorPtr};
                    nextIsBrighter =lastColor.Value==nextColor.Value|| (lastColor.r <= nextColor.r &&
                                                                        lastColor.g <= nextColor.g &&
                                                                        lastColor.b <= nextColor.b);
                    if (!nextIsBrighter) {
                        var medium = mediumArray[index * StartIndexFactor + i * Stride];
                        lastColor.r = (byte) ((lastColor.r * medium.r) >> 8);
                        lastColor.g = (byte) ((lastColor.g * medium.g) >> 8);
                        lastColor.b = (byte) ((lastColor.b * medium.b) >> 8);
                    }
                }
                {
                    if (lastColor.r >= nextColorPtr->r) {
                            nextColorPtr->r = lastColor.r;
                    }
                    if (lastColor.g >= nextColorPtr->g) {
                            nextColorPtr->g = lastColor.g;
                    }
                    if (lastColor.b >= nextColorPtr->b) {
                            nextColorPtr->b = lastColor.b;
                    }

                }
                lastColor = default;
                 nextIsBrighter = false;
                for (int i = PerIterationCount-1;0< i; --i) {
                    var colorPtr =nextColorPtr;
                     nextColorPtr = colorPtr - Stride;
                    var currentColor = new ColorUnion {
                        Color24 = *colorPtr
                    };
                    if (currentColor.Value==0&&lastColor.Value==0) {
                        nextIsBrighter = true;
                        continue;
                    }
                    if (nextIsBrighter) {
                        lastColor = currentColor;
                    }
                    else {
                        if (lastColor.r < currentColor.r) {
                            lastColor.r = currentColor.r;
                        }
                        else  {
                            colorPtr->r = lastColor.r;
                        }
                        if (lastColor.g < currentColor.g) {
                            lastColor.g = currentColor.g;
                        }
                        else {
                            colorPtr->g = lastColor.g;
                        }
                        if (lastColor.b < currentColor.b) {
                            lastColor.b = currentColor.b;
                        }
                        else {
                            colorPtr->b = lastColor.b;
                        }
                    }
                    var  nextColor =  new ColorUnion { Color24 =*nextColorPtr};
                    nextIsBrighter =lastColor.Value==nextColor.Value|| (lastColor.r <= nextColor.r &&
                                                                        lastColor.g <= nextColor.g &&
                                                                        lastColor.b <= nextColor.b);
                    if (!nextIsBrighter) {
                        var medium = mediumArray[index * StartIndexFactor + i * Stride];
                        lastColor.r = (byte) ((lastColor.r * medium.r) >> 8);
                        lastColor.g = (byte) ((lastColor.g * medium.g) >> 8);
                        lastColor.b = (byte) ((lastColor.b * medium.b) >> 8);
                    }
                }
                {
                    if (lastColor.r >= nextColorPtr->r) {
                            nextColorPtr->r = lastColor.r;
                    }
                    if (lastColor.g >= nextColorPtr->g) {
                            nextColorPtr->g = lastColor.g;
                    }
                    if (lastColor.b >= nextColorPtr->b) {
                            nextColorPtr->b = lastColor.b;
                    }

                }
                
            }
        }
        


        int IndexOf(int x, int y) {
            return x +this.Width *y;
        }

        public void Dispose() {
            _colors.DisposeIfCreated();
            _medium.DisposeIfCreated();
        }
    }
}