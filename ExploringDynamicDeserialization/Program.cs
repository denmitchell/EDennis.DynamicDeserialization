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


            var jsonOut = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonOut);
        }
    }
}
