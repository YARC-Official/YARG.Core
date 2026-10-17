using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YARG.Core.Song;

namespace YARG.Core.Utility
{
    public class JsonHashWrapperConverter : JsonConverter<HashWrapper>
    {
        public override void WriteJson(JsonWriter writer, HashWrapper value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override HashWrapper ReadJson(JsonReader reader, Type objectType, HashWrapper existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String && reader.Value is string hashString)
            {
                return HashWrapper.FromString(hashString.AsSpan());
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                // Handle the broken JSON careers were emitting for a while
                var legacyObject = JObject.Load(reader);
                var hashBytesToken = legacyObject["HashBytes"];
                if (hashBytesToken?.Type != JTokenType.String)
                {
                    return default;
                }

                var base64Hash = hashBytesToken.Value<string>();
                if (base64Hash == null)
                {
                    return default;
                }

                try
                {
                    var hashBytes = Convert.FromBase64String(base64Hash);
                    return hashBytes.Length == HashWrapper.HASH_SIZE_IN_BYTES
                        ? HashWrapper.Create(hashBytes)
                        : default;
                }
                catch (FormatException)
                {
                    return default;
                }
            }

            return default;
        }
    }
}