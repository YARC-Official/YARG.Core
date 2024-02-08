namespace YARG.Core.Utility
{
    public interface IBinarySerializable
    {

        public void Serialize(IBinaryDataWriter writer);

        public void Deserialize(IBinaryDataReader reader, int version = 0);

    }
}