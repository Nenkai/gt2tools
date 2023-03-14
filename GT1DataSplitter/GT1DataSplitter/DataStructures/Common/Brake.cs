﻿using System.Collections.Generic;
using System.Runtime.InteropServices;
using CsvHelper.Configuration;

namespace GT1.DataSplitter
{
    using TypeConverters;

    public class Brake : CsvDataStructure<BrakeData, BrakeCSVMap>
    {
        public Brake()
        {
            Header = "BRAKE";
            StringTableCount = 2;
        }

        protected override string CreateOutputFilename() => CreateDetailedOutputFilename(0x4).Replace(".dat", ".csv");
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] // 0x18
    public struct BrakeData
    {
        public byte Unknown;
        public byte Unknown2;
        public byte Unknown3;
        public byte Padding;
        public ushort CarID;
        public ushort Padding2;
        public uint Price;
        public ushort NamePart1;
        public ushort Padding3;
        public ushort NamePart2;
        public ushort UnknownAlways1;
        public uint Padding4;
    }

    public sealed class BrakeCSVMap : ClassMap<BrakeData>
    {
        public BrakeCSVMap(List<List<string>> tables)
        {
            Map(m => m.Unknown);
            Map(m => m.Unknown2);
            Map(m => m.Unknown3);
            Map(m => m.CarID);
            Map(m => m.Price);
            Map(m => m.NamePart1).TypeConverter(new StringTableLookup(tables[0]));
            Map(m => m.NamePart2).TypeConverter(new StringTableLookup(tables[1]));
            Map(m => m.UnknownAlways1);
        }
    }
}