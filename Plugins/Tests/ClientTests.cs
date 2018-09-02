using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Tests
{
    public class ClientTests
    {
        [Fact]
        public void SetAdditionalPropertyShouldSucceed()
        {
            var client = new Player();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);
        }

        [Fact]
        public void GetAdditionalPropertyShouldSucceed()
        {
            var client = new Player();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);

            Assert.True(client.GetAdditionalProperty<int>("NewProp") == 5, "added property does not match retrieved property");
        }
    }
}
