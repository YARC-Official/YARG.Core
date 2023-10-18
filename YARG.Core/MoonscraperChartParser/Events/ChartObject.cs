// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal abstract class ChartObject : SongObject
    {
        public ChartObject(uint position) : base(position) { }

        // Clone needs to be hideable so it can return a different type in derived classes
        protected override SongObject SongClone() => ChartClone();
        protected abstract ChartObject ChartClone();
        public new ChartObject Clone() => ChartClone();
    }
}
