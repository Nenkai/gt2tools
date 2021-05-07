﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GT2.ModelTool.Structures
{
    using StreamExtensions;

    public class Model
    {
        public ushort Unknown1 { get; set; }
        public ushort Unknown2 { get; set; }
        public ushort Unknown3 { get; set; }
        public ushort Scale { get; set; }
        public List<WheelPosition> WheelPositions { get; set; } = new List<WheelPosition>(4);
        public byte[] Unknown4 { get; set; } = new byte[26];
        public List<LOD> LODs { get; set; }
        public Shadow Shadow { get; set; }

        public void ReadFromCDO(Stream stream) {
            stream.Position = 0x08;
            Unknown1 = stream.ReadUShort();
            if (Unknown1 == 0) {
                stream.Position = 0x18;
                Unknown1 = stream.ReadUShort();
            }

            Unknown2 = stream.ReadUShort();
            Unknown3 = stream.ReadUShort();
            Scale = stream.ReadUShort();

            for (int i = 0; i < 4; i++) {
                var wheelPosition = new WheelPosition();
                wheelPosition.ReadFromCDO(stream);
                WheelPositions.Add(wheelPosition);
            }

            stream.Position += 0x828;
            ushort lodCount = stream.ReadUShort();
            LODs = new List<LOD>(lodCount);

            stream.Read(Unknown4);

            for (int i = 0; i < lodCount; i++)
            {
                var lod = new LOD();
                lod.ReadFromCDO(stream);
                LODs.Add(lod);
            }
            Shadow = new Shadow();
            Shadow.ReadFromCDO(stream);
            if (stream.Position != stream.Length)
            {
                throw new Exception($"{stream.Length - stream.Position} trailing bytes after shadow");
            }
        }

        public void ReadFromCAR(Stream stream)
        {
            stream.Position = 0x10;

            for (int i = 0; i < 4; i++)
            {
                var wheelPosition = new WheelPosition();
                wheelPosition.ReadFromCAR(stream);
                WheelPositions.Add(wheelPosition);
            }

            WheelPositions = new List<WheelPosition> { WheelPositions[2], WheelPositions[3], WheelPositions[0], WheelPositions[1] };

            Unknown1 = stream.ReadUShort();
            Unknown2 = stream.ReadUShort();
            Unknown3 = stream.ReadUShort();
            Scale = stream.ReadUShort();

            stream.Position += 0x04;
            ushort lodCount = stream.ReadUShort();
            LODs = new List<LOD>(lodCount);

            stream.Position += 0x42;

            for (int i = 1; i <= lodCount; i++)
            {
                var lod = new LOD();
                lod.ReadFromCAR(stream);
                LODs.Add(lod);
                if (i != lodCount)
                {
                    stream.Position += 40; // gap between LODs
                }
            }
            Shadow = new Shadow();
            Shadow.ReadFromCAR(stream);
            if (stream.Position != stream.Length)
            {
                throw new Exception($"{stream.Length - stream.Position} trailing bytes after shadow");
            }
        }

        public void WriteToCDO(Stream stream)
        {
            // GT header
            stream.Write(new byte[] { 0x47, 0x54, 0x02 });
            stream.Position = 0x18;
            stream.WriteUShort(Unknown1);
            stream.WriteUShort(Unknown2);
            stream.WriteUShort(Unknown3);
            stream.WriteUShort(Scale);

            foreach (WheelPosition wheelPosition in WheelPositions)
            {
                wheelPosition.WriteToCDO(stream);
            }

            stream.Position = 0x868;
            stream.WriteUShort((ushort)LODs.Count);
            stream.Write(Unknown4);

            foreach (LOD lod in LODs)
            {
                lod.WriteToCDO(stream);
            }

            Shadow.WriteToCDO(stream);
        }

        public void WriteToOBJ(TextWriter modelWriter, TextWriter materialWriter)
        {
            modelWriter.WriteLine("mtllib out.mtl");

            // scale, unknowns, etc?
            modelWriter.WriteLine($"# scale: {Scale}");

            for (int i = 0; i < WheelPositions.Count; i++)
            {
                WheelPositions[i].WriteToOBJ(modelWriter, i);
            }

            int vertexNumber = WheelPositions.Count + 1;
            int normalNumber = 1;
            int coordNumber = 1;
            var materialNames = new Dictionary<string, int?>();

            for (int i = 0; i < LODs.Count; i++)
            {
                LODs[i].WriteToOBJ(modelWriter, i, vertexNumber, normalNumber, coordNumber, materialNames);
                vertexNumber += LODs[i].Vertices.Count;
                normalNumber += LODs[i].Normals.Count;
                coordNumber += LODs[i].GetAllUVCoords().Count;
            }

            Shadow.WriteToOBJ(modelWriter, vertexNumber);

            materialWriter.WriteLine("newmtl untextured");
            materialWriter.WriteLine("Kd 0 0 0");

            foreach (var materialName in materialNames)
            {
                materialWriter.WriteLine($"newmtl {materialName.Key}");
                if (materialName.Value == null)
                {
                    materialWriter.WriteLine("Kd 0 0 0");
                }
                else
                {
                    materialWriter.WriteLine($"map_Kd palette{materialName.Value}.bmp");
                }
            }
        }

        public void ReadFromOBJ(TextReader reader)
        {
            var lods = new LOD[3];
            var wheelPositions = new WheelPosition[4];
            string line;
            int currentWheelPosition = -1;
            int currentLODNumber = -1;
            bool shadow = false;
            string currentMaterial = "untextured";
            Vertex wheelPositionCandidate = null;
            LOD currentLOD = null;
            var vertices = new List<Vertex>();
            var normals = new List<Normal>();
            var uvCoords = new List<UVCoordinate>();
            var usedVertexIDs = new List<int>();
            var usedNormalIDs = new List<int>();
            int shadowVertexStartID = 0;
            double currentScale = 1;
            do
            {
                line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith("o ") || line.StartsWith("g "))
                {
                    string[] objectNameParts = line.Split(' ')[1].Split('/');
                    if (objectNameParts[0].StartsWith("wheelpos"))
                    {
                        currentWheelPosition = int.Parse(objectNameParts[0].Replace("wheelpos", ""));
                    }
                    else if (objectNameParts[0].StartsWith("lod"))
                    {
                        if (wheelPositions.Any(position => position == null))
                        {
                            throw new Exception("Expected four wheel position objects before any LOD objects.");
                        }
                        if (currentLOD != null)
                        {
                            currentLOD.Vertices = usedVertexIDs.OrderBy(id => id).Distinct().Select(id => vertices[id]).ToList();
                            usedVertexIDs = new List<int>();
                            currentLOD.Normals = usedNormalIDs.OrderBy(id => id).Distinct().Select(id => normals[id]).ToList();
                            usedNormalIDs = new List<int>();
                            currentLOD.GenerateBoundingBox();
                        }
                        currentLODNumber = int.Parse(objectNameParts[0].Replace("lod", ""));
                        currentLOD = new LOD();
                        currentLOD.PrepareForOBJRead();
                        lods[currentLODNumber] = currentLOD;
                        currentScale = GetScale(objectNameParts);
                        currentLOD.Scale = LOD.ConvertScale(currentScale);
                    }
                    else if (objectNameParts[0].StartsWith("shadow"))
                    {
                        if (lods.Any(lod => lod == null))
                        {
                            throw new Exception("Expected three LOD objects before shadow object.");
                        }
                        currentLOD.Vertices = usedVertexIDs.OrderBy(id => id).Distinct().Select(id => vertices[id]).ToList();
                        currentLOD.Normals = usedNormalIDs.OrderBy(id => id).Distinct().Select(id => normals[id]).ToList();
                        currentLOD.GenerateBoundingBox();
                        shadow = true;
                        Shadow = new Shadow();
                        Shadow.PrepareForOBJRead();
                        shadowVertexStartID = vertices.Count;
                        currentScale = GetScale(objectNameParts);
                        Shadow.Scale = LOD.ConvertScale(currentScale);
                    }
                }
                else if (line.StartsWith("v "))
                {
                    if (shadow)
                    {
                        var vertex = new ShadowVertex();
                        vertex.ReadFromOBJ(line, currentScale);
                        Shadow.Vertices.Add(vertex);
                    }
                    else
                    {
                        var vertex = new Vertex();
                        vertex.ReadFromOBJ(line, currentScale);
                        vertices.Add(vertex);
                        if (currentLODNumber == -1)
                        {
                            wheelPositionCandidate = vertex;
                        }
                    }
                }
                else if (line.StartsWith("vt "))
                {
                    var uvCoord = new UVCoordinate();
                    uvCoord.ReadFromOBJ(line);
                    uvCoords.Add(uvCoord);
                }
                else if (line.StartsWith("vn "))
                {
                    var normal = new Normal();
                    normal.ReadFromOBJ(line);
                    normals.Add(normal);
                }
                else if (line.StartsWith("usemtl "))
                {
                    currentMaterial = line.Split(' ')[1];
                }
                else if (line.StartsWith("f "))
                {
                    if (shadow)
                    {
                        var polygon = new ShadowPolygon();
                        polygon.ReadFromOBJ(line, Shadow.Vertices, shadowVertexStartID);
                        if (polygon.IsQuad)
                        {
                            Shadow.Quads.Add(polygon);
                        }
                        else
                        {
                            Shadow.Triangles.Add(polygon);
                        }
                    }
                    else if (currentLODNumber == -1)
                    {
                        continue;
                        //throw new Exception("Face found outside of a LOD or shadow.");
                    }
                    else if (currentMaterial.StartsWith("untextured"))
                    {
                        var polygon = new Polygon();
                        polygon.ReadFromOBJ(line, vertices, normals, currentMaterial, usedVertexIDs, usedNormalIDs);
                        if (polygon.IsQuad)
                        {
                            currentLOD.Quads.Add(polygon);
                        }
                        else
                        {
                            currentLOD.Triangles.Add(polygon);
                        }
                    }
                    else
                    {
                        var polygon = new UVPolygon();
                        polygon.ReadFromOBJ(line, vertices, normals, uvCoords, currentMaterial, usedVertexIDs, usedNormalIDs);
                        if (polygon.IsQuad)
                        {
                            currentLOD.UVQuads.Add(polygon);
                        }
                        else
                        {
                            currentLOD.UVTriangles.Add(polygon);
                        }
                    }
                }

                if (currentWheelPosition != -1 && wheelPositionCandidate != null)
                {
                    var position = new WheelPosition();
                    position.ReadFromOBJ(wheelPositionCandidate);
                    wheelPositions[currentWheelPosition] = position;
                    wheelPositionCandidate = null;
                    currentWheelPosition = -1;
                }
            }
            while (line != null);

            Shadow.GenerateBoundingBox();
            LODs = lods.ToList();
            WheelPositions = wheelPositions.ToList();
        }

        private double GetScale(string[] objectNameParts)
        {
            foreach (string part in objectNameParts)
            {
                string[] pair = part.Split('=');
                if (pair.Length == 2 && pair[0] == "scale")
                {
                    return double.Parse(pair[1]);
                }
            }
            return 1;
        }
    }
}