using System;

namespace YARG.Core.Song
{
    internal enum ScanResult
    {
        Success,
        DirectoryError,
        DuplicateFilesFound,
        IniEntryCorruption,
        NoName,
        NoNotes,
        DTAError,
        MoggError,
        MoggError_Update,
        UnsupportedEncryption,
        MissingCONMidi,
        PossibleCorruption,
        FailedSngLoad,

        InvalidResolution,
        InvalidResolution_Update,
        InvalidResolution_Upgrade,

        NoAudio,
        PathTooLong,
        MultipleMidiTrackNames,
        MultipleMidiTrackNames_Update,
        MultipleMidiTrackNames_Upgrade,

        LooseChart_Warning,
    }

    internal struct ScanUnexpected
    {
        private ScanResult _error;
        public readonly ScanResult Error => _error;

        public ScanUnexpected(ScanResult error)
        {
            _error = error;
        }
    }

    internal struct ScanExpected<T>
    {
        private ScanResult _result;
        private T _value;

        public readonly bool HasValue => _result == ScanResult.Success;

        public readonly T Value
        {
            get
            {
                if (_result == ScanResult.Success)
                {
                    return _value;
                }
                throw new InvalidOperationException();
            }
        }

        public readonly ScanResult Error => _result;

        public ScanExpected(in T value)
        {
            _value = value;
            _result = ScanResult.Success;
        }

        public ScanExpected(in ScanUnexpected unexpected)
        {
            _result = unexpected.Error;
            _value = default!;
        }

        public static implicit operator bool(in ScanExpected<T> expected) => expected.HasValue;
        public static implicit operator ScanExpected<T>(in T value) => new(in value);
        public static implicit operator ScanExpected<T>(in ScanUnexpected unexpected) => new(in unexpected);
    }
}
