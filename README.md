# IOCPReadFileDemo
use win32 api with IOCP and Overlapped IO to read file

## Manually bind IOCP steps
1. Call CreateIoCompletionPort function to create a IOCP handle
2. Call the CreateIoCompletionPort function again to associate the IOCP handle with the device handle
3. According to the number of processors, create cpu * 2 worker threads
4. Call GetQueuedCompletionStatus function within the worker threads, get the result after calling, and push the result into the IO thread's queue for callback