
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Text;
using Juice.XUnit;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;

namespace Juice.Core.Tests
{
    public class JsonTest(ITestOutputHelper output)
    {
        private System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        [IgnoreOnCIFact(DisplayName = "Test Json"), TestPriority(1)]
        public void TestJson()
        {
            var json = "{\"name\":\"John Doe\",\"age\":30,\"city\":\"New York\"}";

            var persons = new List<Person>();
            var batchsize = 1000;
            for (int i = 0; i < batchsize; i++)
            {
                persons.Add(new Person
                {
                    Name = $"John Doe {i}",
                    Age = 30,
                    City = "New York"
                });
            }

            var bigDymanicObject = new Dictionary<string, object>();
            for (int i = 0; i < 1000; i++)
            {
                bigDymanicObject.Add($"key{i}", $"value{i}");
            }

            var _ = System.Text.Json.JsonSerializer.Deserialize<Person>(json, options);
            _ = Newtonsoft.Json.JsonConvert.DeserializeObject<Person>(json);

            var timer = Stopwatch.StartNew();

            var person = SystemJsonDeserialize<Person>(json, timer, "person");

            person = NewtonsoftJsonDeserialize<Person>(json, timer, "person");

            output.WriteLine("");

            SystemJsonSerialize(person, timer);

            NewtonsoftJsonSerialize(person, timer);

            output.WriteLine("");

            SystemJsonSerialize(persons, timer, "1000 persons");

            json = NewtonsoftJsonSerialize(persons, timer, "1000 persons");

            output.WriteLine("");

            SystemJsonDeserialize<List<Person>>(json, timer, "1000 persons");

            NewtonsoftJsonDeserialize<List<Person>>(json, timer, "1000 persons");

            output.WriteLine("");

            SystemJsonSerialize(bigDymanicObject, timer);

            json = NewtonsoftJsonSerialize(bigDymanicObject, timer);

            output.WriteLine("");

            var dict = SystemJsonDeserialize<Dictionary<string, object>>(json, timer, "Dictionary<string, object>");

            NewtonsoftJsonDeserialize<Dictionary<string, object>>(json, timer, "Dictionary<string, object>");

            output.WriteLine("");

            ExpandoObject obj = new ExpandoObject();
            obj.TryAdd("name", "John Doe");
            obj.TryAdd("age", 30);
            obj.TryAdd("city", "New York");

            // add 1000 properties
            for (int i = 0; i < 1000; i++)
            {
                obj.TryAdd($"key{i}", $"value{i}");
            }

            SystemJsonSerialize(obj, timer, "ExpandoObject");
            json = NewtonsoftJsonSerialize(obj, timer, "ExpandoObject");
            output.WriteLine("");

            SystemJsonDeserialize<ExpandoObject>(json, timer, "ExpandoObject");
            NewtonsoftJsonDeserialize<ExpandoObject>(json, timer, "ExpandoObject");
            output.WriteLine("");

            JObject jObject = new JObject();
            jObject.Add("name", "John Doe");
            jObject.Add("age", 30);
            jObject.Add("city", "New York");
            // add 1000 properties
            for (int i = 0; i < 1000; i++)
            {
                jObject.Add($"key{i}", $"value{i}");
            }

            SystemJsonSerialize(jObject, timer, "JObject");
            json = NewtonsoftJsonSerialize(jObject, timer, "JObject");

            output.WriteLine("");

            NewtonsoftJsonDeserialize<JObject>(json, timer, "JObject");

            output.WriteLine("");

            SystemJsonSerializeUtf8Bytes(jObject, timer);
            NewtonsoftJsonSerializeUtf8Bytes(jObject, timer);
        }

        [IgnoreOnCIFact(DisplayName = "Test Json reversed order"), TestPriority(1)]
        public void TestJson1()
        {
            var json = "{\"name\":\"John Doe\",\"age\":30,\"city\":\"New York\"}";

            var persons = new List<Person>();
            var batchsize = 1000;
            for (int i = 0; i < batchsize; i++)
            {
                persons.Add(new Person
                {
                    Name = $"John Doe {i}",
                    Age = 30,
                    City = "New York"
                });
            }

            var bigDymanicObject = new Dictionary<string, object>();
            for (int i = 0; i < 1000; i++)
            {
                bigDymanicObject.Add($"key{i}", $"value{i}");
            }

            var _ = System.Text.Json.JsonSerializer.Deserialize<Person>(json, options);
            _ = Newtonsoft.Json.JsonConvert.DeserializeObject<Person>(json);

            var timer = Stopwatch.StartNew();


            var person = NewtonsoftJsonDeserialize<Person>(json, timer, "person");
            person = SystemJsonDeserialize<Person>(json, timer, "person");

            output.WriteLine("");

            NewtonsoftJsonSerialize(person, timer);
            SystemJsonSerialize(person, timer);

            output.WriteLine("");

            NewtonsoftJsonSerialize(persons, timer, "1000 persons");
            json = SystemJsonSerialize(persons, timer, "1000 persons");

            output.WriteLine("");

            NewtonsoftJsonDeserialize<List<Person>>(json, timer, "1000 persons");
            SystemJsonDeserialize<List<Person>>(json, timer, "1000 persons");

            output.WriteLine("");


            NewtonsoftJsonSerialize(bigDymanicObject, timer);
            json = SystemJsonSerialize(bigDymanicObject, timer);

            output.WriteLine("");

            var dict = NewtonsoftJsonDeserialize<Dictionary<string, object>>(json, timer, "Dictionary<string, object>");

            SystemJsonDeserialize<Dictionary<string, object>>(json, timer, "Dictionary<string, object>");

            output.WriteLine("");

            ExpandoObject obj = new ExpandoObject();
            obj.TryAdd("name", "John Doe");
            obj.TryAdd("age", 30);
            obj.TryAdd("city", "New York");

            // add 1000 properties
            for (int i = 0; i < 1000; i++)
            {
                obj.TryAdd($"key{i}", $"value{i}");
            }

            NewtonsoftJsonSerialize(obj, timer, "ExpandoObject");
            json = SystemJsonSerialize(obj, timer, "ExpandoObject");

            output.WriteLine("");

            NewtonsoftJsonDeserialize<ExpandoObject>(json, timer, "ExpandoObject");
            SystemJsonDeserialize<ExpandoObject>(json, timer, "ExpandoObject");


            output.WriteLine("");

            JObject jObject = new JObject();
            jObject.Add("name", "John Doe");
            jObject.Add("age", 30);
            jObject.Add("city", "New York");
            // add 1000 properties
            for (int i = 0; i < 1000; i++)
            {
                jObject.Add($"key{i}", $"value{i}");
            }

            json = NewtonsoftJsonSerialize(jObject, timer, "JObject");
            output.WriteLine(json);
            json = SystemJsonSerialize(jObject, timer, "JObject");

            output.WriteLine(json);
            output.WriteLine("");

            NewtonsoftJsonDeserialize<JObject>(json, timer, "JObject");
        }


        private string SystemJsonSerialize<T>(
            T? obj, Stopwatch timer, [CallerArgumentExpression("obj")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            var json1 = System.Text.Json.JsonSerializer.Serialize(obj);
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"System.Text.Json.JsonSerializer.Serialize {name}: {elapsed}us {allocatedMemory}kb");
            return json1;
        }

        private string NewtonsoftJsonSerialize<T>(
            T? obj, Stopwatch timer, [CallerArgumentExpression("obj")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            var json1 = Newtonsoft.Json.JsonConvert.SerializeObject(obj);
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"Newtonsoft.Json.JsonConvert.SerializeObject {name}: {elapsed}us {allocatedMemory}kb");
            return json1;
        }

        private T? SystemJsonDeserialize<T>(
            string json, Stopwatch timer, [CallerArgumentExpression("json")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            var obj = System.Text.Json.JsonSerializer.Deserialize<T>(json, options);
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"System.Text.Json.JsonSerializer.Deserialize {name}: {elapsed}us {allocatedMemory}kb");
            return obj;
        }

        private T? NewtonsoftJsonDeserialize<T>(
            string json, Stopwatch timer, [CallerArgumentExpression("json")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"Newtonsoft.Json.JsonConvert.DeserializeObject {name}: {elapsed}us {allocatedMemory}kb");
            return obj;
        }

        private void SystemJsonSerializeUtf8Bytes<T>(
            T? obj, Stopwatch timer, [CallerArgumentExpression("obj")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            var json1 = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj);
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"System.Text.Json.JsonSerializer.SerializeToUtf8Bytes {name}: {elapsed}us {allocatedMemory}kb");
        }

        private void NewtonsoftJsonSerializeUtf8Bytes<T>(
            T? obj, Stopwatch timer, [CallerArgumentExpression("obj")] string? name = null)
        {
            name = name ?? typeof(T).Name;
            timer.Restart();
            var allocatedMemory = GC.GetTotalMemory(true);
            // create a bytes array from the json string
            var json1 = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
            allocatedMemory = (GC.GetTotalMemory(true) - allocatedMemory) / 1024;
            var elapsed = timer.Elapsed.TotalMicroseconds;
            output.WriteLine($"Newtonsoft.Json.JsonConvert.SerializeToUtf8Bytes {name}: {elapsed}us {allocatedMemory}kb");
        }

        internal class Person
        {
            public required string Name { get; set; }
            public int Age { get; set; }
            public required string City { get; set; }
        }
    }
}
