﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using CsvHelper;

namespace GT2.CourseInfoEditor
{
    using StreamExtensions;

    class Program
    {
        public static Dictionary<uint, string> DisplayNames = new Dictionary<uint, string>();

        static void Main(string[] args)
        {
            using (var file = new FileStream(".crsinfo", FileMode.Open, FileAccess.Read))
            {
                file.ReadUInt(); // CRS\0
                file.ReadUShort(); // 0x0002
                ushort courseCount = file.ReadUShort();

                file.Position += courseCount * 8 * 3;

                while (file.Position < file.Length)
                {
                    uint start = (uint)file.Position;
                    var bytes = new List<byte>();
                    byte newByte;
                    do
                    {
                        newByte = (byte)file.ReadByte();
                        if (newByte != 0)
                        {
                            bytes.Add(newByte);
                        }
                    }
                    while (newByte != 0);

                    string courseName = Encoding.UTF8.GetString(bytes.ToArray());
                    DisplayNames.Add(start, courseName);
                }

                file.Position = 8;

                using (var outFile = new FileStream($"Courses.csv", FileMode.Create, FileAccess.Write))
                {
                    using (TextWriter output = new StreamWriter(outFile, Encoding.UTF8))
                    {
                        using (CsvWriter csv = new CsvWriter(output))
                        {
                            csv.Configuration.RegisterClassMap<CourseCSVMap>();
                            csv.Configuration.ShouldQuote = (field, context) => true;
                            csv.WriteHeader<Course>();
                            csv.NextRecord();

                            for (int i = 0; i < courseCount; i++)
                            {
                                long blockStart = file.Position;

                                Course course = new Course();
                                course.DisplayName = file.ReadUInt();
                                course.Filename = file.ReadUInt();

                                byte flags = (byte)file.ReadByte();
                                course.IsNight = IsBitSet(flags, 0);
                                course.IsEvening = IsBitSet(flags, 1);
                                course.IsDirt = IsBitSet(flags, 2);
                                course.Is2Player = IsBitSet(flags, 3);
                                course.IsReverse = IsBitSet(flags, 4);
                                course.IsPointToPoint = IsBitSet(flags, 5);
                                course.Flag7 = IsBitSet(flags, 6);

                                course.Padding = (byte)file.ReadByte();
                                course.Skybox = file.ReadUShort();
                                course.LightingArea1Colour = ToRGBHex(file.ReadUShort());
                                course.LightingArea1ColourMultiplier = file.ReadUShort();
                                course.LightingArea2Colour = ToRGBHex(file.ReadUShort());
                                course.LightingArea2ColourMultiplier = file.ReadUShort();
                                course.LightingArea3Colour = ToRGBHex(file.ReadUShort());
                                course.LightingArea3ColourMultiplier = file.ReadUShort();

                                if (file.Position != blockStart + (8 * 3))
                                {
                                    throw new System.Exception("Block size incorrect.");
                                }

                                csv.WriteRecord(course);
                                csv.NextRecord();
                            }
                        }
                    }
                }
            }
        }

        static bool IsBitSet(byte value, int position)
        {
            int flag = value;
            flag = flag >> position;
            flag = flag & 0x1;
            return flag == 1;
        }

        static string ToRGBHex(ushort colour)
        {
            int R = colour & 0x1F;
            int G = (colour >> 5) & 0x1F;
            int B = (colour >> 10) & 0x1F;
            return $"#{R * 8:X2}{G * 8:X2}{B * 8:X2}";
        }
    }
}
