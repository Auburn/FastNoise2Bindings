using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using static NativeFastNoise2;
using static Unity.Mathematics.math;

public class NativeFastNoise2Test : MonoBehaviour
{
    /* Test Class with example Burst Jobs to integrate the FastNoise2 native library with unity NativeArrays and Jobs
     @Author Simon Lebettre, Neovariance Games (owner), MIT License
     contribution to https://github.com/Auburn/FastNoise2Bindings by Jordan Peck
    */


    [Multiline(5)]
    public string Notices = "Use the context menus (right click) to generate test textures,\n\n" +
       "you can also Copy/Paste an encoded Tree \ninto the [EncodedTree] field and check the UseEncodedTree bool ";

    public Texture2D texture2DDirect;
    public Texture2D texture2DJob;
    public Texture2D texture2DParallelJob;

    public int2 TexSize = 256;
    public int BatchSize = 8;

    public bool UseEncodedTree = false;

    public string EncodedTree = "DQAFAAAAAAAAQAgAAAAAAD8AAAAAAA==";



    [BurstCompile]
    struct FastNoiseJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public IntPtr NodePtr;
        public int2 Size;
        public float Frequency;
        public int Seed;

        [WriteOnly] public NativeArray<float> NoiseOut; //Writeonly concerning unity's job system, inside fastnoise doesn't matter.
        [WriteOnly] public NativeReference<OutputMinMax> MinMaxOut;

        public unsafe void Execute()
        {
            MinMaxOut.Value = GenUniformGrid2D(NodePtr, NoiseOut, 0, 0, Size.x, Size.y, Frequency, Seed);
        }
    }

    [BurstCompile]
    struct FastNoiseJobParallelForBatch : IJobParallelForBatch
    {
        [NativeDisableUnsafePtrRestriction] public IntPtr NodePtr;
        public int2 Size;
        public float Frequency;
        public int Seed;
        public int BatchSize;

        [WriteOnly] public NativeArray<float> NoiseOut;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<OutputMinMax> MinMaxesOut;
        //one minmax per batch, so each thread safely writes at its own index (startIndex / BatchSize), ouside of standard //job range     

        public void Execute(int startIndex, int idxCount)
        {
            int xStart = startIndex % Size.x;
            int yStart = startIndex / Size.x;

            int endIndex = startIndex + idxCount - 1;

            int xEnd = endIndex % Size.x;
            int yEnd = endIndex / Size.x;

            int xCount = xEnd - xStart + 1;
            int yCount = yEnd - yStart + 1;

            int batchIndex = startIndex / BatchSize;

            Assert.AreEqual(idxCount, xCount * yCount, $"{idxCount} shoud be equal to {xCount}*{yCount} ");

            //need to pass the actual subarray to fastnoise, not the whole array!
            //which actually guarantees thread safety, fastnoise has no access to the global array. 
            var subArray = NoiseOut.GetSubArray(startIndex, idxCount);

            MinMaxesOut[batchIndex] = GenUniformGrid2D(NodePtr, subArray, xStart, yStart, xCount, yCount, Frequency, Seed);
        }

        //Note : parallel execution of the highly optimised SIMD native code in fastnoise may not be the best design.
        // it might be faster to use the IJob running on the whole dataset
        // => requires profiling in real usage conditions
    }


    private NativeFastNoise2 SetupFastNoise()
    {
        //Setup is in managed code (Update, OnUpdate) and then the actual work can be done in burst jobs

        if (UseEncodedTree)
        {
            NativeFastNoise2 nodeTree = FromEncodedNodeTree(EncodedTree);
            Assert.IsNotNull(nodeTree, "invalid encoded tree");
            return nodeTree;
        }

        NativeFastNoise2 fractal = new NativeFastNoise2("FractalFBm");
        fractal.Set("Source", new NativeFastNoise2("Simplex"));
        fractal.Set("Gain", 0.3f);
        fractal.Set("Lacunarity", 0.6f);
        return fractal;
    }

    private void Start()
    {
        TestAll();
        Debug.Log($"Textures are generated in {nameof(NativeFastNoise2Test)}", this);
    }

    [ContextMenu("Test All")]
    public void TestAll()
    {
        TestDirect();
        TestJob();
        TestJobParallel();
    }

    [ContextMenu("Test Direct Call")]
    public void TestDirect()
    {
        texture2DDirect = null;
        NativeFastNoise2 fastNoise = SetupFastNoise();

        var noiseOut = new NativeArray<float>(TexSize.x * TexSize.y, Allocator.Temp);

        var minMax = GenUniformGrid2D(fastNoise.NodeHandlePtr, noiseOut, 0, 0, TexSize.x, TexSize.y, frequency: 0.02f, seed: 1234);

        Debug.Log($"Direct Call generated texture2DDirect, MinMax [{minMax.min} {minMax.max}]");

        SetPixels(noiseOut, minMax, ref texture2DDirect);
        noiseOut.Dispose();
    }

    [ContextMenu("Test threaded IJob")]
    public void TestJob()
    {
        texture2DJob = null;

        NativeFastNoise2 fastNoise = SetupFastNoise();

        IntPtr nodePtr = fastNoise.NodeHandlePtr; //with this pointer we can now call static fastnoise API

        var noiseOut = new NativeArray<float>(TexSize.x * TexSize.y, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var minMaxOut = new NativeReference<OutputMinMax>(Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var job = new FastNoiseJob
        {
            Size = TexSize,
            Frequency = 0.02f,
            Seed = 1234,
            NodePtr = nodePtr,
            MinMaxOut = minMaxOut,
            NoiseOut = noiseOut
        };

        var handle = job.Schedule();//schedule on a job thread

        handle.Complete(); // instantly complete, nulling the usefulness of the thread, this is just a test ;)   

        var minMax = minMaxOut.Value;
        Debug.Log($"Job generated texture2DJob, MinMax [{minMax.min} {minMax.max}]");

        SetPixels(noiseOut, minMax, ref texture2DJob);

        noiseOut.Dispose();
        minMaxOut.Dispose();
    }

    [ContextMenu("Test multithread IJobParallelForBatch")]
    public void TestJobParallel()
    {
        texture2DParallelJob = null;

        NativeFastNoise2 fastNoise = SetupFastNoise();

        IntPtr nodePtr = fastNoise.NodeHandlePtr;

        var noiseOut = new NativeArray<float>(TexSize.x * TexSize.y, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        int batchCount = (int)ceil(TexSize.x * TexSize.y / (float)BatchSize);

        var minMaxesOut = new NativeArray<OutputMinMax>(batchCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

        var job = new FastNoiseJobParallelForBatch
        {
            Size = TexSize,
            Frequency = 0.02f,
            Seed = 1234,
            BatchSize = BatchSize,
            NodePtr = nodePtr,
            MinMaxesOut = minMaxesOut,
            NoiseOut = noiseOut
        };

        var handle = job.ScheduleBatch(noiseOut.Length, BatchSize);
        handle.Complete(); // instantly complete all threads 

        OutputMinMax minMax = new OutputMinMax();

        foreach (var mm in minMaxesOut)
        {
            minMax.Merge(mm);
        }
        Debug.Log($"Parallel Job generated texture2DParallelJob, MinMax [{minMax.min} {minMax.max}]");

        SetPixels(noiseOut, minMax, ref texture2DParallelJob);

        noiseOut.Dispose();
        minMaxesOut.Dispose();
    }

    private void SetPixels(NativeArray<float> noiseOut, OutputMinMax minMax, ref Texture2D tex)
    {
        float scale = 255.0f / (minMax.max - minMax.min);

        //can be made faster (and garbage free) with setpixels nativearray ( TextureFormat.R8 would be super efficient)
        // + can move the scaling into the job, avoiding to pass the minmaxOut around
        Color32[] pixels = new Color32[TexSize.x * TexSize.y];
        Color32 color = Color.white;
        for (int i = 0; i < TexSize.x * TexSize.y; ++i)
        {
            float noise = noiseOut[i];
            noise = round((noise - minMax.min) * scale);
            noise = clamp(noise, 0, 255);
            color.r = color.g = color.b = (byte)noise;
            pixels[i] = color;
        }

        tex = new Texture2D(TexSize.x, TexSize.y);
        tex.SetPixels32(pixels);
        tex.Apply();
    }

    /*Note: maybe some pretty cool advanced stuff could be done with fastnoise2 and 
    Unity.Burst.Intrinsics.v64
    Unity.Burst.Intrinsics.v128
    Unity.Burst.Intrinsics.v256
    */
}
