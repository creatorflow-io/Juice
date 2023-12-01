using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Juice.Collections;
using Juice.Services;
using Xunit;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class OrderedConcurrentBagTest
    {
        private ITestOutputHelper _output;

        public OrderedConcurrentBagTest(ITestOutputHelper output)
        {
            _output = output;
        }

        private class TestObject : IEquatable<TestObject>, IComparable<TestObject>
        {
            public TestObject(int id, string name, DateTime dateTime)
            {
                Id = id;
                Name = name;
                DateTime = dateTime;
            }
            public int Id { get; }
            public string Name { get; }
            public DateTime DateTime { get; private set; }

            public void SetDateTime(DateTime dateTime)
            {
                DateTime = dateTime;
            }

            public int CompareTo(TestObject? other)
            {
                return other == null ? 1 : DateTime.CompareTo(other.DateTime);
            }
            public bool Equals(TestObject? other) => Id.Equals(other?.Id);
        }

        private class TestObjectComparer : IComparer<TestObject>
        {
            public static TestObjectComparer Default { get; } = new TestObjectComparer();
            public int Compare(TestObject? x, TestObject? y)
            {
                return x == null && y == null ? 0
                    : x != null && y == null ? 1
                    : x == null && y != null ? -1
                    : DateTime.Compare(x!.DateTime, y!.DateTime);
            }
        }

        [Fact]
        public void Data_should_ordered()
        {
            var cb = new OrderedConcurrentBag<TestObject>(TestObjectComparer.Default);

            List<Task> bagAddTasks = new List<Task>();
            for (int i = 0; i < 500; i++)
            {
                var item = new TestObject(i, $"TestObject {i}", DateTime.Now.AddSeconds(Random.Shared.NextInt64(60)));
                bagAddTasks.Add(Task.Run(() =>
                {
                    if (!cb.TryAdd(item))
                    {
                        _output.WriteLine("Failed to add item {0}", item.Id);
                    }
                }));
            }
            var clock = new Stopwatch();
            clock.Start();
            // Wait for all tasks to complete
            Task.WaitAll(bagAddTasks.ToArray());

            _output.WriteLine("Adding items took {0}ms", clock.ElapsedMilliseconds);

            //cb.Sort();

            var arr = cb.ToArray();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                Assert.True(TestObjectComparer.Default.Compare(arr[i], arr[i + 1]) <= 0);
            }

            // Consume the items in the bag
            List<Task> bagConsumeTasks = new List<Task>();
            int itemsInBag = 0;
            while (!cb.IsEmpty)
            {
                bagConsumeTasks.Add(Task.Run(() =>
                {
                    if (cb.TryTake(out var item))
                    {
                        _output.WriteLine("{0} {1}", item.Name, item.DateTime);
                        Interlocked.Increment(ref itemsInBag);
                    }
                }));
            }
            Task.WaitAll(bagConsumeTasks.ToArray());
            clock.Stop();
            _output.WriteLine("Total took {0}ms", clock.ElapsedMilliseconds);

            _output.WriteLine($"There were {itemsInBag} items in the bag");

            // Checks the bag for an item
            // The bag should be empty and this should not print anything
            if (cb.TryPeek(out var unexpectedItem))
            {
                _output.WriteLine("Found an item in the bag when it should be empty");
            }

        }

        [Fact]
        public void Data_should_order_manually()
        {
            var cb = new OrderedConcurrentBag<TestObject>();

            List<Task> bagAddTasks = new List<Task>();
            for (int i = 0; i < 500; i++)
            {
                var item = new TestObject(i, $"TestObject {i}", DateTime.Now.AddSeconds(Random.Shared.NextInt64(60)));
                bagAddTasks.Add(Task.Run(() =>
                {
                    if (!cb.TryAdd(item, false))
                    {
                        _output.WriteLine("Failed to add item {0}", item.Id);
                    }
                }));
            }
            var clock = new Stopwatch();
            clock.Start();
            // Wait for all tasks to complete
            Task.WaitAll(bagAddTasks.ToArray());

            cb.Sort();

            _output.WriteLine("Adding items took {0}ms", clock.ElapsedMilliseconds);


            var arr = cb.ToArray();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                Assert.True(TestObjectComparer.Default.Compare(arr[i], arr[i + 1]) <= 0);
            }

            // Consume the items in the bag
            List<Task> bagConsumeTasks = new List<Task>();
            int itemsInBag = 0;
            while (!cb.IsEmpty)
            {
                bagConsumeTasks.Add(Task.Run(() =>
                {
                    if (cb.TryTake(out var item))
                    {
                        _output.WriteLine("{0} {1}", item.Name, item.DateTime);
                        Interlocked.Increment(ref itemsInBag);
                    }
                }));
            }
            Task.WaitAll(bagConsumeTasks.ToArray());
            clock.Stop();
            _output.WriteLine("Total took {0}ms", clock.ElapsedMilliseconds);

            _output.WriteLine($"There were {itemsInBag} items in the bag");

            // Checks the bag for an item
            // The bag should be empty and this should not print anything
            if (cb.TryPeek(out var unexpectedItem))
            {
                _output.WriteLine("Found an item in the bag when it should be empty");
            }

        }

        [Fact]
        public void Data_should_order_after_update()
        {
            var cb = new OrderedConcurrentBag<TestObject>();

            List<Task> bagAddTasks = new List<Task>();
            for (int i = 0; i < 500; i++)
            {
                var item = new TestObject(i, $"TestObject {i}", DateTime.Now.AddSeconds(Random.Shared.NextInt64(60)));
                bagAddTasks.Add(Task.Run(() =>
                {
                    if (!cb.TryAdd(item, false))
                    {
                        _output.WriteLine("Failed to add item {0}", item.Id);
                    }
                }));
            }
            var clock = new Stopwatch();
            clock.Start();
            // Wait for all tasks to complete
            Task.WaitAll(bagAddTasks.ToArray());

            cb.Sort();

            _output.WriteLine("Adding items took {0}ms", clock.ElapsedMilliseconds);

            var arr = cb.ToArray();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                Assert.True(TestObjectComparer.Default.Compare(arr[i], arr[i + 1]) <= 0);
            }

            for (var i = 0; i < 200; i++)
            {
                var item = new TestObject(i, $"Updated TestObject {i}", DateTime.Now.AddMinutes(2));
                if (!cb.TryUpdate(item))
                {
                    _output.WriteLine("Failed to update item {0}. It does not exist.", item.Id);
                }
            }

            arr = cb.ToArray();
            for (int i = 0; i < arr.Length - 1; i++)
            {
                Assert.True(TestObjectComparer.Default.Compare(arr[i], arr[i + 1]) <= 0);
            }

            // Consume the items in the bag
            List<Task> bagConsumeTasks = new List<Task>();
            int itemsInBag = 0;
            while (!cb.IsEmpty)
            {
                bagConsumeTasks.Add(Task.Run(() =>
                {
                    if (cb.TryTake(out var item))
                    {
                        _output.WriteLine("{0} {1}", item.Name, item.DateTime);
                        Interlocked.Increment(ref itemsInBag);
                    }
                }));
            }
            Task.WaitAll(bagConsumeTasks.ToArray());
            clock.Stop();
            _output.WriteLine("Total took {0}ms", clock.ElapsedMilliseconds);

            _output.WriteLine($"There were {itemsInBag} items in the bag");

            // Checks the bag for an item
            // The bag should be empty and this should not print anything
            if (cb.TryPeek(out var unexpectedItem))
            {
                _output.WriteLine("Found an item in the bag when it should be empty");
            }

        }

        [Fact]
        public void GenKeys()
        {
            for (var i = 0; i < 3; i++)
            {
                var key = new DefaultStringIdGenerator().GenerateRandomId(8);
                _output.WriteLine(key.ToUpper());
            }
        }
    }
}
