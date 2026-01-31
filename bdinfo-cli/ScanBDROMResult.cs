using System;
using System.Collections.Generic;

namespace BDInfo
{
    public class ScanBDROMResult
    {
        public Exception? ScanException { get; set; }
        public Dictionary<string, Exception> FileExceptions { get; } = new Dictionary<string, Exception>();
    }
}
