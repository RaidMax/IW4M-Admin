using ApplicationTests.Mocks;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;

namespace ApplicationTests
{
    public class ScriptPluginServiceResolverTests
    {
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .BuildBase()
                .AddSingleton<ScriptPluginServiceResolver>()
                .AddSingleton<IScriptResolverMock, ScriptResolverMock>()
                .AddSingleton(new ScriptResolverMock {  Value = "test" })
                .AddSingleton<IScriptResolverGenericMock<int, string>, ScriptResolverGenericMock<int,string>>()
                .AddSingleton(new ScriptResolverGenericMock<int, string> { Value = 123, Value2 = "test" })
                .BuildServiceProvider();
        }

        [Test]
        public void Test_ResolveType()
        {
            var resolver = serviceProvider.GetService<ScriptPluginServiceResolver>();
            var expectedResolvedService = serviceProvider.GetService<ScriptResolverMock>();
            var resolvedService = resolver.ResolveService(nameof(ScriptResolverMock));

            Assert.AreEqual(expectedResolvedService, resolvedService);
        }

        [Test]
        public void Test_ResolveType_Interface()
        {
            var resolver = serviceProvider.GetService<ScriptPluginServiceResolver>();
            var expectedResolvedService = serviceProvider.GetService<IScriptResolverMock>();
            var resolvedService = resolver.ResolveService(nameof(IScriptResolverMock));

            Assert.AreEqual(expectedResolvedService, resolvedService);
        }

        [Test]
        public void Test_ResolveGenericType()
        {
            var resolver = serviceProvider.GetService<ScriptPluginServiceResolver>();
            var expectedResolvedService = serviceProvider.GetService<ScriptResolverGenericMock<int, string>>();
            var resolvedService = resolver.ResolveService("ScriptResolverGenericMock", new[] { "Int32", "String" });

            Assert.AreEqual(expectedResolvedService, resolvedService);
        }

        [Test]
        public void Test_ResolveGenericType_Interface()
        {
            var resolver = serviceProvider.GetService<ScriptPluginServiceResolver>();
            var expectedResolvedService = serviceProvider.GetService<IScriptResolverGenericMock<int, string>>();
            var resolvedService = resolver.ResolveService("IScriptResolverGenericMock", new[] { "Int32", "String" });

            Assert.AreEqual(expectedResolvedService, resolvedService);
        }
    }
}
