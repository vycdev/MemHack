using System.Diagnostics;
using System.Runtime.InteropServices;

internal class Program
{
    // Import Windows API functions
    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool VirtualQueryEx(nint hProcess, nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

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

    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int PROCESS_VM_READ = 0x0010;

    private static void Main(string[] args)
    {
        if (!IsAdministrator())
        {
            RelaunchAsAdministrator();
        }

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
        bool isValue = int.TryParse(Console.ReadLine(), out int value);

        if (!isValue)
        {
            Console.WriteLine("Invalid value");
            return;
        }

        foreach (Process selectedProcess in selectedProcesses)
        {
            IntPtr handle = OpenProcess(PROCESS_VM_READ, false, selectedProcess.Id);

            IntPtr address = IntPtr.Zero;
            const uint bufferSize = 1024;
            byte[] buffer = new byte[bufferSize];

            while (true)
            {
                // Search the value in the process memory
                MEMORY_BASIC_INFORMATION memInfo = new();
                uint memInfoSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

                // Query memory region 
                if (!VirtualQueryEx(handle, address, out memInfo, memInfoSize))
                {
                    Console.WriteLine("No more memory regions to query");
                    break; // Exit the loop if VirtualQueryEx fails
                }

                // Check if the memory region is readable
                if (memInfo.State == PROCESS_VM_READ && (memInfo.Protect & PROCESS_QUERY_INFORMATION) != 0)
                {
                    long regionSize = memInfo.RegionSize.ToInt64();
                    IntPtr regionBaseAddress = memInfo.BaseAddress;

                    for (long offset = 0; offset < regionSize; offset += bufferSize)
                    {
                        IntPtr currentAddress = IntPtr.Add(regionBaseAddress, (int)offset);

                        try
                        {
                            // read memory 
                            if (ReadProcessMemory(handle, currentAddress, buffer, bufferSize, out nint bytesRead) && bytesRead.ToInt32() > 0)
                            {
                                for (int j = 0; j < bytesRead.ToInt32() - sizeof(int); i++)
                                {
                                    int readValue = BitConverter.ToInt32(buffer, j);

                                    if (readValue == value)
                                    {
                                        Console.WriteLine("Found at: " + currentAddress);
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Memory region is not readable");
                    break;
                }
            }
        }
    }

    static bool IsAdministrator()
    {
        // Check if the current process is running with admin privileges
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
    static void RelaunchAsAdministrator()
    {
        try
        {
            // Start the current process as Administrator
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.Verb = "runas";  // Request elevated privileges
            startInfo.FileName = System.Reflection.Assembly.GetExecutingAssembly().Location;  // Current executable file

            // Start the new process
            Process.Start(startInfo);
            Environment.Exit(0);  // Exit the current process so it doesn't continue running with low permissions
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred while trying to relaunch as administrator: " + ex.Message);
        }
    }
}

