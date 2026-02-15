# Unity FastNoise2 Native Bindings
for the [FastNoise2](https://github.com/Auburn/FastNoise2) noise generation library, using NativeArray, Jobs and Burst.  


[Complete Example usage](https://github.com/Neovariance/FastNoise2Bindings/blob/master/UnityProject/Assets/FastNoise2/NativeFastNoise2Test.cs)

```c#
[BurstCompile]
    struct FastNoiseJob : IJob
    {
        [NativeDisableUnsafePtrRestriction] public IntPtr NodePtr;
        public int2 Size;
        public float Frequency;
        public int Seed;

        [WriteOnly] public NativeArray<float> NoiseOut;
        [WriteOnly] public NativeReference<OutputMinMax> MinMaxOut;

        public unsafe void Execute()
        {
            MinMaxOut.Value = NativeFastNoise2.GenUniformGrid2D(NodePtr, NoiseOut, 0, 0, Size.x, Size.y, Frequency, Seed);
        }
    }
```

```c#
        NativeFastNoise2 fractal = new NativeFastNoise2("FractalFBm");
        fractal.Set("Source", new NativeFastNoise2("Simplex"));
        fractal.Set("Gain", 0.3f);
        fractal.Set("Lacunarity", 0.6f);

        IntPtr nodePtr = fractal.NodeHandlePtr; //with this pointer we can now call the static NativeFastNoise2 API

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
```


![Usage0](https://github.com/Neovariance/FastNoise2Bindings/blob/master/UnityProject/usage.jpg)
![Usage1](https://github.com/Neovariance/FastNoise2Bindings/blob/master/UnityProject/usage1.jpg)




