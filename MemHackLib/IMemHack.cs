using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemHackLib;
public interface IMemHack
{
    public const uint BufferSize = 1024;
    public Type ValueType { get; set; }
    
    public List<nint> MemorySearch(uint processId, long desiredValue);
    public string WriteAddressValue(uint processId, nint targetPointer, long value);
    public List<nint> FilterPointers(uint processId, List<nint> pointers, long newValue);
    public List<(string title, uint processId)> GetAllWindows();
    public List<(string title, uint processId)> GetAllProcesses();
}
