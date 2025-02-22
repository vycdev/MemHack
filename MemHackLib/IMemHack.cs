using MemHackLib.PlatformImplementations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemHackLib;
public interface IMemHack
{
    public const uint BufferSize = 1024;

    public static IMemHack Create() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new MemHackWin(); // Windows-specific implementation
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new MemHackLin(); // Linux-specific implementation
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported.");
        }
    }

    public Type ValueType { get; set; }
    
    public List<nint> MemorySearch(uint processId, long desiredValue);
    public string WriteAddressValue(uint processId, nint targetPointer, long value);
    public List<nint> FilterPointers(uint processId, List<nint> pointers, long newValue);
    public List<(string title, uint processId)> GetAllWindows();
    public List<(string title, uint processId)> GetAllProcesses();
}
