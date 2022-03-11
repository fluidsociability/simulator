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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Subdivide : MonoBehaviour
{
    public NavMeshSurface navMeshSurface;
    public GameObject exposedParent;
    public GameObject undercoverParent;
    public GameObject importedModel;
    public GameObject walkablePrefab;
    public GameObject exposedPrefab;

    // Start is called before the first frame update

    const ulong negativeXMask = 0x1UL << 60;
    const ulong negativeYMask = 0x1UL << 61;
    const ulong negativeZMask = 0x1UL << 62;

    bool EncodeVertex(Vector3 vertex, out ulong encodedVertex)
    {
        // Encoding 20 bits per scalar dimension + 1 sign bit = 63 bits.
        // 2^20 mm = 1048576 mm ~= 1 km;
        if (
            -1000f > vertex.x
            || vertex.x > 1000f
            || -1000f > vertex.y
            || vertex.y > 1000f
            || -1000f > vertex.z
            || vertex.z > 1000f
        )
        {
            encodedVertex = 0;
            return false;
        }

        int quantizedX = (int)(vertex.x * 1000.0f);
        int quantizedY = (int)(vertex.y * 1000.0f);
        int quantizedZ = (int)(vertex.z * 1000.0f);
        bool xIsNegative = vertex.x < 0;
        ulong x = (ulong)Math.Abs(quantizedX) & 0x000FFFFF;
        bool yIsNegative = vertex.y < 0;
        ulong y = (ulong)Math.Abs(quantizedY) & 0x000FFFFF;
        bool zIsNegative = vertex.z < 0;
        ulong z = (ulong)Math.Abs(quantizedZ) & 0x000FFFFF;

        encodedVertex =
            (xIsNegative ? negativeXMask : 0)
            | x << 40
            | (yIsNegative ? negativeYMask : 0)
            | y << 20
            | (zIsNegative ? negativeZMask : 0x0UL)
            | z << 0;

        return true;
    }

    Vector3 DecodeVertex(ulong encodedVertex)
    {
        // Encoding 20 bits per scalar dimension + 1 sign bit = 63 bits.
        // 2^20 mm = 1048576 mm ~= 1 km;
        bool xIsNegative = (encodedVertex & negativeXMask) == negativeXMask;
        bool yIsNegative = (encodedVertex & negativeYMask) == negativeYMask;
        bool zIsNegative = (encodedVertex & negativeZMask) == negativeZMask;
        ulong positiveX = (encodedVertex >> 40) & 0x000FFFFF;
        ulong positiveY = (encodedVertex >> 20) & 0x000FFFFF;
        ulong positiveZ = (encodedVertex >> 0) & 0x000FFFFF;

        return new Vector3(
            (xIsNegative ? -(float)positiveX : positiveX) / 1000f,
            (yIsNegative ? -(float)positiveY : positiveY) / 1000f,
            (zIsNegative ? -(float)positiveZ : positiveZ) / 1000f
        );
    }

    void Start()
    {
        foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        {
            DecomposeMeshEnvironmentally(meshRenderer);
        }

        navMeshSurface.GetComponent<NavMeshSurface>().BuildNavMesh();
        //exposedNavMesh.GetComponent<NavMeshSurface>().BuildNavMesh();
    }

    private void DecomposeMeshEnvironmentally(MeshRenderer meshRenderer)
    {
        bool didSubdivideTriangle = true;
        while (didSubdivideTriangle)
        {
            didSubdivideTriangle = false;
            float sampleResolutionInMeters = 2;
            Mesh sourceMesh = meshRenderer.GetComponent<MeshFilter>().mesh;

            List<int> newTriangles = new List<int>();
            List<Vector3> newPositions = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();
            for (
                int triangleIndex = 0;
                triangleIndex < sourceMesh.triangles.Length;
                triangleIndex += 3
            )
            {
                Vector3 positionA = sourceMesh.vertices[sourceMesh.triangles[triangleIndex]];
                Vector3 positionB = sourceMesh.vertices[sourceMesh.triangles[triangleIndex + 1]];
                Vector3 positionC = sourceMesh.vertices[sourceMesh.triangles[triangleIndex + 2]];

                float distanceAB = Vector3.Distance(positionA, positionB);
                float distanceBC = Vector3.Distance(positionB, positionC);
                float distanceCA = Vector3.Distance(positionC, positionA);

                Vector2 uvA = sourceMesh.uv[sourceMesh.triangles[triangleIndex]];
                Vector2 uvB = sourceMesh.uv[sourceMesh.triangles[triangleIndex + 1]];
                Vector2 uvC = sourceMesh.uv[sourceMesh.triangles[triangleIndex + 2]];

                if (
                    distanceAB > sampleResolutionInMeters
                    || distanceBC > sampleResolutionInMeters
                    || distanceCA > sampleResolutionInMeters
                )
                {
                    // Subdivide the triangle
                    Vector3 midPositionAB = (positionA + positionB) / 2;
                    Vector3 midPositionBC = (positionB + positionC) / 2;
                    Vector3 midPositionCA = (positionC + positionA) / 2;

                    Vector2 midUVAB = (uvA + uvB) / 2;
                    Vector2 midUVBC = (uvB + uvC) / 2;
                    Vector2 midUVCA = (uvC + uvA) / 2;

                    newTriangles.AddRange(
                        new int[]
                        {
                            newPositions.Count,
                            newPositions.Count + 1,
                            newPositions.Count + 2
                        }
                    );
                    newPositions.AddRange(
                        new Vector3[] { positionA, midPositionAB, midPositionCA }
                    );

                    newTriangles.AddRange(
                        new int[]
                        {
                            newPositions.Count,
                            newPositions.Count + 1,
                            newPositions.Count + 2
                        }
                    );
                    newPositions.AddRange(
                        new Vector3[] { positionB, midPositionBC, midPositionAB }
                    );

                    newTriangles.AddRange(
                        new int[]
                        {
                            newPositions.Count,
                            newPositions.Count + 1,
                            newPositions.Count + 2
                        }
                    );
                    newPositions.AddRange(
                        new Vector3[] { positionC, midPositionCA, midPositionBC }
                    );

                    newTriangles.AddRange(
                        new int[]
                        {
                            newPositions.Count,
                            newPositions.Count + 1,
                            newPositions.Count + 2
                        }
                    );
                    newPositions.AddRange(
                        new Vector3[] { midPositionAB, midPositionBC, midPositionCA }
                    );

                    newUVs.AddRange(new Vector2[] { uvA, midUVAB, midUVCA });
                    newUVs.AddRange(new Vector2[] { uvB, midUVBC, midUVAB });
                    newUVs.AddRange(new Vector2[] { uvC, midUVCA, midUVBC });
                    newUVs.AddRange(new Vector2[] { midUVAB, midUVBC, midUVCA });

                    didSubdivideTriangle = true;
                }
                else
                {
                    newTriangles.AddRange(
                        new int[]
                        {
                            newPositions.Count,
                            newPositions.Count + 1,
                            newPositions.Count + 2
                        }
                    );
                    newPositions.AddRange(new Vector3[] { positionA, positionB, positionC });
                    newUVs.AddRange(new Vector2[] { uvA, uvB, uvC });
                }
            }

            meshRenderer.GetComponent<MeshFilter>().mesh = new Mesh();
            meshRenderer.GetComponent<MeshFilter>().mesh.vertices = newPositions.ToArray();
            meshRenderer.GetComponent<MeshFilter>().mesh.uv = newUVs.ToArray();
            meshRenderer.GetComponent<MeshFilter>().mesh.triangles = newTriangles.ToArray();
        }

        List<int> newStandardTriangles = new List<int>();
        List<Vector3> newStandardPositions = new List<Vector3>();
        List<Vector2> newStandardUVs = new List<Vector2>();

        List<int> newExposedTriangles = new List<int>();
        List<Vector3> newExposedPositions = new List<Vector3>();
        List<Vector2> newExposedUVs = new List<Vector2>();

        Mesh subdividedMesh = meshRenderer.GetComponent<MeshFilter>().mesh;
        Transform meshTransform = meshRenderer.transform;
        for (
            int triangleIndex = 0;
            triangleIndex < subdividedMesh.triangles.Length;
            triangleIndex += 3
        )
        {
            Vector3 worldVertexA = meshTransform.TransformPoint(
                subdividedMesh.vertices[triangleIndex]
            );
            Vector3 worldVertexB = meshTransform.TransformPoint(
                subdividedMesh.vertices[triangleIndex + 1]
            );
            Vector3 worldVertexC = meshTransform.TransformPoint(
                subdividedMesh.vertices[triangleIndex + 2]
            );
            Vector3 sideA = worldVertexA - worldVertexB;
            Vector3 sideB = worldVertexB - worldVertexC;
            Vector3 cross = Vector3.Cross(sideA, sideB).normalized;

            if (cross.y > 0.707f)
            {
                int exposedCount = 0;
                List<Vector3> positions = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                for (
                    int vertexIndex = triangleIndex;
                    vertexIndex < triangleIndex + 3;
                    vertexIndex++
                )
                {
                    if (vertexIndex < subdividedMesh.vertices.Length)
                    {
                        positions.Add(subdividedMesh.vertices[vertexIndex]);
                        uvs.Add(subdividedMesh.uv[vertexIndex]);

                        Vector3 worldPosition = meshTransform.TransformPoint(
                            subdividedMesh.vertices[vertexIndex]
                        );

                        RaycastHit hit;
                        if (Physics.Raycast(worldPosition, Vector3.up, out hit))
                        {
                            // Not exposed
                        }
                        else
                        {
                            exposedCount++;
                        }
                    }
                    else { }
                }
                if (exposedCount >= 2)
                {
                    newExposedTriangles.AddRange(
                        new int[]
                        {
                            newExposedPositions.Count,
                            newExposedPositions.Count + 1,
                            newExposedPositions.Count + 2
                        }
                    );
                    newExposedPositions.AddRange(
                        new Vector3[] { positions[0], positions[1], positions[2] }
                    );
                    newExposedUVs.AddRange(new Vector2[] { uvs[0], uvs[1], uvs[2] });
                }
                else
                {
                    newStandardTriangles.AddRange(
                        new int[]
                        {
                            newStandardPositions.Count,
                            newStandardPositions.Count + 1,
                            newStandardPositions.Count + 2
                        }
                    );
                    newStandardPositions.AddRange(
                        new Vector3[] { positions[0], positions[1], positions[2] }
                    );
                    newStandardUVs.AddRange(new Vector2[] { uvs[0], uvs[1], uvs[2] });
                }
            }
        }

        {
            GameObject walkableContainer = Instantiate<GameObject>(
                walkablePrefab,
                undercoverParent.transform
            );
            walkableContainer.transform.position = meshTransform.position;
            walkableContainer.transform.rotation = meshTransform.rotation;
            Mesh walkable = new Mesh();
            walkable.name = "Walkable";
            walkable.vertices = newStandardPositions.ToArray();
            walkable.uv = newStandardUVs.ToArray();
            walkable.triangles = newStandardTriangles.ToArray();
            walkableContainer.GetComponent<MeshFilter>().mesh = walkable;
            walkableContainer.GetComponent<MeshCollider>().sharedMesh = walkable;
        }

        {
            GameObject exposedContainer = Instantiate<GameObject>(
                exposedPrefab,
                exposedParent.transform
            );
            exposedContainer.transform.position = meshTransform.position;
            exposedContainer.transform.rotation = meshTransform.rotation;

            Mesh exposed = new Mesh();
            exposed.name = "Exposed";
            exposed.vertices = newExposedPositions.ToArray();
            exposed.uv = newExposedUVs.ToArray();
            exposed.triangles = newExposedTriangles.ToArray();

            // undercoverMeshFilter.gameObject.SetActive(false);

            exposedContainer.GetComponent<MeshFilter>().mesh = exposed;
            exposedContainer.GetComponent<MeshCollider>().sharedMesh = exposed;
        }
    }

    // Update is called once per frame
    void Update() { }
}
