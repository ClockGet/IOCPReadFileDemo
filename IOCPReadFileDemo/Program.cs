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
        static unsafe void Main(string[] args)
        {
            var safeFileHandle = CreateFile(@"C:\Windows\win.ini", Win32Api.GENERIC_READ, Win32Api.FILE_SHARE_READ, (IntPtr)null, Win32Api.OPEN_EXISTING, Win32Api.FILE_FLAG_OVERLAPPED, new SafeFileHandle(IntPtr.Zero, false));
            ThreadPool.BindHandle(safeFileHandle);
            byte[] buffer = new byte[1024];
            AsyncResult ar = new AsyncResult(buffer);
            NativeOverlapped* nativeOverlapped = new Overlapped(0, 0, IntPtr.Zero, ar).Pack(ReadCompletionCallback, buffer);

            fixed (byte* pBuf = buffer)
            {
                ReadFile(safeFileHandle, pBuf, 1024, IntPtr.Zero, nativeOverlapped);
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
