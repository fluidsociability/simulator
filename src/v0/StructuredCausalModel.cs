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

using FloodSpill;
using FloodSpill.Utilities;
using FLUID;
using FLUID_Simulator;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Experimental.AI;

public class StructuredCausalModel : MonoBehaviour
{
    public System.Random random;
    public GameObject markerPrefab;
    public List<string> versions;
    public Camera encounterCamera;
    public Material wallMaterial;
    public SimulationTicket simulationTicket;

    // public List<Dwelling> dwellings = new List<Dwelling>();
    public List<Place> dwellPoints = new List<Place>();
    public List<Place> semiPrivateSpaces = new List<Place>();
    public List<GameObject> normalDoors = new List<GameObject>();
    public List<GameObject> secondaryDoors = new List<GameObject>();
    public Dictionary<string, List<Place>> elevatorBankDictionary =
        new Dictionary<string, List<Place>>();
    public PathSegmentCollection pathSegmentCollection = new PathSegmentCollection();
    public Texture2D mandatoryTexture;
    public Texture2D optionalTexture;
    public Bounds bounds;
    public GameObject floorPrefab;
    public float elevationShift = 0;
    public List<int> dailyEncounters = new List<int>();

    // public IVoxelGrid privacyPreservingVoxelGrid;
    public SparseVoxelGrid generalVoxelGrid;
    public float totalNavigableSpace = 0;
    public float totalJourneySpace = 0;
    public float totalPauseSpace = 0;
    public float totalPublicSpace = 0;
    public float totalPrivateSpace = 0;
    public float totalSemiPrivateSpace = 0;

    float stepSize = 0.25f;

    public const short navigableBit = 0x0001;
    public const short privacyPreservingBit = 0x0002;
    public const short lockdownBit = 0x0004;
    public const short journeySpaceBit = 0x0008;
    public const short pauseSpaceBit = 0x0010;
    public const short publicSpaceBit = 0x0020;
    public const short privateSpaceBit = 0x0040;
    public const short semiPrivateSpaceBit = 0x0080;
    public const short outsideBit = 0x0100;

    //public const short dwellingPrimaryDoorBit = 0x0200;
    //public const short dwellingSecondaryDoorBit = 0x0400;
    //public const short normalDoorBit = 0x0800;
    public const short allBits = 0x7FFF;

    Vector3 pathScale = new Vector3(1, 3, 1);

    const float journeySpaceRange = 0.5f; // meters
    const float pauseSpaceRange = 4.5f; // meters

    const int maximumSamplesPerCubicMeter = 500;

    public const int secondsPerHour = 60 * 60;
    public const int secondsPerDay = 24 * secondsPerHour;
    public const int secondsPerYear = 365 * secondsPerDay;

    public int startSecond = 0;

    VegaGenerator vegaGenerator = new VegaGenerator();

    #region Job parameters specified in Unity
    public string title;
    public double dayStartTimeInHours;
    public double dayEndTimeInHours;

    string componentsString;

    string familyProtypesString;

    string occupancyString;

    string agentTypeString;

    string useCasesString;

    public Dictionary<string, Weather> weatherByLocation = new Dictionary<string, Weather>();

    [TextArea(5, 5)]
    public string multiUnitResidentialComponentsString;

    [TextArea(5, 5)]
    public string multiUnitResidentialFamilyProtypesString;

    //[TextArea(5, 5)]
    //public string multiUnitResidentialOccupancyString;

    [TextArea(5, 5)]
    public string multiUnitResidentialAgentTypeString;

    [TextArea(5, 5)]
    public string multiUnitResidentialUseCasesString;

    [TextArea(5, 5)]
    public string labComponentsString;

    [TextArea(5, 5)]
    public string labFamilyProtypesString;

    //[TextArea(5, 5)]
    //public string labOccupancyString;

    [TextArea(5, 5)]
    public string labAgentTypeString;

    [TextArea(5, 5)]
    public string labUseCasesString;

    [TextArea(5, 5)]
    public string studentHousingComponentsString;

    [TextArea(5, 5)]
    public string studentHousingFamilyProtypesString;

    //[TextArea(5, 5)]
    //public string studentHousingOccupancyString;

    [TextArea(5, 5)]
    public string studentHousingAgentTypeString;

    [TextArea(5, 5)]
    public string studentHousingUseCasesString;

    [TextArea(5, 5)]
    public string weatherString;

    public GameObject outsidePrefab;
    public GameObject undercoverPrefab;
    public GameObject insidePrefab;

    public GameObject outsideContainerPrefab;
    public GameObject undercoverContainerPrefab;
    public GameObject insideContainerPrefab;

    public GameObject doorPlatformPrefab;

    GameObject outsideContainer;
    GameObject undercoverContainer;
    GameObject insideContainer;
    #endregion

    #region Generated simulation objects
    public NavMeshSurface navMeshSurface
    {
        get { return GetComponent<NavMeshSurface>(); }
    }

    // Dictionary<string, string> socialSyntaxTagByComponentNameDictionary = new Dictionary<string, string>();
    Dictionary<FLUID.Component, int> componentPreferedDestinationMaximumDictionary =
        new Dictionary<FLUID.Component, int>();
    public List<FLUID.Component> components = new List<FLUID.Component>();
    List<UnitOccupancy> unitOccupancies = new List<UnitOccupancy>();

    //List<Occupancy> occupancies = new List<Occupancy>();
    List<AgentType> agentTypes = new List<AgentType>();
    public List<UseCase> useCases = new List<UseCase>();
    public List<Agent> agents = new List<Agent>();

    GameObject sparksContainer = null;
    GameObject walkingContainer = null;
    GameObject laserbeamContainer = null;
    GameObject firstContactContainer = null;
    public GameObject walkingPrefab;
    public GameObject sparkPrefab;
    public GameObject householdPrefab;
    public GameObject elevatorPrefab;
    public GameObject mailRoomPrefab;
    public GameObject amenityRoomPrefab;

    //public GameObject outsidePrefab;
    public GameObject garbageRoomPrefab;
    public GameObject linePrefab;
    public GameObject encounterPrefab;

    Dictionary<string, Dictionary<string, UInt32>> elevatorEncounters =
        new Dictionary<string, Dictionary<string, uint>>();
    #endregion

    public DateTime simulationStartTime;
    const string tagPrefix = "FLUID ";
    public Vector3 spaceStep = new Vector3(0.025f, 0.025f, 0.025f);
    static Vector3 verticalOffset = new Vector3(0.0f, 0.02f, 0.0f);

    //static Vector3 doorAdjust = new Vector3(0, -0.01f, 0);
    public AtlasHandler atlasHandler;

    FloodSpiller floodSpiller = new FloodSpiller();

    public SpaceTypeWeights dryTemperateSpaceTypeWeights = new SpaceTypeWeights(1.0f, 1.0f, 1.1f);

    public SpaceTypeWeights dryIntemperateSpaceTypeWeights = new SpaceTypeWeights(2.0f, 2.0f, 1.0f);

    public SpaceTypeWeights wetTemperateSpaceTypeWeights = new SpaceTypeWeights(2.0f, 1.0f, 1.0f);

    public SpaceTypeWeights wetIntemperateSpaceTypeWeights = new SpaceTypeWeights(3.0f, 2.0f, 1.0f);

    List<SpaceTypeWeights> weatherWeights = new List<SpaceTypeWeights>();

    Dictionary<long, int> conversationLocationCounts = new Dictionary<long, int>();

    public DateTime newYearMidnight = new DateTime(2020, 1, 1);

    public static Vector3 FindBottomCenter(GameObject gameObject)
    {
        // We need to find the center of the object and make it an attractor for agents.
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        // float maxY = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        bool wasEnabled = gameObject.GetComponent<MeshRenderer>().enabled;
        gameObject.GetComponent<MeshRenderer>().enabled = true;
        foreach (Renderer renderer in gameObject.GetComponents<Renderer>())
        {
            minX = Math.Min(minX, renderer.bounds.min[0]);
            maxX = Math.Max(maxX, renderer.bounds.max[0]);
            minY = Math.Min(minY, renderer.bounds.min[1]);
            // maxY = Math.Max(maxY, renderer.bounds.max[1]);
            minZ = Math.Min(minZ, renderer.bounds.min[2]);
            maxZ = Math.Max(maxZ, renderer.bounds.max[2]);
        }
        gameObject.GetComponent<MeshRenderer>().enabled = wasEnabled;
        return new Vector3((minX + maxX) / 2, minY, (minZ + maxZ) / 2) + verticalOffset;
    }

    FLUID.Component FindComponentWithName(string name)
    {
        FLUID.Component result = null;
        foreach (FLUID.Component component in components)
        {
            if (component.name == name)
            {
                result = component;
                break;
            }
        }
        return result;
    }

    private void FloodFill(
        SparseVoxelGrid sparseVoxelGrid,
        Dictionary<long, int[]> seeds,
        Dictionary<long, byte> previouslyVisitedVoxels,
        short sourceValue,
        short sourceMask,
        short replacementValue,
        short replacementMask
    )
    {
        if (seeds.Count == 0)
            return;

        // cache the first element
        long encodedSeed = -1;
        foreach (long someEncodedSeed in seeds.Keys)
        {
            encodedSeed = someEncodedSeed;
            break;
        }
        int[] seed = seeds[encodedSeed];

        // Add seeds in the vicinity of the starting seeds.
        Predicate<int, int> positionQualifier = (xIndex, zIndex) =>
            (
                sparseVoxelGrid.GetAt(xIndex, seed[1], zIndex, sourceMask)
                == (sourceValue & sourceMask)
            );
        Action<int, int> positionVisitor = (xIndex, zIndex) =>
        {
            for (
                int zz = Math.Max(0, zIndex - 1);
                zz < Math.Min(zIndex + 2, sparseVoxelGrid.Depth);
                zz++
            )
            {
                for (
                    int yy = Math.Max(0, seed[1] - 1);
                    yy < Math.Min(seed[1] + 2, sparseVoxelGrid.Height);
                    yy += 2
                )
                {
                    for (
                        int xx = Math.Max(0, xIndex - 1);
                        xx < Math.Min(xIndex + 2, sparseVoxelGrid.Width);
                        xx++
                    )
                    {
                        long encodedIndex = sparseVoxelGrid.EncodeIndex(xx, yy, zz);
                        if (previouslyVisitedVoxels.ContainsKey(encodedIndex) == false)
                        {
                            if (
                                sparseVoxelGrid.GetAt(encodedIndex, sourceMask)
                                == (sourceValue & sourceMask)
                            )
                            {
                                seeds[encodedIndex] = new int[] { xx, yy, zz };
                            }
                        }
                    }
                }
            }
        };

        var floodParameters = new FloodParameters(startX: seed[0], startY: seed[2])
        {
            Qualifier = positionQualifier,
            SpreadingPositionVisitor = positionVisitor
        };

        int[,] result = new int[sparseVoxelGrid.Width, sparseVoxelGrid.Depth];

        floodSpiller.SpillFlood(floodParameters, result);

        // Copy the result into the target layer.
        for (int z = 0; z < sparseVoxelGrid.Depth; z++)
        {
            for (int x = 0; x < sparseVoxelGrid.Width; x++)
            {
                if (result[x, z] != int.MaxValue)
                {
                    long encodedIndex = sparseVoxelGrid.EncodeIndex(x, seed[1], z);
                    sparseVoxelGrid.SetAt(encodedIndex, replacementValue, replacementMask);
                    previouslyVisitedVoxels[encodedIndex] = 0x01;
                }
            }
        }
        seeds.Remove(encodedSeed);
        List<long> encodedSeedsToRemove = new List<long>();
        foreach (long testSeed in seeds.Keys)
        {
            if (sparseVoxelGrid.GetAt(testSeed, sourceMask) != (sourceValue & sourceMask))
            {
                encodedSeedsToRemove.Add(testSeed);
            }
        }
        foreach (long keyToRemove in encodedSeedsToRemove)
        {
            seeds.Remove(keyToRemove);
        }
        previouslyVisitedVoxels[encodedSeed] = 0x01;
    }

    private bool IsTaggedMesh(GameObject testObject)
    {
        MeshFilter meshFilter;
        if (testObject.TryGetComponent(out meshFilter))
        {
            if (meshFilter.mesh.name.Contains(tagPrefix))
            {
                return true;
            }
        }
        return false;
    }

    public static int GetAgentTypeIDByName(string agentTypeName)
    {
        int count = NavMesh.GetSettingsCount();
        string[] agentTypeNames = new string[count + 2];
        for (var i = 0; i < count; i++)
        {
            int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            string name = NavMesh.GetSettingsNameFromID(id);
            if (name == agentTypeName)
            {
                return id;
            }
        }
        return -1;
    }

    public enum Exposure
    {
        Outside,
        Undercover,
        Inside,
        Mixed
    }

    private static Exposure ConditionallySubdivideTriangle(
        Vector3 positionA,
        Vector3 positionB,
        Vector3 positionC,
        float hypotenuseLength,
        SparseVoxelGrid sparseVoxelGrid,
        float threshold,
        Bounds bounds,
        Vector3 spaceStep,
        List<Vector3> outsideTriangles,
        List<Vector3> undercoverTriangles,
        List<Vector3> insideTriangles
    )
    {
        Exposure result = Exposure.Mixed;
        if (hypotenuseLength > threshold)
        {
            // Subdivide the triangle into four sub triangles.
            Vector3 midPositionAB = (positionA + positionB) / 2;
            Vector3 midPositionBC = (positionB + positionC) / 2;
            Vector3 midPositionCA = (positionC + positionA) / 2;

            Exposure centerTriangleExposure = ConditionallySubdivideTriangle(
                midPositionAB,
                midPositionBC,
                midPositionCA,
                hypotenuseLength / 2.0f,
                sparseVoxelGrid,
                threshold,
                bounds,
                spaceStep,
                outsideTriangles,
                undercoverTriangles,
                insideTriangles
            );
            Exposure triangleAExposure = ConditionallySubdivideTriangle(
                positionA,
                midPositionAB,
                midPositionCA,
                hypotenuseLength / 2.0f,
                sparseVoxelGrid,
                threshold,
                bounds,
                spaceStep,
                outsideTriangles,
                undercoverTriangles,
                insideTriangles
            );
            Exposure triangleBExposure = ConditionallySubdivideTriangle(
                positionB,
                midPositionBC,
                midPositionAB,
                hypotenuseLength / 2.0f,
                sparseVoxelGrid,
                threshold,
                bounds,
                spaceStep,
                outsideTriangles,
                undercoverTriangles,
                insideTriangles
            );
            Exposure triangleCExposure = ConditionallySubdivideTriangle(
                positionC,
                midPositionCA,
                midPositionBC,
                hypotenuseLength / 2.0f,
                sparseVoxelGrid,
                threshold,
                bounds,
                spaceStep,
                outsideTriangles,
                undercoverTriangles,
                insideTriangles
            );

            bool allUndercover = true;
            bool allInside = true;
            bool allOutside = true;
            foreach (
                Exposure exposure in new Exposure[]
                {
                    centerTriangleExposure,
                    triangleAExposure,
                    triangleBExposure,
                    triangleCExposure
                }
            )
            {
                allOutside = allOutside && exposure == Exposure.Outside;
                allUndercover = allUndercover && exposure == Exposure.Undercover;
                allInside = allInside && exposure == Exposure.Inside;
            }
            if (allOutside)
            {
                result = Exposure.Outside;
            }
            else if (allUndercover)
            {
                result = Exposure.Undercover;
            }
            else if (allInside)
            {
                result = Exposure.Inside;
            }
            else
            {
                #region Add center triangle to a mesh subregion
                {
                    Vector3[] centerTriangleCorners = new Vector3[]
                    {
                        midPositionAB,
                        midPositionBC,
                        midPositionCA
                    };
                    switch (centerTriangleExposure)
                    {
                        case Exposure.Outside:
                            outsideTriangles.AddRange(centerTriangleCorners);
                            break;

                        case Exposure.Undercover:
                            undercoverTriangles.AddRange(centerTriangleCorners);
                            break;

                        case Exposure.Inside:
                            insideTriangles.AddRange(centerTriangleCorners);
                            break;
                    }
                }
                #endregion
                #region Add triangle A to a mesh subregion
                {
                    Vector3[] triangleACorners = new Vector3[]
                    {
                        positionA,
                        midPositionAB,
                        midPositionCA
                    };
                    switch (triangleAExposure)
                    {
                        case Exposure.Outside:
                            outsideTriangles.AddRange(triangleACorners);
                            break;

                        case Exposure.Undercover:
                            undercoverTriangles.AddRange(triangleACorners);
                            break;

                        case Exposure.Inside:
                            insideTriangles.AddRange(triangleACorners);
                            break;
                    }
                }
                #endregion
                #region Add triangle B to a mesh subregion
                {
                    Vector3[] triangleBCorners = new Vector3[]
                    {
                        positionB,
                        midPositionBC,
                        midPositionAB
                    };
                    switch (triangleBExposure)
                    {
                        case Exposure.Outside:
                            outsideTriangles.AddRange(triangleBCorners);
                            break;

                        case Exposure.Undercover:
                            undercoverTriangles.AddRange(triangleBCorners);
                            break;

                        case Exposure.Inside:
                            insideTriangles.AddRange(triangleBCorners);
                            break;
                    }
                }
                #endregion
                #region Add triangle C to a mesh subregion
                {
                    Vector3[] triangleCCorners = new Vector3[]
                    {
                        positionC,
                        midPositionCA,
                        midPositionBC
                    };
                    switch (triangleCExposure)
                    {
                        case Exposure.Outside:
                            outsideTriangles.AddRange(triangleCCorners);
                            break;

                        case Exposure.Undercover:
                            undercoverTriangles.AddRange(triangleCCorners);
                            break;

                        case Exposure.Inside:
                            insideTriangles.AddRange(triangleCCorners);
                            break;
                    }
                }
                #endregion

                result = Exposure.Mixed;
            }
        }
        else
        {
            #region Fire rays from all corners.
            int noObstructionsAboveCount = 0;
            int vertexMarkedAsOutsideCount = 0;
            foreach (Vector3 vertex in new Vector3[] { positionA, positionB, positionC })
            {
                RaycastHit downwardHit;
                if (
                    Physics.Raycast(
                        vertex + Vector3.up,
                        Vector3.down,
                        out downwardHit,
                        Mathf.Infinity
                    )
                )
                {
                    int xIndex = (int)Math.Round(
                        (downwardHit.point.x - bounds.min.x) / spaceStep.x
                    );
                    int yIndex = (int)Math.Round(
                        (downwardHit.point.y - bounds.min.y + sparseVoxelGrid.elevationShift)
                            / spaceStep.y
                    );
                    int zIndex = (int)Math.Round(
                        (downwardHit.point.z - bounds.min.z) / spaceStep.z
                    );
                    for (
                        int y = Math.Max(0, yIndex - 1);
                        y < Math.Min(yIndex + 2, sparseVoxelGrid.Height);
                        y++
                    )
                    {
                        if (sparseVoxelGrid.GetAt(xIndex, y, zIndex, outsideBit) == outsideBit)
                        {
                            vertexMarkedAsOutsideCount++;
                            break;
                        }
                    }
                }
                RaycastHit upwardHit;
                if (Physics.Raycast(vertex, Vector3.up, out upwardHit) == false)
                {
                    noObstructionsAboveCount++;
                }
            }
            #endregion

            // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
            if (noObstructionsAboveCount == 3)
            {
                result = Exposure.Outside;
            }
            else if (vertexMarkedAsOutsideCount == 3)
            {
                result = Exposure.Undercover;
            }
            else if (noObstructionsAboveCount == 0 && vertexMarkedAsOutsideCount == 0)
            {
                result = Exposure.Inside;
            }
            else
            {
                // Its a mixture.
                if (noObstructionsAboveCount > 0)
                {
                    result = Exposure.Outside;
                }
                else if (vertexMarkedAsOutsideCount == 2)
                {
                    result = Exposure.Undercover;
                }
                else
                {
                    result = Exposure.Inside;
                }
            }
        }
        return result;
    }

    private void PartitionMesh(
        MeshRenderer meshRenderer,
        GameObject outsideContainer,
        GameObject undercoverContainer,
        GameObject insideContainer,
        SparseVoxelGrid sparseVoxelGrid,
        Bounds bounds,
        Vector3 spaceStep
    )
    {
        const float sampleResolutionInMeters = 1;
        Mesh sourceMesh = meshRenderer.GetComponent<MeshFilter>().mesh;
        Transform meshTransform = meshRenderer.transform;

        List<Vector3> outsideTriangles = new List<Vector3>();
        List<Vector3> undercoverTriangles = new List<Vector3>();
        List<Vector3> insideTriangles = new List<Vector3>();
        for (
            int triangleVertexIndex = 0;
            triangleVertexIndex < sourceMesh.triangles.Length;
            triangleVertexIndex += 3
        )
        {
            Vector3 localPositionA = sourceMesh.vertices[
                sourceMesh.triangles[triangleVertexIndex + 0]
            ];
            Vector3 localPositionB = sourceMesh.vertices[
                sourceMesh.triangles[triangleVertexIndex + 1]
            ];
            Vector3 localPositionC = sourceMesh.vertices[
                sourceMesh.triangles[triangleVertexIndex + 2]
            ];

            Vector3 worldPositionA = meshTransform.TransformPoint(localPositionA);
            Vector3 worldPositionB = meshTransform.TransformPoint(localPositionB);
            Vector3 worldPositionC = meshTransform.TransformPoint(localPositionC);

            //Vector3 sideA = worldPositionA - worldPositionB;
            //Vector3 sideB = worldPositionB - worldPositionC;
            //Vector3 normal = Vector3.Cross(sideA, sideB).normalized;

            //if (normal.y > 0.6f) // ~-45 < normal.y < ~45 degrees to vertical...
            {
                #region Find the length of the longest edge to subdivide
                // ie: the length of the hypotenuse

                float distanceAB = Vector3.Distance(worldPositionA, localPositionB);
                float distanceBC = Vector3.Distance(localPositionB, localPositionC);
                float distanceCA = Vector3.Distance(localPositionC, worldPositionA);

                float hypotenuseLength;
                if (distanceAB > distanceBC && distanceAB > distanceCA)
                {
                    hypotenuseLength = distanceAB;
                }
                else if (distanceBC > distanceCA && distanceBC > distanceAB)
                {
                    hypotenuseLength = distanceBC;
                }
                else
                {
                    hypotenuseLength = distanceCA;
                }
                #endregion

                switch (
                    ConditionallySubdivideTriangle(
                        worldPositionA,
                        worldPositionB,
                        worldPositionC,
                        hypotenuseLength,
                        sparseVoxelGrid,
                        sampleResolutionInMeters,
                        bounds,
                        spaceStep,
                        outsideTriangles,
                        undercoverTriangles,
                        insideTriangles
                    )
                )
                {
                    case Exposure.Outside:
                        outsideTriangles.AddRange(
                            new Vector3[] { localPositionA, localPositionB, localPositionC }
                        );
                        break;

                    case Exposure.Undercover:
                        undercoverTriangles.AddRange(
                            new Vector3[] { localPositionA, localPositionB, localPositionC }
                        );
                        break;

                    case Exposure.Inside:
                        insideTriangles.AddRange(
                            new Vector3[] { localPositionA, localPositionB, localPositionC }
                        );
                        break;

                    default:
                        // triangles have already been added inside ConditionallySubdivideTriangle (recursively..)
                        break;
                }
            }
        }

        {
            GameObject outsideGameObject = Instantiate<GameObject>(
                outsidePrefab,
                outsideContainer.transform
            );
            outsideGameObject.transform.position = meshTransform.position;
            outsideGameObject.transform.rotation = meshTransform.rotation;
            Mesh outsideMesh = new Mesh();
            outsideMesh.name = "Outside";
            outsideMesh.vertices = outsideTriangles.ToArray();
            List<int> offsets = new List<int>();
            for (int index = 0; index < outsideTriangles.Count; index++)
            {
                offsets.Add(index);
            }
            outsideMesh.triangles = offsets.ToArray();
            outsideMesh.Optimize();
            outsideGameObject.GetComponent<MeshFilter>().mesh = outsideMesh;
            outsideGameObject.GetComponent<MeshCollider>().sharedMesh = outsideMesh;
        }

        {
            GameObject undercoverGameObject = Instantiate<GameObject>(
                undercoverPrefab,
                undercoverContainer.transform
            );
            undercoverGameObject.transform.position = meshTransform.position;
            undercoverGameObject.transform.rotation = meshTransform.rotation;

            Mesh undercover = new Mesh();
            undercover.name = "Undercover";
            undercover.vertices = undercoverTriangles.ToArray();
            List<int> offsets = new List<int>();
            for (int index = 0; index < undercoverTriangles.Count; index++)
            {
                offsets.Add(index);
            }
            undercover.triangles = offsets.ToArray();
            undercover.Optimize();
            undercoverGameObject.GetComponent<MeshFilter>().mesh = undercover;
            undercoverGameObject.GetComponent<MeshCollider>().sharedMesh = undercover;
        }

        {
            GameObject insideGameObject = Instantiate<GameObject>(
                insidePrefab,
                insideContainer.transform
            );
            insideGameObject.transform.position = meshTransform.position;
            insideGameObject.transform.rotation = meshTransform.rotation;

            Mesh inside = new Mesh();
            inside.name = "Inside";
            inside.vertices = insideTriangles.ToArray();
            List<int> offsets = new List<int>();
            for (int index = 0; index < insideTriangles.Count; index++)
            {
                offsets.Add(index);
            }
            inside.triangles = offsets.ToArray();
            inside.Optimize();

            insideGameObject.GetComponent<MeshFilter>().mesh = inside;
            insideGameObject.GetComponent<MeshCollider>().sharedMesh = inside;
        }
    }

    class CalendarWrapper
    {
        public CalendarWrapper(string name)
        {
            this.name = name;
        }

        public string name { get; set; }
        public List<List<ActivitySummary>> dailyActivitySummaries =
            new List<List<ActivitySummary>>();
    }

    public void StartSimulation(
        AtlasHandler atlasHandler,
        SimulationTicket simulationTicket,
        GameObject importedModel
    )
    {
        #region Initialize instance
        this.atlasHandler = atlasHandler;
        this.simulationTicket = simulationTicket;

        weatherWeights.Add(dryTemperateSpaceTypeWeights);
        weatherWeights.Add(dryIntemperateSpaceTypeWeights);
        weatherWeights.Add(wetTemperateSpaceTypeWeights);
        weatherWeights.Add(wetIntemperateSpaceTypeWeights);

        Camera.main.GetComponent<Orbit>().logos = importedModel.transform;
        Camera.main.GetComponent<Orbit>().ResetCamera();

        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Simulation Starting.")
        );
        atlasHandler.UpdateTicket(simulationTicket);

        #region Clear out the old simulation data
        components.Clear();
        agentTypes.Clear();
        unitOccupancies.Clear();
        useCases.Clear();
        agents.Clear();
        dailyEncounters.Clear();

        Destroy(sparksContainer);
        Destroy(walkingContainer);
        Destroy(firstContactContainer);
        Destroy(laserbeamContainer);
        #endregion

        bounds = GetBounds(importedModel);

        random = new System.Random(simulationTicket.Seed);
        simulationStartTime = DateTime.Now;
        DateTime lastHeartBeat = simulationStartTime;
        int defaultLayerIndex = LayerMask.NameToLayer("Default");
        int passThroughLayerIndex = LayerMask.NameToLayer("Pass Through");
        GameObject doorPlatform = Instantiate(doorPlatformPrefab);
        doorPlatform.SetActive(false);
        GameObject visitingDoorPlatform = Instantiate(doorPlatformPrefab);
        visitingDoorPlatform.SetActive(false);
        #endregion

        // -- Phase 1: Load spreadsheet

        #region Select hyperparameters based on building type
        switch (simulationTicket.buildingType.ToLower())
        {
            case "multi-unit residential":
                componentsString = multiUnitResidentialComponentsString;
                familyProtypesString = multiUnitResidentialFamilyProtypesString;
                //occupancyString = multiUnitResidentialOccupancyString;
                agentTypeString = multiUnitResidentialAgentTypeString;
                useCasesString = multiUnitResidentialUseCasesString;
                break;

            case "research laboratory":
                componentsString = labComponentsString;
                familyProtypesString = labFamilyProtypesString;
                //occupancyString = labOccupancyString;
                agentTypeString = labAgentTypeString;
                useCasesString = labUseCasesString;
                break;

            case "student residence":
                componentsString = studentHousingComponentsString;
                familyProtypesString = studentHousingFamilyProtypesString;
                //occupancyString = studentHousingOccupancyString;
                agentTypeString = studentHousingAgentTypeString;
                useCasesString = studentHousingUseCasesString;
                break;

            default:
                simulationTicket.eventLog.Add(
                    new SimulationEvent(
                        SimulationEventType.Error,
                        $"Unsupported usage specified: \"{simulationTicket.buildingType}\""
                    )
                );
                atlasHandler.UpdateTicket(simulationTicket);
                Debug.LogError($"Unsupported usage specified: \"{simulationTicket.buildingType}\"");
                return;
        }
        #endregion

        #region Load in the FLUID components
        try
        {
            components.Add(FLUID.Component.defaultComponent);
            bool headingsLine = true;
            foreach (
                string line in componentsString.Split(
                    new char[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (headingsLine)
                {
                    headingsLine = false;
                }
                else
                {
                    FLUID.Component component = new FLUID.Component(line);
                    components.Add(component);

                    // Cache some values in dictionaries to avoid having to scan for them later.
                    // socialSyntaxTagByComponentNameDictionary[fluidComponent.name] = fluidComponent.socialSyntaxTag;
                    componentPreferedDestinationMaximumDictionary[component] =
                        component.preferedDestinationMaximum;
                }
            }
        }
        catch (Exception e) { }
        #endregion

        #region Load in the agent types
        {
            bool headingsLine = true;

            foreach (
                string line in agentTypeString.Split(
                    new char[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (headingsLine)
                {
                    headingsLine = false;
                }
                else
                {
                    try
                    {
                        agentTypes.Add(new AgentType(line));
                    }
                    catch (Exception e) { }
                }
            }
        }
        #endregion

        #region Load in the family prototypes
        {
            bool headingsLine = true;
            foreach (
                string line in familyProtypesString.Split(
                    new char[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (headingsLine)
                {
                    headingsLine = false;
                }
                else
                {
                    try
                    {
                        unitOccupancies.Add(new UnitOccupancy(line, agentTypes));
                    }
                    catch (Exception e) { }
                }
            }
        }
        #endregion

        #region Load in the use cases
        Dictionary<UseCase, string> parentUseCasesStrings = new Dictionary<UseCase, string>();
        {
            bool headingsLine = true;
            foreach (
                string line in useCasesString.Split(
                    new char[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (headingsLine)
                {
                    headingsLine = false;
                }
                else
                {
                    try
                    {
                        useCases.Add(new UseCase(line, components, agentTypes));
                    }
                    catch (Exception e) { }
                }
            }
        }
        #endregion

        #region Bind use cases to agent types
        foreach (AgentType agentType in agentTypes)
        {
            agentType.BindUseCases(useCases);
        }
        #endregion

        #region Decode the parent use cases
        // This needs to be a separate pass to support forward referencing.
        {
            foreach (UseCase useCase in useCases)
            {
                useCase.ResolveParentUseCases(useCases);
                useCase.isRootUseCase = useCase.parentUseCases.Count == 0;
            }
        }
        #endregion

        #region Detect relevant elements in the model
        dwellPoints.Clear();
        try
        {
            foreach (
                MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>()
            )
            {
                if (meshRenderer.gameObject.name.Contains("[3866687]")) { }
                if (meshRenderer.gameObject.name.Contains("Bridge Space"))
                {
                    meshRenderer.gameObject.name = meshRenderer.gameObject.name.Replace(
                        "Bridge Space",
                        "Semi-Private Space"
                    );
                }
                if (IsTaggedMesh(meshRenderer.gameObject))
                {
                    #region Check to see if this mesh is an instance of a FLUID Component
                    Place result = null;
                    FLUID.Component matchingComponent = null;
                    foreach (FLUID.Component component in components)
                    {
                        if (meshRenderer.gameObject.name.Contains(component.name))
                        {
                            // Its a place!
                            Place place = meshRenderer.gameObject.AddComponent<Place>();
                            place.component = component;

                            #region Find out which use cases this place participates in, if any
                            // If there is any chance an agent might "live" here
                            if (place.component.baseOccupancy + place.component.variance > 0)
                            {
                                // Look for occupancies whose population overlaps with that of the suite
                                foreach (UnitOccupancy unitOccupancy in unitOccupancies)
                                {
                                    // Does the population of this Unit Occupancy fit within the range of populations the dwelling can support?
                                    if (
                                        place.component.baseOccupancy - place.component.variance
                                            <= unitOccupancy.population
                                        && unitOccupancy.population
                                            <= place.component.baseOccupancy
                                                - place.component.variance
                                    )
                                    {
                                        // Ensure all use cases of all agent types that live in this Unity Occupancy
                                        foreach (AgentType agentType in unitOccupancy.agentMix)
                                        {
                                            foreach (UseCase useCase in agentType.useCases)
                                            {
                                                // If the use case starts at the agent's default location...
                                                if (
                                                    useCase.startingPointComponents.Contains(
                                                        FLUID.Component.defaultComponent
                                                    )
                                                )
                                                {
                                                    // If the use case starting point is not already present in the use case dictionaries...
                                                    if (
                                                        useCase.startingPointComponents.Contains(
                                                            place.component
                                                        ) == false
                                                    )
                                                    {
                                                        // Add this to the use case dictionaries.
                                                        useCase.startingPointComponents.Add(
                                                            place.component
                                                        );
                                                        useCase.startingPointComponentPropensities.Add(
                                                            1
                                                        );
                                                        useCase.startingComponentPropensityDictionary[
                                                            place.component
                                                        ] = 1;
                                                    }
                                                }
                                                // If the use case ends at the agent's default location...
                                                if (
                                                    useCase.destinationComponents.Contains(
                                                        FLUID.Component.defaultComponent
                                                    )
                                                )
                                                {
                                                    // If the use case destination is not already present in the use case dictionaries...
                                                    if (
                                                        useCase.destinationComponents.Contains(
                                                            place.component
                                                        ) == false
                                                    )
                                                    {
                                                        // Add this to the use case dictionaries.
                                                        useCase.destinationComponents.Add(
                                                            place.component
                                                        );
                                                        useCase.destinationComponentPropensities.Add(
                                                            1
                                                        );
                                                        useCase.destinationComponentPropensityDictionary[
                                                            place.component
                                                        ] = 1;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            switch (place.component.socialSyntax)
                            {
                                case SocialSyntaxEnum.Circulation:
                                    #region Add elevator
                                    // Elevator doors have a special format which can be decoded into which elevator bank
                                    // this particular elevator belongs to.
                                    int preambleLength =
                                        place.gameObject.name.IndexOf("Elevator Door")
                                        + "Elevator Door".Length;
                                    int nameLength =
                                        place.gameObject.name.LastIndexOf("[") - preambleLength;

                                    string bankName = place.gameObject.name
                                        .Substring(preambleLength, nameLength)
                                        .Trim();
                                    if (string.IsNullOrEmpty(bankName))
                                        bankName = "<Default>";

                                    // If this is the first elevator we have encountered from this bank...
                                    if (elevatorBankDictionary.ContainsKey(bankName) == false)
                                    {
                                        // create a new elevator bank
                                        elevatorBankDictionary[bankName] = new List<Place>();
                                    }
                                    // Add the elevator to the bank.
                                    elevatorBankDictionary[bankName].Add(place);
                                    #endregion
                                    break;
                            }

                            if (result == null)
                            {
                                result = place;
                                matchingComponent = component;
                            }
                            else
                            {
                                if (component.name.Length > matchingComponent.name.Length)
                                {
                                    matchingComponent = component;
                                    result.component = matchingComponent;
                                }
                            }
                        }
                    }

                    if (result != null)
                    {
                        dwellPoints.Add(result);
                    }
                    #endregion

                    #region Check to see if this mesh is a FLUID Secondary Door
                    if (name.Contains("Secondary Door"))
                    {
                        secondaryDoors.Add(meshRenderer.gameObject);
                        continue;
                    }
                    #endregion
                }
                else
                {
                    #region Check to see if this mesh is a "normal" door
                    if (meshRenderer.gameObject.name.ToLower().Contains("door"))
                    {
                        normalDoors.Add(meshRenderer.gameObject);
                        continue;
                    }
                    #endregion

                    #region Check to see if this mesh is "door like"
                    // Infer unlabelled door like forms
                    if (
                        meshRenderer.gameObject.name.ToLower().Contains("wall") == false
                        && meshRenderer.gameObject.name.ToLower().Contains("stair") == false
                        && meshRenderer.gameObject.name.ToLower().Contains("window") == false
                        && meshRenderer.gameObject.name.ToLower().Contains("floor") == false
                        && meshRenderer.gameObject.name.Contains("FLUID") == false
                    )
                    {
                        // Does the mesh have the height of the door?
                        if (1.8f < meshRenderer.bounds.size.y && meshRenderer.bounds.size.y < 2.5f)
                        {
                            float maxLateralExtent = Mathf.Max(
                                meshRenderer.bounds.size.x,
                                meshRenderer.bounds.size.z
                            );
                            // Does the mesh have the width of the door?
                            if (0.6f < maxLateralExtent && maxLateralExtent < 1.1f)
                            {
                                normalDoors.Add(meshRenderer.gameObject);
                                continue;
                            }
                        }
                    }
                    #endregion
                }
            }

            #region Now remove the default use case from all agent types
            foreach (AgentType agentType in agentTypes)
            {
                foreach (UseCase useCase in agentType.useCases)
                {
                    if (useCase.startingPointComponents.Contains(FLUID.Component.defaultComponent))
                    {
                        useCase.startingPointComponents.Remove(FLUID.Component.defaultComponent);
                    }
                    if (useCase.destinationComponents.Contains(FLUID.Component.defaultComponent))
                    {
                        useCase.destinationComponents.Remove(FLUID.Component.defaultComponent);
                    }
                }
            }
            #endregion
        }
        catch (Exception e)
        {
            Debug.Log($"Model tag detection failed: {e.Message} -> {e.StackTrace}");
        }
        #endregion

        #region Load in the weather
        foreach (
            WeatherAsJson weatherAtLocation in JsonConvert.DeserializeObject<WeatherAsJson[]>(
                weatherString
            )
        )
        {
            Weather weather = new Weather();
            weather.isHotColdByHour = new BitArray(
                Convert.FromBase64String(
                    weatherAtLocation.isHotCold.Substring(2, weatherAtLocation.isHotCold.Length - 3)
                )
            );
            weather.isRainingByHour = new BitArray(
                Convert.FromBase64String(
                    weatherAtLocation.isRaining.Substring(2, weatherAtLocation.isRaining.Length - 3)
                )
            );
            weatherByLocation[weatherAtLocation.name] = weather;
        }
        #endregion

        simulationTicket.simulationProgress = 0.01f;
        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Loaded hyperparameters")
        );
        atlasHandler.UpdateTicket(simulationTicket);

        // -- Phase 2: Spacial analysis

        #region Turn off "shape form" objects
        foreach (Transform childTransform in importedModel.transform)
        {
            string name = childTransform.gameObject.name;

            if (name.Contains("shape form"))
            {
                childTransform.gameObject.SetActive(false);
            }
        }
        #endregion

        #region Ensure all meshes can be detected by ray casting
        foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        {
            MeshCollider meshCollider;
            if (meshRenderer.TryGetComponent<MeshCollider>(out meshCollider) == false)
            {
                meshRenderer.gameObject.AddComponent<MeshCollider>();
            }
        }
        #endregion

        // Try to match families

        #region Find the residential center of the building.
        Vector3 sum = new Vector3();
        int dwellingCount = 0;
        foreach (Place place in dwellPoints)
        {
            if (place is Place)
            {
                sum += place.BottomCenter;
                dwellingCount++;
            }
        }
        Vector3 residentialCenter = sum / dwellingCount;
        #endregion

        #region Sort the dwellings by distance from the residential center
        SortedDictionary<float, Place> suitesByRange = new SortedDictionary<float, Place>();
        foreach (Place place in dwellPoints)
        {
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Suite:
                    float range =
                        (place.BottomCenter - residentialCenter).magnitude
                        + (float)random.NextDouble() * 0.001f;
                    suitesByRange[range] = (Place)place;
                    break;

                default:
                    break;
            }
        }
        #endregion

        #region Try to preserve cohort family size and spatial proximity between designs
        // The current approach uses a cumulative memory of previously assigned households
        // to residences sorted by distance from some reference point (residential center).

        // The idea is to assign the same families to appropriate dwellings in some consistent way
        // Attempt 1: Find the closest available dwelling which meets the criteria of the household.

        // Note: The introduction of households, agentProfiles etc. arrived very late in the game
        // and has not been fully normalized.  There are some redundancies between Agent and AgentProfile.
        // Essentially, the Household and AgentProfile contain a persistent subset of the parameters
        // of a dwelling and its agents.
        // One approach would be to hide ephemeral properties of dwellings
        // and agents from serialization and serialize these classes directly. (not now..)
        List<Household> existingHouseholds = new List<Household>();
        List<Household> newHouseholds = new List<Household>();

        foreach (Place suite in suitesByRange.Values)
        {
            int lower = (int)(suite.component.baseOccupancy - suite.component.variance);
            int upper = (int)(suite.component.baseOccupancy + suite.component.variance);
            Household selectedHousehold = null;

            #region Try to match an existing household to this dwelling, if any
            foreach (Household household in existingHouseholds)
            {
                int householdPopulation = household.agentProfiles.Count;
                if (lower <= householdPopulation && householdPopulation <= upper)
                {
                    // We have found a valid place for the family
                    selectedHousehold = household;

                    // Assign the agents from the household to the dwelling
                    foreach (AgentProfile agentProfile in household.agentProfiles)
                    {
                        // Create and initialize a new agent
                        Agent agent = new Agent();
                        agent.id = agents.Count;
                        agent.suite = suite;
                        agent.agentProfile = agentProfile;
                        agent.caution = agentProfile.caution;
                        agents.Add(agent);

                        #region Find and assign the agent's type
                        foreach (AgentType agentType in agentTypes)
                        {
                            if (agentType.name == agentProfile.type)
                            {
                                suite.unitOccupancy.agentMix.Add(agentType);
                                agent.agentType = agentType;
                                break;
                            }
                        }
                        #endregion
                    }
                }
            }
            #endregion

            #region If we couldn't find an existing household, create one
            if (selectedHousehold == null)
            {
                double totalPropensity = 0;
                IList<UnitOccupancy> candidateUnitOccupancies = new List<UnitOccupancy>();
                foreach (UnitOccupancy unitOccupancy in unitOccupancies)
                {
                    if (
                        suite.component.baseOccupancy - suite.component.variance
                            <= unitOccupancy.population
                        && unitOccupancy.population
                            <= suite.component.baseOccupancy - suite.component.variance
                    )
                    {
                        candidateUnitOccupancies.Add(unitOccupancy);
                        totalPropensity += unitOccupancy.propensity;
                    }
                }

                // Sample the distribution
                double randomSample = random.NextDouble();
                double cumulativePropensity = 0;
                foreach (UnitOccupancy candidateUnitOccupancy in candidateUnitOccupancies)
                {
                    cumulativePropensity += candidateUnitOccupancy.propensity / totalPropensity;
                    if (cumulativePropensity > randomSample)
                    {
                        // We have a winner.
                        suite.unitOccupancy = candidateUnitOccupancy;
                        break;
                    }
                }
                if (suite.unitOccupancy == null)
                {
                    // No matching family prototype is available for this dwelling
                    // (Should not be possible..)
                    Debug.Break();
                    throw new Exception(
                        "Found a dwelling which has no matching family prototypes."
                    );
                }

                Household household = new Household(suite.component, suite.unitOccupancy.isFamily);
                newHouseholds.Add(household);

                foreach (AgentType agentType in suite.unitOccupancy.agentMix)
                {
                    double cautionRandomSample = random.NextDouble();

                    // Create and initialize a new agent

                    AgentProfile agentProfile = new AgentProfile();

                    Agent agent = new Agent();
                    agent.id = agents.Count;
                    agent.agentType = agentType;
                    agent.suite = suite;
                    agent.agentProfile = agentProfile;
                    agent.caution =
                        agentType.cautionBaseline
                        + agentType.cautionVariance * (2 * cautionRandomSample - 1);
                    agents.Add(agent);

                    agentProfile.type = agentType.name;
                    agentProfile.caution = agent.caution;
                    household.agentProfiles.Add(agentProfile);
                }
            }
            #endregion
        }
        #endregion

        JsonSerializer serializer = new JsonSerializer();
        serializer.NullValueHandling = NullValueHandling.Ignore;

        using (StreamWriter sw = new StreamWriter("cohort.json"))
        {
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                serializer.Serialize(writer, newHouseholds);
            }
        }

        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Found waypoints.")
        );
        atlasHandler.UpdateTicket(simulationTicket);

        #region Create the 3d space type voxel grids
        int xCount = (int)Math.Round(bounds.size.x / spaceStep.x) + 1;
        int yCount = (int)Math.Round(bounds.size.y / spaceStep.y) + 1;
        int zCount = (int)Math.Round(bounds.size.z / spaceStep.z) + 1;

        // privacyPreservingVoxelGrid = new SparseVoxelGrid(xCount, yCount, zCount);
        generalVoxelGrid = new SparseVoxelGrid(xCount, yCount, zCount);
        #endregion

        /*
        I developed a way to generate a bunch of floor maps with different accessibility to accomplish various tasks.
        This  machinery can generate a partitioned floor mesh for the entire model which has different movement
        penalties: namely Outside, Undercover and Inside built into the geometry.

        There is also an "implicit" penalty of "infinity" for an agent to pass through the front door of
        a dwelling other than their own.

        So to complete spatial inference of a building we not only need to capture Outside, Undercover and Inside;
        we also need to capture circulation space, pause space and private space.

        Lets start with Inside
        What is it to be "Inside"?
        This is a vague and ambiguous question..let switch to:
        What is it to be "Outside"? and, what is it to be "Undercover" and then we can define "Inside" to be that
        which is not "Outside" or "Undercover" .
        Which will give us "Inside"

        To work out what is "Outside":

        Close all doors

        Pour magic paint on the ground at the location of any affordance labelled "Outside".
        It will creep its way to every entrance and exit of the building.

        To work out what is undercover, keep subdividing triangles in the floor plates until the longest side is
        shorter than, say, 1 meter.

        Then fire a ray straight up from each vertex of all the small triangles.
        If the ray hits something, this corner of the triangle is undercover.


        We can use a similar technique to give us "private space".
        
        What is it to be in a private space?

        It is everywhere that is not "semi-private" or "public" space that is adjacent to a dwelling entrance.

        Where are public spaces?

        Affordances are publicly accessible points in space.

        Thus if we lock all primary and secondary dwelling doors, and then pour magic paint on the ground
        at every affordance and elevator entrance, the paint will make it to the doorstep of every dwelling
        in the model.

        We can then determine Semi-private space by hiding everything but objects marked with the "Sem-private"
        space type and rasterizing these into the voxel grid.

        Thus we need to paint the model with "public" paint correctly before we can infer private space.
        */

        /*
        It is important to distingish two separate properties that are intimately related.
        Path finding is related to layer.  Objects in all but the "pass through" layer are considered barriers
        to agent movement.  This is completely different to ray casting in which line of sight operations
        can reveal such things as the correct elevation of the floor, whether or not a given point is exposed
        to rainfall and line of sight between agents.  Ray casting is not sensitive to layers, however objects
        that should block rays need to have an active "Collider" to hit which is distinct from a RenderMesh which
        is purely for aesthic output.

        Thus, changing a door's layer from default to pass through would remove it from barriers to path finding
        but nothing more.
        */

        #region Make all mandatory/optional affordances "pass through"
        foreach (Place place in dwellPoints)
        {
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Suite:
                    break;

                default:
                    place.gameObject.layer = passThroughLayerIndex;
                    place.GetComponent<Collider>().enabled = false;
                    break;
            }
        }
        #endregion

        /*
        When planning journeys from dwellings to various destinations, separate paths are statically generated
        - from every dwelling entrance to the vertically closest elevator door (for each bank),
        - from every dwelling entrance to every relevant affordance
        - and from every affordance the the vertically closest elevator (for each bank).
        In order to support secondary exits from a dwelling, all secondary dwelling doors
        are left open (moved to the pass through layer) during path finding as are all normal doors.
        All dwelling entrances, however, are locked (moved to the default layer).

        Thus once we have determined the navigation meshes with various doors locked and unlocked,
        we can use these meshes as proxies for the building's floor space.

        The first floor plate we want to create is the one to use as the proxy for the entire building.
        This will be partitioned in various ways to produce a number of space types:
        namely Outside, Undercover and Inside.

        We want to avoid regenerating, and then repartitioning, the floor space of the entire building
        for each dwelling (all dwelling doors are locked except the one which is the dwelling of the
        active family of agents).

        To do this we generate a special floor mesh where all doors are opened except dwelling doors.
        We can then open a dwelling door by placing a floor plate at the location of the front door which
        agent's within this dwelling can start and end their journeys.

        This is called the partitionable mesh.
        */

        #region Unlock all doors except dwelling entrances
        foreach (Place place in dwellPoints)
        {
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Suite:
                    place.gameObject.layer = defaultLayerIndex;
                    place.GetComponent<Collider>().enabled = true;
                    break;

                default:
                    break;
            }
        }

        // Unlock all the secondary doors.
        foreach (GameObject secondaryDoor in secondaryDoors)
        {
            secondaryDoor.layer = passThroughLayerIndex;
            secondaryDoor.GetComponent<Collider>().enabled = false;
        }

        // Unlock all the normal doors.
        foreach (GameObject normalDoor in normalDoors)
        {
            normalDoor.layer = passThroughLayerIndex;
            normalDoor.GetComponent<Collider>().enabled = false;
        }
        #endregion

        #region Create a partitionable navigation mesh object
        navMeshSurface.agentTypeID = GetAgentTypeIDByName("Tight");
        navMeshSurface.BuildNavMesh();
        navMeshSurface.agentTypeID = 0;

        GameObject partitionalbleNavigationMesh = Instantiate(floorPrefab);
        {
            partitionalbleNavigationMesh.name = "Partitionable";

            Mesh mesh = new Mesh();
            NavMeshTriangulation triangulatedNavMesh = NavMesh.CalculateTriangulation();
            mesh.name = "Partitionable Mesh";
            mesh.vertices = triangulatedNavMesh.vertices;
            mesh.triangles = triangulatedNavMesh.indices;

            partitionalbleNavigationMesh.GetComponent<MeshFilter>().mesh = mesh;
            partitionalbleNavigationMesh.GetComponent<MeshCollider>().sharedMesh = mesh;
            partitionalbleNavigationMesh.GetComponent<MeshRenderer>().enabled = true;
        }
        partitionalbleNavigationMesh.SetActive(false);
        #endregion

        #region Attempt to minimize vertical aliasing of the voxel grid
        // Find the unique elevations of all vertices in all trangles.
        Mesh sourceMesh = partitionalbleNavigationMesh.GetComponent<MeshFilter>().mesh;
        List<float> elevations = new List<float>();
        for (
            int triangleVertexIndex = 0;
            triangleVertexIndex < sourceMesh.triangles.Length;
            triangleVertexIndex += 1
        )
        {
            Vector3 position = sourceMesh.vertices[sourceMesh.triangles[triangleVertexIndex]];
            if (elevations.Contains(position.y) == false)
            {
                elevations.Add(position.y);
            }
        }

        // We now have the elevations of all the y coordinates..
        // Try to find a configuration of voxel origin and space step that missess all of them by some margin.
        // To do this, randomly select a number of differnt elevations and keep the one that has the
        // least divergence distance to from the nearest voxel center elevation
        float leastDivergence = float.PositiveInfinity;
        float bestElevationShift = 0;
        for (int counter = 0; counter < 10; counter++)
        {
            // Randomly move the origin
            float elevationShift = (float)random.NextDouble() % spaceStep.y;
            float voxelCenterElevation = elevationShift + spaceStep.y / 2;
            float divergence = float.PositiveInfinity;
            foreach (float elevation in elevations)
            {
                float distance = Math.Abs((elevation % spaceStep.y) - voxelCenterElevation);
                if (distance < divergence)
                {
                    divergence = distance;
                }
            }
            if (divergence < leastDivergence)
            {
                leastDivergence = divergence;
                bestElevationShift = elevationShift;
            }
        }
        generalVoxelGrid.elevationShift = bestElevationShift;
        #endregion

        /*
        We can now mark all the navigatable space in the building.
        This will include the dwelling entrances and this data will only exist
        in the sparse voxel grid.
        Instead of turning on the doors, we create floor plates (identical to the floor plates we will use later)
        than span the gap left by the hidden dwelling entrance.
         */

        #region Turn off imported model
        foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.enabled = false;
            meshRenderer.GetComponent<Collider>().enabled = false;
            // meshRenderer.gameObject.layer = passThroughLayerIndex;
        }
        #endregion

        #region Create a navigation space mesh object
        partitionalbleNavigationMesh.SetActive(true);

        GameObject navigationSpaceMesh = Instantiate(floorPrefab);
        // Create a dummy object 1 meter below the model to give some room for inference at the lowest level

        {
            GameObject dummyObject = Instantiate(doorPlatformPrefab);
            dummyObject.transform.position = new Vector3(
                bounds.center.x,
                bounds.min.y - 1,
                bounds.center.z
            );

            navMeshSurface.BuildNavMesh();

            navigationSpaceMesh.name = "Navigation Space NavMesh";

            Mesh mesh = new Mesh();
            NavMeshTriangulation triangulatedNavMesh = NavMesh.CalculateTriangulation();
            mesh.name = "Navigation Space Mesh";
            mesh.vertices = triangulatedNavMesh.vertices;
            mesh.triangles = triangulatedNavMesh.indices;

            navigationSpaceMesh.GetComponent<MeshFilter>().mesh = mesh;
            navigationSpaceMesh.GetComponent<MeshCollider>().sharedMesh = mesh;
            navigationSpaceMesh.GetComponent<MeshRenderer>().enabled = true;

            Destroy(dummyObject);
        }
        navigationSpaceMesh.SetActive(false);
        partitionalbleNavigationMesh.SetActive(false);
        #endregion

        #region Mark navigation space
        navigationSpaceMesh.SetActive(true);
        float area = SampleVoxels(navigableBit, navigableBit);
        navigationSpaceMesh.SetActive(false);
        #endregion

        // We now have navigation space marked in the voxel grid

        // Next lock all the doors and generate a new navigation mesh.
        // This mesh will be partitioned by any door which is akin to
        // all doors being locked: lockdown.

        #region Lock all doors
        // Lock all the main dwelling doors.
        foreach (Place place in dwellPoints)
        {
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Suite:
                    place.gameObject.layer = passThroughLayerIndex;
                    place.GetComponent<Collider>().enabled = false;
                    place.GetComponent<MeshRenderer>().enabled = false;
                    break;

                default:
                    break;
            }
        }
        // Lock all the secondary doors.
        foreach (GameObject secondaryDoor in secondaryDoors)
        {
            secondaryDoor.layer = defaultLayerIndex;
            secondaryDoor.GetComponent<Collider>().enabled = true;
            secondaryDoor.GetComponent<MeshRenderer>().enabled = true;
        }

        // Lock all the normal doors.
        foreach (GameObject normalDoor in normalDoors)
        {
            normalDoor.layer = defaultLayerIndex;
            normalDoor.GetComponent<Collider>().enabled = true;
            normalDoor.GetComponent<MeshRenderer>().enabled = true;
        }
        #endregion

        #region Create a lockdown navigation mesh object
        partitionalbleNavigationMesh.SetActive(true);
        navMeshSurface.BuildNavMesh();
        GameObject lockdownNavMesh = Instantiate<GameObject>(floorPrefab);
        {
            lockdownNavMesh.name = "Lockdown NavMesh";
            Mesh mesh = new Mesh();
            NavMeshTriangulation masterTriangulatedNavMesh = NavMesh.CalculateTriangulation();
            mesh.name = "Lockdown Mesh";
            mesh.vertices = masterTriangulatedNavMesh.vertices;
            mesh.triangles = masterTriangulatedNavMesh.indices;
            lockdownNavMesh.GetComponent<MeshFilter>().mesh = mesh;
            lockdownNavMesh.GetComponent<MeshCollider>().sharedMesh = mesh;
            lockdownNavMesh.GetComponent<MeshRenderer>().enabled = true;
        }
        partitionalbleNavigationMesh.SetActive(false);
        SampleVoxels(lockdownBit, lockdownBit);
        lockdownNavMesh.SetActive(false);
        #endregion

        #region Mark lockdown space
        {
            navigationSpaceMesh.SetActive(true);
            Dictionary<long, int[]> seeds = new Dictionary<long, int[]>();
            foreach (Place place in dwellPoints)
            {
                if (place.component.name.Contains("Outside"))
                {
                    RaycastHit downwardHit;
                    // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                    if (
                        Physics.Raycast(
                            place.BottomCenter + Vector3.up,
                            Vector3.down,
                            out downwardHit,
                            Mathf.Infinity
                        )
                    )
                    {
                        int xIndex,
                            yIndex,
                            zIndex;
                        Quantize(downwardHit.point, out xIndex, out yIndex, out zIndex);
                        for (
                            int y = Math.Max(0, yIndex - 1);
                            y <= Math.Min(yIndex + 1, generalVoxelGrid.Height - 1);
                            y++
                        )
                        {
                            long spaceTypeMask = generalVoxelGrid.GetAt(
                                xIndex,
                                yIndex,
                                zIndex,
                                navigableBit | lockdownBit
                            );
                            if (spaceTypeMask == (short)(navigableBit | lockdownBit))
                            {
                                seeds[generalVoxelGrid.EncodeIndex(xIndex, yIndex, zIndex)] =
                                    new int[] { xIndex, yIndex, zIndex };
                            }
                        }
                    }
                }
            }

            int iterationCount = 0;
            Dictionary<long, byte> previouslyVisitedVoxels = new Dictionary<long, byte>();
            while (seeds.Count > 0)
            {
                FloodFill(
                    generalVoxelGrid,
                    seeds,
                    previouslyVisitedVoxels,
                    navigableBit | lockdownBit,
                    navigableBit | lockdownBit,
                    outsideBit,
                    outsideBit
                );
                // It is unproven that this algorithm will always reach a halting condition.
                // Thus, an upper limit is applied to prevent such infinite computation.
                if (iterationCount > 10000)
                {
                    break;
                }
                iterationCount++;
                lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
            }
            navigationSpaceMesh.SetActive(false);
        }

        //lockdownNavMesh.SetActive(true);
        // SampleVoxels(lockdownBit, lockdownBit);
        //lockdownNavMesh.SetActive(false);
        #endregion

        #region Turn off normal doors
        foreach (GameObject normalDoor in normalDoors)
        {
            normalDoor.layer = passThroughLayerIndex;
            normalDoor.GetComponent<Collider>().enabled = false;
            normalDoor.GetComponent<MeshRenderer>().enabled = false;
        }
        #endregion

        #region Create a privacy preserving navigation mesh object
        partitionalbleNavigationMesh.SetActive(true);
        navMeshSurface.BuildNavMesh();
        GameObject privacyPreservingNavMesh = Instantiate<GameObject>(floorPrefab);
        {
            privacyPreservingNavMesh.name = "Privacy Preserving NavMesh";
            Mesh mesh = new Mesh();
            NavMeshTriangulation masterTriangulatedNavMesh = NavMesh.CalculateTriangulation();
            mesh.name = "Privacy Preserving Mesh";
            mesh.vertices = masterTriangulatedNavMesh.vertices;
            mesh.triangles = masterTriangulatedNavMesh.indices;
            privacyPreservingNavMesh.GetComponent<MeshFilter>().mesh = mesh;
            privacyPreservingNavMesh.GetComponent<MeshCollider>().sharedMesh = mesh;
            privacyPreservingNavMesh.GetComponent<MeshRenderer>().enabled = true;
        }
        partitionalbleNavigationMesh.SetActive(false);
        SampleVoxels(privacyPreservingBit, privacyPreservingBit);
        privacyPreservingNavMesh.SetActive(false);
        #endregion

        #region Mark public space
        navigationSpaceMesh.SetActive(true);
        // SampleVoxels(privacyPreservingBit, privacyPreservingBit);
        // Flood fill from known public places such as Outside and Elevator Doors
        {
            Dictionary<long, int[]> seeds = new Dictionary<long, int[]>();
            foreach (Place place in dwellPoints)
            {
                if (place.component.name.Contains("Outside"))
                {
                    RaycastHit downwardHit;
                    // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                    if (
                        Physics.Raycast(
                            place.BottomCenter + Vector3.up,
                            Vector3.down,
                            out downwardHit,
                            Mathf.Infinity
                        )
                    )
                    {
                        int xIndex,
                            yIndex,
                            zIndex;
                        Quantize(downwardHit.point, out xIndex, out yIndex, out zIndex);
                        long spaceTypeMask = generalVoxelGrid.GetAt(
                            xIndex,
                            yIndex,
                            zIndex,
                            navigableBit | privacyPreservingBit
                        );
                        if (spaceTypeMask == (short)(navigableBit | privacyPreservingBit))
                        {
                            seeds[generalVoxelGrid.EncodeIndex(xIndex, yIndex, zIndex)] = new int[]
                            {
                                xIndex,
                                yIndex,
                                zIndex
                            };
                        }
                    }
                }
            }
            int iterationCount = 0;
            Dictionary<long, byte> previouslyVisitedVoxels = new Dictionary<long, byte>();
            while (seeds.Count > 0)
            {
                FloodFill(
                    generalVoxelGrid,
                    seeds,
                    previouslyVisitedVoxels,
                    navigableBit | privacyPreservingBit,
                    navigableBit | privacyPreservingBit,
                    publicSpaceBit,
                    publicSpaceBit
                );
                if (iterationCount > 10000)
                {
                    break;
                }
                iterationCount++;
                lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
            }
        }
        navigationSpaceMesh.SetActive(false);
        #endregion

        #region Hide dwelling entrances
        // Lock all the main dwelling doors.
        foreach (Place place in dwellPoints)
        {
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Suite:
                    place.gameObject.layer = passThroughLayerIndex;
                    place.GetComponent<Collider>().enabled = false;
                    place.GetComponent<MeshRenderer>().enabled = false;
                    break;

                default:
                    break;
            }
        }
        // Lock all the secondary doors.
        foreach (GameObject secondaryDoor in secondaryDoors)
        {
            secondaryDoor.layer = passThroughLayerIndex;
            secondaryDoor.GetComponent<Collider>().enabled = false;
            secondaryDoor.GetComponent<MeshRenderer>().enabled = false;
        }
        #endregion

        #region Work out where the floors are
        SortedList<float, float> markedElevations = new SortedList<float, float>();
        if (elevatorBankDictionary.Count > 0)
        {
            #region Use elevator entrances as floor elevations
            foreach (string bank in elevatorBankDictionary.Keys)
            {
                foreach (Place elevatorDoor in elevatorBankDictionary[bank])
                {
                    float sample = elevatorDoor.BottomCenter.y;
                    int yIndex = (int)Math.Round(
                        (sample - bounds.min.y + generalVoxelGrid.elevationShift) / spaceStep.y
                    );
                    int closestIndex = -1;
                    if (generalVoxelGrid.yCounts.ContainsKey(yIndex))
                    {
                        // Exact match is available
                        closestIndex = yIndex;
                    }
                    else
                    {
                        // Find the nearest marked elevation
                        int minimumDistance = int.MaxValue;
                        foreach (int markedYIndex in generalVoxelGrid.yCounts.Keys)
                        {
                            int distance = Math.Abs(markedYIndex - yIndex);
                            if (distance < minimumDistance)
                            {
                                closestIndex = markedYIndex;
                                minimumDistance = distance;
                            }
                        }
                    }
                    float areaOfFloor =
                        generalVoxelGrid.yCounts[closestIndex] * spaceStep.x * spaceStep.z;
                    float adjustedSample =
                        closestIndex * spaceStep.y + bounds.min.y + generalVoxelGrid.elevationShift;
                    if (markedElevations.ContainsKey(adjustedSample) == false)
                    {
                        markedElevations.Add(adjustedSample, areaOfFloor);
                    }
                }
            }
            #endregion
        }
        else
        {
            #region Use dwelling entrances as floor elevations
            foreach (Place place in dwellPoints)
            {
                if (place is Place)
                {
                    float sample = place.BottomCenter.y;
                    int yIndex = (int)Math.Round(
                        (sample - bounds.min.y + generalVoxelGrid.elevationShift) / spaceStep.y
                    );
                    int closestIndex = -1;
                    if (generalVoxelGrid.yCounts.ContainsKey(yIndex))
                    {
                        // Exact match is available
                        closestIndex = yIndex;
                    }
                    else
                    {
                        // Find the nearest marked elevation
                        int minimumDistance = int.MaxValue;
                        foreach (int markedYIndex in generalVoxelGrid.yCounts.Keys)
                        {
                            int distance = Math.Abs(markedYIndex - yIndex);
                            if (distance < minimumDistance)
                            {
                                closestIndex = markedYIndex;
                                minimumDistance = distance;
                            }
                        }
                    }
                    float areaOfFloor =
                        generalVoxelGrid.yCounts[closestIndex] * spaceStep.x * spaceStep.z;
                    float adjustedSample =
                        closestIndex * spaceStep.y + bounds.min.y + generalVoxelGrid.elevationShift;
                    if (markedElevations.ContainsKey(adjustedSample) == false)
                    {
                        markedElevations.Add(adjustedSample, areaOfFloor);
                    }
                }
            }
            #endregion
        }

        #region Infer floor elevations from voxel data
        const float minimumArea = 100; // square meters
        SortedList<int, float> inferredFloorElevations = new SortedList<int, float>();
        {
            foreach (int yIndex in generalVoxelGrid.yCounts.Keys)
            {
                if (yIndex < generalVoxelGrid.Height - 1)
                {
                    float areaOfFloor =
                        generalVoxelGrid.yCounts[yIndex] * spaceStep.x * spaceStep.z;
                    if (areaOfFloor > minimumArea)
                    {
                        float areaOfFloorBelow = 0;
                        float areaOfFloorAbove = 0;
                        int yBelowIndex = Math.Max(0, yIndex - 1);
                        int yAboveIndex = Math.Min(yIndex + 1, generalVoxelGrid.Height - 1);
                        if (generalVoxelGrid.yCounts.ContainsKey(yBelowIndex))
                        {
                            areaOfFloorBelow =
                                generalVoxelGrid.yCounts[yBelowIndex] * spaceStep.x * spaceStep.z;
                        }
                        if (generalVoxelGrid.yCounts.ContainsKey(yAboveIndex))
                        {
                            areaOfFloorAbove =
                                generalVoxelGrid.yCounts[yAboveIndex] * spaceStep.x * spaceStep.z;
                        }
                        if (areaOfFloorAbove < areaOfFloor && areaOfFloorBelow < areaOfFloor)
                        {
                            // Let's call it a floor.
                            // float elevation = bounds.min.y + generalVoxelGrid.elevationShift + yIndex * spaceStep.y;
                            inferredFloorElevations.Add(yIndex, areaOfFloor);
                        }
                    }
                }
            }
        }
        List<int> floorIndices = new List<int>();
        foreach (int yIndex in inferredFloorElevations.Keys)
        {
            floorIndices.Add(yIndex);
        }
        #endregion

        #endregion

        #region Build a "condensing" map which maps nearby voxel values to known floors
        // And then build a map that collapses voxels from above and below into these floor plates.

        // The metaphor is "fence posts and wires" in which posts are floor plates and the wire
        // represnts the distance between them.  Each floor plate will "pull" voxels from half way
        // to the next floor plate into the texture.
        // This elegantly deals with case where two floor plates are consective to one another.
        // The mechanism is to introduce a level of indirection in y so different values of y can
        // be mapped to the nearest floorplate.
        // The fence posts are represented by floorIndices
        // Thus we need the bounds between each pair
        Dictionary<int, int> yCondensingMap = new Dictionary<int, int>();
        {
            for (int index = 0; index < generalVoxelGrid.Height; index++)
            {
                int minimumDistance = int.MaxValue;
                int closestFloorIndex = -1;
                foreach (int floorYIndex in floorIndices)
                {
                    int distance = Math.Abs(index - floorYIndex);
                    if (distance < minimumDistance)
                    {
                        minimumDistance = distance;
                        closestFloorIndex = floorYIndex;
                    }
                }
                yCondensingMap[index] = closestFloorIndex;
            }
        }
        #endregion

        #region Mark private space
        {
            //privacyNavMesh.SetActive(true);
            //doorPlatform.SetActive(true);
            Dictionary<long, int[]> seeds = new Dictionary<long, int[]>();
            float distanceToSearch = 0.6f; // meters
            foreach (Place place in dwellPoints)
            {
                switch (place.component.socialSyntax)
                {
                    case SocialSyntaxEnum.Suite:
                        int xIndex,
                            yIndex,
                            zIndex;
                        Quantize(place.BottomCenter, out xIndex, out yIndex, out zIndex);
                        for (
                            int zz = Math.Max(0, zIndex - (int)(distanceToSearch / spaceStep.z));
                            zz
                                <= Math.Min(
                                    zIndex + (int)(distanceToSearch / spaceStep.z),
                                    generalVoxelGrid.Depth - 1
                                );
                            zz++
                        )
                        {
                            for (
                                int yy = Math.Max(
                                    0,
                                    yIndex - (int)(distanceToSearch / spaceStep.y)
                                );
                                yy
                                    <= Math.Min(
                                        yIndex + (int)(distanceToSearch / spaceStep.y),
                                        generalVoxelGrid.Height - 1
                                    );
                                yy++
                            )
                            {
                                for (
                                    int xx = Math.Max(
                                        0,
                                        xIndex - (int)(distanceToSearch / spaceStep.x)
                                    );
                                    xx
                                        <= Math.Min(
                                            xIndex + (int)(distanceToSearch / spaceStep.x),
                                            generalVoxelGrid.Width - 1
                                        );
                                    xx++
                                )
                                {
                                    if (
                                        generalVoxelGrid.GetAt(
                                            xx,
                                            yy,
                                            zz,
                                            navigableBit | publicSpaceBit
                                        ) == navigableBit
                                    )
                                    {
                                        seeds[generalVoxelGrid.EncodeIndex(xx, yy, zz)] = new int[]
                                        {
                                            xx,
                                            yy,
                                            zz
                                        };
                                        //GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                        //marker.transform.localScale = spaceStep;
                                        //marker.transform.position = new Vector3(
                                        //    xx * spaceStep.x + bounds.min.x,
                                        //    yy * spaceStep.x + bounds.min.y + generalVoxelGrid.elevationShift,
                                        //    zz * spaceStep.x + bounds.min.z);
                                    }
                                }
                            }
                        }
                        break;

                    default:
                        break;
                }
            }
            // doorPlatform.transform.position = dwelling.BottomCenter + doorAdjust;

            int iterationCount = 0;
            Dictionary<long, byte> previouslyVisitedVoxels = new Dictionary<long, byte>();
            while (seeds.Count > 0)
            {
                FloodFill(
                    generalVoxelGrid,
                    seeds,
                    previouslyVisitedVoxels,
                    navigableBit,
                    navigableBit | publicSpaceBit,
                    privateSpaceBit,
                    privateSpaceBit
                );
                if (iterationCount > 10000)
                {
                    break;
                }
                iterationCount++;
                lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
            }
            //doorPlatform.SetActive(false);
            //privacyNavMesh.SetActive(false);
        }
        #endregion

        #region Hide all doors
        //// Unlock all the main dwelling doors.
        //foreach (Place place in pointsOfInterest)
        //{
        //    if (place is Place)
        //    {
        //        Place dwelling = (Place)place;
        //        dwelling.gameObject.layer = passThroughLayerIndex;
        //        dwelling.GetComponent<Collider>().enabled = false;
        //        dwelling.GetComponent<MeshRenderer>().enabled = false;
        //    }
        //}
        //// Unlock all the secondary doors.
        //foreach (GameObject secondaryDoor in secondaryDoors)
        //{
        //    secondaryDoor.layer = passThroughLayerIndex;
        //    secondaryDoor.GetComponent<Collider>().enabled = false;
        //    secondaryDoor.GetComponent<MeshRenderer>().enabled = false;
        //}
        //// Unlock all the normal doors.
        //foreach (GameObject normalDoor in normalDoors)
        //{
        //    normalDoor.layer = passThroughLayerIndex;
        //    normalDoor.GetComponent<Collider>().enabled = false;
        //    normalDoor.GetComponent<MeshRenderer>().enabled = false;
        //}
        #endregion

        #region Mark semi-private spaces
        foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        {
            if (
                IsTaggedMesh(meshRenderer.gameObject)
                && meshRenderer.name.Contains("Semi-Private Space")
            )
            {
                meshRenderer.GetComponent<Collider>().enabled = true;
            }
        }
        SampleVoxels(semiPrivateSpaceBit, semiPrivateSpaceBit);
        #endregion

        #region Turn on imported model
        //foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        //{
        //    // This may inadvertaintly turn on affordances and doors.
        //    meshRenderer.GetComponent<Collider>().enabled = true;
        //}
        #endregion

        #region Mark outside space
        {
            // Flood fill from known public places such as Outside and Elevator Doors
            Dictionary<long, int[]> seeds = new Dictionary<long, int[]>();

            foreach (Place place in dwellPoints)
            {
                if (place.component.name.Contains("Outside"))
                {
                    foreach (
                        MeshRenderer meshRenderer in place.GetComponentsInChildren<MeshRenderer>()
                    )
                    {
                        meshRenderer.GetComponent<Collider>().enabled = false;
                    }
                    RaycastHit downwardHit;
                    // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                    if (
                        Physics.Raycast(
                            place.BottomCenter + Vector3.up,
                            Vector3.down,
                            out downwardHit,
                            Mathf.Infinity
                        )
                    )
                    {
                        int xIndex,
                            yIndex,
                            zIndex;
                        Quantize(place.BottomCenter, out xIndex, out yIndex, out zIndex);
                        seeds[generalVoxelGrid.EncodeIndex(xIndex, yIndex, zIndex)] = new int[]
                        {
                            xIndex,
                            yIndex,
                            zIndex
                        };
                    }
                }
            }

            int iterationCount = 0;
            Dictionary<long, byte> previouslyVisitedVoxels = new Dictionary<long, byte>();
            while (seeds.Count > 0)
            {
                FloodFill(
                    generalVoxelGrid,
                    seeds,
                    previouslyVisitedVoxels,
                    lockdownBit,
                    lockdownBit,
                    outsideBit,
                    outsideBit
                );
                if (iterationCount > 1000)
                {
                    break;
                }
                iterationCount++;
                lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
            }
        }
        #endregion

        #region Make everything invisible to agents but visble to ray casting
        foreach (MeshRenderer meshRenderer in importedModel.GetComponentsInChildren<MeshRenderer>())
        {
            meshRenderer.gameObject.layer = passThroughLayerIndex;
            meshRenderer.GetComponent<Collider>().enabled = true;
        }
        #endregion

        #region Set up the floor fragment containers once
        outsideContainer = Instantiate<GameObject>(outsideContainerPrefab);
        outsideContainer.transform.parent = navMeshSurface.transform;
        undercoverContainer = Instantiate<GameObject>(undercoverContainerPrefab);
        undercoverContainer.transform.parent = navMeshSurface.transform;
        insideContainer = Instantiate<GameObject>(insideContainerPrefab);
        insideContainer.transform.parent = navMeshSurface.transform;
        #endregion

        #region Partition the master mesh
        partitionalbleNavigationMesh.SetActive(true);
        Rebuild(partitionalbleNavigationMesh);
        #endregion

        // -- Phase 3: Agent simulation

        #region Determine the start date of the simulation
        List<Vector3> errorRays = new List<Vector3>();
        DateTime startDate;
        string startDateString = simulationTicket.startDate;
        // March 21st May 2nd Oct 3rd Nov 4th
        foreach (string ending in new string[] { "st", "nd", "rd", "th" })
        {
            if (startDateString.EndsWith(ending))
            {
                startDateString = startDateString.Substring(
                    0,
                    startDateString.Length - ending.Length
                );
            }
        }
        if (DateTime.TryParse($"{startDateString} {newYearMidnight.Year}", out startDate))
        {
            startSecond = (int)(startDate - newYearMidnight).TotalSeconds;
        }
        #endregion

        #region Find all logically possible use cases for each occupied place
        IDictionary<Place, IList<UseCase>> relevantUsecasesByPlace =
            new Dictionary<Place, IList<UseCase>>();
        {
            foreach (Place place in dwellPoints)
            {
                switch (place.component.socialSyntax)
                {
                    case SocialSyntaxEnum.Suite:

                        #region Match affordances relevant all possible agent types that can dwell here
                        {
                            foreach (UnitOccupancy unitOccupancy in unitOccupancies)
                            {
                                if (
                                    place.component.baseOccupancy - place.component.variance
                                        <= unitOccupancy.population
                                    && unitOccupancy.population
                                        <= place.component.baseOccupancy - place.component.variance
                                )
                                {
                                    foreach (AgentType agentType in unitOccupancy.agentMix)
                                    {
                                        foreach (UseCase useCase in agentType.useCases)
                                        {
                                            if (
                                                useCase.startingComponentPropensityDictionary.ContainsKey(
                                                    FLUID.Component.defaultComponent
                                                )
                                            )
                                            {
                                                if (
                                                    useCase.destinationComponentPropensityDictionary.ContainsKey(
                                                        FLUID.Component.defaultComponent
                                                    )
                                                )
                                                {
                                                    // We are waking up at our suite
                                                    if (
                                                        relevantUsecasesByPlace.ContainsKey(place)
                                                        == false
                                                    )
                                                    {
                                                        relevantUsecasesByPlace[place] =
                                                            new List<UseCase>();
                                                    }
                                                    if (
                                                        relevantUsecasesByPlace[place].Contains(
                                                            useCase
                                                        ) == false
                                                    )
                                                    {
                                                        relevantUsecasesByPlace[place].Add(useCase);
                                                    }
                                                }
                                                else
                                                {
                                                    foreach (Place otherPlace in dwellPoints)
                                                    {
                                                        if (
                                                            useCase.destinationComponents.Contains(
                                                                otherPlace.component
                                                            )
                                                        )
                                                        {
                                                            if (
                                                                relevantUsecasesByPlace.ContainsKey(
                                                                    place
                                                                ) == false
                                                            )
                                                            {
                                                                relevantUsecasesByPlace[place] =
                                                                    new List<UseCase>();
                                                            }
                                                            if (
                                                                relevantUsecasesByPlace[
                                                                    place
                                                                ].Contains(useCase) == false
                                                            )
                                                            {
                                                                relevantUsecasesByPlace[place].Add(
                                                                    useCase
                                                                );
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else if (
                                                useCase.destinationComponentPropensityDictionary.ContainsKey(
                                                    FLUID.Component.defaultComponent
                                                )
                                            )
                                            {
                                                foreach (Place otherPlace in dwellPoints)
                                                {
                                                    if (
                                                        useCase.startingPointComponents.Contains(
                                                            otherPlace.component
                                                        )
                                                    )
                                                    {
                                                        if (
                                                            relevantUsecasesByPlace.ContainsKey(
                                                                place
                                                            ) == false
                                                        )
                                                        {
                                                            relevantUsecasesByPlace[place] =
                                                                new List<UseCase>();
                                                        }
                                                        if (
                                                            relevantUsecasesByPlace[place].Contains(
                                                                useCase
                                                            ) == false
                                                        )
                                                        {
                                                            relevantUsecasesByPlace[place].Add(
                                                                useCase
                                                            );
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                throw new NotImplementedException();
                                            }
                                        }
                                    }
                                }
                            }

                            // If no-one is using the affordance, skip it
                        }
                        #endregion
                        break;
                }
            }
        }
        #endregion

        #region Bind semi-private spaces to nearby dwellings
        PathManager pathManager = new PathManager(this);
        List<Vector3[]> circulationSpacePaths = new List<Vector3[]>();
        foreach (Place place in dwellPoints)
        {
            #region Determine the least number of private spaces held by any dwelling
            int leastNumberOfSemiPrivateSpaces = int.MaxValue;
            foreach (Place otherPlace in dwellPoints)
            {
                switch (otherPlace.component.socialSyntax)
                {
                    case SocialSyntaxEnum.Suite:
                        if (otherPlace.semiPrivateSpaces.Count < leastNumberOfSemiPrivateSpaces)
                        {
                            leastNumberOfSemiPrivateSpaces = otherPlace.semiPrivateSpaces.Count;
                        }
                        break;
                }
            }
            #endregion

            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.SemiPrivate:
                    // We have found a semi-private space.  Try to find a nearby dwelling to bind to.
                    List<Place> candidateSuites = new List<Place>();
                    foreach (Place otherPlace in dwellPoints)
                    {
                        switch (otherPlace.component.socialSyntax)
                        {
                            case SocialSyntaxEnum.Suite:
                                if (
                                    otherPlace.semiPrivateSpaces.Count
                                    == leastNumberOfSemiPrivateSpaces
                                )
                                {
                                    candidateSuites.Add(otherPlace);
                                }
                                break;
                        }
                    }

                    bool bound = false;
                    float upper = 1;
                    float lower = 0;
                    while (bound == false)
                    {
                        Vector3[] shortestPath = null;
                        float shortestPathLength = float.PositiveInfinity;
                        Place closestSuite = null;
                        foreach (Place candidateSuite in candidateSuites)
                        {
                            float deltaY =
                                Mathf.Abs(place.BottomCenter.y - candidateSuite.BottomCenter.y)
                                + 0.001f * (float)random.NextDouble();
                            if (lower <= deltaY && deltaY <= upper)
                            {
                                Vector3[] path = pathManager.GetPath(
                                    candidateSuite,
                                    place,
                                    false,
                                    false
                                );
                                if (path != null)
                                {
                                    if (shortestPath == null)
                                    {
                                        shortestPath = path;
                                        shortestPathLength = PathManager.GetPathLength(path);
                                        closestSuite = candidateSuite;
                                    }
                                    else
                                    {
                                        float length = PathManager.GetPathLength(path);
                                        if (length < shortestPathLength)
                                        {
                                            shortestPath = path;
                                            shortestPathLength = length;
                                            closestSuite = candidateSuite;
                                        }
                                    }
                                }
                            }
                        }
                        if (shortestPath != null)
                        {
                            closestSuite.semiPrivateSpaces.Add(place);
                            bound = true;
                        }
                        else
                        {
                            // Keep looking vertically
                            lower = upper;
                            upper += 1;
                        }
                    }
                    break;
            }
        }
        #endregion

        #region Find all paths to places that participate in a given use case
        try
        {
            foreach (Place place in relevantUsecasesByPlace.Keys)
            {
                List<UseCase> useCasesWithoutAffordances = new List<UseCase>();
                foreach (UseCase useCase in relevantUsecasesByPlace[place])
                {
                    bool useCaseHasAccessibleDestination = false;
                    // Does the use case start at "Default"?
                    if (
                        useCase.startingComponentPropensityDictionary.ContainsKey(
                            FLUID.Component.defaultComponent
                        )
                    )
                    {
                        // Does the use case end at "Default"?
                        if (
                            useCase.destinationComponentPropensityDictionary.ContainsKey(
                                FLUID.Component.defaultComponent
                            )
                        )
                        {
                            // No journeys here
                            useCaseHasAccessibleDestination = true;
                        }
                        else
                        {
                            foreach (Place otherPlace in dwellPoints)
                            {
                                if (otherPlace != place) // We can't have journeys to ourself
                                {
                                    switch (otherPlace.component.socialSyntax)
                                    {
                                        case SocialSyntaxEnum.SemiPrivate:
                                            if (place.semiPrivateSpaces.Contains(otherPlace))
                                            {
                                                var result = pathManager.GetPath(
                                                    place,
                                                    otherPlace,
                                                    false,
                                                    false
                                                );
                                                if (result != null)
                                                {
                                                    useCaseHasAccessibleDestination = true;
                                                    circulationSpacePaths.Add(result);
                                                }
                                            }
                                            break;

                                        default:
                                            if (
                                                useCase.destinationComponents.Contains(
                                                    otherPlace.component
                                                )
                                            )
                                            {
                                                var result = pathManager.GetPath(
                                                    place,
                                                    otherPlace,
                                                    false,
                                                    false
                                                );
                                                if (result != null)
                                                {
                                                    useCaseHasAccessibleDestination = true;
                                                    circulationSpacePaths.Add(result);
                                                }
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    else if (
                        useCase.destinationComponentPropensityDictionary.ContainsKey(
                            FLUID.Component.defaultComponent
                        )
                    )
                    {
                        foreach (Place otherPlace in dwellPoints)
                        {
                            if (otherPlace != place) // We can't have journeys to ourself
                            {
                                if (useCase.startingPointComponents.Contains(otherPlace.component))
                                {
                                    var result = pathManager.GetPath(
                                        place,
                                        otherPlace,
                                        false,
                                        false
                                    );
                                    if (result != null)
                                    {
                                        useCaseHasAccessibleDestination = true;
                                        circulationSpacePaths.Add(result);
                                    }
                                }
                            }
                        }
                    }
                    if (useCaseHasAccessibleDestination == false)
                    {
                        useCasesWithoutAffordances.Add(useCase);
                    }
                }
                foreach (UseCase useCase in useCasesWithoutAffordances)
                {
                    relevantUsecasesByPlace[place].Remove(useCase);
                }
            }
        }
        catch (Exception e) { }
        #endregion

        #region Find paths to elevators
        foreach (string bank in elevatorBankDictionary.Keys)
        {
            foreach (Place place in dwellPoints)
            {
                switch (place.component.socialSyntax)
                {
                    case SocialSyntaxEnum.Suite:
                    case SocialSyntaxEnum.Mandatory:
                    case SocialSyntaxEnum.Optional:
                        Vector3[] result = pathManager.GetPathToClosestElevator(
                            place,
                            bank,
                            false,
                            false
                        );
                        if (result != null)
                        {
                            circulationSpacePaths.Add(result);
                        }
                        break;
                }
            }
        }
        #endregion

        simulationTicket.eventLog.Add(
            new SimulationEvent(
                SimulationEventType.Information,
                "Found the paths to each affordance from each dwelling, if any"
            )
        );
        atlasHandler.UpdateTicket(simulationTicket);

        #region Cull paths to distant affordances where others are "closer"
        pathManager.CullPathsToDistantAffordances(relevantUsecasesByPlace);
        #endregion

        #region Collect all the use cases and sort them by durations.
        // Since some durations might be identical, I add a tiny random offset so we can sort all durations uniquely.
        foreach (Agent agent in agents)
        {
            foreach (UseCase useCase in agent.agentType.useCases)
            {
                bool useCaseIsAvailable = false;
                if (
                    useCase.startingComponentPropensityDictionary.ContainsKey(
                        FLUID.Component.defaultComponent
                    )
                )
                {
                    foreach (FLUID.Component component in useCase.destinationComponents)
                    {
                        if (pathManager.PathExistsTo(agent.suite, component))
                        {
                            useCaseIsAvailable = true;
                            break;
                        }
                    }
                }
                else if (
                    useCase.destinationComponentPropensityDictionary.ContainsKey(
                        FLUID.Component.defaultComponent
                    )
                )
                {
                    foreach (FLUID.Component component in useCase.startingPointComponents)
                    {
                        if (pathManager.PathExistsFrom(agent.suite, component))
                        {
                            useCaseIsAvailable = true;
                            break;
                        }
                    }
                }
                if (useCaseIsAvailable)
                {
                    TimeSpan duration =
                        useCase.dwellTime + TimeSpan.FromSeconds(random.NextDouble());
                    agent.sortedUseCases[duration] = useCase;
                }
            }
        }
        #endregion

        Debug.Log("Completed use case analysis.");

        #region Work out if each place is inside, undercover or outside
        foreach (Place place in dwellPoints)
        {
            place.GetComponent<Collider>().enabled = false;
            place.exposure = ExposureEnum.Inside;
            switch (place.component.socialSyntax)
            {
                case SocialSyntaxEnum.Mandatory:
                case SocialSyntaxEnum.Suite:
                    place.exposure = ExposureEnum.Inside; // Redundant, but deliberate
                    break;

                case SocialSyntaxEnum.Optional:
                    RaycastHit raycastHit;
                    if (
                        Physics.Raycast(
                            place.BottomCenter + Vector3.up,
                            Vector3.up,
                            out raycastHit,
                            2
                        )
                    )
                    {
                        // Its under cover
                        // Now fire a ray down at the floor and see if this is undercover.
                        RaycastHit downwardHit;
                        if (
                            Physics.Raycast(
                                place.BottomCenter + Vector3.up,
                                Vector3.down,
                                out downwardHit,
                                Mathf.Infinity
                            )
                        )
                        {
                            int xIndex = (int)Math.Round(
                                (downwardHit.point.x - bounds.min.x) / spaceStep.x
                            );
                            int yIndex = (int)Math.Round(
                                (
                                    downwardHit.point.y
                                    - bounds.min.y
                                    + generalVoxelGrid.elevationShift
                                ) / spaceStep.y
                            );
                            int zIndex = (int)Math.Round(
                                (downwardHit.point.z - bounds.min.z) / spaceStep.z
                            );
                            if (
                                generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, outsideBit)
                                == outsideBit
                            )
                            {
                                place.exposure = ExposureEnum.Undercover;
                            }
                        }
                    }
                    else
                    {
                        // Its outside
                        place.exposure = ExposureEnum.Outside;
                    }
                    break;
            }
        }
        #endregion

        #region Generate agent activities
        {
            doorPlatform.SetActive(true);
            for (int day = 0; day < simulationTicket.DaysToSimulate; day++)
            {
                foreach (Agent agent in agents)
                {
                    doorPlatform.transform.position = agent.suite.BottomCenter;
                    doorPlatform.transform.rotation = Quaternion.Euler(0, 0, 0);
                    navMeshSurface.BuildNavMesh();

                    // Create a brand new day (calendar).
                    agent.calendar.Add(new DayCalendar());

                    lastHeartBeat = agent.AssignActivities(
                        this,
                        pathManager,
                        lastHeartBeat,
                        agent.calendar,
                        day
                    );
                }
            }
            doorPlatform.SetActive(false);
        }

        if (simulationTicket.DaysToSimulate > 0)
        {
            simulationTicket.simulationProgress = 0.37f;
            simulationTicket.eventLog.Add(
                new SimulationEvent(
                    SimulationEventType.Information,
                    "Built calendars for all agents for all days."
                )
            );
            atlasHandler.UpdateTicket(simulationTicket);
        }
        else
        {
            simulationTicket.simulationProgress = 0.37f;
            simulationTicket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Information, "Created all agents.")
            );
            atlasHandler.UpdateTicket(simulationTicket);
        }
        #endregion

        #region Create an index for all path segments
        int maxTraversals = 0;
        foreach (Agent agent in agents)
        {
            for (int day = 0; day < simulationTicket.DaysToSimulate; day++)
            {
                foreach (Activity activity in agent.calendar[day].activities)
                {
                    foreach (IAction action in activity.chosenPlan.actions)
                    {
                        if (action is Journey)
                        {
                            Journey journey = (Journey)action;
                            for (int index = 1; index < journey.Path.Length; index++)
                            {
                                int traversals = pathSegmentCollection.AddTraversal(
                                    journey.Path[index - 1],
                                    journey.Path[index]
                                );
                                if (traversals > maxTraversals)
                                    maxTraversals = traversals;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Find the floor plates
        // It is a reasonable assumption that the floor elevation of an elevator landing is a good proxy for a
        // floor plate.
        //foreach (string elevatorBank in elevatorBankDictionary.Keys)
        //{
        //    foreach (ElevatorDoor elevatorDoor in elevatorBankDictionary[elevatorBank])
        //    {
        //        RaycastHit raycastHit;
        //        if (Physics.Raycast(elevatorDoor.BottomCenter + Vector3.up, Vector3.down, out raycastHit, 2))
        //        {
        //            int sampleYIndex = (int)Mathf.Round((raycastHit.point.y - bounds.min.y + generalVoxelGrid.elevationShift) / spaceStep.y);
        //            markedElevations[sampleYIndex] = "Elevator Landing";
        //        }
        //    }
        //}
        //// It is also a reasonable assumption that the floor elevation of a dwelling entrance is a good prozxy for a
        //// floor plate.
        //foreach (Dwelling dwelling in dwellings)
        //{
        //    RaycastHit raycastHit;
        //    if (Physics.Raycast(dwelling.BottomCenter + Vector3.up, Vector3.down, out raycastHit, 2))
        //    {
        //        int sampleYIndex = (int)Mathf.Round((raycastHit.point.y - bounds.min.y + generalVoxelGrid.elevationShift) / spaceStep.y);
        //        markedElevations[sampleYIndex] = "Dwelling";
        //    }
        //}
        #endregion

        #region Mark space syntax voxels
        Destroy(visitingDoorPlatform);
        Destroy(doorPlatform);

        Dictionary<Vector3, Vector3> pairs = new Dictionary<Vector3, Vector3>();
        foreach (Vector3[] path in circulationSpacePaths)
        {
            // Smash the paths into segments and try to match up segments as best we can.
            for (int nextIndex = 1; nextIndex < path.Length; nextIndex++)
            {
                int previousIndex = nextIndex - 1;
                List<Vector3> pair = new List<Vector3> { path[previousIndex], path[nextIndex] };
                bool precendentExists = false;
                if (pairs.ContainsKey(pair[0]) && pairs[pair[0]] == pair[1])
                {
                    // Forward segment found
                    precendentExists = true;
                }
                else if (pairs.ContainsKey(pair[1]) && pairs[pair[1]] == pair[0])
                {
                    // Reverse segment found
                    precendentExists = true;
                }

                if (precendentExists == false)
                {
                    pairs[pair[0]] = pair[1];
                }
                else { }
            }
        }

        foreach (Vector3 from in pairs.Keys)
        {
            Vector3[] path = new Vector3[] { from, pairs[from] };
            MarkPath(path, bounds.min, 0, journeySpaceRange, journeySpaceBit);
            lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
            MarkPath(path, bounds.min, journeySpaceRange, pauseSpaceRange, pauseSpaceBit);
            lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
        }
        /*
        foreach (bool isHotCold in new bool[] { false })
        {
            foreach (bool isRaining in new bool[] { false })
            {
                foreach (Place place in dwellPoints)
                {
                    switch (place.component.socialSyntax)
                    {
                        case SocialSyntaxEnum.Suite:
                            {
                                #region Get the current affordance path dictionary based on weather conditions
                                Dictionary<Place, Vector3[]> currentAffordancePathDictionary;
                                if (isHotCold)
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenWetIntemperate;
                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenDryIntemperate;
                                    }
                                }
                                else
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenWetTemperate;
                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenDryTemperate;
                                    }
                                }
                                #endregion

                                foreach (Vector3[] navMeshPath in currentAffordancePathDictionary.Values)
                                {
                                    MarkPath(navMeshPath, bounds.min, 0, journeySpaceRange, journeySpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                    MarkPath(navMeshPath, bounds.min, journeySpaceRange, pauseSpaceRange, pauseSpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                }
                            }

                            {
                                #region Get the current elevator path dictionary based on weather conditions
                                Dictionary<string, Vector3[]> currentAffordancePathDictionary;
                                if (isHotCold)
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToNearestElevatorBankDictionaryWhenWetIntemperate;

                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToNearestElevatorBankDictionaryWhenDryIntemperate;
                                    }
                                }
                                else
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToNearestElevatorBankDictionaryWhenWetTemperate;
                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToNearestElevatorBankDictionaryWhenDryTemperate;
                                    }
                                }
                                #endregion

                                foreach (Vector3[] navMeshPath in currentAffordancePathDictionary.Values)
                                {
                                    MarkPath(navMeshPath, bounds.min, 0, journeySpaceRange, journeySpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                    MarkPath(navMeshPath, bounds.min, journeySpaceRange, pauseSpaceRange, pauseSpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                }
                            }
                            break;

                        default:
                            {
                                #region Get the current affordance path dictionary based on weather conditions
                                Dictionary<Place, Vector3[]> currentAffordancePathDictionary;
                                if (isHotCold)
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenWetIntemperate;
                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenDryIntemperate;
                                    }
                                }
                                else
                                {
                                    if (isRaining)
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenWetTemperate;
                                    }
                                    else
                                    {
                                        currentAffordancePathDictionary = place.pathToDictionaryWhenDryTemperate;
                                    }
                                }
                                #endregion

                                foreach (Vector3[] path in currentAffordancePathDictionary.Values)
                                {
                                    MarkPath(path, bounds.min, 0, journeySpaceRange, journeySpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                    MarkPath(path, bounds.min, journeySpaceRange, pauseSpaceRange, pauseSpaceBit);
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                }

                            }
                            break;
                    }
                }
            }
        }
        */
        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Inferred space types.")
        );
        atlasHandler.UpdateTicket(simulationTicket);
        #endregion

        #region Paint nearest pause space maps

        #region Create a "map" to apply to each pixel of the floor plate
        const float pauseSpaceAttractionDistance = 8;
        float[,] coneDepthMap = new float[
            (int)(2 * pauseSpaceAttractionDistance / spaceStep.x),
            (int)(2 * pauseSpaceAttractionDistance / spaceStep.z)
        ];
        for (int z = 0; z < coneDepthMap.GetLength(1); z++)
        {
            int offsetZ = (int)((z - coneDepthMap.GetLength(1) / 2) * spaceStep.z);
            for (int x = 0; x < coneDepthMap.GetLength(0); x++)
            {
                int offsetX = (int)((x - coneDepthMap.GetLength(0) / 2) * spaceStep.x);
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
                if (distance < pauseSpaceAttractionDistance)
                {
                    coneDepthMap[x, z] = pauseSpaceAttractionDistance - distance;
                }
            }
        }
        #endregion

        Dictionary<int, int[,]> nearestPauseSpaceMapsByElevation = new Dictionary<int, int[,]>();
        {
            // For each floor plate
            foreach (int yIndex in floorIndices)
            {
                // Create a "texture map" in which each pixel contains the location
                // of the nearest pause space spawn point.
                int[,] map = new int[xCount, zCount];

                float[,] zBuffer = new float[xCount, zCount];

                // for each scan line (z)
                for (int zIndex = 0; zIndex < map.GetLength(1); zIndex += 5)
                {
                    //int zStart = Math.Max(0, zIndex - depthMap.GetLength(1)/2);
                    //int zEnd = Math.Min(zIndex + depthMap.GetLength(1) / 2, map.GetLength(1) - 1);

                    // for each pixel (x) in the z'th scanline
                    for (int xIndex = 0; xIndex < generalVoxelGrid.Width; xIndex += 5)
                    {
                        // If the current pixel is tagged as pause space ..
                        if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, pauseSpaceBit) != 0x00)
                        {
                            // Event: We have encounted a valid pause space dwell point (PSDP).

                            // The plan:
                            // Paint the a cone with a "colour" that represents the xy location of this PSDP
                            // Render the prebaked cone depth map centerd on the current pixel.
                            // The z buffer will is used to compare the relative distances of competing PSDPs
                            // at each point in the floor plate.


                            // Use z buffer compositing to produce the map
                            int xRadius = coneDepthMap.GetLength(0) / 2;
                            int zRadius = coneDepthMap.GetLength(1) / 2;

                            // Loop over the cone depth map
                            for (int zCone = 0; zCone < coneDepthMap.GetLength(1); zCone++)
                            {
                                // What scanline matches the zCone'th scanline of the cone depth map?
                                int zMap = zIndex - zRadius + zCone;
                                if (0 <= zMap && zMap < map.GetLength(1))
                                {
                                    for (int xCone = 0; xCone < coneDepthMap.GetLength(0); xCone++)
                                    {
                                        // What pixel matches the xCone'th pixel of the zCone'th scanline of the cone depth map?
                                        int xMap = xIndex - xRadius + xCone;

                                        // Is there a pixel to compare? (edge condition.. literally)
                                        if (0 <= xMap && xMap < map.GetLength(0))
                                        {
                                            // Is it further away?
                                            if (coneDepthMap[xCone, zCone] > zBuffer[xMap, zMap])
                                            {
                                                // Write the xz coordindate into the map
                                                map[xMap, zMap] =
                                                    zIndex * map.GetLength(0) + xIndex;
                                                zBuffer[xMap, zMap] = coneDepthMap[xCone, zCone];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                nearestPauseSpaceMapsByElevation[yIndex] = map;

                #region Save the maps as images (debug only)
                //Texture2D mapTexture = new Texture2D(map.GetLength(0), map.GetLength(1));
                //for (int z = 0; z < map.GetLength(1); z++)
                //{
                //    for (int x = 0; x < map.GetLength(0); x++)
                //    {
                //        mapTexture.SetPixel(x, z, new Color((int)(map[x, z] % map.GetLength(0)) / 255.0f, (int)(map[x, z] / map.GetLength(0)) / 255.0f, 0));
                //    }
                //}
                //File.WriteAllBytes($"Map {yIndex}.png", mapTexture.EncodeToPNG());
                #endregion
            }
        }
        #endregion

        #region Infer agent interactions
        int secondsOfPotentialInteraction = 0;
        int secondsOfTwoOrMoreVisibleAgents = 0;
        int secondsOfAgentsWhoAreOpen = 0;
        int secondsOfAgentsWhoAreInRange = 0;
        int secondsOfAgentsWhoAreInRangeAndVisible = 0;
        int secondsOfAgentsWhoEncountered = 0;
        if (true)
        {
            const float horizontalEncounterDistanceLimit = 23.0f; // meters
            const float verticalEncounterDistanceLimit = 5.4f; // meters
            TimeSpan encounterMemoryPeriod = TimeSpan.FromSeconds(15);
            Vector3 eyeOffset = new Vector3(0, 1.7f, 0);

            int totalEncounters = 0;
            int totalGreetings = 0;
            int totalConversations = 0;
            int maxEncounters = 0;
            int maxGreetings = 0;
            int maxConversations = 0;

            #region Establish start and end times to simulate over
            TimeSpan startTime = TimeSpan.Parse(simulationTicket.StartTime);
            TimeSpan endTime = TimeSpan.Parse(simulationTicket.EndTime);
            #endregion

            for (int day = 0; day < simulationTicket.DaysToSimulate; day++)
            {
                int todaysEncounters = 0;
                int todaysGreetings = 0;
                int todaysConversations = 0;

                #region Create a bit mask of agent activity.
                BitArray anyoneInAttendance = new BitArray(secondsPerDay);
                foreach (Agent agent in agents)
                {
                    anyoneInAttendance = anyoneInAttendance.Or(agent.visibilityByDay[day]);
                }
                #endregion

                try
                {
                    Dictionary<Agent, ISample> lastAgentSamples = null;
                    Dictionary<Agent, float> strangerSeparationThresholds =
                        new Dictionary<Agent, float>();
                    Dictionary<Agent, float> strangerCollisionThresholds =
                        new Dictionary<Agent, float>();
                    Dictionary<Agent, TimeSpan> strangerAwkwardnessTimeouts =
                        new Dictionary<Agent, TimeSpan>();
                    foreach (Agent agent in agents)
                    {
                        strangerSeparationThresholds[agent] = (float)(
                            8 + 1.0 * (2 * random.NextDouble() - 1)
                        );
                        strangerCollisionThresholds[agent] = (float)(
                            1.5 + 0.5 * (2 * random.NextDouble() - 1)
                        );
                        strangerAwkwardnessTimeouts[agent] = TimeSpan.FromSeconds(
                            5 + 2 * (2 * random.NextDouble() - 1)
                        );

                        agent.encountersByDay[day] = 0;
                        agent.greetingsByDay[day] = 0;
                        agent.conversationsByDay[day] = 0;
                    }
                    for (
                        TimeSpan simulatedTime = startTime;
                        simulatedTime < endTime;
                        simulatedTime += TimeSpan.FromSeconds(1)
                    )
                    {
                        bool a = true;
                        bool b = true;
                        bool c = true;
                        bool d = true;
                        if (anyoneInAttendance[(int)simulatedTime.TotalSeconds])
                        {
                            secondsOfPotentialInteraction++;
                            #region Find active agents
                            List<Agent> visibleAgents = new List<Agent>();
                            foreach (Agent agent in agents)
                            {
                                if (agent.visibilityByDay[day][(int)simulatedTime.TotalSeconds])
                                {
                                    visibleAgents.Add(agent);
                                }
                            }
                            #endregion

                            if (visibleAgents.Count <= 1)
                                continue;

                            secondsOfTwoOrMoreVisibleAgents++;

                            #region Find agents that are in open space and in elevators
                            Dictionary<Agent, ISample> publicAgentSamples =
                                new Dictionary<Agent, ISample>();
                            // Dictionary<Agent, Vector3> publicAgentLocations = new Dictionary<Agent, Vector3>();
                            Dictionary<Agent, string> publicAgentElevators =
                                new Dictionary<Agent, string>();
                            foreach (Agent activeAgent in visibleAgents)
                            {
                                foreach (Activity activity in activeAgent.calendar[day].activities)
                                {
                                    foreach (IAction action in activity.chosenPlan.actions)
                                    {
                                        if (
                                            action.startTime <= simulatedTime
                                            && simulatedTime < action.startTime + action.duration
                                        )
                                        {
                                            activeAgent.currentActivity = activity;
                                            if (action is Journey)
                                            {
                                                Journey journey = (Journey)action;
                                                Vector3 location;
                                                if (
                                                    journey.GetLocationAtTime(
                                                        simulatedTime,
                                                        out location
                                                    )
                                                )
                                                {
                                                    // publicAgentLocations[activeAgent] = location;
                                                    publicAgentSamples[activeAgent] =
                                                        journey.GetSampleAtTime(simulatedTime);
                                                }
                                            }
                                            else if (action is Motionless)
                                            {
                                                Motionless motionless = (Motionless)action;
                                                Vector3 location;
                                                if (
                                                    motionless.GetLocationAtTime(
                                                        simulatedTime,
                                                        out location
                                                    )
                                                )
                                                {
                                                    // publicAgentLocations[activeAgent] = location;
                                                    publicAgentSamples[activeAgent] =
                                                        motionless.GetSampleAtTime(simulatedTime);
                                                }
                                            }
                                            else if (action is InElevator)
                                            {
                                                InElevator inElevator = (InElevator)action;

                                                publicAgentElevators[activeAgent] =
                                                    $"{inElevator.bank}-{inElevator.car}";
                                            }
                                        }
                                    }
                                }
                            }
                            #endregion

                            foreach (Agent agent in publicAgentSamples.Keys)
                            {
                                #region Work out if agent is motionless
                                bool isMotionless = false;
                                // Two pass .. clearing then blocking..
                                foreach (Activity activity in agent.calendar[day].activities)
                                {
                                    switch (activity.useCase.opportunityFunction)
                                    {
                                        case OpportunityFunction.ClearCalendar:
                                            foreach (IAction action in activity.chosenPlan.actions)
                                            {
                                                if (
                                                    action.startTime <= simulatedTime
                                                    && simulatedTime
                                                        <= action.startTime + action.duration
                                                )
                                                {
                                                    isMotionless = action is Motionless;
                                                }
                                            }
                                            break;
                                    }
                                }
                                foreach (Activity activity in agent.calendar[day].activities)
                                {
                                    switch (activity.useCase.opportunityFunction)
                                    {
                                        case OpportunityFunction.BlockCalendar:
                                            foreach (IAction action in activity.chosenPlan.actions)
                                            {
                                                if (
                                                    action.startTime <= simulatedTime
                                                    && simulatedTime
                                                        <= action.startTime + action.duration
                                                )
                                                {
                                                    isMotionless = action is Motionless;
                                                }
                                            }
                                            break;
                                    }
                                }
                                #endregion

                                #region Give each agent in mind a salience score
                                SortedDictionary<float, Agent> otherAgentSalienceDictionary =
                                    new SortedDictionary<float, Agent>();

                                foreach (Agent otherAgentInMind in agent.otherAgentsInMind.Keys)
                                {
                                    Anchor anchor = agent.otherAgentsInMind[otherAgentInMind];
                                    float salience = 0;
                                    // Known associate
                                    if (agent.associateDictionary.ContainsKey(otherAgentInMind))
                                    {
                                        salience += 150;
                                    }
                                    // Known acquantance
                                    else if (
                                        agent.acquaintanceDictionary.ContainsKey(otherAgentInMind)
                                    )
                                    {
                                        salience += 100;
                                    }
                                    else // they are a stranger
                                    {
                                        salience += 0;
                                    }

                                    // Add their "interest"
                                    salience += anchor.interest;

                                    // Fading from memory
                                    float elapsedTimeSinceLastPerception = (float)(
                                        simulatedTime - anchor.lastPerceivedAt
                                    ).TotalSeconds;
                                    salience += Math.Max(0, elapsedTimeSinceLastPerception * -0.1f);

                                    // If we greeted earlier in this encounter
                                    if (anchor.considerGreeting == false) // We don't greet people we just greeted.
                                    {
                                        salience += 50;
                                    }

                                    // If we had a conversation in this encounter
                                    if (anchor.considerConversation == false)
                                    {
                                        salience += 100;
                                    }

                                    // We negate the saliences so the largest values appear first.
                                    otherAgentSalienceDictionary[-salience] = otherAgentInMind;
                                }
                                #endregion

                                #region Work out which agents to forget
                                List<float> saliencesToForget = new List<float>();

                                foreach (
                                    float negatedOtherAgentSalience in otherAgentSalienceDictionary.Keys
                                )
                                {
                                    float otherAgentSalience = -negatedOtherAgentSalience;
                                    Agent agentInMind = otherAgentSalienceDictionary[
                                        negatedOtherAgentSalience
                                    ];
                                    Anchor agentInMindAnchor = agent.otherAgentsInMind[agentInMind];

                                    // The memory of the other agent has faded
                                    if (
                                        simulatedTime - agentInMindAnchor.lastPerceivedAt
                                        > encounterMemoryPeriod
                                    )
                                    {
                                        saliencesToForget.Add(otherAgentSalience);
                                        // Add an encounter with this agent
                                        // ???
                                    }
                                }

                                int count = 0;
                                foreach (
                                    float negatedOtherAgentSalience in otherAgentSalienceDictionary.Keys
                                )
                                {
                                    float otherAgentSalience = -negatedOtherAgentSalience;
                                    Agent otherAgent = otherAgentSalienceDictionary[
                                        negatedOtherAgentSalience
                                    ];
                                    if (saliencesToForget.Contains(otherAgentSalience) == false)
                                    {
                                        // The agent can only keeps 7 other agents in mind
                                        if (count > 7)
                                        {
                                            saliencesToForget.Add(otherAgentSalience);
                                        }
                                    }
                                    count++;
                                }
                                #endregion

                                #region Now forget them (or perhaps move them to medium term memory).  Unexplored counterfactuals
                                foreach (float salienceToForget in saliencesToForget)
                                {
                                    Agent otherAgent = otherAgentSalienceDictionary[
                                        -salienceToForget
                                    ];
                                    if (agent.agentInteractionsDictionary.ContainsKey(otherAgent))
                                    {
                                        List<IInteraction> interactions =
                                            agent.agentInteractionsDictionary[otherAgent];
                                        foreach (IInteraction interaction in interactions)
                                        {
                                            if (
                                                interaction.startTime
                                                > simulatedTime - encounterMemoryPeriod
                                            )
                                            {
                                                if (interaction is Encounter)
                                                {
                                                    Encounter encounter = (Encounter)interaction;
                                                    encounter.forgotten = true;
                                                    // Debug.Log($"An encounter was forgotten at {simulatedTime}");
                                                    //todaysEncounters--;
                                                    //totalEncounters--;
                                                }
                                            }
                                        }
                                    }
                                    agent.otherAgentsInMind.Remove(
                                        otherAgentSalienceDictionary[-salienceToForget]
                                    );
                                    otherAgentSalienceDictionary.Remove(-salienceToForget);
                                }
                                #endregion

                                foreach (Agent otherAgent in publicAgentSamples.Keys)
                                {
                                    // You can't (physically) encounter yourself (can you)?
                                    if (agent == otherAgent)
                                        continue;

                                    // Likelihood of Looking = (1 - (Slots Occupied * 0.07
                                    // +Familiar * 0.03
                                    // + Well Known * 0.07))
                                    // *Openness

                                    int slotsOccupied = agent.otherAgentsInMind.Count;
                                    int familiar = agent.acquaintanceDictionary.ContainsKey(
                                        otherAgent
                                    )
                                      ? 1
                                      : 0;
                                    int wellKnown = agent.associateDictionary.ContainsKey(
                                        otherAgent
                                    )
                                      ? 1
                                      : 0;
                                    double dwellingOpenness = isMotionless
                                        ? agent.currentActivity.departing.component.agentOpenness
                                        : 1;
                                    double openness =
                                        (
                                            1
                                            - (
                                                slotsOccupied * 0.07
                                                + familiar * 0.04
                                                + wellKnown * 0.07
                                            )
                                        )
                                        * agent.agentType.openness
                                        * dwellingOpenness;

                                    // If the agent is not looking up, then skip to the next agent.
                                    if (random.NextDouble() > openness)
                                        continue;
                                    if (a)
                                    {
                                        secondsOfAgentsWhoAreOpen++;
                                        a = false;
                                    }
                                    int agentXIndex,
                                        agentYIndex,
                                        agentZIndex;
                                    Quantize(
                                        publicAgentSamples[agent].location,
                                        out agentXIndex,
                                        out agentYIndex,
                                        out agentZIndex
                                    );
                                    Vector3 separation =
                                        publicAgentSamples[otherAgent].location
                                        - publicAgentSamples[agent].location;

                                    #region Is the other agent within range and visible?
                                    bool otherAgentIsInRangeAndVisible = false;
                                    float horizontalDistance = Mathf.Sqrt(
                                        separation.x * separation.x + separation.z * separation.z
                                    );
                                    if (
                                        horizontalDistance < horizontalEncounterDistanceLimit
                                        && Mathf.Abs(separation.y) < verticalEncounterDistanceLimit
                                    )
                                    {
                                        if (b)
                                        {
                                            secondsOfAgentsWhoAreInRange++;
                                            b = false;
                                        }
                                        if (separation.magnitude < 0.1f) // meters
                                        {
                                            // The agents are co-resident in space
                                            otherAgentIsInRangeAndVisible = true;
                                        }
                                        // Fire a ray
                                        RaycastHit raycastHit;
                                        if (
                                            Physics.Raycast(
                                                publicAgentSamples[agent].location + eyeOffset,
                                                separation,
                                                out raycastHit
                                            )
                                        )
                                        {
                                            if (raycastHit.distance > separation.magnitude - 0.01f)
                                            {
                                                // Its a hit
                                                otherAgentIsInRangeAndVisible = true;
                                                if (c)
                                                {
                                                    secondsOfAgentsWhoAreInRangeAndVisible++;
                                                    c = false;
                                                }
                                            }
                                        }
                                    }
                                    #endregion

                                    if (otherAgentIsInRangeAndVisible)
                                    {
                                        #region Find/Create/Update the anchor for the other agent
                                        bool isMomentOfFirstEncounter =
                                            agent.otherAgentsInMind.ContainsKey(otherAgent)
                                            == false;
                                        if (isMomentOfFirstEncounter)
                                        {
                                            if (d)
                                            {
                                                secondsOfAgentsWhoEncountered++;
                                                d = false;
                                            }
                                            // Add this perception to short term experience
                                            Anchor newAnchor = new Anchor();
                                            newAnchor.perceivedAgent = otherAgent;
                                            newAnchor.firstPerceivedAt = simulatedTime;
                                            newAnchor.interest = (float)random.NextDouble() * 100f;

                                            newAnchor.familiarity = Anchor.Familiarity.Stranger;
                                            if (
                                                agent.acquaintanceDictionary.ContainsKey(otherAgent)
                                            )
                                            {
                                                newAnchor.familiarity = Anchor
                                                    .Familiarity
                                                    .Acquaintance;
                                            }
                                            if (agent.associateDictionary.ContainsKey(otherAgent))
                                            {
                                                newAnchor.familiarity = Anchor
                                                    .Familiarity
                                                    .Associate;
                                            }
                                            newAnchor.considerGreeting = true;
                                            newAnchor.lastAwkwardnessBasedGreetingAt = null;
                                            newAnchor.distanceBasedGreetingAttempted = false;
                                            newAnchor.collisionBasedGreetingAttempted = false;

                                            agent.otherAgentsInMind[otherAgent] = newAnchor;
                                        }
                                        #endregion

                                        Anchor anchor = agent.otherAgentsInMind[otherAgent];

                                        // Update our time of last perception
                                        anchor.lastPerceivedAt = simulatedTime;

                                        #region Handle encounters
                                        if (isMomentOfFirstEncounter)
                                        {
                                            // Add an encounter
                                            maxEncounters = AddEncounter(
                                                maxEncounters,
                                                publicAgentSamples,
                                                TimeSpan.FromDays(day) + simulatedTime,
                                                agent,
                                                anchor.perceivedAgent
                                            );
                                            agent.encountersByDay[day]++;
                                            todaysEncounters++;
                                            totalEncounters++;

                                            #region Check for promotion to a higher level of familiarity
                                            if (
                                                agent.agentInteractionsDictionary.ContainsKey(
                                                    otherAgent
                                                )
                                            )
                                            {
                                                switch (anchor.familiarity)
                                                {
                                                    case Anchor.Familiarity.Stranger:
                                                        #region Have we just passed the "acquaintance threshold? Yes? Add a new acquaintance
                                                        // Have we encountered the other agent at least 5 times?
                                                        if (
                                                            agent.agentInteractionsDictionary[
                                                                otherAgent
                                                            ].Count >= 5
                                                        )
                                                        {
                                                            // Promote the relationship to acquantance.
                                                            Acquaintance acquaintance =
                                                                new Acquaintance();
                                                            acquaintance.otherAgent = otherAgent;
                                                            acquaintance.startTime = simulatedTime;

                                                            agent.acquaintanceDictionary[
                                                                otherAgent
                                                            ] = acquaintance;

                                                            anchor.familiarity = Anchor
                                                                .Familiarity
                                                                .Acquaintance;

                                                            // Debug.Log("Acquaintanceship formed at " + day + " + " + timeOfDay);
                                                        }
                                                        #endregion
                                                        break;

                                                    case Anchor.Familiarity.Acquaintance:
                                                        #region Have we just passed the "associate threshold"? Yes? Add a new associate
                                                        if (
                                                            agent.associateDictionary.ContainsKey(
                                                                otherAgent
                                                            ) == false
                                                        )
                                                        {
                                                            if (
                                                                agent.agentInteractionsDictionary.ContainsKey(
                                                                    otherAgent
                                                                )
                                                            )
                                                            {
                                                                int greetingsCounter = 0;
                                                                foreach (
                                                                    IInteraction interaction in agent.agentInteractionsDictionary[
                                                                        otherAgent
                                                                    ]
                                                                )
                                                                {
                                                                    if (interaction is Greeting)
                                                                    {
                                                                        greetingsCounter++;
                                                                    }
                                                                }
                                                                if (greetingsCounter >= 5)
                                                                {
                                                                    // Promote the relationship to an associate
                                                                    Associate associate =
                                                                        new Associate();
                                                                    associate.otherAgent =
                                                                        otherAgent;
                                                                    associate.startTime =
                                                                        simulatedTime;

                                                                    agent.associateDictionary[
                                                                        otherAgent
                                                                    ] = associate;
                                                                    // Debug.Log("Association formed at " + day + " + " + timeOfDay);
                                                                }
                                                            }
                                                        }
                                                        #endregion
                                                        break;
                                                }
                                            }
                                            #endregion
                                        }
                                        #endregion

                                        #region Handle greetings
                                        bool attemptGreeting = false;
                                        switch (anchor.familiarity)
                                        {
                                            case Anchor.Familiarity.Acquaintance:
                                            case Anchor.Familiarity.Associate:
                                                attemptGreeting = isMomentOfFirstEncounter;
                                                break;

                                            case Anchor.Familiarity.Stranger:
                                                // If we haven't greeted for a full 60 seconds, its not happening
                                                if (
                                                    (
                                                        simulatedTime - anchor.firstPerceivedAt
                                                    ).TotalSeconds > 60
                                                )
                                                {
                                                    anchor.considerGreeting = false;
                                                }

                                                if (anchor.considerGreeting)
                                                {
                                                    // Close proximity
                                                    if (
                                                        anchor.distanceBasedGreetingAttempted
                                                            == false
                                                        && separation.magnitude
                                                            < strangerSeparationThresholds[agent]
                                                    )
                                                    {
                                                        attemptGreeting = true;
                                                        anchor.distanceBasedGreetingAttempted =
                                                            true;
                                                    }

                                                    // Collision
                                                    if (
                                                        anchor.collisionBasedGreetingAttempted
                                                            == false
                                                        && separation.magnitude
                                                            < strangerCollisionThresholds[agent]
                                                    ) // meters
                                                    {
                                                        attemptGreeting = true;
                                                        anchor.collisionBasedGreetingAttempted =
                                                            true;
                                                    }

                                                    // Awkwardness
                                                    if (
                                                        anchor.lastAwkwardnessBasedGreetingAt
                                                        == null
                                                    )
                                                    {
                                                        // Time out is 5 +/- 2 seconds
                                                        if (
                                                            simulatedTime - anchor.lastPerceivedAt
                                                            > strangerAwkwardnessTimeouts[agent]
                                                        )
                                                        {
                                                            attemptGreeting = true;
                                                            anchor.lastAwkwardnessBasedGreetingAt =
                                                                simulatedTime;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        // Time out is 5 +/- 2 seconds
                                                        if (
                                                            simulatedTime
                                                                - anchor
                                                                    .lastAwkwardnessBasedGreetingAt
                                                                    .Value
                                                            > strangerAwkwardnessTimeouts[agent]
                                                        )
                                                        {
                                                            attemptGreeting = true;
                                                            anchor.lastAwkwardnessBasedGreetingAt =
                                                                simulatedTime;
                                                        }
                                                    }
                                                }
                                                break;
                                        }
                                        #endregion

                                        if (attemptGreeting)
                                        {
                                            #region Determine if the other agent is within view cone
                                            bool isWithinViewCone = false;
                                            if (
                                                lastAgentSamples != null
                                                && lastAgentSamples.ContainsKey(agent)
                                            )
                                            {
                                                Vector3 agentMovement =
                                                    publicAgentSamples[agent].location
                                                    - lastAgentSamples[agent].location;
                                                // If agent is standing still..
                                                if (agentMovement.magnitude < 0.01f)
                                                {
                                                    isWithinViewCone = true;
                                                }
                                                else // agent is moving
                                                {
                                                    Vector3 directionToOtherAgent =
                                                        publicAgentSamples[otherAgent].location
                                                        - publicAgentSamples[agent].location;

                                                    float angleFromForward = Vector3.Angle(
                                                        agentMovement,
                                                        directionToOtherAgent
                                                    );
                                                    isWithinViewCone =
                                                        Math.Abs(angleFromForward) <= 90;
                                                }
                                            }
                                            #endregion

                                            double propensityToGreet = CalculatePropensityToGreet(
                                                agent,
                                                anchor.perceivedAgent,
                                                true
                                            );
                                            if (random.NextDouble() < propensityToGreet)
                                            {
                                                #region Add a greeting
                                                maxGreetings = AddGreeting(
                                                    maxGreetings,
                                                    publicAgentSamples,
                                                    TimeSpan.FromDays(day) + simulatedTime,
                                                    agent,
                                                    anchor.perceivedAgent
                                                );
                                                totalGreetings++;
                                                agent.greetingsByDay[day]++;
                                                todaysGreetings++;
                                                anchor.considerGreeting = false;
                                                #endregion

                                                // Conversations..

                                                #region Familiarity
                                                float propensityToConverse = 0.01f; // stranger
                                                if (
                                                    agent.acquaintanceDictionary.ContainsKey(
                                                        otherAgent
                                                    )
                                                )
                                                {
                                                    propensityToConverse = 0.1f;
                                                }
                                                if (
                                                    agent.associateDictionary.ContainsKey(
                                                        otherAgent
                                                    )
                                                )
                                                {
                                                    propensityToConverse = 0.9f;
                                                }
                                                #endregion

                                                #region Distance-based propensity
                                                const float maximumConversationDistance = 23f; // meters
                                                const float moderateConversationDistance = 10f; // meters
                                                // If the agents are between the Moderate Conversation Distance (e.g. 10 m) to the Maximum Conversation Distance(e.g. 23 m) apart
                                                if (
                                                    moderateConversationDistance
                                                        < horizontalDistance
                                                    && horizontalDistance
                                                        < maximumConversationDistance
                                                )
                                                {
                                                    // Then subtract the Moderate Conversation Distance from the number of meters and multiply by 2.5,
                                                    float factor =
                                                        (
                                                            horizontalDistance
                                                            - moderateConversationDistance
                                                        ) * 2.5f;
                                                    // Subtract the result from the Conversation Propensity
                                                    propensityToConverse = Mathf.Max(
                                                        0,
                                                        propensityToConverse - factor / 100
                                                    );
                                                }
                                                // If the agents are less than the Moderate Conversation Distance meters apart,
                                                else if (
                                                    horizontalDistance
                                                    < moderateConversationDistance
                                                )
                                                {
                                                    // Then subtract the number of meters distance from the Moderate Conversation Distance,
                                                    float factor =
                                                        moderateConversationDistance
                                                        - horizontalDistance;
                                                    // Add the result to the Conversation Propensity
                                                    propensityToConverse = Mathf.Min(
                                                        1,
                                                        propensityToConverse + factor / 100
                                                    );
                                                }
                                                #endregion

                                                #region Speed based propensity
                                                const float hasteInhibitionThreshold = 1.1f;
                                                const float hasteInhibitionMaximum = 0.1f;
                                                float hastePenalty = 0;
                                                if (
                                                    agent.currentActivity.speed
                                                    > hasteInhibitionThreshold
                                                )
                                                {
                                                    hastePenalty += Mathf.Min(
                                                        hasteInhibitionMaximum,
                                                        (float)(
                                                            agent.currentActivity.speed
                                                            - agent.agentType.velocityNorm
                                                        ) / 100f
                                                    );
                                                }
                                                // Might consider at some point
                                                // if (otherAgent.currentActivity.speed > hasteInhibitionThreshold)
                                                // {
                                                //     hastePenalty += Mathf.Min(hasteInhibitionMaximum, (float)(otherAgent.currentActivity.speed - otherAgent.agentType.velocityNorm) / 100f);
                                                // }
                                                propensityToConverse = Mathf.Max(
                                                    0,
                                                    propensityToConverse - hastePenalty
                                                );
                                                #endregion

                                                #region Crowding propensity
                                                const float sociallyPrimedFactor = 0.05f;
                                                if (
                                                    3 <= agent.otherAgentsInMind.Count
                                                    && agent.otherAgentsInMind.Count <= 5
                                                )
                                                {
                                                    propensityToConverse = Mathf.Min(
                                                        1.0f,
                                                        propensityToConverse + sociallyPrimedFactor
                                                    );
                                                }
                                                else if (agent.otherAgentsInMind.Count > 5)
                                                {
                                                    propensityToConverse = Mathf.Max(
                                                        0.0f,
                                                        propensityToConverse - sociallyPrimedFactor
                                                    );
                                                }
                                                #endregion

                                                #region Prospect refuge
                                                {
                                                    // If barriers to sight exist within the Prospect Refuge Distance(e.g. 5 m)
                                                    // for fewer than the Prospect Refuge Arc(e.g. 180 degrees) around the agent,
                                                    // Then divide the number of unobstructed degrees by 18,

                                                    const int numberOfRays = 8;
                                                    const float prospectRefugeDistance = 5;
                                                    const float prospectRefugeArc = 180;
                                                    const float prospectRefugeMaximumArc = 270;
                                                    float unobstructedDegrees = 0;
                                                    for (
                                                        float angle = 0;
                                                        angle < 360;
                                                        angle += 360 / numberOfRays
                                                    )
                                                    {
                                                        RaycastHit wallHit;
                                                        // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.

                                                        if (
                                                            Physics.Raycast(
                                                                publicAgentSamples[agent].location
                                                                    + Vector3.up,
                                                                Quaternion.Euler(0, angle, 0)
                                                                    * Vector3.right,
                                                                out wallHit,
                                                                Mathf.Infinity
                                                            )
                                                        )
                                                        {
                                                            if (
                                                                wallHit.distance
                                                                > prospectRefugeDistance
                                                            )
                                                            {
                                                                unobstructedDegrees +=
                                                                    360 / numberOfRays;
                                                            }
                                                        }
                                                    }
                                                    if (
                                                        unobstructedDegrees
                                                        > prospectRefugeMaximumArc
                                                    )
                                                    {
                                                        // Very enclosed space
                                                        propensityToConverse = Mathf.Max(
                                                            0,
                                                            propensityToConverse
                                                                - unobstructedDegrees / 9f / 100f
                                                        );
                                                    }
                                                    else if (
                                                        unobstructedDegrees > prospectRefugeArc
                                                    )
                                                    {
                                                        // Enclosed area
                                                        propensityToConverse = Mathf.Max(
                                                            0,
                                                            propensityToConverse
                                                                - unobstructedDegrees / 18f / 100f
                                                        );
                                                    }
                                                    else
                                                    {
                                                        // Open area
                                                        // No effect
                                                    }
                                                }
                                                #endregion

                                                #region Feature attraction
                                                const float featureAttractionDistance = 10; // meters
                                                foreach (Place affordance in dwellPoints)
                                                {
                                                    RaycastHit virtualHit;
                                                    // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                                                    if (
                                                        Physics.Raycast(
                                                            publicAgentSamples[agent].location
                                                                + Vector3.up,
                                                            affordance.BottomCenter + Vector3.up,
                                                            out virtualHit,
                                                            Mathf.Infinity
                                                        )
                                                    )
                                                    {
                                                        // If the ray did not hit something before passing through the affordance mark
                                                        if (
                                                            virtualHit.distance
                                                            > Vector3.Distance(
                                                                publicAgentSamples[agent].location,
                                                                affordance.BottomCenter
                                                            )
                                                        )
                                                        {
                                                            //
                                                            if (
                                                                virtualHit.distance
                                                                < featureAttractionDistance
                                                            )
                                                            {
                                                                propensityToConverse +=
                                                                    (
                                                                        featureAttractionDistance
                                                                        - virtualHit.distance
                                                                    )
                                                                    * 2f
                                                                    / 100f;
                                                                break; // This adjustment only occurs once
                                                            }
                                                        }
                                                        else
                                                        {
                                                            // The ray hit an obstruction before reaching the affordance
                                                        }
                                                    }
                                                }
                                                #endregion

                                                #region Pause space attraction
                                                {
                                                    #region Get the distance to the nearest pause space
                                                    int closestElevation = int.MaxValue;
                                                    foreach (int yIndex in floorIndices)
                                                    {
                                                        if (
                                                            Mathf.Abs(agentYIndex - yIndex)
                                                            < closestElevation
                                                        )
                                                        {
                                                            closestElevation = yIndex;
                                                        }
                                                    }
                                                    if (nearestPauseSpaceMapsByElevation.Count > 0)
                                                    {
                                                        int[,] map =
                                                            nearestPauseSpaceMapsByElevation[
                                                                closestElevation
                                                            ];
                                                        // Write the xz coordindate into the map
                                                        int nearestPauseSpaceXIndex =
                                                            map[agentXIndex, agentZIndex]
                                                            % map.GetLength(0);
                                                        int nearestPauseSpaceZIndex =
                                                            map[agentXIndex, agentZIndex]
                                                            / map.GetLength(0);
                                                        float nearestPauseSpaceX =
                                                            bounds.min.x
                                                            + spaceStep.x * nearestPauseSpaceXIndex;
                                                        float nearestPauseSpaceZ =
                                                            bounds.min.z
                                                            + spaceStep.z * nearestPauseSpaceZIndex;
                                                        float distanceToNearestPauseSpace =
                                                            Mathf.Sqrt(
                                                                nearestPauseSpaceX
                                                                    * nearestPauseSpaceX
                                                                    + nearestPauseSpaceZ
                                                                        * nearestPauseSpaceZ
                                                            );
                                                        #endregion

                                                        if (
                                                            distanceToNearestPauseSpace
                                                            < pauseSpaceAttractionDistance
                                                        )
                                                        {
                                                            propensityToConverse = Mathf.Min(
                                                                1.0f,
                                                                propensityToConverse
                                                                    + (
                                                                        pauseSpaceAttractionDistance
                                                                        - distanceToNearestPauseSpace
                                                                    )
                                                                        * 2.5f
                                                                        / 100f
                                                            );
                                                        }
                                                    }
                                                }
                                                #endregion

                                                #region Confinement and intimacy
                                                {
                                                    //If there are obstructions to sight for more than the Confinement Arc(e.g. 270 degrees) within
                                                    //        the Confinement Diameter Threshold(e.g. 1 m),
                                                    //    Then subtract the number of centimeters average distance from 100,
                                                    //        Divide the result by 5,
                                                    //        Subtract the result from the Conversation Propensity.

                                                    const int numberOfRays = 8;
                                                    const float confinementArc = 270f; // degrees
                                                    const float confinementDiameterThreshold = 1f; // meters
                                                    const float intimacyDiameterThreshold = 3; // meters
                                                    float obstructedDegrees = 0;
                                                    float accumlatedConfinementDistances = 0;
                                                    float accumlatedConfinementCount = 0;
                                                    float intimacyDegrees = 0;
                                                    float accumlatedIntimacyDistances = 0;
                                                    float accumlatedIntimacyCount = 0;
                                                    for (
                                                        float angle = 0;
                                                        angle < 360;
                                                        angle += 360 / numberOfRays
                                                    )
                                                    {
                                                        RaycastHit wallHit;
                                                        // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                                                        if (
                                                            Physics.Raycast(
                                                                publicAgentSamples[agent].location
                                                                    + Vector3.up,
                                                                Quaternion.Euler(0, angle, 0)
                                                                    * Vector3.right,
                                                                out wallHit,
                                                                Mathf.Infinity
                                                            )
                                                        )
                                                        {
                                                            if (
                                                                wallHit.distance
                                                                < confinementDiameterThreshold
                                                            )
                                                            {
                                                                accumlatedConfinementCount++;
                                                                obstructedDegrees +=
                                                                    360 / numberOfRays;
                                                                accumlatedConfinementDistances +=
                                                                    confinementDiameterThreshold
                                                                    - wallHit.distance;
                                                            }
                                                            else if (
                                                                wallHit.distance
                                                                < intimacyDiameterThreshold
                                                            )
                                                            {
                                                                accumlatedIntimacyCount++;
                                                                intimacyDegrees +=
                                                                    360 / numberOfRays;
                                                                accumlatedIntimacyDistances +=
                                                                    intimacyDiameterThreshold
                                                                    - wallHit.distance;
                                                            }
                                                        }
                                                    }
                                                    if (accumlatedConfinementCount > 0)
                                                    {
                                                        float averageConfinementDistance =
                                                            accumlatedConfinementDistances
                                                            / accumlatedConfinementCount;
                                                        if (obstructedDegrees > confinementArc)
                                                        {
                                                            // Very enclosed space
                                                            propensityToConverse = Mathf.Max(
                                                                0,
                                                                propensityToConverse
                                                                    - averageConfinementDistance
                                                                        * 0.2f
                                                            );
                                                        }
                                                    }
                                                    else if (
                                                        accumlatedIntimacyCount > confinementArc
                                                    )
                                                    {
                                                        float averageIntimacyDistance =
                                                            accumlatedIntimacyDistances
                                                            / accumlatedIntimacyCount;
                                                        // Enclosed area
                                                        propensityToConverse = Mathf.Min(
                                                            1,
                                                            propensityToConverse
                                                                + averageIntimacyDistance * 0.2f
                                                        );
                                                    }
                                                    else
                                                    {
                                                        // Open area
                                                        // No effect
                                                    }
                                                }
                                                #endregion

                                                #region Bridge space
                                                const float bridgeSpaceAdder = 0.1f;
                                                if (
                                                    generalVoxelGrid.GetAt(
                                                        generalVoxelGrid.EncodeIndex(
                                                            agentXIndex,
                                                            agentYIndex,
                                                            agentZIndex
                                                        ),
                                                        semiPrivateSpaceBit
                                                    ) == semiPrivateSpaceBit
                                                )
                                                {
                                                    propensityToConverse = Mathf.Min(
                                                        1.0f,
                                                        propensityToConverse + bridgeSpaceAdder
                                                    );
                                                }
                                                #endregion

                                                #region Natural Light
                                                const float naturalLightConversationAdder = 0.1f;
                                                if (
                                                    TimeSpan.FromHours(6) <= simulatedTime
                                                    && simulatedTime < TimeSpan.FromHours(18)
                                                )
                                                {
                                                    propensityToConverse = Mathf.Min(
                                                        1.0f,
                                                        propensityToConverse
                                                            + naturalLightConversationAdder
                                                    );
                                                }
                                                #endregion

                                                #region Meteorological factors
                                                {
                                                    int hour =
                                                        day * 24
                                                        + (int)(
                                                            simulatedTime.TotalSeconds
                                                            / secondsPerHour
                                                        );
                                                    bool isHotCold = weatherByLocation[
                                                        simulationTicket.Location
                                                    ].isHotColdByHour[hour];
                                                    bool isRaining = weatherByLocation[
                                                        simulationTicket.Location
                                                    ].isRainingByHour[hour];
                                                    NavMeshHit navMeshHit;
                                                    if (
                                                        NavMesh.SamplePosition(
                                                            publicAgentSamples[agent].location,
                                                            out navMeshHit,
                                                            1f,
                                                            NavMesh.AllAreas
                                                        )
                                                    )
                                                    {
                                                        if (
                                                            navMeshHit.mask
                                                            == NavMesh.GetAreaFromName("Outside")
                                                        )
                                                        {
                                                            if (isHotCold && isRaining)
                                                            {
                                                                propensityToConverse = Mathf.Max(
                                                                    0,
                                                                    propensityToConverse - 0.30f
                                                                );
                                                            }
                                                            else if (isRaining)
                                                            {
                                                                propensityToConverse = Mathf.Max(
                                                                    0,
                                                                    propensityToConverse - 0.20f
                                                                );
                                                            }
                                                            else if (isHotCold)
                                                            {
                                                                propensityToConverse = Mathf.Max(
                                                                    0,
                                                                    propensityToConverse - 0.10f
                                                                );
                                                            }
                                                            else
                                                            {
                                                                propensityToConverse = Mathf.Min(
                                                                    1,
                                                                    propensityToConverse + 0.05f
                                                                );
                                                            }
                                                        }
                                                        else if (
                                                            navMeshHit.mask
                                                            == NavMesh.GetAreaFromName("Undercover")
                                                        )
                                                        {
                                                            if (isHotCold && isRaining)
                                                            {
                                                                propensityToConverse = Mathf.Max(
                                                                    0,
                                                                    propensityToConverse - 0.10f
                                                                );
                                                            }
                                                            else if (isRaining)
                                                            {
                                                                propensityToConverse = Mathf.Min(
                                                                    1,
                                                                    propensityToConverse + 0.05f
                                                                );
                                                            }
                                                            else if (isHotCold)
                                                            {
                                                                propensityToConverse = Mathf.Max(
                                                                    0,
                                                                    propensityToConverse - 0.10f
                                                                );
                                                            }
                                                            else
                                                            {
                                                                propensityToConverse = Mathf.Min(
                                                                    1,
                                                                    propensityToConverse + 0.05f
                                                                );
                                                            }
                                                        }
                                                        else // if (navMeshHit.mask == NavMesh.GetAreaFromName("Inside"))
                                                        {
                                                            // Do nothing
                                                        }
                                                    }
                                                }
                                                #endregion

                                                #region Caution
                                                propensityToConverse *= (float)agent.caution;
                                                #endregion

                                                if (random.NextDouble() < propensityToConverse)
                                                {
                                                    Vector3 conversationPosition =
                                                        publicAgentSamples[agent].location;

                                                    #region Get the distance to the nearest pause space
                                                    int smallestDifference = int.MaxValue;
                                                    int closestElevation = int.MaxValue;
                                                    foreach (int yIndex in floorIndices)
                                                    {
                                                        if (
                                                            Mathf.Abs(agentYIndex - yIndex)
                                                            < smallestDifference
                                                        )
                                                        {
                                                            smallestDifference = Mathf.Abs(
                                                                agentYIndex - yIndex
                                                            );
                                                            closestElevation = yIndex;
                                                        }
                                                    }
                                                    int[,] map = nearestPauseSpaceMapsByElevation[
                                                        closestElevation
                                                    ];
                                                    // Write the xz coordindate into the map
                                                    int nearestPauseSpaceXIndex =
                                                        map[agentXIndex, agentZIndex]
                                                        % map.GetLength(0);
                                                    int nearestPauseSpaceZIndex =
                                                        map[agentXIndex, agentZIndex]
                                                        / map.GetLength(0);
                                                    float nearestPauseSpaceX =
                                                        bounds.min.x
                                                        + spaceStep.x * nearestPauseSpaceXIndex;
                                                    float nearestPauseSpaceY =
                                                        bounds.min.y
                                                        + spaceStep.y * closestElevation
                                                        + elevationShift;
                                                    float nearestPauseSpaceZ =
                                                        bounds.min.z
                                                        + spaceStep.z * nearestPauseSpaceZIndex;
                                                    Vector3 nearestPauseSpacePosition = new Vector3(
                                                        nearestPauseSpaceX,
                                                        nearestPauseSpaceY,
                                                        nearestPauseSpaceZ
                                                    );
                                                    float distanceToNearestPauseSpace = Mathf.Sqrt(
                                                        nearestPauseSpaceX * nearestPauseSpaceX
                                                            + nearestPauseSpaceZ
                                                                * nearestPauseSpaceZ
                                                    );
                                                    #endregion

                                                    if (
                                                        distanceToNearestPauseSpace
                                                        < pauseSpaceAttractionDistance
                                                    )
                                                    {
                                                        conversationPosition =
                                                            nearestPauseSpacePosition;
                                                    }

                                                    // Debug.Log($"Conversation occurred on day {day} at {timeOfDay} between {agent.agentType.name} at {publicAgentSamples[agent].location} and {otherAgent.agentType.name} at {publicAgentSamples[agent].location}");

                                                    maxConversations = AddConversation(
                                                        maxConversations,
                                                        publicAgentSamples,
                                                        TimeSpan.FromDays(day) + simulatedTime,
                                                        agent,
                                                        anchor.perceivedAgent,
                                                        conversationPosition
                                                    );
                                                    totalConversations++;
                                                    agent.conversationsByDay[day]++;
                                                    todaysConversations++;
                                                    anchor.considerConversation = false;
                                                }
                                            }
                                        }
                                    }

                                    if (DateTime.Now - lastHeartBeat > TimeSpan.FromSeconds(60))
                                    {
                                        DateTime start = DateTime.Now;
                                        GC.Collect();
                                    }
                                    lastHeartBeat = UpdateHeartbeat(atlasHandler, lastHeartBeat);
                                }

                                lastAgentSamples = publicAgentSamples;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.Log($"Exception occurred {e.Message} at {e.StackTrace}");
                }
                simulationTicket.eventLog.Add(
                    new SimulationEvent(
                        SimulationEventType.Information,
                        "Day " + day + " simulated"
                    )
                );
                atlasHandler.UpdateTicket(simulationTicket);
                dailyEncounters.Add(todaysEncounters);
            }
            simulationTicket.encounters = totalEncounters;
            simulationTicket.greetings = totalGreetings;
            simulationTicket.conversations = totalConversations;
        }

        #region Generate calendar summary for each agent
        if (false)
        {
            // List<CalendarWrapper> calendarWrappers = new List<CalendarWrapper>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Results for {simulationTicket.ModelName}");
            sb.AppendLine(
                $"There were {secondsOfPotentialInteraction} seconds where at least one agent was active"
            );
            sb.AppendLine(
                $"There were {secondsOfTwoOrMoreVisibleAgents} seconds where more than one agent was active"
            );
            sb.AppendLine(
                $"There were {secondsOfAgentsWhoAreOpen} seconds where any agent was open to an interaction"
            );
            sb.AppendLine(
                $"There were {secondsOfAgentsWhoAreInRange} seconds where any agent is within range of another agent to interact"
            );
            sb.AppendLine(
                $"There were {secondsOfAgentsWhoAreInRangeAndVisible} seconds where any agent is within range and has line of sight"
            );
            sb.AppendLine(
                $"There were {secondsOfAgentsWhoEncountered} seconds where encounters occurred"
            );
            sb.AppendLine();
            sb.AppendLine($"Calendar for {simulationTicket.DaysToSimulate} days of simulation");
            sb.AppendLine();

            for (int index = 0; index < agents.Count; index++)
            {
                Agent agent = agents[index];
                sb.AppendLine(
                    $"Agent: {agent.agentType.name} from {agent.suite.name} at {agent.suite.BottomCenter}"
                );
                // CalendarWrapper calendarWrapper = new CalendarWrapper($"Agent {index} : {agents[index].agentType.name}");
                for (int day = 0; day < simulationTicket.DaysToSimulate; day++)
                {
                    int hoursOfRain = 0;
                    int hoursOfIntemperate = 0;
                    for (
                        int hour = (int)TimeSpan
                            .FromSeconds(startSecond + TimeSpan.FromDays(day).TotalSeconds)
                            .TotalHours;
                        hour
                            < (int)TimeSpan
                                .FromSeconds(startSecond + TimeSpan.FromDays(day + 1).TotalSeconds)
                                .TotalHours;
                        hour += 1
                    )
                    {
                        bool isHotCold = weatherByLocation[
                            simulationTicket.Location
                        ].isHotColdByHour[hour];
                        if (isHotCold)
                            hoursOfIntemperate++;
                        bool isRaining = weatherByLocation[
                            simulationTicket.Location
                        ].isRainingByHour[hour];
                        if (isRaining)
                            hoursOfRain++;
                    }
                    sb.AppendLine(
                        $"  {(newYearMidnight + TimeSpan.FromSeconds(startSecond) + TimeSpan.FromDays(day)).ToLongDateString()} : Rained for {hoursOfRain} hours, Intemperate for {hoursOfIntemperate} hours"
                    );
                    //List<ActivitySummary> dailyActivitySummary = new List<ActivitySummary>();
                    // calendarWrapper.dailyActivitySummaries.Add(dailyActivitySummary);
                    SortedDictionary<TimeSpan, Activity> timeSeries =
                        new SortedDictionary<TimeSpan, Activity>();
                    if (index > agents.Count) { }
                    if (day > agents[index].calendar.Count) { }
                    try
                    {
                        foreach (Activity activity in agents[index].calendar[day].activities)
                        {
                            //ActivitySummary activitySummary = new ActivitySummary(activity.useCase.name, activity.startTime, activity.finishTime);
                            //dailyActivitySummary.Add(activitySummary);
                            timeSeries[activity.startTime] = activity;
                        }
                    }
                    catch (Exception w) { }
                    foreach (Activity activity in timeSeries.Values)
                    {
                        string journeyLog = "";
                        foreach (IAction action in activity.chosenPlan.actions)
                        {
                            if (action.duration.TotalSeconds < 0.01)
                                continue;
                            if (journeyLog.Length > 0)
                                journeyLog += " -> ";
                            if (action is Journey)
                            {
                                Journey journey = (Journey)action;
                                journeyLog +=
                                    $"Walk from {journey.Path[0]} to {journey.Path[journey.Path.Length - 1]}";
                            }
                            if (action is InElevator)
                            {
                                InElevator inElevator = (InElevator)action;
                                journeyLog += $"Elevator in {inElevator.bank}";
                            }
                            if (action is Absent)
                            {
                                if (
                                    activity.useCase.destinationComponentPropensityDictionary.ContainsKey(
                                        FLUID.Component.defaultComponent
                                    )
                                )
                                {
                                    journeyLog += "Awake";
                                }
                                else
                                {
                                    journeyLog += "Absent";
                                }
                            }
                            if (action is Motionless)
                            {
                                Motionless motionless = (Motionless)action;
                                journeyLog +=
                                    $"Dwell with {((int)motionless.visibility * 100)}% visibility";
                            }
                            journeyLog +=
                                $"({TimeSpan.FromSeconds((int)action.duration.TotalSeconds)})";
                        }
                        sb.AppendLine(
                            $"    {activity.useCase.name} from {activity.startTime} to {activity.finishTime} via {journeyLog}"
                        );
                    }
                }
                //calendarWrappers.Add(calendarWrapper);
                //if (index > 2) break;
            }

            #region Write out calendars file
            JsonSerializer jsonSerializer = new JsonSerializer();
            serializer.NullValueHandling = NullValueHandling.Ignore;
            using (StreamWriter sw = new StreamWriter("Calendar.txt"))
            {
                sw.Write(sb.ToString());
                //using (JsonWriter writer = new JsonTextWriter(sw))
                //{
                //    jsonSerializer.Serialize(writer, calendarWrappers);
                //}
            }
            #endregion
        }
        #endregion

        #region Update most/least daily encounters/greetings
        simulationTicket.mostEncountersOnAnyGivenDay = 0;
        simulationTicket.leastEncountersOnAnyGivenDay = int.MaxValue;
        simulationTicket.mostGreetingsOnAnyGivenDay = 0;
        simulationTicket.leastGreetingsOnAnyGivenDay = int.MaxValue;
        for (int day = 0; day < simulationTicket.DaysToSimulate; day++)
        {
            foreach (Agent agent in agents)
            {
                if (agent.encountersByDay.ContainsKey(day))
                {
                    if (agent.encountersByDay[day] > simulationTicket.mostEncountersOnAnyGivenDay)
                    {
                        simulationTicket.mostEncountersOnAnyGivenDay = agent.encountersByDay[day];
                    }
                    if (agent.encountersByDay[day] < simulationTicket.leastEncountersOnAnyGivenDay)
                    {
                        simulationTicket.leastEncountersOnAnyGivenDay = agent.encountersByDay[day];
                    }
                }
                if (agent.greetingsByDay.ContainsKey(day))
                {
                    if (agent.greetingsByDay[day] > simulationTicket.mostGreetingsOnAnyGivenDay)
                    {
                        simulationTicket.mostGreetingsOnAnyGivenDay = agent.greetingsByDay[day];
                    }
                    if (agent.greetingsByDay[day] < simulationTicket.leastGreetingsOnAnyGivenDay)
                    {
                        simulationTicket.leastGreetingsOnAnyGivenDay = agent.greetingsByDay[day];
                    }
                }
                if (agent.conversationsByDay.ContainsKey(day))
                {
                    if (
                        agent.conversationsByDay[day]
                        > simulationTicket.mostConversationsOnAnyGivenDay
                    )
                    {
                        simulationTicket.mostConversationsOnAnyGivenDay = agent.conversationsByDay[
                            day
                        ];
                    }
                    if (
                        agent.conversationsByDay[day]
                        < simulationTicket.leastConversationsOnAnyGivenDay
                    )
                    {
                        simulationTicket.leastConversationsOnAnyGivenDay = agent.conversationsByDay[
                            day
                        ];
                    }
                }
            }
        }
        #endregion

        simulationTicket.acquaintanceships = 0;
        simulationTicket.associations = 0;

        foreach (Agent agent in agents)
        {
            simulationTicket.acquaintanceships += agent.acquaintanceDictionary.Keys.Count;
            simulationTicket.associations += agent.associateDictionary.Keys.Count;
        }
        simulationTicket.simulationProgress = 0.9f;
        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Simulation complete.")
        );
        atlasHandler.UpdateTicket(simulationTicket);
        #endregion

        // Phase 4: Render results
        #region Calculate space type metrics
        totalNavigableSpace = 0;
        totalJourneySpace = 0;
        totalPauseSpace = 0;
        totalPublicSpace = 0;
        totalPrivateSpace = 0;
        totalSemiPrivateSpace = 0;

        {
            int xIndex;
            int yIndex;
            int zIndex;
            foreach (long key in ((SparseVoxelGrid)generalVoxelGrid).valuesByEncodedIndex.Keys)
            {
                ((SparseVoxelGrid)generalVoxelGrid).DecodeKey(
                    key,
                    out xIndex,
                    out yIndex,
                    out zIndex
                );

                if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, journeySpaceBit) != 0x00)
                {
                    totalJourneySpace += spaceStep.x * spaceStep.z;
                }
                else if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, pauseSpaceBit) != 0x00)
                {
                    totalPauseSpace += spaceStep.x * spaceStep.z;
                }
                else if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, publicSpaceBit) != 0x00)
                {
                    totalPublicSpace += spaceStep.x * spaceStep.z;
                }
                else if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, privateSpaceBit) != 0x00)
                {
                    totalPrivateSpace += spaceStep.x * spaceStep.z;
                }
                else if (
                    generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, semiPrivateSpaceBit) != 0x00
                )
                {
                    totalSemiPrivateSpace += spaceStep.x * spaceStep.z;
                }
                else if (generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, navigableBit) != 0x00)
                {
                    totalNavigableSpace += spaceStep.x * spaceStep.z;
                }
            }
        }
        #endregion

        #region Generate the baseline metrics
        simulationTicket.agentPopulation = agents.Count;
        simulationTicket.areaPerAgent = totalNavigableSpace / agents.Count;

        #region Agent Type Populations
        simulationTicket.agentTypePopulations.Clear();
        foreach (AgentType agentType in agentTypes)
        {
            int count = 0;
            foreach (Agent agent in agents)
            {
                if (agent.agentType == agentType)
                {
                    count++;
                }
            }
            if (count > 0)
            {
                simulationTicket.agentTypePopulations.Add(
                    new LabelledPopulation(agentType.name, count)
                );
            }
        }
        #endregion

        #region Dwelling Type Populations
        simulationTicket.dwellingTypePopulations.Clear();
        //foreach (Occupancy occupancy in occupancies)
        //{
        //    int count = 0;
        //    foreach (IPlace place in pointsOfInterest)
        //    {
        //        if (place is Place)
        //        {
        //            if (((Place)place).occupancy == occupancy)
        //            {
        //                count++;
        //            }
        //        }
        //    }
        //    if (count > 0)
        //    {
        //        simulationTicket.dwellingTypePopulations.Add(new LabelledPopulation(occupancy.name, count));
        //    }
        //}
        #endregion

        simulationTicket.elevatorBankCount = elevatorBankDictionary.Count;

        #region Mandatory/Optional Destination Populations
        {
            Dictionary<string, int> mandatoryLabelCounts = new Dictionary<string, int>();
            Dictionary<string, int> optionalLabelCounts = new Dictionary<string, int>();
            foreach (Place affordance in dwellPoints)
            {
                switch (affordance.component.socialSyntax)
                {
                    case SocialSyntaxEnum.Mandatory:
                        if (mandatoryLabelCounts.ContainsKey(affordance.name) == false)
                        {
                            mandatoryLabelCounts[affordance.name] = 0;
                        }
                        mandatoryLabelCounts[affordance.name]++;
                        break;

                    case SocialSyntaxEnum.Optional:
                        if (optionalLabelCounts.ContainsKey(affordance.name) == false)
                        {
                            optionalLabelCounts[affordance.name] = 0;
                        }
                        optionalLabelCounts[affordance.name]++;
                        break;
                }
            }
            simulationTicket.mandatoryDestinationPopulations.Clear();
            foreach (string label in mandatoryLabelCounts.Keys)
            {
                simulationTicket.mandatoryDestinationPopulations.Add(
                    new LabelledPopulation(label, mandatoryLabelCounts[label])
                );
            }
            simulationTicket.optionalDestinationPopulations.Clear();
            foreach (string label in optionalLabelCounts.Keys)
            {
                simulationTicket.optionalDestinationPopulations.Add(
                    new LabelledPopulation(label, optionalLabelCounts[label])
                );
            }
        }
        #endregion

        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Information, "Generated summary statistics.")
        );
        atlasHandler.UpdateTicket(simulationTicket);
        #endregion

        #region Derive the Vega statistics
        simulationTicket.simulationProgress = 0.98f;
        simulationTicket.outputProgress = 0.1f;
        atlasHandler.UpdateTicket(simulationTicket);

        // TODO
        vegaGenerator.GenerateData(this);

        simulationTicket.simulationProgress = 1.0f;
        simulationTicket.outputProgress = 1.0f;
        simulationTicket.eventLog.Add(
            new SimulationEvent(
                SimulationEventType.Information,
                "Completed outputing simulation statistics."
            )
        );
        atlasHandler.UpdateTicket(simulationTicket);
        #endregion

        #region Generate the glTF
        MeshSerializer meshSerializer = new MeshSerializer();

        #region Add the navigable mesh
        // Last due to painters algorithm transparency
        meshSerializer.Add(
            new HomogeneousMesh(
                "Navigable Space",
                partitionalbleNavigationMesh.GetComponent<MeshFilter>().mesh.vertices,
                partitionalbleNavigationMesh.GetComponent<MeshFilter>().mesh.triangles,
                null,
                new Color32(127, 127, 127, 100)
            )
        ); // ??? some.. trasparent grey.. kinda
        #endregion

        #region Generate space type textured meshes for each floor plate
        {
            // Loop over all known floor plate elevations
            //foreach (int yIndex in inferredElevations.Keys)
            //{
            //    if (inferredElevations[yIndex] > 100)
            //    {
            //        if (markedElevations.ContainsKey(yIndex) == false)
            //        {
            //            markedElevations[yIndex] = "Inferred";
            //        }
            //    }
            //}
            #region Collapse voxels into floorplates
            {
                Dictionary<long, short> valuesByEncodedIndex = new Dictionary<long, short>(
                    generalVoxelGrid.valuesByEncodedIndex
                );
                foreach (long encodedIndex in valuesByEncodedIndex.Keys)
                {
                    int x,
                        y,
                        z;
                    generalVoxelGrid.DecodeKey(encodedIndex, out x, out y, out z);
                    long mappedKey = generalVoxelGrid.EncodeIndex(x, yCondensingMap[y], z);
                    if (generalVoxelGrid.valuesByEncodedIndex.ContainsKey(mappedKey))
                    {
                        generalVoxelGrid.valuesByEncodedIndex[mappedKey] = (short)(
                            generalVoxelGrid.valuesByEncodedIndex[mappedKey]
                            | generalVoxelGrid.valuesByEncodedIndex[encodedIndex]
                        );
                    }
                    else
                    {
                        generalVoxelGrid.valuesByEncodedIndex[mappedKey] =
                            generalVoxelGrid.valuesByEncodedIndex[encodedIndex];
                    }
                }
            }
            #endregion

            int layerIndex = 0;
            int floorCounter = 1;
            foreach (int yIndex in floorIndices)
            {
                #region Map the values from the space type grid into a pixel colour values
                Color32[] colors = new Color32[generalVoxelGrid.Width * generalVoxelGrid.Depth];
                for (int xIndex = 0; xIndex < generalVoxelGrid.Width; xIndex++)
                {
                    for (int zIndex = 0; zIndex < generalVoxelGrid.Depth; zIndex++)
                    {
                        int flippedZIndex = generalVoxelGrid.Depth - 1 - zIndex;

                        short sample = generalVoxelGrid.GetAt(xIndex, yIndex, zIndex, allBits);
                        //if ((sample & navigableBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(129, 197, 148, 255);
                        //}
                        //if ((sample & publicSpaceBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(0, 255, 0, 255);
                        //}
                        ////if ((sample & privacyPreservingBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(0, 0, 255, 255);
                        //}

                        //if ((sample & outsideBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(255, 0, 255, 255);
                        //}
                        if ((sample & semiPrivateSpaceBit) != 0x00)
                        {
                            colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(
                                72,
                                183,
                                209,
                                255
                            );
                        }
                        else if ((sample & journeySpaceBit) != 0x00)
                        {
                            colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(
                                141,
                                128,
                                159,
                                255
                            );
                        }
                        else if ((sample & privateSpaceBit) != 0x00)
                        {
                            colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(
                                234,
                                148,
                                141,
                                255
                            );
                        }
                        else if ((sample & pauseSpaceBit) != 0x00)
                        {
                            colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(
                                232,
                                187,
                                83,
                                255
                            );
                        }
                        //if ((sample & lockdownBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(255, 255, 0, 255);
                        //}
                        //if ((sample & exposedBit) != 0x00)
                        //{
                        //    colors[flippedZIndex * generalVoxelGrid.Width + xIndex] = new Color32(0, 0, 255, 255);
                        //}
                    }
                }
                #endregion

                #region Create a flat polygon to show the pixel data at the correct elevation
                // Create a rectangular shape out of two triangles such that the texture mapped in to
                // the model matches the underlying geometry exactly.
                List<Vector3> corners = new List<Vector3>()
                {
                    // We can use the size of the navigation mesh in the xz plane as the reference size for the rectangle.
                    new Vector3(
                        bounds.min.x,
                        bounds.min.y + generalVoxelGrid.elevationShift + yIndex * spaceStep.y,
                        bounds.min.z
                    ),
                    new Vector3(
                        bounds.max.x,
                        bounds.min.y + generalVoxelGrid.elevationShift + yIndex * spaceStep.y,
                        bounds.min.z
                    ),
                    new Vector3(
                        bounds.max.x,
                        bounds.min.y + generalVoxelGrid.elevationShift + yIndex * spaceStep.y,
                        bounds.max.z
                    ),
                    new Vector3(
                        bounds.min.x,
                        bounds.min.y + generalVoxelGrid.elevationShift + yIndex * spaceStep.y,
                        bounds.max.z
                    )
                };
                // Simple UV map.
                List<Vector2> uvs = new List<Vector2>()
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                };
                // Two connected triangles
                List<int> triangles = new List<int>() { 0, 1, 2, 0, 2, 3 };
                #endregion

                #region Create an image and map the colour data into it.
                Texture2D unityTexture = new Texture2D(
                    generalVoxelGrid.Width,
                    generalVoxelGrid.Depth
                );
                unityTexture.SetPixels32(colors);
                #endregion

                #region Create a textured floor plate
                string floorName = $"{layerIndex} Unnamed";
                foreach (float markedElevation in markedElevations.Keys)
                {
                    int markedElevationIndex = (int)Math.Round(
                        (markedElevation - bounds.min.y + generalVoxelGrid.elevationShift)
                            / spaceStep.y
                    );
                    if (Math.Abs(markedElevationIndex - yIndex) < 2)
                    {
                        floorName = $"Level {floorCounter}";
                        break;
                    }
                }

                TexturedMesh floorPlate = new TexturedMesh(
                    floorName,
                    corners.ToArray(),
                    uvs.ToArray(),
                    triangles.ToArray(),
                    null,
                    unityTexture
                );
                #endregion
                layerIndex++;

                meshSerializer.Insert(0, floorPlate);
                if (floorName.EndsWith("Unnamed") == false)
                {
                    floorCounter++;
                }
            }
            simulationTicket.numberOfFloors = floorCounter - 1;
            atlasHandler.UpdateTicket(simulationTicket);
        }
        #endregion

        #region Add affordance markers
        meshSerializer.Add(
            new TexturedMesh(
                "Mandatory Marker",
                new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(0, 1, 0)
                },
                new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                },
                new int[] { 0, 1, 2, 2, 3, 0 },
                null,
                mandatoryTexture
            )
        );

        meshSerializer.Add(
            new TexturedMesh(
                "Optional Marker",
                new Vector3[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(0, 1, 0)
                },
                new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                },
                new int[] { 0, 1, 2, 2, 3, 0 },
                null,
                optionalTexture
            )
        );

        List<Vector3> mandatoryPointPairs = new List<Vector3>();
        List<Vector3> optionalPointPairs = new List<Vector3>();
        Vector3 markerSize = new Vector3(0, 2, 0);
        foreach (Place affordance in dwellPoints)
        {
            switch (affordance.component.socialSyntax)
            {
                case SocialSyntaxEnum.Mandatory:
                    mandatoryPointPairs.Add(affordance.BottomCenter);
                    mandatoryPointPairs.Add(affordance.BottomCenter + markerSize);
                    break;

                case SocialSyntaxEnum.Optional:
                    optionalPointPairs.Add(affordance.BottomCenter);
                    optionalPointPairs.Add(affordance.BottomCenter + markerSize);
                    break;
            }
        }
        if (mandatoryPointPairs.Count > 0)
        {
            meshSerializer.Add(
                new Lines(
                    "Mandatory Affordances",
                    mandatoryPointPairs.ToArray(),
                    new Color32(0, 0, 0, 255)
                )
            );
        }
        if (mandatoryPointPairs.Count > 0)
        {
            meshSerializer.Add(
                new Lines(
                    "Optional Affordances",
                    optionalPointPairs.ToArray(),
                    new Color32(127, 127, 127, 255)
                )
            );
        }
        #endregion

        #region Add interaction lines
        {
            Dictionary<Vector3, int> encountersInSpace = new Dictionary<Vector3, int>();
            Dictionary<Vector3, int> greetingsInSpace = new Dictionary<Vector3, int>();
            const int maximumRays = 1000000;
            int rayCounter = 0;
            for (int dayIndex = 0; dayIndex < simulationTicket.DaysToSimulate; dayIndex++)
            {
                List<Vector3> encounterLaserEndPoints = new List<Vector3>();
                List<Vector3> greetingLaserEndPoints = new List<Vector3>();
                foreach (Agent agent in agents)
                {
                    if (agent.agentInteractionsDictionary != null)
                    {
                        foreach (Agent otherAgent in agent.agentInteractionsDictionary.Keys)
                        {
                            foreach (
                                IInteraction interaction in agent.agentInteractionsDictionary[
                                    otherAgent
                                ]
                            )
                            {
                                Vector3 myCubeCorner = CubeCorner(interaction.myPosition);
                                Vector3 theirCubeCorner = CubeCorner(interaction.theirPosition);
                                if (interaction is Encounter)
                                {
                                    if (encountersInSpace.ContainsKey(myCubeCorner) == false)
                                    {
                                        encountersInSpace[myCubeCorner] = 0;
                                    }
                                    if (encountersInSpace.ContainsKey(theirCubeCorner) == false)
                                    {
                                        encountersInSpace[theirCubeCorner] = 0;
                                    }
                                    if (
                                        encountersInSpace[myCubeCorner]
                                            < maximumSamplesPerCubicMeter
                                        || encountersInSpace[theirCubeCorner]
                                            < maximumSamplesPerCubicMeter
                                    )
                                    {
                                        encounterLaserEndPoints.Add(
                                            interaction.myPosition + new Vector3(0, 1.7f, 0)
                                        );
                                        // encounterLaserEndPoints.Add((interaction.myPosition + interaction.theirPosition) / 2 + new Vector3(0, 1.7f, 0));
                                        encounterLaserEndPoints.Add(
                                            (interaction.theirPosition) + new Vector3(0, 1.7f, 0)
                                        );
                                        encountersInSpace[myCubeCorner]++;
                                        encountersInSpace[theirCubeCorner]++;
                                    }
                                }
                                else if (interaction is Greeting)
                                {
                                    if (greetingsInSpace.ContainsKey(myCubeCorner) == false)
                                    {
                                        greetingsInSpace[myCubeCorner] = 0;
                                    }
                                    if (greetingsInSpace.ContainsKey(theirCubeCorner) == false)
                                    {
                                        greetingsInSpace[theirCubeCorner] = 0;
                                    }
                                    if (
                                        greetingsInSpace[myCubeCorner] < maximumSamplesPerCubicMeter
                                        || greetingsInSpace[theirCubeCorner]
                                            < maximumSamplesPerCubicMeter
                                    )
                                    {
                                        greetingLaserEndPoints.Add(
                                            interaction.myPosition + new Vector3(0, 1.75f, 0)
                                        );
                                        greetingLaserEndPoints.Add(
                                            (interaction.myPosition + interaction.theirPosition) / 2
                                                + new Vector3(0, 1.75f, 0)
                                        );
                                        greetingsInSpace[myCubeCorner]++;
                                        greetingsInSpace[theirCubeCorner]++;
                                    }
                                }
                                rayCounter++;
                                if (rayCounter > maximumRays)
                                    break;
                            }
                            if (rayCounter > maximumRays)
                                break;
                        }
                    }
                    if (rayCounter > maximumRays)
                        break;
                }
                Lines encounterLasers = new Lines(
                    "Day " + dayIndex + " Encounters",
                    encounterLaserEndPoints.ToArray(),
                    new Color32(255, 165, 0, 255)
                );
                meshSerializer.Add(encounterLasers);
                Lines greetingLasers = new Lines(
                    "Day " + dayIndex + " Greetings",
                    greetingLaserEndPoints.ToArray(),
                    new Color32(0, 255, 0, 255)
                );
                meshSerializer.Add(greetingLasers);
            }
        }
        #endregion

        #region Add encounter x's
        {
            Dictionary<Vector3, int> encountersInSpace = new Dictionary<Vector3, int>();
            Vector3 lowerLift = new Vector3(0, 0.1f, 0);
            List<Vector3> xVertices = new List<Vector3>();
            foreach (PathSegment pathSegment in pathSegmentCollection.pathSegments)
            {
                if (pathSegment.encounters.Count > 0)
                {
                    const float xRadius = 0.15f;
                    foreach (Encounter encounter in pathSegment.encounters)
                    {
                        double rotation = random.NextDouble() * Math.PI / 2;
                        Vector3 pointA = new Vector3(
                            (float)Math.Sin(rotation) * xRadius,
                            0,
                            (float)Math.Cos(rotation) * xRadius
                        );
                        Vector3 pointB = new Vector3(
                            (float)Math.Sin(rotation + Math.PI / 2) * xRadius,
                            0,
                            (float)Math.Cos(rotation + Math.PI / 2) * xRadius
                        );

                        Vector3 cubeCorner = CubeCorner(encounter.myPosition);
                        if (encountersInSpace.ContainsKey(cubeCorner) == false)
                        {
                            encountersInSpace[cubeCorner] = 0;
                        }
                        if (encountersInSpace[cubeCorner] < maximumSamplesPerCubicMeter)
                        {
                            xVertices.Add(encounter.myPosition + lowerLift + pointA);
                            xVertices.Add(encounter.myPosition + lowerLift - pointA);
                            xVertices.Add(encounter.myPosition + lowerLift + pointB);
                            xVertices.Add(encounter.myPosition + lowerLift - pointB);
                            encountersInSpace[cubeCorner]++;
                        }
                    }
                }
            }
            if (xVertices.Count > 0)
            {
                Lines encounterXs = new Lines(
                    "Encounter Xs",
                    xVertices.ToArray(),
                    new Color32(255, 165, 0, 255)
                );
                meshSerializer.Add(encounterXs);
            }
        }
        #endregion

        #region Add greetings x's
        {
            Dictionary<Vector3, int> greetingsInSpace = new Dictionary<Vector3, int>();
            Vector3 lowerLift = new Vector3(0, 0.1f, 0);
            List<Vector3> xVertices = new List<Vector3>();
            foreach (PathSegment pathSegment in pathSegmentCollection.pathSegments)
            {
                if (pathSegment.greetings.Count > 0)
                {
                    const float xRadius = 0.15f;
                    foreach (Greeting greeting in pathSegment.greetings)
                    {
                        double rotation = random.NextDouble() * Math.PI / 2;
                        Vector3 pointA = new Vector3(
                            (float)Math.Sin(rotation) * xRadius,
                            0,
                            (float)Math.Cos(rotation) * xRadius
                        );
                        Vector3 pointB = new Vector3(
                            (float)Math.Sin(rotation + Math.PI / 2) * xRadius,
                            0,
                            (float)Math.Cos(rotation + Math.PI / 2) * xRadius
                        );

                        Vector3 cubeCorner = CubeCorner(greeting.myPosition);
                        if (greetingsInSpace.ContainsKey(cubeCorner) == false)
                        {
                            greetingsInSpace[cubeCorner] = 0;
                        }
                        if (greetingsInSpace[cubeCorner] < maximumSamplesPerCubicMeter)
                        {
                            xVertices.Add(greeting.myPosition + lowerLift + pointA);
                            xVertices.Add(greeting.myPosition + lowerLift - pointA);
                            xVertices.Add(greeting.myPosition + lowerLift + pointB);
                            xVertices.Add(greeting.myPosition + lowerLift - pointB);
                            greetingsInSpace[cubeCorner]++;
                        }
                    }
                }
            }
            if (xVertices.Count > 0)
            {
                Lines greetingXs = new Lines(
                    "Greeting Xs",
                    xVertices.ToArray(),
                    new Color32(0, 255, 0, 255)
                );
                meshSerializer.Add(greetingXs);
            }
        }
        #endregion

        #region Add conversation x's
        {
            const float xRadius = 0.3f;
            Vector3 lowerLift = new Vector3(0, 0.1f, 0);
            Dictionary<Vector3, int> conversationsInSpace = new Dictionary<Vector3, int>();
            List<Vector3> xVertices = new List<Vector3>();
            foreach (long encodedPosition in conversationLocationCounts.Keys)
            {
                Vector3 position = Decode(encodedPosition);
                double rotation = random.NextDouble() * Math.PI / 2;
                Vector3 pointA = new Vector3(
                    (float)Math.Sin(rotation) * xRadius,
                    0,
                    (float)Math.Cos(rotation) * xRadius
                );
                Vector3 pointB = new Vector3(
                    (float)Math.Sin(rotation + Math.PI / 2) * xRadius,
                    0,
                    (float)Math.Cos(rotation + Math.PI / 2) * xRadius
                );
                Vector3 cubeCorner = CubeCorner(position);
                if (conversationsInSpace.ContainsKey(cubeCorner) == false)
                {
                    conversationsInSpace[cubeCorner] = 0;
                }
                if (conversationsInSpace[cubeCorner] < maximumSamplesPerCubicMeter)
                {
                    xVertices.Add(position + lowerLift + pointA);
                    xVertices.Add(position + lowerLift - pointA);
                    xVertices.Add(position + lowerLift + pointB);
                    xVertices.Add(position + lowerLift - pointB);
                    conversationsInSpace[cubeCorner]++;
                }
            }

            if (xVertices.Count > 0)
            {
                Lines conversationXs = new Lines(
                    "Conversation Xs",
                    xVertices.ToArray(),
                    new Color32(255, 0, 0, 255)
                );
                meshSerializer.Add(conversationXs);
            }

            //List<Vector3> markVertices = new List<Vector3>();
            //Vector3 lowerLift = new Vector3(0, 0.1f, 0);
            //foreach (long encodedPosition in conversationLocationCounts.Keys)
            //{
            //    float radius = 0.1f;// * conversationLocationCounts[encodedPosition];
            //    Vector3 position = Decode(encodedPosition);
            //    markVertices.Add(position + lowerLift + Vector3.forward * radius);
            //    markVertices.Add(position + lowerLift + Vector3.right * radius);
            //    markVertices.Add(position + lowerLift + Vector3.right * radius);
            //    markVertices.Add(position + lowerLift + Vector3.back * radius);
            //    markVertices.Add(position + lowerLift + Vector3.back * radius);
            //    markVertices.Add(position + lowerLift + Vector3.left);
            //    markVertices.Add(position + lowerLift + Vector3.left);
            //    markVertices.Add(position + lowerLift + Vector3.forward * radius);
            //    GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //    marker.transform.position = position;
            //}
            //if (markVertices.Count > 0)
            //{
            //    Lines conversationXs = new Lines("Conversation Xs", markVertices.ToArray(), new Color32(0, 255, 0, 0));
            //    meshSerializer.Add(conversationXs);
            //}
        }
        #endregion

        #region Add traffic bars
        {
            Vector3 lift = new Vector3(0, 0.1f, 0);
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangleIndices = new List<int>();
            foreach (PathSegment pathSegment in pathSegmentCollection.pathSegments)
            {
                if (pathSegment.traversals > 0)
                {
                    int startIndex = vertices.Count;
                    Vector3 heightOffset = new Vector3(
                        0,
                        0.7f * pathSegment.traversals / maxTraversals,
                        0
                    );
                    vertices.Add(pathSegment.ends[0] + lift);
                    vertices.Add(pathSegment.ends[1] + lift);
                    vertices.Add(pathSegment.ends[1] + heightOffset + lift);
                    vertices.Add(pathSegment.ends[0] + heightOffset + lift);

                    triangleIndices.AddRange(
                        new int[] { startIndex, startIndex + 1, startIndex + 2 }
                    );
                    triangleIndices.AddRange(
                        new int[] { startIndex + 2, startIndex + 3, startIndex }
                    );
                }
            }
            if (vertices.Count > 0)
            {
                HomogeneousMesh trafficMesh = new HomogeneousMesh(
                    "Traffic",
                    vertices.ToArray(),
                    triangleIndices.ToArray(),
                    null,
                    new Color32(0, 0, 255, 200)
                );
                meshSerializer.Add(trafficMesh);
            }
        }
        #endregion

        #region Add the error rays, if any
        if (errorRays.Count > 0)
        {
            Lines greetingLasers = new Lines(
                "Errors",
                errorRays.ToArray(),
                new Color32(255, 0, 0, 255)
            );
            meshSerializer.Add(greetingLasers);
        }
        #endregion

        #region Write out the glTF file
        int fileSizeInBytes = 0;
        string glTFFileName = "Model.glTF";
        using (StreamWriter streamWriter = new StreamWriter(glTFFileName))
        {
            string glTFContent = meshSerializer.Serialize();
            fileSizeInBytes = System.Text.Encoding.ASCII.GetByteCount(glTFContent);
            streamWriter.Write(glTFContent);
        }
        #endregion
        #endregion

        #region Force the glTF file to be written to disk before exiting
        FileInfo info = new FileInfo(glTFFileName);
        DateTime deadManSwitchStartTime = DateTime.Now;
        TimeSpan deadManSwitchTimeout = TimeSpan.FromMinutes(10);

        while (deadManSwitchStartTime + deadManSwitchTimeout > DateTime.Now)
        {
            Thread.Sleep(100);
            info.Refresh();
            if (info.Length >= fileSizeInBytes)
            {
                break;
            }
        }
        if (deadManSwitchStartTime + deadManSwitchTimeout > DateTime.Now)
        {
            simulationTicket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Error, "Writing glTF timed out.")
            );
        }
        else
        {
            simulationTicket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Error, "Writing glTF completed.")
            );
        }
        #endregion

        simulationTicket.eventLog.Add(
            new SimulationEvent(SimulationEventType.Error, "Simulator exiting.")
        );
        Debug.Log("Simulation completed.");
        atlasHandler.UpdateTicket(simulationTicket);

        // Bye bye
        Application.Quit();
    }

    private static DateTime UpdateHeartbeat(AtlasHandler atlasHandler, DateTime lastHeartBeat)
    {
        if (DateTime.Now - lastHeartBeat > TimeSpan.FromMinutes(1))
        {
            atlasHandler.UpdateTimeStamp();
            lastHeartBeat = DateTime.Now;
            GC.Collect();
        }

        return lastHeartBeat;
    }

    private void Rebuild(GameObject masterNavigationMesh)
    {
        #region Clear floor fragment containers
        foreach (Transform child in outsideContainer.transform)
        {
            GameObject.Destroy(child.gameObject);
        }
        foreach (Transform child in undercoverContainer.transform)
        {
            GameObject.Destroy(child.gameObject);
        }
        foreach (Transform child in insideContainer.transform)
        {
            GameObject.Destroy(child.gameObject);
        }
        #endregion

        masterNavigationMesh.SetActive(true);
        PartitionMesh(
            masterNavigationMesh.GetComponent<MeshRenderer>(),
            outsideContainer,
            undercoverContainer,
            insideContainer,
            generalVoxelGrid,
            bounds,
            spaceStep
        );

        masterNavigationMesh.SetActive(false);
        navMeshSurface.BuildNavMesh();
    }

    private float SampleVoxels(short value, short mask)
    {
        float area = 0;
        // March through space to find the voxels that are at the same level the navigation mesh.
        for (int xIndex = 0; xIndex < generalVoxelGrid.Width; xIndex++)
        {
            for (int zIndex = 0; zIndex < generalVoxelGrid.Depth; zIndex++)
            {
                float x = bounds.min.x + (xIndex + 0.5f) * spaceStep.x;
                float z = bounds.min.z + (zIndex + 0.5f) * spaceStep.z;
                Vector3 origin = new Vector3(
                    x,
                    bounds.max.y + generalVoxelGrid.elevationShift - 1,
                    z
                ); // Try to avoid the roof..
                while (origin.y > bounds.min.y + generalVoxelGrid.elevationShift - 1)
                {
                    RaycastHit downwardHit;
                    // This may intersect the floor so we fire the ray from a meter above the current sample position and fire a ray downward.
                    if (Physics.Raycast(origin, Vector3.down, out downwardHit, Mathf.Infinity))
                    {
                        int yIndex = (int)Math.Round(
                            (downwardHit.point.y - bounds.min.y + generalVoxelGrid.elevationShift)
                                / spaceStep.y
                        );
                        generalVoxelGrid.SetAt(xIndex, yIndex, zIndex, value, mask);
                        area += spaceStep.x * spaceStep.z;
                        origin.y = downwardHit.point.y - spaceStep.y;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return area;
    }

    private static Vector3 CubeCorner(Vector3 position)
    {
        return new Vector3((int)position.x, (int)position.y, (int)position.z);
    }

    private static double CalculatePropensityToGreet(
        Agent agent,
        Agent otherVisibleAgent,
        bool isWithinViewCone
    )
    {
        #region Establish a prior for the chance of greeting
        double propensityToGreet = 0;

        if (agent.associateDictionary.ContainsKey(otherVisibleAgent))
        {
            // They are an existing associate
            propensityToGreet = 1.00; // 100%
        }
        else if (agent.acquaintanceDictionary.ContainsKey(otherVisibleAgent))
        {
            // They are an existing acquaintance
            propensityToGreet = 0.5; // 50%
        }
        else
        {
            // They are a stranger
            propensityToGreet = 0.05; // 5%
        }
        #endregion

        #region Update prior if other agent is within view
        if (isWithinViewCone)
        {
            propensityToGreet += 0.10; // 10%
        }
        #endregion

        #region Update prior if other individual is already interacting with some other agent.
        // Can we find an active interaction going on between other agents?
        // Does the other agent have any past interactions?
        //if (otherAgent.agentInteractionsDictionary != null)  // Yes..
        //{
        //    // Is the other agent already in a conversation with me?
        //    if (otherAgent.agentInteractionsDictionary.ContainsKey(agent))
        //    {
        //        foreach (IInteraction interaction in otherAgent.agentInteractionsDictionary[agent])
        //        {
        //            if (interaction is Greeting && timeOfDay - interaction.startTime < greetingDuration)
        //            {
        //                // This agent is currently greeting me
        //            }
        //            else if (interaction is Conversation && timeOfDay - interaction.startTime < conversationDuration)
        //            {
        //                // This agent is currently engaged in a conversation with me
        //            }
        //        }
        //    }

        //    // We want to find
        //    foreach (Agent visibleAgent in visibleAgents)
        //    {
        //        if (visibleAgent != otherAgent)
        //        {
        //            if (otherAgent.agentInteractionsDictionary.ContainsKey(visibleAgent))
        //            {
        //                foreach (IInteraction interaction in otherAgent.agentInteractionsDictionary[visibleAgent])
        //                {
        //                    if (interaction is Greeting && timeOfDay - interaction.startTime < greetingDuration)
        //                    {
        //                        // This agent is currently greeting another agent
        //                    }
        //                    else if (interaction is Conversation && timeOfDay - interaction.startTime < conversationDuration)
        //                    {
        //                        // This agent is currently engaged in a conversation with another agent
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        #endregion

        #region Update prior if "social triangulation" occurs

        #endregion

        #region Apply caution
        propensityToGreet = propensityToGreet * (1.0 - agent.caution);
        #endregion

        return propensityToGreet;
    }

    private int AddEncounter(
        int maxEncounters,
        Dictionary<Agent, ISample> activeAgentSamples,
        TimeSpan timeOfDay,
        Agent agent,
        Agent otherVisibleAgent
    )
    {
        #region Create a new encounter
        Encounter encounter = new Encounter();
        encounter.startTime = timeOfDay;
        encounter.cooldown = TimeSpan.FromSeconds(15);
        encounter.myPosition = activeAgentSamples[agent].location;
        encounter.theirPosition = activeAgentSamples[otherVisibleAgent].location;
        if (agent.agentInteractionsDictionary.ContainsKey(otherVisibleAgent) == false)
        {
            agent.agentInteractionsDictionary[otherVisibleAgent] = new List<IInteraction>();
        }
        agent.agentInteractionsDictionary[otherVisibleAgent].Add(encounter);

        if (activeAgentSamples[agent] is PathSample)
        {
            PathSample pathSample = (PathSample)activeAgentSamples[agent];
            PathSegment pathSegment = pathSegmentCollection.Find(
                pathSample.segmentStart,
                pathSample.segmentEnd
            );
            pathSegment.encounters.Add(encounter);
            if (pathSegment.encounters.Count > maxEncounters)
                maxEncounters = pathSegment.encounters.Count;
        }
        else if (activeAgentSamples[agent] is DwellSample)
        {
            // Do something profound here...
        }
        #endregion

        return maxEncounters;
    }

    private int AddGreeting(
        int maxGreetings,
        Dictionary<Agent, ISample> activeAgentSamples,
        TimeSpan timeOfDay,
        Agent agent,
        Agent otherVisibleAgent
    )
    {
        #region Create a new greeting for this agent
        {
            Greeting greeting = new Greeting();
            greeting.startTime = timeOfDay;
            greeting.cooldown = TimeSpan.FromMinutes(5);
            greeting.myPosition = activeAgentSamples[agent].location;
            greeting.theirPosition = activeAgentSamples[otherVisibleAgent].location;
            if (agent.agentInteractionsDictionary.ContainsKey(otherVisibleAgent) == false)
            {
                agent.agentInteractionsDictionary[otherVisibleAgent] = new List<IInteraction>();
            }
            agent.agentInteractionsDictionary[otherVisibleAgent].Add(greeting);

            if (activeAgentSamples[agent] is PathSample)
            {
                PathSample pathSample = (PathSample)activeAgentSamples[agent];
                PathSegment pathSegment = pathSegmentCollection.Find(
                    pathSample.segmentStart,
                    pathSample.segmentEnd
                );
                pathSegment.greetings.Add(greeting);
                if (pathSegment.greetings.Count > maxGreetings)
                    maxGreetings = pathSegment.greetings.Count;
            }
            else if (activeAgentSamples[agent] is DwellSample)
            {
                // Do something profound here...
            }
        }
        #endregion

        #region Create a new greeting for the other agent
        {
            Greeting greeting = new Greeting();
            greeting.startTime = timeOfDay;
            greeting.cooldown = TimeSpan.FromMinutes(5);
            greeting.myPosition = activeAgentSamples[otherVisibleAgent].location;
            greeting.theirPosition = activeAgentSamples[agent].location;
            if (otherVisibleAgent.agentInteractionsDictionary.ContainsKey(agent) == false)
            {
                otherVisibleAgent.agentInteractionsDictionary[agent] = new List<IInteraction>();
            }
            otherVisibleAgent.agentInteractionsDictionary[agent].Add(greeting);

            if (activeAgentSamples[otherVisibleAgent] is PathSample)
            {
                PathSample pathSample = (PathSample)activeAgentSamples[otherVisibleAgent];
                PathSegment pathSegment = pathSegmentCollection.Find(
                    pathSample.segmentStart,
                    pathSample.segmentEnd
                );
                pathSegment.greetings.Add(greeting);
                if (pathSegment.greetings.Count > maxGreetings)
                    maxGreetings = pathSegment.greetings.Count;
            }
            else if (activeAgentSamples[otherVisibleAgent] is DwellSample)
            {
                // Do something profound here...
            }
        }
        #endregion

        return maxGreetings;
    }

    private int AddConversation(
        int maxConversations,
        Dictionary<Agent, ISample> activeAgentSamples,
        TimeSpan timeOfDay,
        Agent agent,
        Agent otherVisibleAgent,
        Vector3 conversationPosition
    )
    {
        #region Create a new conversation for this agent
        {
            Conversation conversation = new Conversation();
            conversation.startTime = timeOfDay;
            conversation.cooldown = TimeSpan.FromMinutes(5);
            conversation.myPosition = activeAgentSamples[agent].location;
            conversation.conversationPosition = conversationPosition;
            conversation.theirPosition = activeAgentSamples[otherVisibleAgent].location;
            if (agent.agentInteractionsDictionary.ContainsKey(otherVisibleAgent) == false)
            {
                agent.agentInteractionsDictionary[otherVisibleAgent] = new List<IInteraction>();
            }
            agent.agentInteractionsDictionary[otherVisibleAgent].Add(conversation);

            if (activeAgentSamples[agent] is PathSample)
            {
                PathSample pathSample = (PathSample)activeAgentSamples[agent];
                PathSegment pathSegment = pathSegmentCollection.Find(
                    pathSample.segmentStart,
                    pathSample.segmentEnd
                );
                pathSegment.conversations.Add(conversation);
            }
            else if (activeAgentSamples[agent] is DwellSample)
            {
                // Do something profound here...
            }
            long encodedPosition = Encode(conversationPosition);
            if (conversationLocationCounts.ContainsKey(encodedPosition) == false)
            {
                conversationLocationCounts[encodedPosition] = 0;
            }
            conversationLocationCounts[encodedPosition] += 1;
        }
        #endregion

        #region Create a new conversation for the other agent
        {
            Conversation conversation = new Conversation();
            conversation.startTime = timeOfDay;
            conversation.cooldown = TimeSpan.FromMinutes(5);
            conversation.myPosition = activeAgentSamples[otherVisibleAgent].location;
            conversation.conversationPosition = conversationPosition;
            conversation.theirPosition = activeAgentSamples[agent].location;
            if (otherVisibleAgent.agentInteractionsDictionary.ContainsKey(agent) == false)
            {
                otherVisibleAgent.agentInteractionsDictionary[agent] = new List<IInteraction>();
            }
            otherVisibleAgent.agentInteractionsDictionary[agent].Add(conversation);

            if (activeAgentSamples[otherVisibleAgent] is PathSample)
            {
                PathSample pathSample = (PathSample)activeAgentSamples[otherVisibleAgent];
                PathSegment pathSegment = pathSegmentCollection.Find(
                    pathSample.segmentStart,
                    pathSample.segmentEnd
                );
                pathSegment.conversations.Add(conversation);
                if (pathSegment.conversations.Count > maxConversations)
                    maxConversations = pathSegment.conversations.Count;
            }
            else if (activeAgentSamples[otherVisibleAgent] is DwellSample)
            {
                // Do something profound here...
            }
            long encodedPosition = Encode(conversationPosition);
            if (conversationLocationCounts.ContainsKey(encodedPosition) == false)
            {
                conversationLocationCounts[encodedPosition] = 0;
            }
            conversationLocationCounts[encodedPosition] += 1;
        }
        #endregion
        return maxConversations;
    }

    void Quantize(Vector3 position, out int xIndex, out int yIndex, out int zIndex)
    {
        xIndex = Math.Max(
            0,
            Math.Min(
                (int)Math.Round((position.x - bounds.min.x) / spaceStep.x),
                generalVoxelGrid.Width - 1
            )
        );
        yIndex = Math.Max(
            0,
            Math.Min(
                (int)Math.Round(
                    (position.y - bounds.min.y + generalVoxelGrid.elevationShift) / spaceStep.y
                ),
                generalVoxelGrid.Height - 1
            )
        );
        zIndex = Math.Max(
            0,
            Math.Min(
                (int)Math.Round((position.z - bounds.min.z) / spaceStep.z),
                generalVoxelGrid.Depth - 1
            )
        );
    }

    long Encode(Vector3 position)
    {
        int xIndex,
            yIndex,
            zIndex;
        Quantize(position, out xIndex, out yIndex, out zIndex);
        return generalVoxelGrid.EncodeIndex(xIndex, yIndex, zIndex);
    }

    Vector3 Decode(long position)
    {
        int xIndex,
            yIndex,
            zIndex;
        generalVoxelGrid.DecodeKey(position, out xIndex, out yIndex, out zIndex);

        return new Vector3(
            xIndex * spaceStep.x + bounds.min.x,
            yIndex * spaceStep.y + bounds.min.y + generalVoxelGrid.elevationShift,
            zIndex * spaceStep.z + bounds.min.z
        );
    }

    public void MarkPath(
        Vector3[] navMeshPath,
        Vector3 boundingBoxMinimum,
        float startRange,
        float endRange,
        short spaceTypeFlag
    )
    {
        for (int nextIndex = 1; nextIndex < navMeshPath.Length; nextIndex++)
        {
            Vector3 thisCorner = navMeshPath[nextIndex - 1];
            Vector3 nextCorner = navMeshPath[nextIndex];
            float distanceToTravel = Vector3.Distance(thisCorner, nextCorner);
            Vector3 step = (nextCorner - thisCorner).normalized * stepSize;
            for (int stepCount = 0; stepCount < distanceToTravel / stepSize; stepCount++)
            {
                Vector3 samplePoint = thisCorner + stepCount * step;

                MarkSpaceType(boundingBoxMinimum, startRange, endRange, samplePoint, spaceTypeFlag);
            }
        }
    }

    private void MarkSpaceType(
        Vector3 boundingBoxMinimum,
        float startRange,
        float endRange,
        Vector3 samplePoint,
        short spaceTypeFlag
    )
    {
        float circumference = 2 * Mathf.PI * endRange;
        float angleIncrement = 6 * spaceStep.magnitude / circumference;

        // We now have our sample point.
        // Next we need to march through space marking nearby voxels with the selected space type

        for (float angle = 0; angle < 2 * Mathf.PI; angle += angleIncrement)
        {
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));

            // Establish the distance to the nearest "wall", if any
            RaycastHit raycastHit;
            float range = endRange;
            if (Physics.Raycast(samplePoint + Vector3.up, direction, out raycastHit, endRange))
            {
                range = raycastHit.distance;
            }

            // March along the ray marking all navigable, unclassified space with the current space type.
            for (float rayStep = startRange; rayStep < range; rayStep += spaceStep.magnitude)
            {
                Vector3 sample = samplePoint + direction * rayStep;
                int xIndex,
                    yIndex,
                    zIndex;
                Quantize(sample, out xIndex, out yIndex, out zIndex);

                int yUpIndex = Math.Min(generalVoxelGrid.Height - 1, yIndex + 1);
                int yDownIndex = Math.Max(0, yIndex - 1);

                // Expect some edge effects due to different triangulation of navigation mesh.
                for (int x = xIndex; x <= Math.Min(xIndex + 1, generalVoxelGrid.Width - 1); x++)
                {
                    for (int z = zIndex; z <= Math.Min(zIndex + 1, generalVoxelGrid.Depth - 1); z++)
                    {
                        if (generalVoxelGrid.GetAt(x, yIndex, z, publicSpaceBit) == publicSpaceBit)
                        {
                            generalVoxelGrid.SetAt(x, yIndex, z, spaceTypeFlag, spaceTypeFlag);
                        }
                        else if (
                            generalVoxelGrid.GetAt(x, yUpIndex, z, publicSpaceBit) == publicSpaceBit
                        )
                        {
                            generalVoxelGrid.SetAt(x, yIndex, z, spaceTypeFlag, spaceTypeFlag);
                        }
                        else if (
                            generalVoxelGrid.GetAt(x, yDownIndex, z, publicSpaceBit)
                            == publicSpaceBit
                        )
                        {
                            generalVoxelGrid.SetAt(x, yIndex, z, spaceTypeFlag, spaceTypeFlag);
                        }
                    }
                }
            }
        }
    }

    static public void FitViewToModel(Transform model, out Vector3 min, out Vector3 max)
    {
        // We need to find the center of the object and make it an attractor for agents.
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (Transform child in model)
        {
            foreach (Renderer renderer in child.gameObject.GetComponentsInChildren<Renderer>())
            {
                minX = Math.Min(minX, renderer.bounds.min[0]);
                maxX = Math.Max(maxX, renderer.bounds.max[0]);
                minY = Math.Min(minY, renderer.bounds.min[1]);
                maxY = Math.Max(maxY, renderer.bounds.max[1]);
                minZ = Math.Min(minZ, renderer.bounds.min[2]);
                maxZ = Math.Max(maxZ, renderer.bounds.max[2]);
            }
        }
        min = new Vector3(minX, minY, minZ);
        max = new Vector3(maxX, maxY, maxZ);
    }

    Bounds GetRenderBounds(GameObject gameObject)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Renderer render = gameObject.GetComponent<Renderer>();
        if (render != null)
        {
            return render.bounds;
        }
        return bounds;
    }

    Bounds GetBounds(GameObject gameObject)
    {
        Vector3 bottomLeftBack = new Vector3(
            float.PositiveInfinity,
            float.PositiveInfinity,
            float.PositiveInfinity
        );
        Vector3 topRightFront = new Vector3(
            float.NegativeInfinity,
            float.NegativeInfinity,
            float.NegativeInfinity
        );
        foreach (MeshRenderer meshRenderer in gameObject.GetComponentsInChildren<MeshRenderer>())
        {
            if (meshRenderer.bounds.min.x < bottomLeftBack.x)
            {
                bottomLeftBack.x = meshRenderer.bounds.min.x;
            }
            if (meshRenderer.bounds.min.y < bottomLeftBack.y)
            {
                bottomLeftBack.y = meshRenderer.bounds.min.y;
            }
            if (meshRenderer.bounds.min.z < bottomLeftBack.z)
            {
                bottomLeftBack.z = meshRenderer.bounds.min.z;
            }
            if (meshRenderer.bounds.max.x > topRightFront.x)
            {
                topRightFront.x = meshRenderer.bounds.max.x;
            }
            if (meshRenderer.bounds.max.y > topRightFront.y)
            {
                topRightFront.y = meshRenderer.bounds.max.y;
            }
            if (meshRenderer.bounds.max.z > topRightFront.z)
            {
                topRightFront.z = meshRenderer.bounds.max.z;
            }
        }
        return new Bounds(
            (bottomLeftBack + topRightFront) / 2,
            topRightFront - bottomLeftBack + 2 * spaceStep
        );
    }

    float GetScaledPathLength(Vector3[] points, Vector3 scale)
    {
        float result = 0;
        for (int nextIndex = 1; nextIndex < points.Length; nextIndex++)
        {
            Vector3 thisScaledCorner = Vector3.Scale(points[nextIndex - 1], scale);
            Vector3 nextScaledCorner = Vector3.Scale(points[nextIndex], scale);
            result += Vector3.Distance(thisScaledCorner, nextScaledCorner);
        }
        return result;
    }
}
