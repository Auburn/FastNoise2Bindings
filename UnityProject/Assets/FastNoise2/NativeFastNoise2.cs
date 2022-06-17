using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

/* Based on https://github.com/Auburn/FastNoise2Bindings/blob/master/CSharp/FastNoise2.cs by Jordan Peck

  passing directly NativeArray<float> memory to the dll, instead of managed arrays float[]
  using NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut) for nativeArrays,
  and adress of float2 &minMax for return values
  setup / metadata is managed, actual usage is all static unsafe, jobs and burst compatible

  see NativeFastNoise2Test for example usage with jobs

@Author Simon Lebettre, Neovariance Games (owner), MIT License
*/

public class NativeFastNoise2
{
    public struct OutputMinMax
    {
        public OutputMinMax(float minValue = float.PositiveInfinity, float maxValue = float.NegativeInfinity)
        {
            min = minValue;
            max = maxValue;
        }

        public OutputMinMax(float2 f)
        {
            min = f.x;
            max = f.y;
        }

        public void Merge(OutputMinMax other)
        {
            min = Math.Min(min, other.min);
            max = Math.Max(max, other.max);
        }

        public float min;
        public float max;
    }

    public NativeFastNoise2(string metadataName)
    {
        if (!metadataNameLookup.TryGetValue(FormatLookup(metadataName), out mMetadataId))
        {
            throw new ArgumentException("Failed to find metadata name: " + metadataName);
        }

        mNodeHandle = fnNewFromMetadata(mMetadataId);
    }

    private NativeFastNoise2(IntPtr nodeHandle)
    {
        mNodeHandle = nodeHandle;
        mMetadataId = fnGetMetadataID(nodeHandle);
    }

    ~NativeFastNoise2()
    {
        fnDeleteNodeRef(mNodeHandle);
    }

    public static NativeFastNoise2 FromEncodedNodeTree(string encodedNodeTree)
    {
        IntPtr nodeHandle = fnNewFromEncodedNodeTree(encodedNodeTree);

        if (nodeHandle == IntPtr.Zero)
        {
            return null;
        }

        return new NativeFastNoise2(nodeHandle);
    }

    public uint GetSIMDLevel()
    {
        return fnGetSIMDLevel(mNodeHandle);
    }

    public void Set(string memberName, float value)
    {
        Metadata.Member member;
        if (!nodeMetadata[mMetadataId].members.TryGetValue(FormatLookup(memberName), out member))
        {
            throw new ArgumentException("Failed to find member name: " + memberName);
        }

        switch (member.type)
        {
            case Metadata.Member.Type.Float:
                if (!fnSetVariableFloat(mNodeHandle, member.index, value))
                {
                    throw new ExternalException("Failed to set float value");
                }
                break;

            case Metadata.Member.Type.Hybrid:
                if (!fnSetHybridFloat(mNodeHandle, member.index, value))
                {
                    throw new ExternalException("Failed to set float value");
                }
                break;

            default:
                throw new ArgumentException(memberName + " cannot be set to a float value");
        }
    }

    public void Set(string memberName, int value)
    {
        Metadata.Member member;
        if (!nodeMetadata[mMetadataId].members.TryGetValue(FormatLookup(memberName), out member))
        {
            throw new ArgumentException("Failed to find member name: " + memberName);
        }

        if (member.type != Metadata.Member.Type.Int)
        {
            throw new ArgumentException(memberName + " cannot be set to an int value");
        }

        if (!fnSetVariableIntEnum(mNodeHandle, member.index, value))
        {
            throw new ExternalException("Failed to set int value");
        }
    }

    public void Set(string memberName, string enumValue)
    {
        Metadata.Member member;
        if (!nodeMetadata[mMetadataId].members.TryGetValue(FormatLookup(memberName), out member))
        {
            throw new ArgumentException("Failed to find member name: " + memberName);
        }

        if (member.type != Metadata.Member.Type.Enum)
        {
            throw new ArgumentException(memberName + " cannot be set to an enum value");
        }

        int enumIdx;
        if (!member.enumNames.TryGetValue(FormatLookup(enumValue), out enumIdx))
        {
            throw new ArgumentException("Failed to find enum value: " + enumValue);
        }

        if (!fnSetVariableIntEnum(mNodeHandle, member.index, enumIdx))
        {
            throw new ExternalException("Failed to set enum value");
        }
    }

    public void Set(string memberName, NativeFastNoise2 nodeLookup)
    {
        Metadata.Member member;
        if (!nodeMetadata[mMetadataId].members.TryGetValue(FormatLookup(memberName), out member))
        {
            throw new ArgumentException("Failed to find member name: " + memberName);
        }

        switch (member.type)
        {
            case Metadata.Member.Type.NodeLookup:
                if (!fnSetNodeLookup(mNodeHandle, member.index, nodeLookup.mNodeHandle))
                {
                    throw new ExternalException("Failed to set node lookup");
                }
                break;

            case Metadata.Member.Type.Hybrid:
                if (!fnSetHybridNodeLookup(mNodeHandle, member.index, nodeLookup.mNodeHandle))
                {
                    throw new ExternalException("Failed to set node lookup");
                }
                break;

            default:
                throw new ArgumentException(memberName + " cannot be set to a node lookup");
        }
    }

    public static unsafe OutputMinMax GenUniformGrid2D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                               int xStart, int yStart,
                               int xSize, int ySize,
                               float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid2D(nodeHandle, noiseOutPtr, xStart, yStart, xSize, ySize, frequency, seed, &minMax);
        return new OutputMinMax(minMax);
    }

    public static unsafe OutputMinMax GenUniformGrid3D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xStart, int yStart, int zStart,
                                   int xSize, int ySize, int zSize,
                                   float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid3D(nodeHandle, noiseOutPtr, xStart, yStart, zStart, xSize, ySize, zSize, frequency, seed, &minMax);
        return new OutputMinMax(minMax);
    }

    public static unsafe OutputMinMax GenUniformGrid4D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xStart, int yStart, int zStart, int wStart,
                                   int xSize, int ySize, int zSize, int wSize,
                                   float frequency, int seed)
    {
        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenUniformGrid4D(nodeHandle, noiseOutPtr, xStart, yStart, zStart, wStart, xSize, ySize, zSize, wSize, frequency, seed, &minMax);
        return new OutputMinMax(minMax);
    }

    public static unsafe OutputMinMax GenTileable2D(IntPtr nodeHandle, NativeArray<float> noiseOut,
                                   int xSize, int ySize,
                                   float frequency, int seed)
    {

        void* noiseOutPtr = NativeArrayUnsafeUtility.GetUnsafePtr(noiseOut);

        float2 minMax = new float2(float.PositiveInfinity, float.NegativeInfinity);
        fnGenTileable2D(nodeHandle, noiseOutPtr, xSize, ySize, frequency, seed, &minMax);
        return new OutputMinMax(minMax);
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
        return new OutputMinMax(minMax);
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
        return new OutputMinMax(minMax);
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
        return new OutputMinMax(minMax);
    }

    public float GenSingle2D(float x, float y, int seed)
    {
        return fnGenSingle2D(mNodeHandle, x, y, seed);
    }

    public float GenSingle3D(float x, float y, float z, int seed)
    {
        return fnGenSingle3D(mNodeHandle, x, y, z, seed);
    }

    public float GenSingle4D(float x, float y, float z, float w, int seed)
    {
        return fnGenSingle4D(mNodeHandle, x, y, z, w, seed);
    }

    private IntPtr mNodeHandle = IntPtr.Zero;
    private int mMetadataId = -1;
    public class Metadata
    {
        public struct Member
        {
            public enum Type
            {
                Float,
                Int,
                Enum,
                NodeLookup,
                Hybrid,
            }

            public string name;
            public Type type;
            public int index;
            public Dictionary<string, int> enumNames;
        }

        public int id;
        public string name;
        public Dictionary<string, Member> members;
    }

    static NativeFastNoise2()
    {
        int metadataCount = fnGetMetadataCount();

        nodeMetadata = new Metadata[metadataCount];
        metadataNameLookup = new Dictionary<string, int>(metadataCount);

        // Collect metadata for all FastNoise node classes
        for (int id = 0; id < metadataCount; id++)
        {
            Metadata metadata = new Metadata();

            metadata.id = id;
            metadata.name = FormatLookup(Marshal.PtrToStringAnsi(fnGetMetadataName(id)));
            //Console.WriteLine(id + " - " + metadata.name);
            metadataNameLookup.Add(metadata.name, id);

            int variableCount = fnGetMetadataVariableCount(id);
            int nodeLookupCount = fnGetMetadataNodeLookupCount(id);
            int hybridCount = fnGetMetadataHybridCount(id);
            metadata.members = new Dictionary<string, Metadata.Member>(variableCount + nodeLookupCount + hybridCount);

            // Init variables
            for (int variableIdx = 0; variableIdx < variableCount; variableIdx++)
            {
                Metadata.Member member = new Metadata.Member();

                member.name = FormatLookup(Marshal.PtrToStringAnsi(fnGetMetadataVariableName(id, variableIdx)));
                member.type = (Metadata.Member.Type)fnGetMetadataVariableType(id, variableIdx);
                member.index = variableIdx;

                member.name = FormatDimensionMember(member.name, fnGetMetadataVariableDimensionIdx(id, variableIdx));

                // Get enum names
                if (member.type == Metadata.Member.Type.Enum)
                {
                    int enumCount = fnGetMetadataEnumCount(id, variableIdx);
                    member.enumNames = new Dictionary<string, int>(enumCount);

                    for (int enumIdx = 0; enumIdx < enumCount; enumIdx++)
                    {
                        member.enumNames.Add(FormatLookup(Marshal.PtrToStringAnsi(fnGetMetadataEnumName(id, variableIdx, enumIdx))), enumIdx);
                    }
                }

                metadata.members.Add(member.name, member);
            }

            // Init node lookups
            for (int nodeLookupIdx = 0; nodeLookupIdx < nodeLookupCount; nodeLookupIdx++)
            {
                Metadata.Member member = new Metadata.Member();

                member.name = FormatLookup(Marshal.PtrToStringAnsi(fnGetMetadataNodeLookupName(id, nodeLookupIdx)));
                member.type = Metadata.Member.Type.NodeLookup;
                member.index = nodeLookupIdx;

                member.name = FormatDimensionMember(member.name, fnGetMetadataNodeLookupDimensionIdx(id, nodeLookupIdx));

                metadata.members.Add(member.name, member);

            }

            // Init hybrids
            for (int hybridIdx = 0; hybridIdx < hybridCount; hybridIdx++)
            {
                Metadata.Member member = new Metadata.Member();

                member.name = FormatLookup(Marshal.PtrToStringAnsi(fnGetMetadataHybridName(id, hybridIdx)));
                member.type = Metadata.Member.Type.Hybrid;
                member.index = hybridIdx;

                member.name = FormatDimensionMember(member.name, fnGetMetadataHybridDimensionIdx(id, hybridIdx));

                metadata.members.Add(member.name, member);

            }
            nodeMetadata[id] = metadata;
        }
    }

    // Append dimension char where neccessary 
    private static string FormatDimensionMember(string name, int dimIdx)
    {
        if (dimIdx >= 0)
        {
            char[] dimSuffix = new char[] { 'x', 'y', 'z', 'w' };
            name += dimSuffix[dimIdx];
        }
        return name;
    }

    // Ignores spaces and caps, harder to mistype strings
    private static string FormatLookup(string s)
    {
        return s.Replace(" ", "").ToLower();
    }

    static private Dictionary<string, int> metadataNameLookup;
    static private Metadata[] nodeMetadata;

    private const string NATIVE_LIB = "FastNoise";

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnNewFromMetadata(int id, uint simdLevel = 0);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnNewFromEncodedNodeTree([MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree, uint simdLevel = 0);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern void fnDeleteNodeRef(IntPtr nodeHandle);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern uint fnGetSIMDLevel(IntPtr nodeHandle);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataID(IntPtr nodeHandle);

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

    [DllImport(NATIVE_LIB)]
    unsafe private static extern float fnGenSingle2D(IntPtr node, float x, float y, int seed);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern float fnGenSingle3D(IntPtr node, float x, float y, float z, int seed);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern float fnGenSingle4D(IntPtr node, float x, float y, float z, float w, int seed);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataCount();

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnGetMetadataName(int id);

    // Variable
    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataVariableCount(int id);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnGetMetadataVariableName(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataVariableType(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataVariableDimensionIdx(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataEnumCount(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnGetMetadataEnumName(int id, int variableIndex, int enumIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern bool fnSetVariableFloat(IntPtr nodeHandle, int variableIndex, float value);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern bool fnSetVariableIntEnum(IntPtr nodeHandle, int variableIndex, int value);

    // Node Lookup
    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataNodeLookupCount(int id);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnGetMetadataNodeLookupName(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataNodeLookupDimensionIdx(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern bool fnSetNodeLookup(IntPtr nodeHandle, int nodeLookupIndex, IntPtr nodeLookupHandle);

    // Hybrid
    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataHybridCount(int id);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern IntPtr fnGetMetadataHybridName(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern int fnGetMetadataHybridDimensionIdx(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern bool fnSetHybridNodeLookup(IntPtr nodeHandle, int nodeLookupIndex, IntPtr nodeLookupHandle);

    [DllImport(NATIVE_LIB)]
    unsafe private static extern bool fnSetHybridFloat(IntPtr nodeHandle, int nodeLookupIndex, float value);

    public IntPtr NodeHandlePtr => mNodeHandle;

}

