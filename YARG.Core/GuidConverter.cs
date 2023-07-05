using System;
using Newtonsoft.Json;

namespace YARG.Core
{
    public class GuidConverter : JsonConverter<Guid>
    {
        public override void WriteJson(JsonWriter writer, Guid value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString("N"));
        }

        public override Guid ReadJson(JsonReader reader, Type objectType, Guid existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.Value == null)
            {
                return Guid.Empty;
            }

            string value = reader.Value.ToString();
            return Guid.Parse(value);
        }

        public override bool CanRead => true;
    }
}