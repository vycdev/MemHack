using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security;

internal class Program
{
    private static IntPtr address = IntPtr.Zero;
    private static byte[] buffer = [];
    private static List<IntPtr> foundAddresses = [];
    private static IntPtr handle = IntPtr.Zero;
    private static readonly uint bufferSize = 1024;
    private static Type valueType = typeof(int); // short, int, long

    #region WINAPI
    // Import Windows API functions
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesWritten);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
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
    public enum ProcessAccessFlags : uint
    {
        PROCESS_VM_OPERATION = 0x0008,
        PROCESS_VM_READ = 0x0010,
        PROCESS_VM_WRITE = 0x0020,

    }

    const int PROCESS_QUERY_INFORMATION = 0x0400;

    // Import Windows API functions for granting privileges
    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    const string SE_DEBUG_NAME = "SeDebugPrivilege";
    const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    const uint TOKEN_QUERY = 0x0008;

    #endregion

    private static void Main(string[] args)
    {
        EnableDebugPrivileges();

        IEnumerable<IGrouping<string, Process>> processes = Process.GetProcesses().ToList().GroupBy(p => p.ProcessName);

        int i = 0;
        foreach (IGrouping<string, Process> processGroup in processes)
        {
            Console.WriteLine(i + ". " + processGroup.Key);
            i++;
        }

        Console.Write("Enter the process number: ");

        bool isNumber = int.TryParse(Console.ReadLine(), out int processNumber);

        if (!isNumber)
        {
            Console.WriteLine("Invalid process number");
            return;
        }

        if (processNumber < 0 || processNumber >= processes.Count())
        {
            Console.WriteLine("Invalid process number");
            return;
        }

        IGrouping<string, Process> selectedProcesses = processes.ElementAt(processNumber);

        Console.Write("Enter the value: ");
        bool isValue = long.TryParse(Console.ReadLine(), out long desiredValue);

        if (!isValue)
        {
            Console.WriteLine("Invalid value");
            return;
        }

        MemorySearch(selectedProcesses, desiredValue);

        int k = 0;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("____________INFO____________");
            Console.WriteLine($"Addresses found: {foundAddresses.Count}");
            Console.WriteLine("____________MENU____________");
            Console.WriteLine("1. Print addresses");
            Console.WriteLine("2. Filter for new value");
            Console.WriteLine("3. Change address value");
            Console.WriteLine("4. Restart");
            Console.WriteLine("5. Exit");

            Console.Write("Enter the option: ");
            string option = Console.ReadLine();
            if (option != null)
                switch (option)
                {
                    case "1":
                        k = 0;
                        foreach (IntPtr foundAddress in foundAddresses)
                        {
                            Console.WriteLine($"{k}. 0x{foundAddress.ToInt64():X}");
                            k++;
                        }

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        break;
                    case "2":
                        Console.Write("Enter the new value: ");
                        isValue = long.TryParse(Console.ReadLine(), out long newValue);

                        if (!isValue)
                        {
                            Console.WriteLine("Invalid value");
                            break;
                        }

                        foundAddresses = FilterPointers(foundAddresses, newValue);

                        break;
                    case "3":
                        int index = 0;
                        if (foundAddresses.Count > 1)
                        {
                            Console.WriteLine("Enter the address index: ");
                            isValue = int.TryParse(Console.ReadLine(), out index);

                            if (!isValue || index < 0 || index >= foundAddresses.Count)
                            {
                                Console.WriteLine("Invalid index");
                                break;
                            }
                        }

                        Console.WriteLine("Enter the new value: ");
                        isValue = int.TryParse(Console.ReadLine(), out int newAddressValue);

                        if (!isValue)
                        {
                            Console.WriteLine("Invalid value");
                            break;
                        }

                        WriteAddressValue(foundAddresses[index], newAddressValue);

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();

                        break;
                    case "4":
                        address = IntPtr.Zero;
                        buffer = [];
                        foundAddresses = [];
                        handle = IntPtr.Zero;
                        valueType = typeof(short);
                        Main(args);
                        break;
                    case "5":
                        return;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
        }
    }

    private static void WriteAddressValue(IntPtr targetPointer, long value)
    {
        byte[] newValueBuffer = valueType switch
        {
            var valueType when valueType == typeof(short) => BitConverter.GetBytes((short)value),
            var valueType when valueType == typeof(int) => BitConverter.GetBytes((int)value),
            var valueType when valueType == typeof(long) => BitConverter.GetBytes(value),
            _ => BitConverter.GetBytes((int)value)
        };

        if (WriteProcessMemory(handle, targetPointer, newValueBuffer, (uint)newValueBuffer.Length, out nint bytesWritten) && bytesWritten == newValueBuffer.Length)
            Console.WriteLine($"Successfully wrote value {value} to address 0x{targetPointer:X}.");
        else
            Console.WriteLine($"Failed to write memory at 0x{targetPointer:X}. Error code: {Marshal.GetLastWin32Error()}");
    }

    private static List<IntPtr> FilterPointers(List<IntPtr> pointers, long newValue)
    {
        List<IntPtr> filteredPointers = [];
        byte[] buffer = new byte[Marshal.SizeOf(valueType)];

        foreach (IntPtr pointer in foundAddresses)
        {
            if (ReadProcessMemory(handle, pointer, buffer, (uint)buffer.Length, out nint bytesRead) && bytesRead > 0)
            {
                long readValue = BufferConvert(buffer, 0);
                if (readValue == newValue)
                {
                    filteredPointers.Add(pointer);
                }
            }
        }

        return filteredPointers;
    }

    private static List<IntPtr> GetMatchingPointers(IntPtr pointer, long desiredValue, long regionSize)
    {
        List<IntPtr> result = [];

        for (long offset = 0; offset < regionSize; offset += bufferSize)
        {
            IntPtr currentAddress = IntPtr.Add(pointer, (int)offset);

            if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
            {
                for (int j = 0; j < bytesRead - Marshal.SizeOf(valueType); j++)
                {
                    long value = BufferConvert(buffer, j);

                    IntPtr address = IntPtr.Add(currentAddress, j); // Calculate the exact address

                    if (value == desiredValue)
                        result.Add(address);
                }
            }
        }

        return result;
    }

    private static void MemorySearch(IEnumerable<Process> selectedProcesses, long desiredValue)
    {
        foreach (Process selectedProcess in selectedProcesses)
        {
            handle = OpenProcess((int)(ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_VM_READ), false, selectedProcess.Id);

            MEMORY_BASIC_INFORMATION memInfo = new();
            uint memInfoSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            // Search the value in the process memory
            while (true)
            {
                if (!VirtualQueryEx(handle, address, out memInfo, memInfoSize))
                    break;

                // Skip uncommited memory regions 
                if (memInfo.State == 0x10000 || memInfo.State == 0x2000)
                {
                    address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
                    continue;
                }

                // Check if the memory region is readable and writable
                if ((memInfo.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE | MemoryProtection.PAGE_READONLY)) != 0)
                {
                    buffer = new byte[bufferSize];
                    IntPtr regionBaseAddress = memInfo.BaseAddress;

                    foundAddresses.AddRange(GetMatchingPointers(regionBaseAddress, desiredValue, memInfo.RegionSize.ToInt64()));
                }

                address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
            }
        }
    }

    private static bool IsValidPointer(IntPtr address)
    {
        bool result = VirtualQueryEx(handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

        if (result == false)
            return false; // Query failed, invalid pointer

        // Check if the memory is committed and accessible
        return (mbi.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE | MemoryProtection.PAGE_READONLY)) != 0;
    }

    private static long BufferConvert(byte[] buffer, int offset) => valueType switch
    {
        var valueType when valueType == typeof(short) => BitConverter.ToInt16(buffer, offset),
        var valueType when valueType == typeof(int) => BitConverter.ToInt32(buffer, offset),
        var valueType when valueType == typeof(long) => BitConverter.ToInt64(buffer, offset),
        _ => BitConverter.ToInt32(buffer, offset),
    };


    private static void EnableDebugPrivileges()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr tokenHandle))
        {
            Console.WriteLine("Failed to open process token.");
            return;
        }

        TOKEN_PRIVILEGES tp = new()
        {
            PrivilegeCount = 1,
            Attributes = SE_PRIVILEGE_ENABLED
        };

        if (!LookupPrivilegeValue(null, SE_DEBUG_NAME, out tp.Luid))
        {
            Console.WriteLine("Failed to look up privilege value.");
            return;
        }

        if (!AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            Console.WriteLine("Failed to adjust token privileges.");
        else
            Console.WriteLine("Debug privileges enabled.");
    }
}

