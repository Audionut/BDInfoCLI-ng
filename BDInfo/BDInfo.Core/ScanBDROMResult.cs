using System;
using System.Collections.Generic;

namespace BDInfo
{
    public class ScanBDROMResult
    {
        public Exception ScanException { get; set; } = null!;
        public Dictionary<string, Exception> FileExceptions { get; } = new Dictionary<string, Exception>();
    }
}
