using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemHackLib.PlatformImplementations
{
    internal class MemHackLin : IMemHack
    {
        public Type ValueType { get; set; } = typeof(int);

        #region Linux Specifics

        // Define constants for ptrace commands
        private const int PTRACE_ATTACH = 16;
        private const int PTRACE_DETACH = 17;
        private const int PTRACE_PEEKDATA = 2;
        private const int PTRACE_POKEDATA = 5;     // PTRACE_POKEDATA to write memory

        // Define process access flags
        [Flags]
        public enum ProcessAccessFlags
        {
            PROCESS_VM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_VM_OPERATION = 0x0008,
        }

        // P/Invoke for ptrace and other necessary Linux system calls
        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int ptrace(int request, int pid, nint addr, nint data);

        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int waitpid(int pid, out int status, int options);

        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int open(string path, int flags);

        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int close(int fd);

        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int read(int fd, byte[] buffer, int size);

        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int write(int fd, byte[] buffer, int size);

        // P/Invoke declarations for X11 functions
        [DllImport("libX11.so.6", EntryPoint = "XOpenDisplay")]
        private static extern nint XOpenDisplay(string display);

        [DllImport("libX11.so.6", EntryPoint = "XCloseDisplay")]
        private static extern int XCloseDisplay(nint display);

        [DllImport("libX11.so.6", EntryPoint = "XRootWindow")]
        private static extern nint XRootWindow(nint display, int screen);

        [DllImport("libX11.so.6", EntryPoint = "XQueryTree")]
        private static extern int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint children, out uint nchildren);

        [DllImport("libX11.so.6", EntryPoint = "XFetchName")]
        private static extern int XFetchName(nint display, nint window, StringBuilder windowName);

        [DllImport("libX11.so.6", EntryPoint = "XGetWindowAttributes")]
        private static extern int XGetWindowAttributes(nint display, nint window, out XWindowAttributes attributes);

        // Define the X11 Window Attributes struct
        [StructLayout(LayoutKind.Sequential)]
        public struct XWindowAttributes
        {
            public int x, y;
            public int width, height;
            public int border_width;
            public int depth;
            public nint visual;
            public nint colormap;
            public int map_state;
            public int all_event_masks;
            public int your_event_mask;
            public nint do_not_propagate_mask;
            public nint override_redirect;
        }

        [DllImport("libX11.so.6", EntryPoint = "XGetWindowProperty")]
        private static extern int XGetWindowProperty(nint display, nint window, Atom property, long offset, long length, bool delete, Atom reqType, out Atom actualType, out int actualFormat, out long nItems, out long bytesAfter, out nint prop);

        // Define the Atom types for the properties
        private static readonly Atom WM_PID = new(34);  // WM_PID atom (commonly used for process ID in X11)
        private static readonly Atom WM_CLIENT_MACHINE = new(31); // WM_CLIENT_MACHINE atom (for client machine name)

        // Struct to define Atom type for X11
        [StructLayout(LayoutKind.Sequential)]
        public struct Atom
        {
            public long atom;
            public Atom(long atom) => this.atom = atom;
        }

        #endregion

        // Example memory reading function using ptrace
        private static long ReadMemory(int pid, nint address)
        {
            int value = ptrace(PTRACE_PEEKDATA, pid, address, 0);
            
            if (value == -1)
                throw new InvalidOperationException("Error reading memory.");
            
            return value;
        }

        // Linux-specific FilterPointers implementation
        public List<nint> FilterPointers(uint processId, List<nint> pointers, long newValue)
        {
            ConcurrentBag<nint> filteredPointers = [];

            // Attach to the target process
            int pid = (int)processId;
            
            ptrace(PTRACE_ATTACH, pid, 0, 0);
            waitpid(pid, out int status, 0); // Wait for the process to stop

            Parallel.ForEach(pointers, pointer =>
            {
                try
                {
                    long readValue = ReadMemory(pid, pointer);
                    if (readValue == newValue)
                    {
                        filteredPointers.Add(pointer); // Add valid pointer to thread-safe collection
                    }
                }
                catch (Exception)
                {
                    // Handle errors reading memory, such as invalid addresses
                }
            });

            // Detach from the target process after the operation is complete
            ptrace(PTRACE_DETACH, pid, 0, 0);

            return filteredPointers.Distinct().ToList(); // Remove duplicates and convert to list
        }

        // Get all processes by reading the /proc filesystem
        public List<(string title, uint processId)> GetAllProcesses()
        {
            List<(string title, uint processId)> processes = [];

            // Get all directories in /proc that are numeric (representing process IDs)
            var processDirs = Directory.GetDirectories("/proc")
                .Where(dir => uint.TryParse(new DirectoryInfo(dir).Name, out _)); // Only directories with numeric names

            foreach (var dir in processDirs)
            {
                try
                {
                    string pid = new DirectoryInfo(dir).Name;
                    string statusFile = Path.Combine(dir, "status");

                    if (File.Exists(statusFile))
                    {
                        // Read the status file to get the process name
                        var lines = File.ReadAllLines(statusFile);
                        string processName = lines
                            .FirstOrDefault(line => line.StartsWith("Name:"))
                            ?.Split([':'], 2)[1]
                            .Trim() ?? string.Empty;

                        if (!string.IsNullOrEmpty(processName))
                            processes.Add((processName, uint.Parse(pid)));
                    }
                }
                catch
                {
                    // Ignore any errors (e.g., access permissions, process disappearing)
                }
            }

            return processes;
        }

        // Get all windows and their titles using X11
        public List<(string title, uint processId)> GetAllWindows()
        {
            List<(string title, uint processId)> windows = [];
            nint display = XOpenDisplay(null);

            if (display == nint.Zero)
                throw new InvalidOperationException("Unable to open X display.");

            nint rootWindow = XRootWindow(display, 0);

            XQueryTree(display, rootWindow, out rootWindow, out nint parentWindow, out nint childrenWindow, out uint numChildren);

            nint[] childWindows = new nint[numChildren];
            Marshal.Copy(childrenWindow, childWindows, 0, (int)numChildren);

            foreach (var window in childWindows)
            {
                try
                {
                    StringBuilder windowTitle = new(256);
                    if (XFetchName(display, window, windowTitle) > 0)
                    {
                        string title = windowTitle.ToString();
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            // Check if the window is visible (simplified logic)
                            XGetWindowAttributes(display, window, out XWindowAttributes attributes);
                            if (attributes.map_state == 1) // If window is mapped (visible)
                            {
                                uint processId = GetProcessIdFromWindow(display, window); // Simplified for demo, requires additional work
                                windows.Add((title, processId));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignore errors
                }
            }

            XCloseDisplay(display);
            return windows;
        }

        // Get the process ID associated with a window using X11 and `/proc`
        private uint GetProcessIdFromWindow(nint display, nint window)
        {
            try
            {
                // Query the window properties to check for the PID
                int result = XGetWindowProperty(display, window, WM_PID, 0, 0, false, WM_PID, out Atom actualType, out int actualFormat, out long nItems, out long bytesAfter, out nint prop);

                if (result == 0 && prop != nint.Zero)
                {
                    // If we successfully fetched a property, try to interpret it as a PID
                    uint pid = (uint)Marshal.ReadInt32(prop);
                    return pid;
                }

                // Fallback: Try matching the window with the process using /proc/{pid}/fd/
                string[] files = Directory.GetFiles("/proc");
                foreach (var file in files)
                {
                    string pidDir = Path.GetFileName(file);
                    if (uint.TryParse(pidDir, out uint pid))
                    {
                        string[] fdFiles = Directory.GetFiles($"/proc/{pid}/fd/");
                        foreach (var fd in fdFiles)
                        {
                            try
                            {
                                // Read the symbolic link at the file descriptor path
                                var fileInfo = new FileInfo(fd);
                                var linkTarget = fileInfo.LinkTarget;
                                if (linkTarget != null && linkTarget.Contains($"window-{window:X}", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    return pid;  // Found a matching process for the window
                                }
                            }
                            catch
                            {
                                // Ignore errors for invalid file descriptors
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore any exceptions and return a placeholder PID
            }

            return 0; // Return 0 if the PID can't be found
        }

        // A simple read memory function using /proc/{pid}/mem
        private bool ReadProcessMemory(int processId, nint address, byte[] buffer, int size, out int bytesRead)
        {
            bytesRead = 0;
            try
            {
                string procMemPath = $"/proc/{processId}/mem";
                using FileStream fs = new(procMemPath, FileMode.Open, FileAccess.Read);

                fs.Seek(address, SeekOrigin.Begin);
                bytesRead = fs.Read(buffer, 0, size);
                return bytesRead > 0;
            }
            catch
            {
                return false;
            }
        }

        // OpenProcess using ptrace
        private nint OpenProcess(int processId)
        {
            // Attach to the target process using ptrace (PTRACE_ATTACH)
            int result = ptrace(PTRACE_ATTACH, processId, nint.Zero, nint.Zero);

            // If ptrace fails, return an invalid handle or throw an exception
            if (result == -1)
                throw new InvalidOperationException($"Failed to attach to process {processId}. Error: {Marshal.GetLastWin32Error()}");

            // Allow time for the process to stop (optional: use sleep to wait for process stop)
            Thread.Sleep(100);

            // Return a handle to the process (can return the processId or other identifier)
            return processId;
        }

        // Close the process handle using ptrace
        private void CloseProcess(int processId)
        {
            // Detach from the target process using ptrace (PTRACE_DETACH)
            int result = ptrace(PTRACE_DETACH, processId, nint.Zero, nint.Zero);

            // If ptrace fails, throw an exception
            if (result == -1)
                throw new InvalidOperationException($"Failed to detach from process {processId}. Error: {Marshal.GetLastWin32Error()}");
        }

        public List<nint> MemorySearch(uint processId, long desiredValue)
        {
            ConcurrentBag<nint> result = [];
            int valueSize = Marshal.SizeOf(ValueType);

            // Open process memory (use ptrace or /proc/{pid}/mem)
            nint handle = OpenProcess((int)processId);

            // Check if the process handle is valid (replace with your own logic)
            if (handle == nint.Zero)
                return [.. result];

            string mapsFilePath = $"/proc/{processId}/maps";
            var memoryRegions = File.ReadLines(mapsFilePath)
                                    .Where(line => line.Contains("rw"))
                                    .Select(line => line.Split(' ')[0])
                                    .ToList();

            // Iterate through memory regions
            foreach (var region in memoryRegions)
            {
                var parts = region.Split('-');
                nint startAddress = (nint)Convert.ToInt64(parts[0], 16);
                nint endAddress = (nint)Convert.ToInt64(parts[1], 16);
                long regionSize = endAddress - startAddress;

                // Process memory region in chunks
                Parallel.For(0, (int)Math.Ceiling((double)regionSize / IMemHack.BufferSize), chunk =>
                {
                    long offset = chunk * IMemHack.BufferSize;
                    if (offset >= regionSize)
                        return;

                    nint currentAddress = nint.Add(startAddress, (int)offset);
                    byte[] buffer = new byte[IMemHack.BufferSize];

                    if (ReadProcessMemory((int)processId, currentAddress, buffer, (int)IMemHack.BufferSize, out int bytesRead) && bytesRead > 0)
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
                                nint pointer = nint.Add(currentAddress, j);
                                result.Add(pointer); // Add to thread-safe collection
                            }
                        }
                    }
                });
            }

            // Close the process handle
            CloseProcess((int)processId);

            return result.Distinct().ToList(); // Return unique results
        }

        // Write value to process memory
        public string WriteAddressValue(uint processId, nint targetPointer, long value)
        {
            byte[] newValueBuffer = BitConverter.GetBytes((int)value);

            // Open the process memory using ptrace
            nint handle = OpenProcess((int)processId);

            if (handle == nint.Zero)
                return $"Failed to open process {processId}. Error: {Marshal.GetLastWin32Error()}";

            // Use ptrace PTRACE_POKEDATA to write the memory value
            nint addr = new(targetPointer);
            nint data = Marshal.UnsafeAddrOfPinnedArrayElement(newValueBuffer, 0);
            int result = ptrace(PTRACE_POKEDATA, (int)processId, addr, data);

            if (result == -1)
                return $"Failed to write memory at 0x{targetPointer:X}. Error code: {Marshal.GetLastWin32Error()}";

            // Detach from the process once writing is done
            CloseProcess((int)processId);

            return $"Successfully wrote value {value} to address 0x{targetPointer:X}.";
        }
    }
}
