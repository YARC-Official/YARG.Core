// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperEngine
{
    public static class EnumX<EnumType> where EnumType : Enum
    {
        public static readonly EnumType[] Values = (EnumType[])Enum.GetValues(typeof(EnumType));
        public static int Count => Values.Length;
    }
}
