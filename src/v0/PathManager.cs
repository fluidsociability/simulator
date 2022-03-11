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

using FLUID;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

public class PathManager
{
    StructuredCausalModel structuredCausalModel { get; set; }
    int defaultLayerIndex;
    int passThroughLayerIndex;
    Dictionary<Place, Dictionary<Place, Vector3[]>> temperateDryPaths =
        new Dictionary<Place, Dictionary<Place, Vector3[]>>();
    Dictionary<Place, Dictionary<Place, Vector3[]>> temperateRainingPaths =
        new Dictionary<Place, Dictionary<Place, Vector3[]>>();
    Dictionary<Place, Dictionary<Place, Vector3[]>> intemperateDryPaths =
        new Dictionary<Place, Dictionary<Place, Vector3[]>>();
    Dictionary<Place, Dictionary<Place, Vector3[]>> intemperateRainingPaths =
        new Dictionary<Place, Dictionary<Place, Vector3[]>>();
    Dictionary<string, Dictionary<Place, Vector3[]>> pathsToNearestElevatorByBank =
        new Dictionary<string, Dictionary<Place, Vector3[]>>();

    public PathManager(StructuredCausalModel structuredCausalModel)
    {
        this.structuredCausalModel = structuredCausalModel;
        defaultLayerIndex = LayerMask.NameToLayer("Default");
        passThroughLayerIndex = LayerMask.NameToLayer("Pass Through");
    }

    Vector3 Quantize(Vector3 input)
    {
        return new Vector3(
            ((int)input.x * 100) / 100f,
            ((int)input.y * 100) / 100f,
            ((int)input.z * 100) / 100f
        );
    }

    public static float GetPathLength(Vector3[] points)
    {
        float result = 0;
        for (int nextIndex = 1; nextIndex < points.Length; nextIndex++)
        {
            Vector3 thisCorner = points[nextIndex - 1];
            Vector3 nextCorner = points[nextIndex];
            result += Vector3.Distance(thisCorner, nextCorner);
        }
        return result;
    }

    public Vector3[] GetPath(Place start, Place end, bool isHotCold, bool isRaining)
    {
        Vector3 from = Quantize(start.BottomCenter);
        Vector3 to = Quantize(end.BottomCenter);

        #region Select the relevant path dictionary based on weather
        Dictionary<Place, Dictionary<Place, Vector3[]>> relevantPaths = null;
        if (isHotCold)
        {
            if (isRaining)
            {
                relevantPaths = intemperateRainingPaths;
            }
            else
            {
                relevantPaths = intemperateDryPaths;
            }
        }
        else
        {
            if (isRaining)
            {
                relevantPaths = temperateRainingPaths;
            }
            else
            {
                relevantPaths = temperateDryPaths;
            }
        }
        #endregion

        #region See if the path has already been found
        if (relevantPaths.ContainsKey(start))
        {
            if (relevantPaths[start].ContainsKey(end))
            {
                // We have already found this path
                return relevantPaths[start][end];
            }
        }
        #endregion

        #region Setup the appropriate agent movement costs based on weather
        NavMeshQueryFilter navMeshQueryFilter = new NavMeshQueryFilter();

        navMeshQueryFilter.agentTypeID = structuredCausalModel.navMeshSurface.agentTypeID;
        navMeshQueryFilter.areaMask = NavMesh.AllAreas;
        NativeArray<float> costs = new NativeArray<float>(new float[32], Allocator.Temp);
        for (int index = 0; index < costs.Length; index++)
        {
            costs[index] = 1.0f;
        }
        if (isHotCold)
        {
            if (isRaining)
            {
                costs[NavMesh.GetAreaFromName("Outside")] = structuredCausalModel
                    .wetIntemperateSpaceTypeWeights
                    .outside;
                costs[NavMesh.GetAreaFromName("Undercover")] = structuredCausalModel
                    .wetIntemperateSpaceTypeWeights
                    .undercover;
                costs[NavMesh.GetAreaFromName("Inside")] = structuredCausalModel
                    .wetIntemperateSpaceTypeWeights
                    .inside;
            }
            else
            {
                costs[NavMesh.GetAreaFromName("Outside")] = structuredCausalModel
                    .dryIntemperateSpaceTypeWeights
                    .outside;
                costs[NavMesh.GetAreaFromName("Undercover")] = structuredCausalModel
                    .dryIntemperateSpaceTypeWeights
                    .undercover;
                costs[NavMesh.GetAreaFromName("Inside")] = structuredCausalModel
                    .dryIntemperateSpaceTypeWeights
                    .inside;
            }
        }
        else
        {
            if (isRaining)
            {
                costs[NavMesh.GetAreaFromName("Outside")] = structuredCausalModel
                    .wetTemperateSpaceTypeWeights
                    .outside;
                costs[NavMesh.GetAreaFromName("Undercover")] = structuredCausalModel
                    .wetTemperateSpaceTypeWeights
                    .undercover;
                costs[NavMesh.GetAreaFromName("Inside")] = structuredCausalModel
                    .wetTemperateSpaceTypeWeights
                    .inside;
            }
            else
            {
                costs[NavMesh.GetAreaFromName("Outside")] = structuredCausalModel
                    .dryTemperateSpaceTypeWeights
                    .outside;
                costs[NavMesh.GetAreaFromName("Undercover")] = structuredCausalModel
                    .dryTemperateSpaceTypeWeights
                    .undercover;
                costs[NavMesh.GetAreaFromName("Inside")] = structuredCausalModel
                    .dryTemperateSpaceTypeWeights
                    .inside;
            }
        }
        #endregion

        #region Setup model as needed
        // Lock all dwelling doors
        {
            foreach (Place place in structuredCausalModel.dwellPoints)
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
        }
        // If start is a dwelling, unlock its door and place a platform at the bottom center
        GameObject startDoorPlatform = null;

        switch (start.component.socialSyntax)
        {
            case SocialSyntaxEnum.Suite:
                startDoorPlatform = GameObject.Instantiate(
                    structuredCausalModel.doorPlatformPrefab
                );
                startDoorPlatform.transform.position = start.BottomCenter;

                start.gameObject.layer = passThroughLayerIndex;
                start.GetComponent<Collider>().enabled = false;
                break;
        }

        // If end is a dwelling, unlock its door and place a platform at the bottom center
        GameObject endDoorPlatform = null;

        switch (end.component.socialSyntax)
        {
            case SocialSyntaxEnum.Suite:
                endDoorPlatform = GameObject.Instantiate(structuredCausalModel.doorPlatformPrefab);
                endDoorPlatform.transform.position = end.BottomCenter;

                start.gameObject.layer = passThroughLayerIndex;
                start.GetComponent<Collider>().enabled = false;
                break;
        }
        #endregion

        NavMeshPath navMeshPathTo = new NavMeshPath();
        structuredCausalModel.navMeshSurface.BuildNavMesh();
        if (relevantPaths.ContainsKey(start) == false)
        {
            relevantPaths[start] = new Dictionary<Place, Vector3[]>();
        }
        if (relevantPaths.ContainsKey(end) == false)
        {
            relevantPaths[end] = new Dictionary<Place, Vector3[]>();
        }
        // We are about to look.. don't look again.
        relevantPaths[start][end] = null;
        relevantPaths[end][start] = null;

        RaycastHit startHit;
        if (Physics.Raycast(start.BottomCenter + Vector3.up, Vector3.down, out startHit, 2))
        {
            RaycastHit endHit;
            if (Physics.Raycast(end.BottomCenter + Vector3.up, Vector3.down, out endHit, 2))
            {
                if (
                    NavMesh.CalculatePath(
                        startHit.point,
                        endHit.point,
                        navMeshQueryFilter,
                        navMeshPathTo
                    )
                )
                {
                    switch (navMeshPathTo.status)
                    {
                        case NavMeshPathStatus.PathComplete:
                            relevantPaths[start][end] = navMeshPathTo.corners;
                            Vector3[] reverse = (Vector3[])relevantPaths[start][end].Clone();
                            Array.Reverse(reverse);
                            relevantPaths[end][start] = reverse;
                            break;

                        default:
                            break;
                    }
                }
            }
        }
        if (relevantPaths[start][end] == null && temperateDryPaths[start][end] != null)
            relevantPaths[start][end] = temperateDryPaths[start][end];
        if (relevantPaths[end][start] == null && temperateDryPaths[end][start] != null)
            relevantPaths[end][start] = temperateDryPaths[end][start];

        if (startDoorPlatform != null)
        {
            GameObject.Destroy(startDoorPlatform);
        }
        if (endDoorPlatform != null)
        {
            GameObject.Destroy(endDoorPlatform);
        }

        return relevantPaths[start][end];
    }

    public Vector3[] GetPathToClosestElevator(
        Place startingPoint,
        string bank,
        bool isHotCold,
        bool isRaining,
        bool reverse = true
    )
    {
        if (pathsToNearestElevatorByBank.ContainsKey(bank) == false)
        {
            pathsToNearestElevatorByBank[bank] = new Dictionary<Place, Vector3[]>();
        }

        if (pathsToNearestElevatorByBank[bank].ContainsKey(startingPoint) == false)
        {
            pathsToNearestElevatorByBank[bank][startingPoint] = null;
            float shortestPathLength = float.PositiveInfinity;
            foreach (Place elevatorDoor in structuredCausalModel.elevatorBankDictionary[bank])
            {
                Vector3 delta = elevatorDoor.BottomCenter - startingPoint.BottomCenter;
                // If within 2 meters vertically and 100 meters horizontally
                if (Mathf.Abs(delta.y) < 2 && (delta.x * delta.x + delta.z * delta.z < 100 * 100))
                {
                    Vector3[] foundPath = GetPath(
                        startingPoint,
                        elevatorDoor,
                        isHotCold,
                        isRaining
                    );
                    float foundPathLength = GetPathLength(foundPath);
                    if (foundPathLength < shortestPathLength)
                    {
                        pathsToNearestElevatorByBank[bank][startingPoint] = foundPath;
                        shortestPathLength = foundPathLength;
                    }
                }
            }
        }
        if (pathsToNearestElevatorByBank[bank][startingPoint] == null)
            return null;
        if (reverse)
        {
            try
            {
                Vector3[] copy = (Vector3[])pathsToNearestElevatorByBank[bank][
                    startingPoint
                ].Clone();
                Array.Reverse(copy);

                return copy;
            }
            catch (Exception e) { }
        }

        return pathsToNearestElevatorByBank[bank][startingPoint];
    }

    public void CullPathsToDistantAffordances(
        IDictionary<Place, IList<UseCase>> relevantUsecasesByPlace
    )
    {
        System.Random random = new System.Random();
        Dictionary<Place, Dictionary<Place, Vector3[]>> relevantPaths;
        foreach (bool isHotCold in new bool[] { false, true })
        {
            foreach (bool isRaining in new bool[] { false, true })
            {
                #region Select the relevant paths
                if (isHotCold)
                {
                    if (isRaining)
                    {
                        relevantPaths = intemperateRainingPaths;
                    }
                    else
                    {
                        relevantPaths = intemperateDryPaths;
                    }
                }
                else
                {
                    if (isRaining)
                    {
                        relevantPaths = temperateRainingPaths;
                    }
                    else
                    {
                        relevantPaths = temperateDryPaths;
                    }
                }
                #endregion

                // The goal of this logic is to remove all paths that are never actually used in a use case.
                try
                {
                    #region Mark all used paths
                    Dictionary<Place, List<Place>> usedPaths = new Dictionary<Place, List<Place>>();
                    foreach (Place suite in relevantPaths.Keys)
                    {
                        switch (suite.component.socialSyntax)
                        {
                            case SocialSyntaxEnum.Suite:
                                if (relevantUsecasesByPlace.ContainsKey(suite))
                                {
                                    // There is a Calendar Clearing inverted path problem here.
                                    foreach (UseCase useCase in relevantUsecasesByPlace[suite])
                                    {
                                        if (
                                            useCase.startingComponentPropensityDictionary.ContainsKey(
                                                FLUID.Component.defaultComponent
                                            )
                                        )
                                        {
                                            // We are starting at the dwell point
                                            if (
                                                useCase.destinationComponentPropensityDictionary.ContainsKey(
                                                    FLUID.Component.defaultComponent
                                                )
                                            )
                                            {
                                                // We are starting and ending at the default component
                                            }
                                            else
                                            {
                                                // We are travelling to some destination
                                                foreach (
                                                    FLUID.Component component in useCase.destinationComponents
                                                )
                                                {
                                                    SortedDictionary<
                                                        float,
                                                        Place
                                                    > candidatesByDistance =
                                                        new SortedDictionary<float, Place>();
                                                    foreach (
                                                        Place otherPlace in relevantPaths[
                                                            suite
                                                        ].Keys
                                                    )
                                                    {
                                                        if (
                                                            useCase.destinationComponents.Contains(
                                                                otherPlace.component
                                                            )
                                                        )
                                                        {
                                                            if (
                                                                relevantPaths[suite][otherPlace]
                                                                != null
                                                            )
                                                            {
                                                                float length =
                                                                    GetPathLength(
                                                                        relevantPaths[suite][
                                                                            otherPlace
                                                                        ]
                                                                    )
                                                                    + (float)random.NextDouble()
                                                                        * 0.001f;
                                                                // We have a candidate
                                                                candidatesByDistance[length] =
                                                                    otherPlace;
                                                            }
                                                        }
                                                    }
                                                    int index = 0;
                                                    foreach (
                                                        Place candidate in candidatesByDistance.Values
                                                    )
                                                    {
                                                        if (
                                                            index
                                                            < component.preferedDestinationMaximum
                                                        )
                                                        {
                                                            if (
                                                                usedPaths.ContainsKey(suite)
                                                                == false
                                                            )
                                                            {
                                                                usedPaths[suite] =
                                                                    new List<Place>();
                                                            }
                                                            if (
                                                                usedPaths.ContainsKey(candidate)
                                                                == false
                                                            )
                                                            {
                                                                usedPaths[candidate] =
                                                                    new List<Place>();
                                                            }
                                                            bool check = false;
                                                            if (
                                                                usedPaths[suite].Contains(candidate)
                                                                == false
                                                            )
                                                            {
                                                                usedPaths[suite].Add(candidate);
                                                                check = true;
                                                            }
                                                            if (
                                                                usedPaths[candidate].Contains(suite)
                                                                == false
                                                            )
                                                            {
                                                                if (check == false)
                                                                    throw new Exception(
                                                                        "Logical flaw"
                                                                    );
                                                                usedPaths[candidate].Add(suite);
                                                            }
                                                        }
                                                        index++;
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
                                            // We are arriving at the dwell pont from somewhere else
                                            foreach (
                                                FLUID.Component component in useCase.startingPointComponents
                                            )
                                            {
                                                SortedDictionary<
                                                    float,
                                                    Place
                                                > candidatesByDistance =
                                                    new SortedDictionary<float, Place>();
                                                foreach (
                                                    Place otherPlace in relevantPaths[suite].Keys
                                                )
                                                {
                                                    if (
                                                        useCase.startingPointComponents.Contains(
                                                            otherPlace.component
                                                        )
                                                    )
                                                    {
                                                        if (
                                                            relevantPaths[suite][otherPlace] != null
                                                        )
                                                        {
                                                            float length =
                                                                GetPathLength(
                                                                    relevantPaths[suite][otherPlace]
                                                                )
                                                                + (float)random.NextDouble()
                                                                    * 0.001f;
                                                            // We have a candidate
                                                            candidatesByDistance[length] =
                                                                otherPlace;
                                                        }
                                                        else if (
                                                            relevantPaths[otherPlace][suite] != null
                                                        )
                                                        {
                                                            float length =
                                                                GetPathLength(
                                                                    relevantPaths[otherPlace][suite]
                                                                )
                                                                + (float)random.NextDouble()
                                                                    * 0.001f;
                                                            // We have a candidate
                                                            candidatesByDistance[length] =
                                                                otherPlace;
                                                        }
                                                    }
                                                }
                                                int index = 0;
                                                foreach (
                                                    Place candidate in candidatesByDistance.Values
                                                )
                                                {
                                                    if (
                                                        index < component.preferedDestinationMaximum
                                                    )
                                                    {
                                                        if (usedPaths.ContainsKey(suite) == false)
                                                        {
                                                            usedPaths[suite] = new List<Place>();
                                                        }
                                                        if (
                                                            usedPaths.ContainsKey(candidate)
                                                            == false
                                                        )
                                                        {
                                                            usedPaths[candidate] =
                                                                new List<Place>();
                                                        }
                                                        bool check = false;
                                                        if (
                                                            usedPaths[suite].Contains(candidate)
                                                            == false
                                                        )
                                                        {
                                                            usedPaths[suite].Add(candidate);
                                                            check = true;
                                                        }
                                                        if (
                                                            usedPaths[candidate].Contains(suite)
                                                            == false
                                                        )
                                                        {
                                                            if (check == false)
                                                                throw new Exception("Logical flaw");
                                                            usedPaths[candidate].Add(suite);
                                                        }
                                                    }
                                                    index++;
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }
                    #endregion

                    #region Find all unused paths
                    Dictionary<Place, List<Place>> unusedPaths =
                        new Dictionary<Place, List<Place>>();
                    foreach (Place from in relevantPaths.Keys)
                    {
                        unusedPaths[from] = new List<Place>();
                        if (usedPaths.ContainsKey(from))
                        {
                            foreach (Place to in relevantPaths[from].Keys)
                            {
                                if (usedPaths[from].Contains(to) == false)
                                {
                                    unusedPaths[from].Add(to);
                                }
                            }
                        }
                        else
                        {
                            unusedPaths[from] = new List<Place>();
                            foreach (Place to in relevantPaths[from].Keys)
                            {
                                unusedPaths[from].Add(to);
                            }
                        }
                    }
                    #endregion

                    #region Remove all unused paths
                    foreach (Place from in unusedPaths.Keys)
                    {
                        foreach (Place to in unusedPaths[from])
                        {
                            relevantPaths[from][to] = null;
                        }
                    }
                    #endregion
                }
                catch (Exception e) { }
            }
        }
    }

    public bool PathExistsTo(Place place, FLUID.Component component)
    {
        if (component == place.component)
            return true;
        if (temperateDryPaths.ContainsKey(place))
        {
            foreach (Place otherPlace in temperateDryPaths[place].Keys)
            {
                if (otherPlace.component == component)
                {
                    if (temperateDryPaths[place][otherPlace] != null)
                        return true;
                }
            }
        }
        return false;
    }

    public bool PathExistsFrom(Place place, FLUID.Component component)
    {
        if (component == place.component)
            return true;
        foreach (Place from in temperateDryPaths.Keys)
        {
            if (from.component == component)
            {
                foreach (Place otherPlace in temperateDryPaths[from].Keys)
                {
                    if (otherPlace == place)
                    {
                        if (temperateDryPaths[from][place] == null)
                        {
                            // Check the reverse direction..
                            if (temperateDryPaths[place][from] != null)
                            {
                                // The reverse path exists.
                            }
                        }
                        return temperateDryPaths[from][place] != null;
                    }
                }
            }
        }
        return false;
    }

    public Dictionary<Place, Vector3[]> GetPlacesReachableOnFoot(Place suite, UseCase useCase)
    {
        Dictionary<Place, Vector3[]> result = new Dictionary<Place, Vector3[]>();

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
                // The "null" journey.
                result[suite] = new Vector3[] { };
            }
            else
            {
                foreach (Place destination in temperateDryPaths[suite].Keys)
                {
                    if (useCase.destinationComponents.Contains(destination.component))
                    {
                        if (temperateDryPaths[suite][destination] != null)
                        {
                            // We have a match.
                            result[destination] = temperateDryPaths[suite][destination];
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
            // This is never called???
            foreach (Place start in temperateDryPaths[suite].Keys)
            {
                if (useCase.startingPointComponents.Contains(start.component))
                {
                    if (temperateDryPaths[start][suite] != null)
                    {
                        // We have a match.
                        result[start] = temperateDryPaths[start][suite];
                    }
                }
            }
        }
        return result;
    }
}
