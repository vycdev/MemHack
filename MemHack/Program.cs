using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security;

internal class Program
{
    private const bool Verbose = false;
    private const int maxDepth = 10;
    private static IntPtr address = IntPtr.Zero;
    private static byte[] buffer;
    private static List<IntPtr> foundAddresses = [];
    private static IntPtr handle = IntPtr.Zero;
    private static uint bufferSize = 256;

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

        var processes = Process.GetProcesses().ToList().GroupBy(p => p.ProcessName);

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
        bool isValue = int.TryParse(Console.ReadLine(), out int desiredValue);

        if (!isValue)
        {
            Console.WriteLine("Invalid value");
            return;
        }

        foreach (Process selectedProcess in selectedProcesses)
        {
            handle = OpenProcess((int)(ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_VM_READ), false, selectedProcess.Id);

            MEMORY_BASIC_INFORMATION memInfo = new();
            uint memInfoSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            // Search the value in the process memory
            while (true)
            {
                // Query memory region 
                if (!VirtualQueryEx(handle, address, out memInfo, memInfoSize))
                {
                    Console.WriteLine("VirtualQueryEx failed, reached end of process' memory.");
                    break; // Exit the loop if VirtualQueryEx fails
                }

                // Skip uncommited memory regions 
                if (memInfo.State == 0x10000 || memInfo.State == 0x2000)
                {
                    if (Verbose)
                        Console.WriteLine($"Memory region is not committed or reserved but unallocated, skipping: 0x{memInfo.BaseAddress.ToInt64():X}");
                    address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
                    continue;
                }

                if (Verbose)
                    Console.WriteLine($"Reading at base address 0x{memInfo.BaseAddress.ToInt64():X}");

                // Check if the memory region is readable and writable
                if (IsValidPointer(address))
                {
                    // bufferSize = (ulong)memInfo.RegionSize.ToInt64();
                    buffer = new byte[bufferSize];
                    IntPtr regionBaseAddress = memInfo.BaseAddress;

                    foundAddresses.AddRange(GetMatchingPointers(regionBaseAddress, desiredValue, memInfo.RegionSize.ToInt64()));

                    // Read memory 
                    //if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
                    //{
                    //    for (int j = 0; j < bytesRead - sizeof(int); j++)
                    //    {
                    //        int readValue = BitConverter.ToInt32(buffer, j);

                    //        if (readValue == value)
                    //        {
                    //            IntPtr foundAddress = IntPtr.Add(currentAddress, j); // Calculate the exact address
                    //            foundAddresses.Add(foundAddress);
                    //            if (Verbose)
                    //                Console.WriteLine($"Found at: 0x{foundAddress.ToInt64():X}");
                    //        }
                    //    }
                    //}
                    //else if (Verbose)
                    //{
                    //    // Handle errors and log the issue
                    //    int errorCode = Marshal.GetLastWin32Error();
                    //    if (errorCode == 5)
                    //        Console.WriteLine($"Access denied at address 0x{currentAddress.ToInt64():X}.");
                    //    else if (errorCode == 299)
                    //        Console.WriteLine($"Partial read at address 0x{currentAddress.ToInt64():X}.");
                    //    else
                    //        Console.WriteLine($"Failed to read memory at address 0x{currentAddress.ToInt64():X}. Error code: {errorCode}");
                    //}
                }
                else if (Verbose)
                {
                    Console.WriteLine($"Skipping non-readable memory region at 0x{memInfo.BaseAddress.ToInt64():X}");
                }

                // Read next memory region
                address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
            }
        }

        int k = 0;
        while (true)
        {
            if (!Verbose)
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
            {
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
                        isValue = int.TryParse(Console.ReadLine(), out int newValue);

                        if (!isValue)
                        {
                            Console.WriteLine("Invalid value");
                            break;
                        }

                        List<IntPtr> filteredPointers = [];
                        foreach (IntPtr pointer in foundAddresses)
                        {
                            // filteredPointers.AddRange(GetMatchingPointers(pointer, newValue));

                            if (ReadProcessMemory(handle, pointer, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
                            {
                                int readValue = BitConverter.ToInt32(buffer, 0);
                                if (readValue == newValue)
                                {
                                    filteredPointers.Add(pointer);
                                }
                            }
                            else if (Verbose)
                            {
                                int errorCode = Marshal.GetLastWin32Error();
                                Console.WriteLine($"Failed to read memory at 0x{pointer:X}. Error code: {errorCode}");
                            }
                        }

                        foundAddresses = filteredPointers;
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

                        byte[] newValueBuffer = BitConverter.GetBytes(newAddressValue);
                        IntPtr targetPointer = foundAddresses[index];

                        IntPtr currentPointer = targetPointer;
                        for (int depth = 0; depth < maxDepth; depth++)
                        {
                            if (ReadProcessMemory(handle, currentPointer, buffer, (uint)bufferSize, out nint bytesRead) && bytesRead == IntPtr.Size)
                            {
                                currentPointer = (IntPtr)BitConverter.ToInt64(buffer, 0); // Get the next pointer in the chain

                                if (depth == maxDepth - 1)
                                {
                                    // Write the new value to the final pointer
                                    if (WriteProcessMemory(handle, currentPointer, newValueBuffer, (uint)newValueBuffer.Length, out nint bytesWritten) && bytesWritten == newValueBuffer.Length)
                                    {
                                        Console.WriteLine($"Successfully wrote value {newAddressValue} to address 0x{currentPointer:X}.");
                                    }
                                    else
                                    {
                                        int errorCode = Marshal.GetLastWin32Error();
                                        Console.WriteLine($"Failed to write memory at 0x{currentPointer:X}. Error code: {errorCode}");
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Failed to read memory at 0x{currentPointer:X}. Stopping chain trace.");
                                break;
                            }
                        }

                        //if (WriteProcessMemory(handle, targetPointer, newValueBuffer, (uint)newValueBuffer.Length, out nint bytesWritten) && bytesWritten == newValueBuffer.Length)
                        //{
                        //    Console.WriteLine($"Successfully wrote value {newAddressValue} to address 0x{targetPointer:X}.");
                        //}
                        //else
                        //{
                        //    int errorCode = Marshal.GetLastWin32Error();
                        //    Console.WriteLine($"Failed to write memory at 0x{targetPointer:X}. Error code: {errorCode}");
                        //}

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();

                        break;
                    case "4":
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
    }

    private static List<IntPtr> GetMatchingPointers(IntPtr pointer, int desiredValue, long regionSize)
    {
        List<IntPtr> result = [];
        int depth = 0;

        // Read memory 
        //if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
        //{
        //    for (int j = 0; j < bytesRead - sizeof(int); j++)
        //    {
        //        int readValue = BitConverter.ToInt32(buffer, j);

        //        if (readValue == value)
        //        {
        //            IntPtr foundAddress = IntPtr.Add(currentAddress, j); // Calculate the exact address
        //            foundAddresses.Add(foundAddress);
        //            if (Verbose)
        //                Console.WriteLine($"Found at: 0x{foundAddress.ToInt64():X}");
        //        }
        //    }
        //}
        // Read the pointer at the current address
        for (long offset = 0; offset < regionSize; offset += bufferSize)
        {
            IntPtr currentAddress = IntPtr.Add(pointer, (int)offset);

            if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
            {
                for (int j = 0; j < bytesRead - sizeof(int); j++)
                {
                    int value = BitConverter.ToInt32(buffer, j);

                    if (value == desiredValue)
                    {
                        IntPtr foundAddress = IntPtr.Add(currentAddress, j); // Calculate the exact address
                        result.Add(currentAddress);
                        if (Verbose)
                            Console.WriteLine($"Found at: 0x{currentAddress.ToInt64():X}");
                    }
                }

                // currentAddress = IntPtr.Add(currentAddress, (int)bufferSize - sizeof(int) + 1);


                //// Dereference the pointer
                //currentAddress = (IntPtr)BitConverter.ToInt64(buffer, 0);

                //if (!IsValidPointer(currentAddress))
                //    break;
            }
            else
            {
                if (Verbose)
                    Console.WriteLine($"Failed to read memory at 0x{currentAddress:X}. Error code: {Marshal.GetLastWin32Error()}");
                //break; // Stop if the chain breaks
            }
        }

        return result;
    }

    private static bool IsValidPointer(IntPtr address)
    {
        bool result = VirtualQueryEx(handle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

        if (result == false)
            return false; // Query failed, invalid pointer

        // Check if the memory is committed and accessible
        return (mbi.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE | MemoryProtection.PAGE_READONLY)) != 0;
    }


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
        {
            Console.WriteLine("Failed to adjust token privileges.");
        }
        else
        {
            Console.WriteLine("Debug privileges enabled.");
        }
    }
}

