using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Input;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayFrame : IBinarySerializable
    {
        public int                  PlayerId;
        public string               PlayerName;
        public Instrument           Instrument;
        public Difficulty           Difficulty;
        public BaseEngineParameters EngineParameters;
        public BaseStats            Stats;
        public int                  InputCount;
        public GameInput[]          Inputs;

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(PlayerId);
            writer.Write(PlayerName);
            writer.Write((byte) Instrument);
            writer.Write((byte) Difficulty);

            EngineParameters.Serialize(writer);
            Stats.Serialize(writer);

            writer.Write(InputCount);
            for (int i = 0; i < InputCount; i++)
            {
                writer.Write(Inputs[i].Time);
                writer.Write(Inputs[i].Action);
                writer.Write(Inputs[i].Integer);
            }
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            PlayerId = reader.ReadInt32();
            PlayerName = reader.ReadString();
            Instrument = (Instrument) reader.ReadByte();
            Difficulty = (Difficulty) reader.ReadByte();

            switch (Instrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                case GameMode.SixFretGuitar:
                    Stats = new GuitarStats();
                    EngineParameters = new GuitarEngineParameters();
                    break;
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                    Stats = new DrumsStats();
                    EngineParameters = new DrumsEngineParameters();
                    break;
                case GameMode.ProGuitar:
                case GameMode.ProKeys:
                case GameMode.Vocals:
                default:
                    Stats = new GuitarStats();
                    EngineParameters = new GuitarEngineParameters();
                    break;
            }

            EngineParameters.Deserialize(reader, version);
            Stats.Deserialize(reader, version);

            InputCount = reader.ReadInt32();

            Inputs = new GameInput[InputCount];
            for (int i = 0; i < InputCount; i++)
            {
                double time = reader.ReadDouble();
                int action = reader.ReadInt32();
                int value = reader.ReadInt32();

                Inputs[i] = new GameInput(time, action, value);
            }
        }
    }
}