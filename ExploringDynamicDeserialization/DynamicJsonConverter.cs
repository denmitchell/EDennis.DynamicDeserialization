using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExploringDynamicDeserialization {
    /// <summary>
    /// Temp Dynamic Converter
    /// by:tchivs@live.cn
    /// </summary>
    public class DynamicJsonConverter<TEntity> : JsonConverter<dynamic> {
        public override dynamic Read(ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) {

            if (reader.TokenType == JsonTokenType.True) {
                return true;
            }

            if (reader.TokenType == JsonTokenType.False) {
                return false;
            }

            if (reader.TokenType == JsonTokenType.Number) {
                if (reader.TryGetInt64(out long l)) {
                    return l;
                }

                return reader.GetDouble();
            }

            if (reader.TokenType == JsonTokenType.String) {
                if (reader.TryGetDateTime(out DateTime datetime)) {
                    return datetime;
                }

                return reader.GetString();
            }

            if (reader.TokenType == JsonTokenType.StartObject) {
                using JsonDocument documentV = JsonDocument.ParseValue(ref reader);
                return ReadObject(documentV.RootElement);
            }
            // Use JsonElement as fallback.
            // Newtonsoft uses JArray or JObject.
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        private object ReadObject(JsonElement jsonElement) {
            var props = jsonElement.EnumerateObject().Select(x => x.Name).ToArray();
            var type = AnonymousTypes<TEntity>.GetOrAddType(props);
            var data = jsonElement.GetRawText();
            return JsonSerializer.Deserialize(data, type);            
        }
        public override void Write(Utf8JsonWriter writer,
            object value, JsonSerializerOptions options) {
        }
    }
}
