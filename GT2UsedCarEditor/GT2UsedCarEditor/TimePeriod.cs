﻿using System.Collections.Generic;
using System.IO;

namespace GT2.UsedCarEditor
{
    using StreamExtensions;

    class TimePeriod
    {
        public List<Manufacturer> Manufacturers { get; set; } = new List<Manufacturer>(39);

        protected string[] ManufacturerNames { get; set; } =
            { "Acura", "Alfa Romeo", "Aston Martin", "Audi", "BMW", "Chevrolet", "Citroen", "Daihatsu", "Dodge", "Fiat", "Ford", "Honda", "Manufacturer12", "Jaguar", "Lancia", "Manufacturer15", "Lister", "Lotus", "Mazda", "Mercedes-Benz", "Manufacturer20", "Mini MG", "Mitsubishi", "Nissan", "Opel", "Peugeot", "Plymouth", "Renault", "RUF", "Shelby", "Subaru", "Suzuki", "Tommykaira", "Toyota", "TVR", "Vauxhall", "Vector", "Venturi", "Volkswagen" };

        public void Read(Stream stream, uint startPosition)
        {
            for (int i = 0; i < 39; i++)
            {
                stream.Position = (i * 4) + startPosition;
                var manufacturer = new Manufacturer() { Name = ManufacturerNames[i] };
                manufacturer.Read(stream, startPosition + stream.ReadUShort(), stream.ReadUShort());
                Manufacturers.Add(manufacturer);
            }
        }

        public void WriteCSV(string directory)
        {
            foreach (Manufacturer manufacturer in Manufacturers)
            {
                manufacturer.WriteCSV(directory);
            }
        }

        public void ReadCSV(string directory)
        {
            foreach (string name in ManufacturerNames)
            {
                var manufacturer = new Manufacturer() { Name = name };

                foreach (string filename in Directory.EnumerateFiles(directory, $"{manufacturer.Name}*.csv"))
                {
                    if (!string.IsNullOrWhiteSpace(name) && File.Exists(filename))
                    {
                        manufacturer.ReadCSV(filename);
                    }
                }
                manufacturer.Sort();
                Manufacturers.Add(manufacturer);
            }
        }

        public uint Write(Stream stream, int indexPosition, uint dataPosition)
        {
            stream.Position = indexPosition;
            stream.WriteUInt(dataPosition);
            uint childDataPosition = dataPosition + (uint)(Manufacturers.Count * 4);

            for (int i = 0; i < Manufacturers.Count; i++)
            {
                childDataPosition = Manufacturers[i].Write(stream, dataPosition + (uint)(i * 4), dataPosition, childDataPosition);
            }

            return (uint)stream.Position;
        }
    }
}
