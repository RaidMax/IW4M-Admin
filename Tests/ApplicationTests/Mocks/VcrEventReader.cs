using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationTests.Mocks
{
    class VcrEventReader : IGameLogReader
    {
        public long Length => throw new NotImplementedException();

        public int UpdateInterval => throw new NotImplementedException();

        public Task<IEnumerable<GameEvent>> ReadEventsFromLog(long fileSizeDiff, long startPosition)
        {
            throw new NotImplementedException();
        }
    }
}
