using System;
using System.Collections.Generic;
using FluentAssertions;
using Juice.Extensions;
using Xunit;

namespace Juice.Core.Tests
{
    public class DictionaryExtensionsTest
    {
        [Fact]
        public void GetOptionTest()
        {
            IDictionary<string, object?> d = new Dictionary<string, object?>() {
                { "Bool", "True"},
                { "False", "False"},
                { "Bool1", true},
                { "Id", Guid.NewGuid() }
            };
            var reference = new Dictionary<string, object?>()
            {
                { "$Id1", Guid.NewGuid().ToString() },
                { "False1", false }
            };
            Assert.True(d.GetOption<bool>("Bool"));
            Assert.True(d.GetOption<bool>("Bool1"));
            Assert.False(d.GetOption<bool>("False"));
            Assert.False(d.GetOption<bool>("False1"));
            var id = d.GetOption<Guid?>("Id");
            id.Should().NotBeNull();
            var id1 = d.GetOption<Guid>("$Id1", default, reference);
            id1.Should().NotBe(Guid.Empty);
        }
    }
}
