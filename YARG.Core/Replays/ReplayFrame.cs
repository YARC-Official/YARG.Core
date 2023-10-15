using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;
using YARG.Core.Input;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayFrame : IBinarySerializable
    {
        public ReplayPlayerInfo     PlayerInfo;
        public BaseEngineParameters EngineParameters;
        public BaseStats            Stats;
        public int                  InputCount;
        public GameInput[]          Inputs;

        public ReplayFrame()
        {
            EngineParameters = new GuitarEngineParameters();
            Stats = new GuitarStats();
            Inputs = Array.Empty<GameInput>();
        }

        public ReplayFrame(BinaryReader reader, int version = 0)
        {
            Deserialize(reader, version);
        }

        public void Serialize(BinaryWriter writer)
        {
            PlayerInfo.Serialize(writer);
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

        [MemberNotNull(nameof(EngineParameters))]
        [MemberNotNull(nameof(Stats))]
        [MemberNotNull(nameof(Inputs))]
        public void Deserialize(BinaryReader reader, int version = 0)
        {
            PlayerInfo = new ReplayPlayerInfo(reader, version);

            switch (PlayerInfo.Profile.CurrentInstrument.ToGameMode())
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
                case GameMode.Vocals:
                    Stats = new VocalsStats();
                    EngineParameters = new VocalsEngineParameters();
                    break;
                default:
                    throw new InvalidOperationException("Stat creation not implemented.");
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