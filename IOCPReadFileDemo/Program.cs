#define MANUAL_BIND_IOCP
using Microsoft.Win32.SafeHandles;
using Node.Utilities.Native;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IOCPReadFileDemo
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public unsafe static extern bool ReadFile(SafeFileHandle hFile, byte* lpBuffer,
           uint nNumberOfBytesToRead, IntPtr lpNumberOfBytesRead, NativeOverlapped* lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern void SetLastError(uint error);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string lpFileName,

           uint dwDesiredAccess,

           uint dwShareMode,

           IntPtr lpSecurityAttributes,

           uint dwCreationDisposition,

           uint dwFlagsAndAttributes,

           SafeFileHandle hTemplateFile);
#if MANUAL_BIND_IOCP
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern SafeFileHandle CreateIoCompletionPort(
                  IntPtr fileHandle, IntPtr existingCompletionPort, UIntPtr completionKey, UInt32 numberOfConcurrentThreads);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern Boolean GetQueuedCompletionStatus(
                IntPtr completionPort, out UInt32 lpNumberOfBytes, out IntPtr lpCompletionKey, out IntPtr lpOverlapped, UInt32 dwMilliseconds);
        private static UInt32 INFINITE_TIMEOUT = unchecked((UInt32)Timeout.Infinite);
        private static IntPtr INVALID_FILE_HANDLE = unchecked((IntPtr)(-1));
        private static IntPtr INVALID_IOCP_HANDLE = IntPtr.Zero;
#endif
        static unsafe void Main(string[] args)
        {
            var safeFileHandle = CreateFile(@"C:\Windows\win.ini", Win32Api.GENERIC_READ, Win32Api.FILE_SHARE_READ, (IntPtr)null, Win32Api.OPEN_EXISTING, Win32Api.FILE_FLAG_OVERLAPPED, new SafeFileHandle(IntPtr.Zero, false));
#if MANUAL_BIND_IOCP
            SafeFileHandle iocpHandle = CreateIoCompletionPort(INVALID_FILE_HANDLE, INVALID_IOCP_HANDLE, UIntPtr.Zero, 0);
            CreateIoCompletionPort(safeFileHandle.DangerousGetHandle(), iocpHandle.DangerousGetHandle(), UIntPtr.Zero, 0);
            int threadCount = Environment.ProcessorCount * 2;
            for (int i = 0; i < threadCount; i++)
            {
                new Thread(() =>
                {
                    try
                    {
                        UInt32 lpNumberOfBytes;
                        IntPtr lpCompletionKey, lpOverlapped;
                        while (GetQueuedCompletionStatus(iocpHandle.DangerousGetHandle(), out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, INFINITE_TIMEOUT))
                        {
                            NativeOverlapped* native = (NativeOverlapped*)lpOverlapped;
                            //推入到IO线程的任务队列
                            ThreadPool.UnsafeQueueNativeOverlapped(native);
                        }
                    }
                    finally
                    {

                    }
                })
                { IsBackground = true }.Start();
            }
#else
            ThreadPool.BindHandle(safeFileHandle);
#endif
            byte[] buffer = new byte[16384];
            AsyncResult ar = new AsyncResult(buffer);
            NativeOverlapped* nativeOverlapped = new Overlapped(0, 0, IntPtr.Zero, ar).Pack(ReadCompletionCallback, buffer);

            fixed (byte* pBuf = buffer)
            {
                ReadFile(safeFileHandle, pBuf, 16384, IntPtr.Zero, nativeOverlapped);
            }
            int workerNums, ioNums;
            ThreadPool.GetAvailableThreads(out workerNums, out ioNums);
            Console.WriteLine("from main thread: available work threads:{0}, io threads:{1}", workerNums, ioNums);
            Console.WriteLine($"Total threads:{Process.GetCurrentProcess().Threads.Count}");
            Console.ReadKey();
            ThreadPool.GetAvailableThreads(out workerNums, out ioNums);
            Console.WriteLine("from main thread: available work threads:{0}, io threads:{1}", workerNums, ioNums);
            Console.WriteLine($"Total threads:{Process.GetCurrentProcess().Threads.Count}");
        }
        private static unsafe void ReadCompletionCallback(uint errCode, uint numBytes, NativeOverlapped* nativeOverlapped)
        {
            unsafe
            {
                try
                {
                    int workerNums, ioNums;
                    ThreadPool.GetAvailableThreads(out workerNums, out ioNums);
                    Console.WriteLine("from callback thread: available work threads:{0}, io threads:{1}", workerNums, ioNums);

#if MANUAL_BIND_IOCP
                    numBytes = (uint)(nativeOverlapped->InternalHigh);
#endif
                    Overlapped overlapped = Overlapped.Unpack(nativeOverlapped);
                    var ar = (AsyncResult)overlapped.AsyncResult;
                    var buffer = (byte[])ar.AsyncState;
                    if (errCode == (uint)Win32Api.GetLastErrorEnum.ERROR_HANDLE_EOF)
                        Console.WriteLine("End of file in callback.");
                    if (errCode != 0 && numBytes != 0)
                    {
                        Console.WriteLine("Error {0} when reading file.", errCode);
                    }
                    Console.WriteLine("Read {0} bytes.", numBytes);
                    Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, (int)numBytes));
                }
                finally
                {
                    Overlapped.Free(nativeOverlapped);
                }
            }
        }
    }
    public class AsyncResult : IAsyncResult
    {
        private object m_UserStateObject;
        private WaitHandle m_WaitHandle;
        private bool m_CompletedSynchronously;
        private bool m_IsCompleted;
        public AsyncResult(object userState)
        {
            m_UserStateObject = userState;
        }
        public object AsyncState
        {
            get { return m_UserStateObject; }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { return m_WaitHandle; }
            internal set { m_WaitHandle = value; }
        }

        public bool CompletedSynchronously
        {
            get { return m_CompletedSynchronously; }
            internal set { m_CompletedSynchronously = value; }
        }

        public bool IsCompleted
        {
            get { return m_IsCompleted; }
            internal set { m_IsCompleted = value; }
        }
    }
}
