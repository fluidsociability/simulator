/*
FLUID Sociability - a simulation tool for evaluating building designs
Copyright (C) 2022 Human Studio, Inc.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace FLUID_Simulator
{
    public interface I3DRepresentation
    {
        string name { get; }
        Vector3[] vertices { get; }
    }

    public interface IMesh : I3DRepresentation
    {
        int[] triangleIndices { get; }
        int[] circumferenceIndices { get; }
    }

    public interface ITexturedMesh : IMesh
    {
        Vector2[] uvs { get; }

        Texture2D texture { get; }
    }

    public interface IHomogeneousMesh : IMesh
    {
        Color32 color { get; }
    }

    public class TexturedMesh : ITexturedMesh
    {
        string _name;
        Vector3[] _vertices;
        Vector2[] _uvs;
        int[] _triangleIndices;
        int[] _circumferenceIndices;
        Texture2D _texture;

        public TexturedMesh(
            string name,
            Vector3[] positionChain,
            Vector2[] uvChain,
            int[] triangleIndices,
            int[] circumferenceIndices,
            Texture2D texture
        )
        {
            _name = name;
            _vertices = positionChain;
            _uvs = uvChain;
            _triangleIndices = triangleIndices;
            _circumferenceIndices = circumferenceIndices;
            _texture = texture;
        }

        public string name
        {
            get { return _name; }
        }
        public Vector3[] vertices
        {
            get { return _vertices; }
        }
        public Vector2[] uvs
        {
            get { return _uvs; }
        }
        public int[] triangleIndices
        {
            get { return _triangleIndices; }
        }
        public int[] circumferenceIndices
        {
            get { return _circumferenceIndices; }
        }
        public Texture2D texture
        {
            get { return _texture; }
        }
    }

    public class HomogeneousMesh : IHomogeneousMesh
    {
        string _name;
        Vector3[] _vertices;
        int[] _triangleIndices;
        int[] _circumferenceIndices;
        Color32 _color;

        public HomogeneousMesh(
            string name,
            Vector3[] vertices,
            int[] triangleIndices,
            int[] circumferenceIndices,
            Color32 color
        )
        {
            _name = name;
            _vertices = vertices;
            _triangleIndices = triangleIndices;
            _circumferenceIndices = circumferenceIndices;
            _color = color;
        }

        public string name
        {
            get { return _name; }
        }
        public Vector3[] vertices
        {
            get { return _vertices; }
        }
        public int[] triangleIndices
        {
            get { return _triangleIndices; }
        }
        public int[] circumferenceIndices
        {
            get { return _circumferenceIndices; }
        }

        public Color32 color
        {
            get { return _color; }
        }
    }

    public class InstancedHomogeneousMesh : IHomogeneousMesh
    {
        string _name;
        Vector3[] _vertices;
        int[] _triangleIndices;
        int[] _circumferenceIndices;
        Color32 _color;
        Vector3[] _positions;
        Vector3[] _scales;

        public InstancedHomogeneousMesh(
            string name,
            Vector3[] vertices,
            int[] triangleIndices,
            Color32 color,
            Vector3[] positions,
            Vector3[] scales
        )
        {
            _name = name;
            _vertices = vertices;
            _triangleIndices = triangleIndices;
            _circumferenceIndices = circumferenceIndices;
            _color = color;
            _positions = positions;
            _scales = scales;
        }

        public string name
        {
            get { return _name; }
        }
        public Vector3[] vertices
        {
            get { return _vertices; }
        }
        public int[] triangleIndices
        {
            get { return _triangleIndices; }
        }
        public int[] circumferenceIndices
        {
            get { return _circumferenceIndices; }
        }

        public Color32 color
        {
            get { return _color; }
        }

        public Vector3[] positions
        {
            get { return _positions; }
        }
        public Vector3[] scales
        {
            get { return _scales; }
        }
    }

    public class Lines : I3DRepresentation
    {
        string _name;
        Vector3[] _vertices;
        Color32 _color;

        public Lines(string name, Vector3[] vertices, Color32 color)
        {
            _name = name;
            _vertices = vertices;
            if (vertices.Length > 100000)
            {
                _vertices.Take(100000);
            }
            _color = color;
        }

        public string name
        {
            get { return _name; }
        }
        public Vector3[] vertices
        {
            get { return _vertices; }
        }
        public Color32 color
        {
            get { return _color; }
        }
    }

    public class MeshSerializer : List<I3DRepresentation>
    {
        public string Serialize()
        {
            glTFLoader.Schema.Gltf glTFObject = null;
            try
            {
                #region Create the lists to hold the glTF children during construction
                List<glTFLoader.Schema.Image> images = new List<glTFLoader.Schema.Image>();
                List<glTFLoader.Schema.Sampler> samplers = new List<glTFLoader.Schema.Sampler>();
                List<glTFLoader.Schema.Texture> textures = new List<glTFLoader.Schema.Texture>();
                List<glTFLoader.Schema.Material> materials = new List<glTFLoader.Schema.Material>();

                List<glTFLoader.Schema.Mesh> meshes = new List<glTFLoader.Schema.Mesh>();

                List<glTFLoader.Schema.Accessor> accessors = new List<glTFLoader.Schema.Accessor>();
                List<glTFLoader.Schema.BufferView> bufferViews =
                    new List<glTFLoader.Schema.BufferView>();
                List<glTFLoader.Schema.Buffer> buffers = new List<glTFLoader.Schema.Buffer>();
                List<glTFLoader.Schema.Node> nodes = new List<glTFLoader.Schema.Node>();
                #endregion

                #region Create the scene
                glTFLoader.Schema.Scene scene = new glTFLoader.Schema.Scene();
                scene.Name = "Scene";
                #endregion

                List<int> rootNodeIndices = new List<int>();

                glTFLoader.Schema.Node rootNode = new glTFLoader.Schema.Node();
                rootNode.Name = "Root";
                // rootNode.Scale = new float[] { 1, 1, -1 };
                nodes.Add(rootNode);
                Vector3 worldMin = Vector3.positiveInfinity;
                Vector3 worldMax = Vector3.negativeInfinity;

                foreach (I3DRepresentation representation in this)
                {
                    if (representation.vertices.Length > 0)
                    {
                        int byteOffset = 0;
                        glTFLoader.Schema.Material glTFMaterial = new glTFLoader.Schema.Material();
                        if (representation is IHomogeneousMesh)
                        {
                            IHomogeneousMesh homogeneousMesh = (IHomogeneousMesh)representation;

                            #region Create a glTF colored material
                            glTFMaterial.Name = homogeneousMesh.name + "_Material";
                            if (homogeneousMesh.color.a == 255)
                            {
                                glTFMaterial.DoubleSided = false;
                                glTFMaterial.AlphaMode = glTFLoader
                                    .Schema
                                    .Material
                                    .AlphaModeEnum
                                    .OPAQUE;
                            }
                            else
                            {
                                glTFMaterial.DoubleSided = true;
                                glTFMaterial.AlphaMode = glTFLoader
                                    .Schema
                                    .Material
                                    .AlphaModeEnum
                                    .BLEND;
                            }
                            glTFMaterial.PbrMetallicRoughness =
                                new glTFLoader.Schema.MaterialPbrMetallicRoughness();

                            glTFMaterial.PbrMetallicRoughness.BaseColorFactor = new float[]
                            {
                                homogeneousMesh.color.r / 255.0f,
                                homogeneousMesh.color.g / 255.0f,
                                homogeneousMesh.color.b / 255.0f,
                                homogeneousMesh.color.a / 255.0f,
                            };
                            glTFMaterial.PbrMetallicRoughness.MetallicFactor = 0;
                            #endregion

                            #region Find the bounds of the mesh.
                            Vector3 min = Vector3.positiveInfinity;
                            Vector3 max = Vector3.negativeInfinity;
                            foreach (Vector3 vertex in homogeneousMesh.vertices)
                            {
                                min = Vector3.Min(vertex, min);
                                max = Vector3.Max(vertex, max);
                                worldMin = Vector3.Min(vertex, min);
                                worldMax = Vector3.Max(vertex, max);
                            }
                            #endregion

                            #region Create Position Accessor
                            glTFLoader.Schema.Accessor positionAccessor =
                                new glTFLoader.Schema.Accessor();
                            positionAccessor.Name = "Position Accessor";
                            positionAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3;
                            positionAccessor.ComponentType = glTFLoader
                                .Schema
                                .Accessor
                                .ComponentTypeEnum
                                .FLOAT;
                            positionAccessor.BufferView = bufferViews.Count;
                            positionAccessor.Count = homogeneousMesh.vertices.Length;
                            positionAccessor.Min = new float[] { min.x, min.y, min.z };
                            positionAccessor.Max = new float[] { max.x, max.y, max.z };
                            #endregion

                            #region Create Position BufferView
                            glTFLoader.Schema.BufferView positionBufferView =
                                new glTFLoader.Schema.BufferView();
                            positionBufferView.Buffer = buffers.Count;
                            positionBufferView.ByteOffset = byteOffset;
                            positionBufferView.ByteLength =
                                sizeof(float) * 3 * homogeneousMesh.vertices.Length;
                            #endregion

                            // Advance the byteOffset
                            byteOffset += positionBufferView.ByteLength;

                            #region Create Triangles Accessor
                            glTFLoader.Schema.Accessor trianglesAccessor =
                                new glTFLoader.Schema.Accessor();
                            trianglesAccessor.Name = "Triangles Accessor";
                            trianglesAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.SCALAR;
                            trianglesAccessor.ComponentType = glTFLoader
                                .Schema
                                .Accessor
                                .ComponentTypeEnum
                                .UNSIGNED_INT;
                            trianglesAccessor.BufferView = bufferViews.Count + 1;
                            trianglesAccessor.Count = homogeneousMesh.triangleIndices.Length;
                            #endregion

                            #region Create Triangles BufferView
                            glTFLoader.Schema.BufferView trianglesBufferView =
                                new glTFLoader.Schema.BufferView();
                            trianglesBufferView.Buffer = buffers.Count;
                            trianglesBufferView.ByteOffset = byteOffset;
                            trianglesBufferView.ByteLength =
                                sizeof(UInt32) * homogeneousMesh.triangleIndices.Length;
                            #endregion

                            // Advance the byteOffset
                            byteOffset += trianglesBufferView.ByteLength;

                            #region Create Buffer
                            glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer();

                            byte[] geometryBuffer = new byte[byteOffset];

                            float[] positionFloats = new float[3 * homogeneousMesh.vertices.Length];
                            for (
                                int vertexCounter = 0;
                                vertexCounter < homogeneousMesh.vertices.Length;
                                vertexCounter++
                            )
                            {
                                positionFloats[vertexCounter * 3 + 0] = homogeneousMesh.vertices[
                                    vertexCounter
                                ].x;
                                positionFloats[vertexCounter * 3 + 1] = homogeneousMesh.vertices[
                                    vertexCounter
                                ].y;
                                positionFloats[vertexCounter * 3 + 2] = homogeneousMesh.vertices[
                                    vertexCounter
                                ].z;
                            }
                            System.Buffer.BlockCopy(
                                positionFloats,
                                0,
                                geometryBuffer,
                                positionBufferView.ByteOffset,
                                positionBufferView.ByteLength
                            );

                            UInt32[] navigationSpaceTriangleIndices = new UInt32[
                                2 * homogeneousMesh.triangleIndices.Length
                            ];
                            for (
                                int triangleCounter = 0;
                                triangleCounter < homogeneousMesh.triangleIndices.Length / 3;
                                triangleCounter++
                            )
                            {
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 0] =
                                    (UInt32)homogeneousMesh.triangleIndices[
                                        triangleCounter * 3 + 0
                                    ];
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 1] =
                                    (UInt32)homogeneousMesh.triangleIndices[
                                        triangleCounter * 3 + 1
                                    ];
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 2] =
                                    (UInt32)homogeneousMesh.triangleIndices[
                                        triangleCounter * 3 + 2
                                    ];
                            }
                            System.Buffer.BlockCopy(
                                navigationSpaceTriangleIndices,
                                0,
                                geometryBuffer,
                                trianglesBufferView.ByteOffset,
                                trianglesBufferView.ByteLength
                            );

                            buffer.ByteLength = byteOffset;
                            buffer.Uri =
                                "data:application/octet-stream;base64,"
                                + Convert.ToBase64String(geometryBuffer);
                            #endregion

                            #region Create a glTF Mesh
                            glTFLoader.Schema.Mesh glTFMesh = new glTFLoader.Schema.Mesh();

                            glTFLoader.Schema.MeshPrimitive glTFMeshPrimitive =
                                new glTFLoader.Schema.MeshPrimitive();
                            glTFMeshPrimitive.Attributes = new Dictionary<string, int>();
                            glTFMeshPrimitive.Attributes["POSITION"] = accessors.Count;
                            glTFMeshPrimitive.Indices = accessors.Count + 1;

                            glTFMeshPrimitive.Material = materials.Count;
                            glTFMeshPrimitive.Mode = glTFLoader
                                .Schema
                                .MeshPrimitive
                                .ModeEnum
                                .TRIANGLES;

                            glTFMesh.Primitives = new glTFLoader.Schema.MeshPrimitive[]
                            {
                                glTFMeshPrimitive
                            };
                            #endregion

                            if (representation is HomogeneousMesh)
                            {
                                #region Create a node
                                glTFLoader.Schema.Node node = new glTFLoader.Schema.Node();
                                node.Name = representation.name;
                                node.Mesh = meshes.Count;
                                rootNodeIndices.Add(nodes.Count);
                                nodes.Add(node);
                                #endregion
                            }
                            else if (representation is InstancedHomogeneousMesh)
                            {
                                InstancedHomogeneousMesh instancedHomogeneousMesh =
                                    (InstancedHomogeneousMesh)representation;

                                for (
                                    int index = 0;
                                    index < instancedHomogeneousMesh.positions.Length;
                                    index++
                                )
                                {
                                    glTFLoader.Schema.Node node = new glTFLoader.Schema.Node();
                                    node.Name = instancedHomogeneousMesh.name + " " + index;
                                    node.Mesh = meshes.Count;
                                    float[] test = new float[]
                                    {
                                        instancedHomogeneousMesh.positions[index].x,
                                        instancedHomogeneousMesh.positions[index].y,
                                        instancedHomogeneousMesh.positions[index].z
                                    };
                                    node.Translation = test;
                                    node.Scale = new float[]
                                    {
                                        instancedHomogeneousMesh.scales[index].x,
                                        instancedHomogeneousMesh.scales[index].y,
                                        instancedHomogeneousMesh.scales[index].z
                                    };

                                    rootNodeIndices.Add(nodes.Count);
                                    nodes.Add(node);
                                    //break;
                                }
                            }

                            materials.Add(glTFMaterial);
                            accessors.Add(positionAccessor);
                            accessors.Add(trianglesAccessor);
                            bufferViews.Add(positionBufferView);
                            bufferViews.Add(trianglesBufferView);
                            buffers.Add(buffer);
                            meshes.Add(glTFMesh);
                        }
                        else if (representation is TexturedMesh)
                        {
                            TexturedMesh texturedMesh = (TexturedMesh)representation;

                            #region Create a glTF texture material
                            glTFMaterial.Name = "Textured Mesh Material " + images.Count;
                            glTFMaterial.DoubleSided = true;
                            glTFMaterial.AlphaMode = glTFLoader.Schema.Material.AlphaModeEnum.MASK;
                            glTFMaterial.AlphaCutoff = 0.1f;
                            glTFMaterial.PbrMetallicRoughness =
                                new glTFLoader.Schema.MaterialPbrMetallicRoughness();
                            glTFMaterial.PbrMetallicRoughness.BaseColorFactor = new float[]
                            {
                                1,
                                1,
                                1,
                                1
                            };
                            glTFMaterial.PbrMetallicRoughness.BaseColorTexture =
                                new glTFLoader.Schema.TextureInfo();
                            glTFMaterial.PbrMetallicRoughness.BaseColorTexture.Index = images.Count;
                            glTFMaterial.PbrMetallicRoughness.MetallicFactor = 0;
                            #endregion

                            #region Create the image
                            // Convert to a png format and extract the byte data
                            byte[] pngBytes = texturedMesh.texture.EncodeToPNG();
                            // Debug.Log("texture: " + texturedMesh.texture.width + ", " + texturedMesh.texture.height);

                            // Create an image in the glTF
                            glTFLoader.Schema.Image image = new glTFLoader.Schema.Image();

                            // Encode the png bytes as base 64 append them to a data uri header.
                            image.Uri = "data:image/png;base64," + Convert.ToBase64String(pngBytes);
                            #endregion

                            #region Create the sampler
                            glTFLoader.Schema.Sampler sampler = new glTFLoader.Schema.Sampler();
                            sampler.MagFilter = glTFLoader.Schema.Sampler.MagFilterEnum.NEAREST;
                            sampler.MinFilter = glTFLoader
                                .Schema
                                .Sampler
                                .MinFilterEnum
                                .LINEAR_MIPMAP_LINEAR;
                            #endregion

                            #region Create the texture
                            glTFLoader.Schema.Texture texture = new glTFLoader.Schema.Texture();
                            texture.Name = texturedMesh.name;
                            texture.Sampler = samplers.Count;
                            texture.Source = images.Count;
                            #endregion

                            #region Find the bounds of the mesh.
                            Vector3 min = Vector3.positiveInfinity;
                            Vector3 max = Vector3.negativeInfinity;
                            foreach (Vector3 vertex in texturedMesh.vertices)
                            {
                                min = Vector3.Min(vertex, min);
                                max = Vector3.Max(vertex, max);
                                worldMin = Vector3.Min(vertex, min);
                                worldMax = Vector3.Max(vertex, max);
                            }
                            #endregion

                            #region Create Position Accessor
                            glTFLoader.Schema.Accessor positionAccessor =
                                new glTFLoader.Schema.Accessor();
                            positionAccessor.Name = "Position Accessor";
                            positionAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3;
                            positionAccessor.ComponentType = glTFLoader
                                .Schema
                                .Accessor
                                .ComponentTypeEnum
                                .FLOAT;
                            positionAccessor.BufferView = bufferViews.Count;
                            positionAccessor.Count = texturedMesh.vertices.Length;
                            positionAccessor.Min = new float[] { min.x, min.y, min.z };
                            positionAccessor.Max = new float[] { max.x, max.y, max.z };
                            #endregion

                            #region Create Position BufferView
                            glTFLoader.Schema.BufferView positionBufferView =
                                new glTFLoader.Schema.BufferView();
                            positionBufferView.Buffer = buffers.Count;
                            positionBufferView.ByteOffset = byteOffset;
                            positionBufferView.ByteLength =
                                sizeof(float) * 3 * texturedMesh.vertices.Length;
                            #endregion

                            // Advance the byteOffset
                            byteOffset += positionBufferView.ByteLength;

                            #region Create UV Accessor
                            glTFLoader.Schema.Accessor uvAccessor =
                                new glTFLoader.Schema.Accessor();
                            uvAccessor.Name = "UV Accessor";
                            uvAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.VEC2;
                            uvAccessor.ComponentType = glTFLoader
                                .Schema
                                .Accessor
                                .ComponentTypeEnum
                                .FLOAT;
                            uvAccessor.BufferView = bufferViews.Count + 1;
                            uvAccessor.Count = texturedMesh.vertices.Length;
                            #endregion

                            #region Create UV BufferView
                            glTFLoader.Schema.BufferView uvBufferView =
                                new glTFLoader.Schema.BufferView();
                            uvBufferView.Buffer = buffers.Count;
                            uvBufferView.ByteOffset = byteOffset;
                            uvBufferView.ByteLength =
                                sizeof(float) * 2 * texturedMesh.vertices.Length;
                            #endregion

                            // Advance the byteOffset
                            byteOffset += uvBufferView.ByteLength;

                            #region Create Triangles Accessor
                            glTFLoader.Schema.Accessor trianglesAccessor =
                                new glTFLoader.Schema.Accessor();
                            trianglesAccessor.Name = "Triangles Accessor";
                            trianglesAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.SCALAR;
                            trianglesAccessor.ComponentType = glTFLoader
                                .Schema
                                .Accessor
                                .ComponentTypeEnum
                                .UNSIGNED_INT;
                            trianglesAccessor.BufferView = bufferViews.Count + 2;
                            trianglesAccessor.Count = texturedMesh.triangleIndices.Length;
                            #endregion

                            #region Create Triangles BufferView
                            glTFLoader.Schema.BufferView trianglesBufferView =
                                new glTFLoader.Schema.BufferView();
                            trianglesBufferView.Buffer = buffers.Count;
                            trianglesBufferView.ByteOffset = byteOffset;
                            trianglesBufferView.ByteLength =
                                sizeof(UInt32) * texturedMesh.triangleIndices.Length;
                            #endregion

                            // Advance the byteOffset
                            byteOffset += trianglesBufferView.ByteLength;

                            #region Create Buffer
                            glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer();

                            byte[] geometryBuffer = new byte[byteOffset];

                            float[] positionFloats = new float[3 * texturedMesh.vertices.Length];
                            for (
                                int vertexCounter = 0;
                                vertexCounter < texturedMesh.vertices.Length;
                                vertexCounter++
                            )
                            {
                                positionFloats[vertexCounter * 3 + 0] = texturedMesh.vertices[
                                    vertexCounter
                                ].x;
                                positionFloats[vertexCounter * 3 + 1] = texturedMesh.vertices[
                                    vertexCounter
                                ].y;
                                positionFloats[vertexCounter * 3 + 2] = texturedMesh.vertices[
                                    vertexCounter
                                ].z;
                            }
                            System.Buffer.BlockCopy(
                                positionFloats,
                                0,
                                geometryBuffer,
                                positionBufferView.ByteOffset,
                                positionBufferView.ByteLength
                            );

                            float[] uvFloats = new float[2 * texturedMesh.uvs.Length];
                            for (
                                int vertexCounter = 0;
                                vertexCounter < texturedMesh.uvs.Length;
                                vertexCounter++
                            )
                            {
                                uvFloats[vertexCounter * 2 + 0] = texturedMesh.uvs[vertexCounter].x;
                                uvFloats[vertexCounter * 2 + 1] = texturedMesh.uvs[vertexCounter].y;
                            }
                            System.Buffer.BlockCopy(
                                uvFloats,
                                0,
                                geometryBuffer,
                                uvBufferView.ByteOffset,
                                uvBufferView.ByteLength
                            );

                            UInt32[] navigationSpaceTriangleIndices = new UInt32[
                                2 * texturedMesh.triangleIndices.Length
                            ];
                            for (
                                int triangleCounter = 0;
                                triangleCounter < texturedMesh.triangleIndices.Length / 3;
                                triangleCounter++
                            )
                            {
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 0] =
                                    (UInt32)texturedMesh.triangleIndices[triangleCounter * 3 + 0];
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 1] =
                                    (UInt32)texturedMesh.triangleIndices[triangleCounter * 3 + 1];
                                navigationSpaceTriangleIndices[triangleCounter * 3 + 2] =
                                    (UInt32)texturedMesh.triangleIndices[triangleCounter * 3 + 2];
                            }
                            System.Buffer.BlockCopy(
                                navigationSpaceTriangleIndices,
                                0,
                                geometryBuffer,
                                trianglesBufferView.ByteOffset,
                                trianglesBufferView.ByteLength
                            );

                            buffer.ByteLength = byteOffset;
                            buffer.Uri =
                                "data:application/octet-stream;base64,"
                                + Convert.ToBase64String(geometryBuffer);

                            #endregion

                            #region create a glTF Mesh
                            glTFLoader.Schema.Mesh glTFMesh = new glTFLoader.Schema.Mesh();

                            glTFLoader.Schema.MeshPrimitive glTFMeshPrimitive =
                                new glTFLoader.Schema.MeshPrimitive();
                            glTFMeshPrimitive.Attributes = new Dictionary<string, int>();
                            glTFMeshPrimitive.Attributes["POSITION"] = accessors.Count;
                            glTFMeshPrimitive.Attributes["TEXCOORD_0"] = accessors.Count + 1;
                            glTFMeshPrimitive.Indices = accessors.Count + 2;
                            // Note the following assignment needs to occur before the material is added
                            glTFMeshPrimitive.Material = materials.Count;
                            glTFMeshPrimitive.Mode = glTFLoader
                                .Schema
                                .MeshPrimitive
                                .ModeEnum
                                .TRIANGLES;

                            glTFMesh.Primitives = new glTFLoader.Schema.MeshPrimitive[]
                            {
                                glTFMeshPrimitive
                            };
                            #endregion

                            #region Create a node
                            glTFLoader.Schema.Node node = new glTFLoader.Schema.Node();
                            node.Name = texturedMesh.name;
                            node.Mesh = meshes.Count;
                            rootNodeIndices.Add(nodes.Count);
                            #endregion

                            materials.Add(glTFMaterial);
                            images.Add(image);
                            samplers.Add(sampler);
                            textures.Add(texture);
                            accessors.Add(positionAccessor);
                            accessors.Add(uvAccessor);
                            accessors.Add(trianglesAccessor);
                            bufferViews.Add(positionBufferView);
                            bufferViews.Add(uvBufferView);
                            bufferViews.Add(trianglesBufferView);
                            buffers.Add(buffer);
                            meshes.Add(glTFMesh);
                            nodes.Add(node);
                        }
                        else if (representation is Lines)
                        {
                            Lines lines = (Lines)representation;
                            if (lines.vertices.Length > 0)
                            {
                                #region Create a glTF colored material
                                glTFMaterial.Name = lines.name + "_Material";
                                if (lines.color.a == 255)
                                {
                                    glTFMaterial.DoubleSided = false;
                                    glTFMaterial.AlphaMode = glTFLoader
                                        .Schema
                                        .Material
                                        .AlphaModeEnum
                                        .OPAQUE;
                                }
                                else
                                {
                                    glTFMaterial.DoubleSided = true;
                                    glTFMaterial.AlphaMode = glTFLoader
                                        .Schema
                                        .Material
                                        .AlphaModeEnum
                                        .BLEND;
                                }
                                glTFMaterial.PbrMetallicRoughness =
                                    new glTFLoader.Schema.MaterialPbrMetallicRoughness();

                                glTFMaterial.PbrMetallicRoughness.BaseColorFactor = new float[]
                                {
                                    lines.color.r / 255.0f,
                                    lines.color.g / 255.0f,
                                    lines.color.b / 255.0f,
                                    lines.color.a / 255.0f,
                                };
                                glTFMaterial.PbrMetallicRoughness.MetallicFactor = 0;
                                #endregion

                                #region Find the bounds of the mesh.
                                Vector3 min = Vector3.positiveInfinity;
                                Vector3 max = Vector3.negativeInfinity;
                                foreach (Vector3 vertex in lines.vertices)
                                {
                                    min = Vector3.Min(vertex, min);
                                    max = Vector3.Max(vertex, max);
                                    worldMin = Vector3.Min(vertex, min);
                                    worldMax = Vector3.Max(vertex, max);
                                }
                                #endregion

                                #region Create Position Accessor
                                glTFLoader.Schema.Accessor positionAccessor =
                                    new glTFLoader.Schema.Accessor();
                                positionAccessor.Name = "Position Accessor";
                                positionAccessor.Type = glTFLoader.Schema.Accessor.TypeEnum.VEC3;
                                positionAccessor.ComponentType = glTFLoader
                                    .Schema
                                    .Accessor
                                    .ComponentTypeEnum
                                    .FLOAT;
                                positionAccessor.BufferView = bufferViews.Count;
                                positionAccessor.Count = lines.vertices.Length;
                                positionAccessor.Min = new float[] { min.x, min.y, min.z };
                                positionAccessor.Max = new float[] { max.x, max.y, max.z };
                                #endregion

                                #region Create Position BufferView
                                glTFLoader.Schema.BufferView positionBufferView =
                                    new glTFLoader.Schema.BufferView();
                                positionBufferView.Buffer = buffers.Count;
                                positionBufferView.ByteOffset = byteOffset;
                                positionBufferView.ByteLength =
                                    sizeof(float) * 3 * lines.vertices.Length;
                                #endregion

                                // Advance the byteOffset
                                byteOffset += positionBufferView.ByteLength;

                                #region Create Buffer
                                glTFLoader.Schema.Buffer buffer = new glTFLoader.Schema.Buffer();

                                byte[] geometryBuffer = new byte[byteOffset];

                                float[] positionFloats = new float[3 * lines.vertices.Length];
                                for (
                                    int vertexCounter = 0;
                                    vertexCounter < lines.vertices.Length;
                                    vertexCounter++
                                )
                                {
                                    positionFloats[vertexCounter * 3 + 0] = lines.vertices[
                                        vertexCounter
                                    ].x;
                                    positionFloats[vertexCounter * 3 + 1] = lines.vertices[
                                        vertexCounter
                                    ].y;
                                    positionFloats[vertexCounter * 3 + 2] = lines.vertices[
                                        vertexCounter
                                    ].z;
                                }
                                System.Buffer.BlockCopy(
                                    positionFloats,
                                    0,
                                    geometryBuffer,
                                    positionBufferView.ByteOffset,
                                    positionBufferView.ByteLength
                                );

                                buffer.ByteLength = byteOffset;
                                buffer.Uri =
                                    "data:application/octet-stream;base64,"
                                    + Convert.ToBase64String(geometryBuffer);
                                #endregion

                                #region Create a glTF Mesh
                                glTFLoader.Schema.Mesh glTFMesh = new glTFLoader.Schema.Mesh();

                                glTFLoader.Schema.MeshPrimitive glTFMeshPrimitive =
                                    new glTFLoader.Schema.MeshPrimitive();
                                glTFMeshPrimitive.Attributes = new Dictionary<string, int>();
                                glTFMeshPrimitive.Attributes["POSITION"] = accessors.Count;

                                glTFMeshPrimitive.Material = materials.Count;
                                glTFMeshPrimitive.Mode = glTFLoader
                                    .Schema
                                    .MeshPrimitive
                                    .ModeEnum
                                    .LINES;

                                glTFMesh.Primitives = new glTFLoader.Schema.MeshPrimitive[]
                                {
                                    glTFMeshPrimitive
                                };
                                #endregion

                                #region Create a node
                                glTFLoader.Schema.Node node = new glTFLoader.Schema.Node();
                                node.Name = lines.name;
                                node.Mesh = meshes.Count;
                                rootNodeIndices.Add(nodes.Count);
                                nodes.Add(node);
                                #endregion

                                materials.Add(glTFMaterial);
                                accessors.Add(positionAccessor);
                                bufferViews.Add(positionBufferView);
                                buffers.Add(buffer);
                                meshes.Add(glTFMesh);
                            }
                        }
                    }
                }
                // Find the center of the model
                Vector3 center = (worldMax + worldMin) / 2;

                // Move the the model such that its center is at the origin
                rootNode.Translation = new float[] { -center.x, -center.y, -center.z };

                // Add the child nodes to the root
                rootNode.Children = rootNodeIndices.ToArray();

                #region Create and populate the glTF object
                glTFObject = new glTFLoader.Schema.Gltf();

                #region Create and add an asset
                glTFLoader.Schema.Asset asset = new glTFLoader.Schema.Asset();
                asset.Generator = "FLUID Simulator";
                asset.Version = "2.0";

                glTFObject.Asset = asset;
                #endregion

                scene.Nodes = new int[] { 0 }; // Add the root node.
                glTFObject.Scenes = new glTFLoader.Schema.Scene[] { scene };
                glTFObject.Scene = 0;

                if (images.Count > 0)
                {
                    glTFObject.Images = images.ToArray();
                    glTFObject.Samplers = samplers.ToArray();
                    glTFObject.Textures = textures.ToArray();
                }
                glTFObject.Materials = materials.ToArray();

                glTFObject.Meshes = meshes.ToArray();

                glTFObject.Accessors = accessors.ToArray();
                glTFObject.Buffers = buffers.ToArray();
                glTFObject.BufferViews = bufferViews.ToArray();
                glTFObject.Nodes = nodes.ToArray();
                #endregion

                // We now have all the meshes specified so we can proceed to pack the
                // binary data into a byte array.
            }
            catch (Exception exception)
            {
                Debug.LogError(exception.Message + "\r\n" + exception.StackTrace);
                int jn = 2;
            }

            return JsonConvert.SerializeObject(glTFObject); //, Formatting.Indented);
        }
    }
}
