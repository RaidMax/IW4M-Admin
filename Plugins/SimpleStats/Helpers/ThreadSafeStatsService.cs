using SharedLibrary.Services;
using StatsPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatsPlugin.Helpers
{
    public class ThreadSafeStatsService
    {

        public GenericRepository<EFClientStatistics> ClientStatSvc { get; private set; }
        public GenericRepository<EFServer> ServerSvc { get; private set; }
        public GenericRepository<EFClientKill> KillStatsSvc { get; private set; }
        public GenericRepository<EFServerStatistics> ServerStatsSvc { get; private set; }
        public GenericRepository<EFClientMessage> MessageSvc { get; private set; }

        public ThreadSafeStatsService()
        {
            ClientStatSvc = new GenericRepository<EFClientStatistics>();
            ServerSvc = new GenericRepository<EFServer>();
            KillStatsSvc = new GenericRepository<EFClientKill>();
            ServerStatsSvc = new GenericRepository<EFServerStatistics>();
            MessageSvc = new GenericRepository<EFClientMessage>();
        }
    }
}
