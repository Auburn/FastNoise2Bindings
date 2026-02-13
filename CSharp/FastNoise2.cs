using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

class FastNoise : IDisposable
{
    public struct OutputMinMax
    {
        public OutputMinMax(float minValue = float.PositiveInfinity, float maxValue = float.NegativeInfinity)
        {
            min = minValue;
            max = maxValue;
        }

        public OutputMinMax(ReadOnlySpan<float> nativeOutputMinMax)
        {
            min = nativeOutputMinMax[0];
            max = nativeOutputMinMax[1];
        }

        public void Merge(OutputMinMax other)
        {
            min = Math.Min(min, other.min);
            max = Math.Max(max, other.max);
        }

        public float min;
        public float max;
    }

    public FastNoise(string metadataName)
    {
        if (!metadataNameLookup.TryGetValue(FormatLookup(metadataName), out mMetadataId))
        {
            throw new ArgumentException("Failed to find metadata name: " + metadataName);
        }

        mNodeHandle = fnNewFromMetadata(mMetadataId);
    }

    private FastNoise(IntPtr nodeHandle)
    {
        mNodeHandle = nodeHandle;
        mMetadataId = fnGetMetadataID(nodeHandle);
    }

    ~FastNoise()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (mNodeHandle != IntPtr.Zero)
        {
            fnDeleteNodeRef(mNodeHandle);
            mNodeHandle = IntPtr.Zero;
        }
    }

    public static FastNoise FromEncodedNodeTree(string encodedNodeTree)
    {
        IntPtr nodeHandle = fnNewFromEncodedNodeTree(encodedNodeTree);

        if (nodeHandle == IntPtr.Zero)
        {
            return null;
        }

        return new FastNoise(nodeHandle);
    }

    public uint GetActiveFeatureSet()
    {
        return fnGetActiveFeatureSet(mNodeHandle);
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

    public void Set(string memberName, FastNoise nodeLookup)
    {
        Metadata.Member member;
        if (!nodeMetadata[mMetadataId].members.TryGetValue(FormatLookup(memberName), out member))
        {
            throw new ArgumentException("Failed to find member name: " + memberName);
        }

        IntPtr nodeLookupHandle = nodeLookup != null ? nodeLookup.mNodeHandle : IntPtr.Zero;

        switch (member.type)
        {
            case Metadata.Member.Type.NodeLookup:
                if (!fnSetNodeLookup(mNodeHandle, member.index, nodeLookupHandle))
                {
                    throw new ExternalException("Failed to set node lookup");
                }
                break;

            case Metadata.Member.Type.Hybrid:
                if (!fnSetHybridNodeLookup(mNodeHandle, member.index, nodeLookupHandle))
                {
                    throw new ExternalException("Failed to set node lookup");
                }
                break;

            default:
                throw new ArgumentException(memberName + " cannot be set to a node lookup");
        }
    }

    public OutputMinMax GenUniformGrid2D(Span<float> noiseOut,
                            float xOffset, float yOffset,
                            int xCount, int yCount,
                            float xStepSize, float yStepSize, int seed)
    {
        if (noiseOut.Length < xCount * yCount)
            throw new ArgumentException($"Output buffer too small. Required: {xCount * yCount}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenUniformGrid2DSpan(mNodeHandle, 
            ref MemoryMarshal.GetReference(noiseOut),
            xOffset, yOffset, xCount, yCount, 
            xStepSize, yStepSize, seed, 
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenUniformGrid3D(Span<float> noiseOut,
                            float xOffset, float yOffset, float zOffset,
                            int xCount, int yCount, int zCount,
                            float xStepSize, float yStepSize, float zStepSize, int seed)
    {
        if (noiseOut.Length < xCount * yCount * zCount)
            throw new ArgumentException($"Output buffer too small. Required: {xCount * yCount * zCount}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenUniformGrid3DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xOffset, yOffset, zOffset, 
            xCount, yCount, zCount,
            xStepSize, yStepSize, zStepSize, seed,
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenUniformGrid4D(Span<float> noiseOut,
                            float xOffset, float yOffset, float zOffset, float wOffset,
                            int xCount, int yCount, int zCount, int wCount,
                            float xStepSize, float yStepSize, float zStepSize, float wStepSize, int seed)
    {
        if (noiseOut.Length < xCount * yCount * zCount * wCount)
            throw new ArgumentException($"Output buffer too small. Required: {xCount * yCount * zCount * wCount}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenUniformGrid4DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xOffset, yOffset, zOffset, wOffset,
            xCount, yCount, zCount, wCount,
            xStepSize, yStepSize, zStepSize, wStepSize, seed,
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenTileable2D(Span<float> noiseOut,
                            int xSize, int ySize,
                            float xStepSize, float yStepSize, int seed)
    {
        if (noiseOut.Length < xSize * ySize)
            throw new ArgumentException($"Output buffer too small. Required: {xSize * ySize}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenTileable2DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xSize, ySize, xStepSize, yStepSize, seed,
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenPositionArray2D(Span<float> noiseOut,
                            ReadOnlySpan<float> xPosArray, ReadOnlySpan<float> yPosArray,
                            float xOffset, float yOffset,
                            int seed)
    {
        if (xPosArray.Length != yPosArray.Length)
            throw new ArgumentException("Position arrays must have the same length");
        
        if (noiseOut.Length < xPosArray.Length)
            throw new ArgumentException($"Output buffer too small. Required: {xPosArray.Length}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenPositionArray2DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xPosArray.Length,
            in MemoryMarshal.GetReference(xPosArray),
            in MemoryMarshal.GetReference(yPosArray),
            xOffset, yOffset, seed,
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenPositionArray3D(Span<float> noiseOut,
                            ReadOnlySpan<float> xPosArray, ReadOnlySpan<float> yPosArray, ReadOnlySpan<float> zPosArray,
                            float xOffset, float yOffset, float zOffset,
                            int seed)
    {
        if (xPosArray.Length != yPosArray.Length || xPosArray.Length != zPosArray.Length)
            throw new ArgumentException("Position arrays must have the same length");
        
        if (noiseOut.Length < xPosArray.Length)
            throw new ArgumentException($"Output buffer too small. Required: {xPosArray.Length}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenPositionArray3DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xPosArray.Length,
            in MemoryMarshal.GetReference(xPosArray),
            in MemoryMarshal.GetReference(yPosArray),
            in MemoryMarshal.GetReference(zPosArray),
            xOffset, yOffset, zOffset, seed,
            ref MemoryMarshal.GetReference(minMax));
        return new OutputMinMax(minMax);
    }

    public OutputMinMax GenPositionArray4D(Span<float> noiseOut,
                            ReadOnlySpan<float> xPosArray, ReadOnlySpan<float> yPosArray, ReadOnlySpan<float> zPosArray, ReadOnlySpan<float> wPosArray,
                            float xOffset, float yOffset, float zOffset, float wOffset,
                            int seed)
    {
        if (xPosArray.Length != yPosArray.Length || xPosArray.Length != zPosArray.Length || xPosArray.Length != wPosArray.Length)
            throw new ArgumentException("Position arrays must have the same length");
        
        if (noiseOut.Length < xPosArray.Length)
            throw new ArgumentException($"Output buffer too small. Required: {xPosArray.Length}, Provided: {noiseOut.Length}");

        Span<float> minMax = stackalloc float[2];
        fnGenPositionArray4DSpan(mNodeHandle,
            ref MemoryMarshal.GetReference(noiseOut),
            xPosArray.Length,
            in MemoryMarshal.GetReference(xPosArray),
            in MemoryMarshal.GetReference(yPosArray),
            in MemoryMarshal.GetReference(zPosArray),
            in MemoryMarshal.GetReference(wPosArray),
            xOffset, yOffset, zOffset, wOffset, seed,
            ref MemoryMarshal.GetReference(minMax));
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
            public string description;
            public Dictionary<string, int> enumNames;

            // Variable-specific
            public float defaultFloat;
            public int defaultIntEnum;
            public float minFloat;
            public float maxFloat;
        }

        public int id;
        public string name;
        public string description;
        public string[] groups;
        public Dictionary<string, Member> members;
    }

    static FastNoise()
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
            metadata.description = Marshal.PtrToStringAnsi(fnGetMetadataDescription(id)) ?? "";
            metadataNameLookup.Add(metadata.name, id);

            int groupCount = fnGetMetadataGroupCount(id);
            metadata.groups = new string[groupCount];
            for (int groupIdx = 0; groupIdx < groupCount; groupIdx++)
            {
                metadata.groups[groupIdx] = Marshal.PtrToStringAnsi(fnGetMetadataGroupName(id, groupIdx)) ?? "";
            }

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
                member.description = Marshal.PtrToStringAnsi(fnGetMetadataVariableDescription(id, variableIdx)) ?? "";
                member.defaultFloat = fnGetMetadataVariableDefaultFloat(id, variableIdx);
                member.defaultIntEnum = fnGetMetadataVariableDefaultIntEnum(id, variableIdx);
                member.minFloat = fnGetMetadataVariableMinFloat(id, variableIdx);
                member.maxFloat = fnGetMetadataVariableMaxFloat(id, variableIdx);

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
                member.description = Marshal.PtrToStringAnsi(fnGetMetadataNodeLookupDescription(id, nodeLookupIdx)) ?? "";

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
                member.description = Marshal.PtrToStringAnsi(fnGetMetadataHybridDescription(id, hybridIdx)) ?? "";
                member.defaultFloat = fnGetMetadataHybridDefault(id, hybridIdx);

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
    private static extern IntPtr fnNewFromMetadata(int id, uint simdLevel = ~0u);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnNewFromEncodedNodeTree([MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree, uint simdLevel = ~0u);

    [DllImport(NATIVE_LIB)]
    private static extern void fnDeleteNodeRef(IntPtr nodeHandle);

    [DllImport(NATIVE_LIB)]
    private static extern uint fnGetActiveFeatureSet(IntPtr nodeHandle);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataID(IntPtr nodeHandle);

    // Span<T> P/Invoke declarations using ref for zero-copy interop
    [DllImport(NATIVE_LIB)]
    private static extern void fnGenUniformGrid2D(IntPtr nodeHandle, ref float noiseOut,
                                   float xOffset, float yOffset,
                                   int xCount, int yCount,
                                   float xStepSize, float yStepSize,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenUniformGrid3D(IntPtr nodeHandle, ref float noiseOut,
                                   float xOffset, float yOffset, float zOffset,
                                   int xCount, int yCount, int zCount,
                                   float xStepSize, float yStepSize, float zStepSize,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenUniformGrid4D(IntPtr nodeHandle, ref float noiseOut,
                                   float xOffset, float yOffset, float zOffset, float wOffset,
                                   int xCount, int yCount, int zCount, int wCount,
                                   float xStepSize, float yStepSize, float zStepSize, float wStepSize,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenTileable2D(IntPtr node, ref float noiseOut,
                                   int xSize, int ySize,
                                   float xStepSize, float yStepSize,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenPositionArray2D(IntPtr node, ref float noiseOut, int count,
                                   in float xPosArray, in float yPosArray,
                                   float xOffset, float yOffset,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenPositionArray3D(IntPtr node, ref float noiseOut, int count,
                                   in float xPosArray, in float yPosArray, in float zPosArray,
                                   float xOffset, float yOffset, float zOffset,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern void fnGenPositionArray4D(IntPtr node, ref float noiseOut, int count,
                                   in float xPosArray, in float yPosArray, in float zPosArray, in float wPosArray,
                                   float xOffset, float yOffset, float zOffset, float wOffset,
                                   int seed, ref float outputMinMax);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGenSingle2D(IntPtr node, float x, float y, int seed);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGenSingle3D(IntPtr node, float x, float y, float z, int seed);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGenSingle4D(IntPtr node, float x, float y, float z, float w, int seed);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataCount();

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataName(int id);

    // Variable
    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataVariableCount(int id);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataVariableName(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataVariableType(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataVariableDimensionIdx(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataEnumCount(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataEnumName(int id, int variableIndex, int enumIndex);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnSetVariableFloat(IntPtr nodeHandle, int variableIndex, float value);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnSetVariableIntEnum(IntPtr nodeHandle, int variableIndex, int value);

    // Node Lookup
    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataNodeLookupCount(int id);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataNodeLookupName(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataNodeLookupDimensionIdx(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnSetNodeLookup(IntPtr nodeHandle, int nodeLookupIndex, IntPtr nodeLookupHandle);

    // Hybrid
    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataHybridCount(int id);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataHybridName(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataHybridDimensionIdx(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnSetHybridNodeLookup(IntPtr nodeHandle, int nodeLookupIndex, IntPtr nodeLookupHandle);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnSetHybridFloat(IntPtr nodeHandle, int nodeLookupIndex, float value);

    // Rich metadata
    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataDescription(int id);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataGroupCount(int id);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataGroupName(int id, int groupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataVariableDescription(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGetMetadataVariableDefaultFloat(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern int fnGetMetadataVariableDefaultIntEnum(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGetMetadataVariableMinFloat(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGetMetadataVariableMaxFloat(int id, int variableIndex);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataNodeLookupDescription(int id, int nodeLookupIndex);

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnGetMetadataHybridDescription(int id, int hybridIndex);

    [DllImport(NATIVE_LIB)]
    private static extern float fnGetMetadataHybridDefault(int id, int hybridIndex);
}

