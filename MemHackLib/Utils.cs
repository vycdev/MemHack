using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemHackLib;

public class Utils
{
    public static long BufferConvert(byte[] buffer, int offset, Type type) => type switch
    {
        var valueType when valueType == typeof(short) => BitConverter.ToInt16(buffer, offset),
        var valueType when valueType == typeof(int) => BitConverter.ToInt32(buffer, offset),
        var valueType when valueType == typeof(long) => BitConverter.ToInt64(buffer, offset),
        _ => BitConverter.ToInt32(buffer, offset),
    };

}
