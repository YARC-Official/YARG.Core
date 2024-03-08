using System;
using System.Globalization;
using Newtonsoft.Json;
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
            if (reader.Value == null)
            {
                return new HashWrapper();
            }

            try
            {
                var hashString = reader.Value.ToString().AsSpan();
                var hashBytes = new byte[HashWrapper.HASH_SIZE_IN_BYTES];

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    var b = byte.Parse(hashString.Slice(i * 2, 2), NumberStyles.AllowHexSpecifier);
                    hashBytes[i] = b;
                }

                return new HashWrapper(hashBytes);
            }
            catch
            {
                return new HashWrapper();
            }
        }
    }
}