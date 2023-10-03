// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal abstract class ChartObject : SongObject
    {
// Non-nullable field 'chart' must contain a non-null value when exiting constructor
// 'chart' is assigned externally as part of this object being added to a chart
#pragma warning disable 8618
        public ChartObject(uint position) : base(position) { }
#pragma warning restore 8618

        // Clone needs to be hideable so it can return a different type in derived classes
        protected override SongObject SongClone() => ChartClone();
        protected abstract ChartObject ChartClone();
        public new ChartObject Clone() => ChartClone();
    }
}
