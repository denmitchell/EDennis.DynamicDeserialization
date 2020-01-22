using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ExploringDynamicDeserialization {
    class Program {
        static void Main(string[] args) {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DynamicJsonConverter<Address>());
            var json = File.ReadAllText("partial1.json");

            var sw = new Stopwatch();
            sw.Start();
            var d = JsonSerializer.Deserialize<List<dynamic>>(json, options);
            sw.Stop();
            Console.WriteLine("Elapsed={0}", sw.Elapsed);

            var sw2 = new Stopwatch();
            sw2.Start();
            var d2 = JsonSerializer.Deserialize<List<dynamic>>(json, options);
            sw2.Stop();
            Console.WriteLine("Elapsed={0}", sw2.Elapsed);

            var addr3 = new Address { StreetAddress = "123 Main" };

            //foreach (var prop in d[0].GetType().GetProperties())
            //    Dynonymous<Address>.Properties[prop.Name].SetValue(addr3, prop.GetValue(d[0]));

            Projection<Address>.Patch(d[0], addr3);

            var jsonOut = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
            var json2Out = JsonSerializer.Serialize(addr3, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonOut);
            Console.WriteLine(json2Out);
        }
    }
}
