using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

internal class Program
{
    private static byte[] buffer = [];
    private const uint bufferSize = 1024;
    private static Type valueType = typeof(int); // short, int, long

    private static List<IntPtr> foundAddresses = [];

    #region WINAPI
    // Import Windows API functions

    // Delegate for EnumWindows callback
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // P/Invoke for EnumWindows
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // P/Invoke for GetWindowText
    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // P/Invoke for GetWindowThreadProcessId
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(long dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool VirtualProtectEx(
        IntPtr hProcess,           // Handle to the process
        IntPtr lpAddress,          // The base address of the memory region
        uint dwSize,               // The size of the region
        uint flNewProtect,         // The new protection flags
        out uint lpflOldProtect    // The old protection flags (output)
    );

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
    public enum ProcessAccessFlags : long
    {
        PROCESS_VM_OPERATION = 0x0008,
        PROCESS_VM_READ = 0x0010,
        PROCESS_VM_WRITE = 0x0020,
        PROCESS_QUERY_INFORMATION = 0x0400,
        PROCESS_ALL_ACCESS = (0x000F0000L | 0x00100000L | 0xFFF)
    }

    #endregion

    private static void Main(string[] args)
    {
        List<(IntPtr hWnd, string title, uint processId)> windows = GetAllWindows();
        int j = 0;
        foreach ((IntPtr hWnd, string title, uint processId) window in windows)
        {
            Console.WriteLine($"{j}. {window.title} - {window.processId}");
            j++;
        }

        Console.Write("Enter the window number: ");
        bool isWindowNumber = int.TryParse(Console.ReadLine(), out int windowNumber);

        if (!isWindowNumber)
        {
            Console.WriteLine("Invalid window number");
            return;
        }

        if (windowNumber < 0 || windowNumber >= windows.Count)
        {
            Console.WriteLine("Invalid window number");
            return;
        }

        (IntPtr hWnd, string title, uint processId) = windows[windowNumber];

        Console.Write("Enter the value: ");
        bool isValue = long.TryParse(Console.ReadLine(), out long desiredValue);

        if (!isValue)
        {
            Console.WriteLine("Invalid value");
            return;
        }

        Console.WriteLine("Searching for addresses...");
        foundAddresses = MemorySearch(processId, desiredValue);

        int k = 0;
        while (true)
        {
            Console.Clear();
            Console.WriteLine("____________INFO____________");
            Console.WriteLine($"Window title: {title}");
            Console.WriteLine($"Process ID: {processId}");
            Console.WriteLine($"Addresses found: {foundAddresses.Count}");
            Console.WriteLine("____________MENU____________");
            Console.WriteLine("1. Print addresses");
            Console.WriteLine("2. Filter for new value");
            Console.WriteLine("3. Change address value");
            Console.WriteLine("4. Change user given address");
            Console.WriteLine("5. Restart");
            Console.WriteLine("6. Exit");

            Console.Write("Enter the option: ");
            string option = Console.ReadLine();
            if (option != null)
                switch (option)
                {
                    case "1":
                        k = 0;
                        foreach (IntPtr foundAddress in foundAddresses)
                        {
                            long value = BufferConvert(buffer, 0);
                            Console.WriteLine($"{k}. 0x{foundAddress.ToInt64():X} - {value}");
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

                        foundAddresses = FilterPointers(processId, foundAddresses, newValue);

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

                        WriteAddressValue(processId, foundAddresses[index], newAddressValue);

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();

                        break;
                    case "4":
                        Console.WriteLine("Enter the address to read: ");
                        string addressString = Console.ReadLine();
                        bool isAddress = long.TryParse(addressString, out long addressValue);
                        if (!isAddress)
                        {
                            Console.WriteLine("Invalid address");
                            return;
                        }

                        // read new address value
                        Console.WriteLine("Enter the new address value: ");
                        string newAddressString = Console.ReadLine();
                        bool isNewAddress = long.TryParse(newAddressString, out long newaddrvalue);
                        if (!isNewAddress)
                        {
                            Console.WriteLine("Invalid new address value");
                            return;
                        }

                        WriteAddressValue(processId, (IntPtr)addressValue, newaddrvalue);
                        Console.ReadLine();

                        break;
                    case "5":
                        buffer = [];
                        foundAddresses = [];
                        valueType = typeof(short);
                        Main(args);
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
        }
    }

    private static List<IntPtr> MemorySearch(uint processId, long desiredValue)
    {
        ConcurrentBag<IntPtr> result = [];
        int valueSize = Marshal.SizeOf(valueType);

        IntPtr address = IntPtr.Zero;
        IntPtr handle = OpenProcess((int)(ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_VM_READ), false, processId);

        MEMORY_BASIC_INFORMATION memInfo = new();
        uint memInfoSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

        while (true)
        {
            if (!VirtualQueryEx(handle, address, out memInfo, memInfoSize))
                break;

            // Skip uncommitted memory regions 
            if (memInfo.State == 0x10000 || memInfo.State == 0x2000)
            {
                address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
                continue;
            }

            // Check if the memory region is readable and writable
            if ((memInfo.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE | MemoryProtection.PAGE_READONLY)) != 0)
            {
                IntPtr regionBaseAddress = memInfo.BaseAddress;
                long regionSize = memInfo.RegionSize.ToInt64();

                // Process memory region in chunks
                Parallel.For(0, (int)Math.Ceiling((double)regionSize / bufferSize), chunk =>
                {
                    long offset = chunk * bufferSize;
                    if (offset >= regionSize)
                        return;

                    IntPtr currentAddress = IntPtr.Add(regionBaseAddress, (int)offset);
                    byte[] buffer = new byte[bufferSize];

                    if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead > 0)
                    {
                        byte[] desiredBytes = BitConverter.GetBytes(desiredValue);

                        for (int j = 0; j <= bytesRead - valueSize; j++)
                        {
                            bool match = true;
                            for (int k = 0; k < valueSize; k++)
                            {
                                if (buffer[j + k] != desiredBytes[k])
                                {
                                    match = false;
                                    break;
                                }
                            }

                            if (match)
                            {
                                IntPtr pointer = IntPtr.Add(currentAddress, j);
                                result.Add(pointer); // Add to thread-safe collection
                            }
                        }
                    }
                });
            }

            address = (IntPtr)(memInfo.BaseAddress + memInfo.RegionSize.ToInt64());
        }

        return [.. result.Distinct()];
    }

    private static void WriteAddressValue(uint processId, IntPtr targetPointer, long value)
    {
        byte[] newValueBuffer = BitConverter.GetBytes(value);

        IntPtr handle = OpenProcess((long)(ProcessAccessFlags.PROCESS_VM_READ | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_QUERY_INFORMATION | ProcessAccessFlags.PROCESS_VM_OPERATION), false, processId);

        if (handle == IntPtr.Zero)
        {
            Console.WriteLine($"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}");
            return;
        }

        if (VirtualQueryEx(handle, targetPointer, out MEMORY_BASIC_INFORMATION memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))))
        {
            Console.WriteLine($"Base Address: 0x{memInfo.BaseAddress:X}");
            Console.WriteLine($"Region Size: 0x{memInfo.RegionSize:X}");
            Console.WriteLine($"Protection: 0x{memInfo.Protect:X}");
            Console.WriteLine($"State: 0x{memInfo.State:X}");

            if (memInfo.State != (uint)0x1000)
            {
                Console.WriteLine($"Address 0x{targetPointer:X} is not in a committed state.");
                return;
            }

            if ((memInfo.Protect & (uint)(MemoryProtection.PAGE_READWRITE | MemoryProtection.PAGE_EXECUTE_READWRITE)) == 0)
            {
                Console.WriteLine($"Address 0x{targetPointer:X} does not have write permissions. Attempting to change protection...");
                if (!VirtualProtectEx(handle, targetPointer, (uint)newValueBuffer.Length, (uint)MemoryProtection.PAGE_READWRITE, out uint oldProtect))
                {
                    Console.WriteLine($"Failed to change protection for address 0x{targetPointer:X}. Error: {Marshal.GetLastWin32Error()}");
                    return;
                }
            }

            if (WriteProcessMemory(handle, targetPointer, newValueBuffer, (uint)newValueBuffer.Length, out nint bytesWritten) && bytesWritten == newValueBuffer.Length)
            {
                Console.WriteLine($"Successfully wrote value {value} to address 0x{targetPointer:X}.");
            }
            else
            {
                Console.WriteLine($"Failed to write memory at 0x{targetPointer:X}. Error code: {Marshal.GetLastWin32Error()}");
            }
        }
        else
        {
            Console.WriteLine($"VirtualQueryEx failed for address 0x{targetPointer:X}. Error: {Marshal.GetLastWin32Error()}");
        }
    }

    private static List<IntPtr> FilterPointers(uint processId, List<IntPtr> pointers, long newValue)
    {
        ConcurrentBag<IntPtr> filteredPointers = [];

        IntPtr handle = OpenProcess((int)(ProcessAccessFlags.PROCESS_VM_OPERATION | ProcessAccessFlags.PROCESS_VM_WRITE | ProcessAccessFlags.PROCESS_VM_READ), false, processId);

        Parallel.ForEach(pointers, pointer =>
        {
            byte[] buffer = new byte[Marshal.SizeOf(valueType)];

            if (ReadProcessMemory(handle, pointer, buffer, (uint)buffer.Length, out nint bytesRead) && bytesRead > 0)
            {
                long readValue = BufferConvert(buffer, 0);
                if (readValue == newValue)
                {
                    filteredPointers.Add(pointer); // Add valid pointer to thread-safe collection
                }
            }
        });

        return filteredPointers.Distinct().ToList(); // Remove duplicates and convert to list
    }

    private static long BufferConvert(byte[] buffer, int offset) => valueType switch
    {
        var valueType when valueType == typeof(short) => BitConverter.ToInt16(buffer, offset),
        var valueType when valueType == typeof(int) => BitConverter.ToInt32(buffer, offset),
        var valueType when valueType == typeof(long) => BitConverter.ToInt64(buffer, offset),
        _ => BitConverter.ToInt32(buffer, offset),
    };

    // Method to get a list of all windows
    public static List<(IntPtr hWnd, string title, uint processId)> GetAllWindows()
    {
        List<(IntPtr hWnd, string title, uint processId)> windows = new();

        EnumWindows((hWnd, lParam) =>
        {
            StringBuilder sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (!string.IsNullOrWhiteSpace(title) && IsWindowVisible(hWnd))
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                windows.Add((hWnd, title, processId));
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return windows;
    }
}

