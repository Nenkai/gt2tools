﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace GT2.DataSplitter
{
    public class CsvDataStructure<TStructure, TMap> : DataStructure where TMap : ClassMap
    {
        public bool CacheFilename { get; set; } = false;
        public TStructure Data { get; set; }

        public CsvDataStructure()
        {
            Size = Marshal.SizeOf<TStructure>();
        }

        public override string CreateOutputFilename(byte[] data)
        {
            return base.CreateOutputFilename(data).Replace(".dat", ".csv");
        }

        public override void Read(Stream infile)
        {
            base.Read(infile);

            GCHandle handle = GCHandle.Alloc(RawData, GCHandleType.Pinned);
            Data = (TStructure)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(TStructure));
            handle.Free();
        }

        public override void Dump()
        {
            string filename = CreateOutputFilename(RawData);
            using (TextWriter output = new StreamWriter(File.Create(filename), Encoding.UTF8))
            {
                using (CsvWriter csv = new CsvWriter(output))
                {
                    csv.Configuration.RegisterClassMap<TMap>();
                    csv.Configuration.QuoteAllFields = true;
                    csv.WriteHeader<TStructure>();
                    csv.NextRecord();
                    csv.WriteRecord(Data);
                    if (CacheFilename)
                    {
                        FileNameCache.Add(Name, filename);
                    }
                }
            }
        }

        public override void Import(string filename)
        {
            try
            {
                using (TextReader input = new StreamReader(filename, Encoding.UTF8))
                {
                    using (CsvReader csv = new CsvReader(input))
                    {
                        csv.Configuration.RegisterClassMap<TMap>();
                        csv.Read();
                        Data = csv.GetRecord<TStructure>();
                        if (CacheFilename)
                        {
                            FileNameCache.Add(Name, filename);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                throw new Exception($"Could not import CSV: {filename}", exception);
            }
        }

        public override void Write(Stream outfile)
        {
            int size = Marshal.SizeOf(Data);
            RawData = new byte[size];

            IntPtr objectPointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(Data, objectPointer, true);
            Marshal.Copy(objectPointer, RawData, 0, size);
            Marshal.FreeHGlobal(objectPointer);

            base.Write(outfile);
        }
    }
}