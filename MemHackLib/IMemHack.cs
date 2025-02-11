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

    public static List<nint> MemorySearch(uint processId, long desiredValue);

    public static string WriteAddressValue(uint processId, nint targetPointer, long value);

    public static List<nint> FilterPointers(uint processId, List<nint> pointers, long newValue);


    public static List<(nint hWnd, string title, uint processId)> GetAllWindows();

}
