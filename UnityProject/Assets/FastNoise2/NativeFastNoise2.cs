using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

/*
Based on https://github.com/Auburn/FastNoise2Bindings/blob/master/CSharp/FastNoise2.cs by Jordan Peck
( ./FastNoise2.cs is an exact copy of it)

passing directly NativeArray<float> memory to the dll, instead of managed arrays float[]
using NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut) for nativeArrays,
and adress of float2 &minMax for return values
setup / metadata is managed, actual usage is all static unsafe, jobs and burst compatible

see NativeFastNoise2Test for example usage with jobs

@Author Simon Lebettre, Neovariance Games (owner), MIT License
*/

partial class FastNoise
{
    public static unsafe OutputMinMax GenUniformGrid2D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                           int xStart, int yStart,
                           int xSize, int ySize,
                           float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid2D(nodeHandle, noiseOutPtr, xStart, yStart, xSize, ySize, frequency, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenUniformGrid3D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xStart, int yStart, int zStart,
                                   int xSize, int ySize, int zSize,
                                   float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid3D(nodeHandle, noiseOutPtr, xStart, yStart, zStart, xSize, ySize, zSize, frequency, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenUniformGrid4D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xStart, int yStart, int zStart, int wStart,
                                   int xSize, int ySize, int zSize, int wSize,
                                   float frequency, int seed)
    {
        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid4D(nodeHandle, noiseOutPtr, xStart, yStart, zStart, wStart, xSize, ySize, zSize, wSize, frequency, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenTileable2D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xSize, int ySize,
                                   float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenTileable2D(nodeHandle, noiseOutPtr, xSize, ySize, frequency, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenPositionArray2D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                         NativeArray<float> xPosArray, NativeArray<float> yPosArray,
                                         float xOffset, float yOffset,
                                         int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);
        void* xPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(xPosArray);
        void* yPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(yPosArray);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenPositionArray2D(nodeHandle, noiseOutPtr, xPosArray.Length, xPosArrayPtr, yPosArrayPtr, xOffset, yOffset, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenPositionArray3D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                         NativeArray<float> xPosArray, NativeArray<float> yPosArray, NativeArray<float> zPosArray,
                                         float xOffset, float yOffset, float zOffset,
                                         int seed)
    {
        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);
        void* xPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(xPosArray);
        void* yPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(yPosArray);
        void* zPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(zPosArray);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);

        fnGenPositionArray3D(nodeHandle, noiseOutPtr, xPosArray.Length, xPosArrayPtr, yPosArrayPtr, zPosArrayPtr, xOffset, yOffset, zOffset, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    public static unsafe OutputMinMax GenPositionArray4D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                         NativeArray<float> xPosArray, NativeArray<float> yPosArray, NativeArray<float> zPosArray, NativeArray<float> wPosArray,
                                         float xOffset, float yOffset, float zOffset, float wOffset,
                                         int seed)
    {
        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);
        void* xPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(xPosArray);
        void* yPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(yPosArray);
        void* zPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(zPosArray);
        void* wPosArrayPtr = NativeArrayUnsafeUtility.GetUnsafePtr(wPosArray);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);

        fnGenPositionArray4D(nodeHandle, noiseOutPtr, xPosArray.Length, xPosArrayPtr, yPosArrayPtr, zPosArrayPtr, wPosArrayPtr, xOffset, yOffset, zOffset, wOffset, seed, &minMax);
        return new OutputMinMax(minMax.x, minMax.y);
    }

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnGenUniformGrid2D(IntPtr nodeHandle, void* noiseOut,
                                   int xStart, int yStart,
                                   int xSize, int ySize,
                                   float frequency, int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern uint fnGenUniformGrid3D(IntPtr nodeHandle, void* noiseOut,
                               int xStart, int yStart, int zStart,
                               int xSize, int ySize, int zSize,
                               float frequency, int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern uint fnGenUniformGrid4D(IntPtr nodeHandle, void* noiseOut,
                                   int xStart, int yStart, int zStart, int wStart,
                                   int xSize, int ySize, int zSize, int wSize,
                                   float frequency, int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnGenTileable2D(IntPtr node, void* noiseOut,
                                    int xSize, int ySize,
                                    float frequency, int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnGenPositionArray2D(IntPtr node, void* noiseOut, int count,
                                         void* xPosArray, void* yPosArray,
                                         float xOffset, float yOffset,
                                         int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnGenPositionArray3D(IntPtr node, void* noiseOut, int count,
                                         void* xPosArray, void* yPosArray, void* zPosArray,
                                         float xOffset, float yOffset, float zOffset,
                                         int seed, void* outputMinMax);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnGenPositionArray4D(IntPtr node, void* noiseOut, int count,
                                         void* xPosArray, void* yPosArray, void* zPosArray, void* wPosArray,
                                         float xOffset, float yOffset, float zOffset, float wOffset,
                                         int seed, void* outputMinMax);

    public IntPtr NodeHandlePtr => mNodeHandle;

}

