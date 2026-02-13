using System;
using System.Runtime.InteropServices;

class FastNoiseNodeEditorIpc : IDisposable
{
    public enum MessageType
    {
        BufferTooSmall = -1,
        None = 0,
        SelectedNode = 1,
        ImportRequest = 2,
    }

    public struct PollResult
    {
        public MessageType type;
        public string encodedNodeTree;
    }

    private IntPtr mIpcHandle = IntPtr.Zero;
    private bool mDisposed = false;

    private static bool sNativeAvailable;

    static FastNoiseNodeEditorIpc()
    {
        try
        {
            // Try to call a function to verify the native library is available
            fnEditorIpcSetNodeEditorPath(null);
            sNativeAvailable = true;
        }
        catch (DllNotFoundException)
        {
            sNativeAvailable = false;
        }
    }

    public static bool IsAvailable()
    {
        return sNativeAvailable;
    }

    public FastNoiseNodeEditorIpc(bool readPrevious = false)
    {
        if (!sNativeAvailable)
        {
            throw new DllNotFoundException("NodeEditorIpc native library is not available");
        }

        mIpcHandle = fnEditorIpcSetup(readPrevious);

        if (mIpcHandle == IntPtr.Zero)
        {
            throw new ExternalException("Failed to setup NodeEditorIpc shared memory");
        }
    }

    ~FastNoiseNodeEditorIpc()
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
        if (!mDisposed)
        {
            if (mIpcHandle != IntPtr.Zero)
            {
                fnEditorIpcRelease(mIpcHandle);
                mIpcHandle = IntPtr.Zero;
            }
            mDisposed = true;
        }
    }

    public bool SendSelectedNode(string encodedNodeTree)
    {
        return fnEditorIpcSendSelectedNode(mIpcHandle, encodedNodeTree);
    }

    public bool SendImportRequest(string encodedNodeTree)
    {
        return fnEditorIpcSendImportRequest(mIpcHandle, encodedNodeTree);
    }

    public PollResult PollMessage(int bufferSize = 4096)
    {
        byte[] buffer = new byte[bufferSize];
        int msgType = fnEditorIpcPollMessage(mIpcHandle, buffer, bufferSize);

        PollResult result = new PollResult();
        result.type = (MessageType)msgType;

        if (msgType > 0)
        {
            // Find null terminator
            int len = Array.IndexOf<byte>(buffer, 0);
            if (len < 0) len = bufferSize;
            result.encodedNodeTree = System.Text.Encoding.ASCII.GetString(buffer, 0, len);
        }

        return result;
    }

    public static void SetNodeEditorPath(string path)
    {
        if (!sNativeAvailable)
        {
            throw new DllNotFoundException("NodeEditorIpc native library is not available");
        }

        fnEditorIpcSetNodeEditorPath(path);
    }

    public static bool StartNodeEditor(string encodedNodeTree = null, bool detached = false, bool childProcess = false)
    {
        if (!sNativeAvailable)
        {
            throw new DllNotFoundException("NodeEditorIpc native library is not available");
        }

        return fnEditorIpcStartNodeEditor(encodedNodeTree, detached, childProcess);
    }

    private const string NATIVE_LIB = "NodeEditorIpc";

    [DllImport(NATIVE_LIB)]
    private static extern IntPtr fnEditorIpcSetup([MarshalAs(UnmanagedType.I1)] bool readPrevious);

    [DllImport(NATIVE_LIB)]
    private static extern void fnEditorIpcRelease(IntPtr ipc);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnEditorIpcSendSelectedNode(IntPtr ipc, [MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnEditorIpcSendImportRequest(IntPtr ipc, [MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree);

    [DllImport(NATIVE_LIB)]
    private static extern int fnEditorIpcPollMessage(IntPtr ipc, byte[] outBuffer, int bufferSize);

    [DllImport(NATIVE_LIB)]
    private static extern void fnEditorIpcSetNodeEditorPath([MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport(NATIVE_LIB)]
    private static extern bool fnEditorIpcStartNodeEditor([MarshalAs(UnmanagedType.LPStr)] string encodedNodeTree,
                                                          [MarshalAs(UnmanagedType.I1)] bool detached,
                                                          [MarshalAs(UnmanagedType.I1)] bool childProcess);
}
