// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    public abstract class ChartObject : SongObject
    {
        [NonSerialized]
        public MoonChart moonChart;

        public ChartObject(uint position) : base(position) { }
    }
}
