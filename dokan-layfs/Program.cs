using DokanNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dokan_layfs
{
    class Program
    {
        static void Main(string[] args)
        {
            var fs = new LayeredFileSystem(@"D:\tmp\layfs\read", @"D:\tmp\layfs\write");
            fs.Mount("p:\\", DokanOptions.DebugMode, 3);
        }
    }
}
