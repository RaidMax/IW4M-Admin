using ApplicationTests.Fixtures;
using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Database;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using StatsWeb;
using StatsWeb.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ApplicationTests
{
    [TestFixture]
    public class StatsWebTests
    {
        private IServiceProvider serviceProvider;
        private DatabaseContext dbContext;
        private ChatResourceQueryHelper queryHelper;

        ~StatsWebTests()
        {
            dbContext.Dispose();
        }

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .AddSingleton<ChatResourceQueryHelper>()
                .BuildBase()
                .BuildServiceProvider();

            SetupDatabase();

            queryHelper = serviceProvider.GetRequiredService<ChatResourceQueryHelper>();
        }

        private void SetupDatabase()
        {
            var contextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            dbContext = contextFactory.CreateContext();
        }

        #region PARSE_SEARCH_INFO
        [Test]
        public void Test_ParseSearchInfo_SanityChecks()
        {
            var query = "chat|".ParseSearchInfo(-1, -1);

            Assert.AreEqual(0, query.Count);
            Assert.AreEqual(0, query.Offset);

            query = "chat|".ParseSearchInfo(int.MaxValue, int.MaxValue);

            Assert.Greater(int.MaxValue, query.Count);
        }

        [Test]
        public void Test_ParseSearchInfo_BeforeFilter_Happy()
        {
            var now = DateTime.Now;
            var date = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            var query = $"chat|before {date.ToString()}".ParseSearchInfo(0, 0);

            Assert.AreEqual(date, query.SentBefore);
        }

        [Test]
        public void Test_ParseSearchInfo_AfterFilter_Happy()
        {
            var now = DateTime.Now;
            var date = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
            var query = $"chat|after {date.ToString()}".ParseSearchInfo(0, 0);

            Assert.AreEqual(date, query.SentAfter);
        }

        [Test]
        public void Test_ParseSearchInfo_ServerFilter_Happy()
        {
            string serverId = "127.0.0.1:28960";
            var query = $"chat|server {serverId}".ParseSearchInfo(0, 0);

            Assert.AreEqual(serverId, query.ServerId);
        }

        [Test]
        public void Test_ParseSearchInfo_ClientFilter_Happy()
        {
            int clientId = 123;
            var query = $"chat|client {clientId.ToString()}".ParseSearchInfo(0, 0);

            Assert.AreEqual(clientId, query.ClientId);
        }

        [Test]
        public void Test_ParseSearchInfo_ContainsFilter_Happy()
        {
            string content = "test";
            var query = $"chat|contains {content}".ParseSearchInfo(0, 0);

            Assert.AreEqual(content, query.MessageContains);
        }

        [Test]
        public void Test_ParseSearchInfo_SortFilter_Happy()
        {
            var direction = SortDirection.Ascending;
            var query = $"chat|sort {direction.ToString().ToLower()}".ParseSearchInfo(0, 0);

            Assert.AreEqual(direction, query.Direction);

            direction = SortDirection.Descending;
            query = $"chat|sort {direction.ToString().ToLower()}".ParseSearchInfo(0, 0);

            Assert.AreEqual(direction, query.Direction);
        }

        [Test]
        public void Test_ParseSearchInfo_InvalidQueryType()
        {
            Assert.Throws<ArgumentException>(() => "player|test".ParseSearchInfo(0, 0));
        }

        [Test]
        public void Test_ParseSearchInfo_NoQueryType()
        {
            Assert.Throws<ArgumentException>(() => "".ParseSearchInfo(0, 0));
        }
        #endregion]

        #region CHAT_RESOURCE_QUERY_HELPER
        [Test]
        public void Test_ChatResourceQueryHelper_Invalid()
        {
            var helper = serviceProvider.GetRequiredService<ChatResourceQueryHelper>();

            Assert.ThrowsAsync<ArgumentException>(() => helper.QueryResource(null));
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_SentAfter()
        {
            var oneHourAhead = DateTime.Now.AddHours(1);
            var msg = MessageGenerators.GenerateMessage(sent: oneHourAhead);

            dbContext.Set<EFClientMessage>()
                .Add(msg);
            await dbContext.SaveChangesAsync();

            var query = $"chat|after {DateTime.Now.ToString()}".ParseSearchInfo(1, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.AreEqual(oneHourAhead, result.Results.First().Date);

            dbContext.Remove(msg);
            await dbContext.SaveChangesAsync();
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_SentBefore()
        {
            var oneHourAgo = DateTime.Now.AddHours(-1);
            var msg = MessageGenerators.GenerateMessage(sent: oneHourAgo);

            dbContext.Set<EFClientMessage>()
                .Add(msg);
            await dbContext.SaveChangesAsync();

            var query = $"chat|before {DateTime.Now.ToString()}".ParseSearchInfo(1, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.AreEqual(oneHourAgo, result.Results.First().Date);

            dbContext.Remove(msg);
            await dbContext.SaveChangesAsync();
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_Server()
        {
            var msg = MessageGenerators.GenerateMessage(sent: DateTime.Now);

            dbContext.Set<EFClientMessage>()
                .Add(msg);
            await dbContext.SaveChangesAsync();

            string serverId = msg.Server.EndPoint;
            var query = $"chat|server {serverId}".ParseSearchInfo(1, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.IsNotEmpty(result.Results);

            dbContext.Remove(msg);
            await dbContext.SaveChangesAsync();
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_Client()
        {
            var msg = MessageGenerators.GenerateMessage(sent: DateTime.Now);

            dbContext.Set<EFClientMessage>()
                .Add(msg);
            await dbContext.SaveChangesAsync();

            int clientId = msg.Client.ClientId;
            var query = $"chat|client {clientId}".ParseSearchInfo(1, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.AreEqual(clientId, result.Results.First().ClientId);

            dbContext.Remove(msg);
            await dbContext.SaveChangesAsync();
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_Contains()
        {
            var msg = MessageGenerators.GenerateMessage(sent: DateTime.Now);
            msg.Message = "this is a test";

            dbContext.Set<EFClientMessage>()
                .Add(msg);
            await dbContext.SaveChangesAsync();

            var query = $"chat|contains {msg.Message}".ParseSearchInfo(1, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.AreEqual(msg.Message, result.Results.First().Message);

            dbContext.Remove(msg);
            await dbContext.SaveChangesAsync();
        }

        [Test]
        public async Task Test_ChatResourceQueryHelper_Sort()
        {
            var firstMessage = MessageGenerators.GenerateMessage(sent: DateTime.Now.AddHours(-1));
            var secondMessage = MessageGenerators.GenerateMessage(sent: DateTime.Now);

            dbContext.Set<EFClientMessage>()
                .Add(firstMessage);
            dbContext.Set<EFClientMessage>()
                .Add(secondMessage);
            await dbContext.SaveChangesAsync();

            var query = $"chat|sort {SortDirection.Ascending}".ParseSearchInfo(2, 0);
            var result = await queryHelper.QueryResource(query);

            Assert.AreEqual(firstMessage.TimeSent, result.Results.First().Date);
            Assert.AreEqual(secondMessage.TimeSent, result.Results.Last().Date);

            query = $"chat|sort {SortDirection.Descending}".ParseSearchInfo(2, 0);
            result = await queryHelper.QueryResource(query);

            Assert.AreEqual(firstMessage.TimeSent, result.Results.Last().Date);
            Assert.AreEqual(secondMessage.TimeSent, result.Results.First().Date);

            dbContext.Remove(firstMessage);
            dbContext.Remove(secondMessage);
            await dbContext.SaveChangesAsync();
        }
        #endregion
    }
}
