using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ExploringDynamicDeserialization {
    class Program {
        static void Main(string[] args) {

            //read in a test JSON file
            var json = File.ReadAllText("partial1.json");

            //build the converter
            var options = new JsonSerializerOptions();
            options.Converters.Add(new DynamicJsonConverter<Address>());

            //test (uncached) creation and use of projection types
            var sw = new Stopwatch();
            sw.Start();
            var d = JsonSerializer.Deserialize<List<dynamic>>(json, options);
            sw.Stop();
            Console.WriteLine("Uncached ... Elapsed={0}", sw.Elapsed);

            //test cached retrieval and use of projection types
            var sw2 = new Stopwatch();
            sw2.Start();
            var _ = JsonSerializer.Deserialize<List<dynamic>>(json, options);
            sw2.Stop();
            Console.WriteLine("Cached ... Elapsed={0}", sw2.Elapsed);

            //try patching a fully typed object
            var addr3 = new Address { StreetAddress = "123 Main" };
            Projection<Address>.Patch(d[0], addr3);

            //serialize the objects.
            var jsonOut = JsonSerializer.Serialize(d, new JsonSerializerOptions { WriteIndented = true });
            var json2Out = JsonSerializer.Serialize(addr3, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonOut);
            Console.WriteLine(json2Out);
        }
    }
}
