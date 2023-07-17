// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal abstract class SyncTrack : SongObject
    {
        public SyncTrack(uint _position) : base(_position) { }

        // Clone needs to be hideable so it can return a different type in derived classes
        protected override SongObject SongClone() => SyncClone();
        protected abstract SyncTrack SyncClone();
        public new SyncTrack Clone() => SyncClone();
    }
}
