using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemHackMe;
public class PointerNode
{
    public IntPtr Address { get; set; }
    public List<PointerNode> Nodes { get; set; } = [];
}
