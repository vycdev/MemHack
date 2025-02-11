using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemHackLib
{
    internal class MemHackWin : IMemHack
    {
        public Type ValueType { get; set; } = typeof(int);

        #region WINAPI
        // Import Windows API functions

        // Delegate for EnumWindows callback
        public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        // P/Invoke for EnumWindows
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        // P/Invoke for GetWindowText
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

        // P/Invoke for GetWindowThreadProcessId
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(nint hWnd);

        [DllImport("kernel32.dll")]
        public static extern nint OpenProcess(long dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualProtectEx(
            nint hProcess,           // Handle to the process
            nint lpAddress,          // The base address of the memory region
            uint dwSize,               // The size of the region
            uint flNewProtect,         // The new protection flags
            out uint lpflOldProtect    // The old protection flags (output)
        );

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public nint BaseAddress;
            public nint AllocationBase;
            public uint AllocationProtect;
            public nint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [Flags]
        public enum MemoryProtection : uint
        {
            PAGE_READWRITE = 0x04,
            PAGE_EXECUTE_READWRITE = 0x40,
            PAGE_READONLY = 0x02
        }

        [Flags]
        public enum ProcessAccessFlags : long
        {
            PROCESS_VM_OPERATION = 0x0008,
            PROCESS_VM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_QUERY_INFORMATION = 0x0400,
            PROCESS_ALL_ACCESS = 0x000F0000L | 0x00100000L | 0xFFF
        }

        #endregion

        public List<nint> FilterPointers(uint processId, List<nint> pointers, long newValue)
        {
            throw new NotImplementedException();
        }

        public List<(string title, uint processId)> GetAllProcesses()
        {
            throw new NotImplementedException();
        }

        public List<(string title, uint processId)> GetAllWindows()
        {
            List<(string title, uint processId)> windows = new();

            EnumWindows((hWnd, lParam) =>
            {
                StringBuilder sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (!string.IsNullOrWhiteSpace(title) && IsWindowVisible(hWnd))
                {
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    windows.Add((title, processId));
                }

                return true; // Continue enumeration
            }, nint.Zero);

            return windows;
        }

        public List<nint> MemorySearch(uint processId, long desiredValue)
        {
            ConcurrentBag<nint> result = [];
            int valueSize = Marshal.SizeOf(ValueType);

            nint address = nint.Zero;
            nint handle = OpenProcess((int)(ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_VM_READ), false, processId);

            MEMORY_BASIC_INFORMATION memInfo = new();
            uint memInfoSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            while (true)
            {
                if (!VirtualQueryEx(handle, address, out memInfo, memInfoSize))
                    break;

                // Skip uncommitted memory regions 
                if (memInfo.State == 0x10000 || memInfo.State == 0x2000)
                {
                    address = (nint)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
                    continue;
                }

                // Check if the memory region is readable and writable
                if ((memInfo.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE | MemoryProtection.PAGE_READONLY)) != 0)
                {
                    nint regionBaseAddress = memInfo.BaseAddress;
                    long regionSize = memInfo.RegionSize.ToInt64();

                    // Process memory region in chunks
                    Parallel.For(0, (int)Math.Ceiling((double)regionSize / IMemHack.BufferSize), chunk =>
                    {
                        long offset = chunk * IMemHack.BufferSize;
                        if (offset >= regionSize)
                            return;

                        nint currentAddress = nint.Add(regionBaseAddress, (int)offset);
                        byte[] buffer = new byte[IMemHack.BufferSize];

                        if (ReadProcessMemory(handle, currentAddress, buffer, IMemHack.BufferSize, out nint bytesRead) && bytesRead > 0)
                        {
                            byte[] desiredBytes = BitConverter.GetBytes(desiredValue);

                            for (int j = 0; j <= bytesRead - valueSize; j++)
                            {
                                bool match = true;
                                for (int k = 0; k < valueSize; k++)
                                    if (buffer[j + k] != desiredBytes[k])
                                    {
                                        match = false;
                                        break;
                                    }

                                if (match)
                                {
                                    nint pointer = nint.Add(currentAddress, j);
                                    result.Add(pointer); // Add to thread-safe collection
                                }
                            }
                        }
                    });
                }

                address = (nint)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
            }

            return [.. result.Distinct()];
        }

        public string WriteAddressValue(uint processId, nint targetPointer, long value)
        {
            byte[] newValueBuffer = BitConverter.GetBytes(value);

            nint handle = OpenProcess((long)(ProcessAccessFlags.PROCESS_VM_READ | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_QUERY_INFORMATION | ProcessAccessFlags.PROCESS_VM_OPERATION), false, processId);

            if (handle == nint.Zero)
                return ($"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}");

            if (VirtualQueryEx(handle, targetPointer, out MEMORY_BASIC_INFORMATION memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))))
            {
                Console.WriteLine($"Base Address: 0x{memInfo.BaseAddress:X}");
                Console.WriteLine($"Region Size: 0x{memInfo.RegionSize:X}");
                Console.WriteLine($"Protection: 0x{memInfo.Protect:X}");
                Console.WriteLine($"State: 0x{memInfo.State:X}");

                if (memInfo.State != 0x1000)
                    return ($"Address 0x{targetPointer:X} is not in a committed state.");

                if ((memInfo.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE)) == 0)
                {
                    Console.WriteLine($"Address 0x{targetPointer:X} does not have write permissions. Attempting to change protection...");
                    if (!VirtualProtectEx(handle, targetPointer, (uint)newValueBuffer.Length, (uint)MemoryProtection.PAGE_READWRITE, out uint oldProtect))
                        return ($"Failed to change protection for address 0x{targetPointer:X}. Error: {Marshal.GetLastWin32Error()}");
                }

                if (WriteProcessMemory(handle, targetPointer, newValueBuffer, (uint)newValueBuffer.Length, out nint bytesWritten) && bytesWritten == newValueBuffer.Length)
                    return ($"Successfully wrote value {value} to address 0x{targetPointer:X}.");
                else
                    return ($"Failed to write memory at 0x{targetPointer:X}. Error code: {Marshal.GetLastWin32Error()}");
            }
            else
                return ($"VirtualQueryEx failed for address 0x{targetPointer:X}. Error: {Marshal.GetLastWin32Error()}");
        }
    }
}
