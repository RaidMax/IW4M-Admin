using IW4MAdmin.Application;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tests
{
    internal static class TestHelpers
    {
        internal static void EmulateClientJoinLog(this Server svr)
        {
            long guid = svr.ClientNum + 1;
            File.AppendAllText(svr.LogPath, $"0:00 J;{guid};{svr.ClientNum};test_client_{svr.ClientNum}\r\n");
        }

        internal static void EmulateClientQuitLog(this Server svr)
        {
            long guid = Math.Max(1, svr.ClientNum);
            File.AppendAllText(svr.LogPath, $"0:00 Q;{guid};{svr.ClientNum};test_client_{svr.ClientNum}\r\n");
        }
    }
}
