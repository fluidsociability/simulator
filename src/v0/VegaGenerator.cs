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
using System.Net;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using FLUID;
using FLUID_Simulator;

public class VegaGenerator
{
    // https://stackoverflow.com/questions/924679/c-sharp-how-can-i-check-if-a-url-exists-is-valid
    static public bool IsValidUrl(string url)
    {
        try
        {
            var request = WebRequest.Create(url);
            request.Timeout = 5000;
            request.Method = "HEAD";

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                HttpStatusCode statusCode = response.StatusCode;
                response.Close();
                return statusCode == HttpStatusCode.OK;
            }
        }
        catch //(Exception exception)
        {
            return false;
        }
    }

    public void GenerateData(StructuredCausalModel structuredCausalModel)
    {
        const int secondsPerMinute = 60;
        const int secondsPerHour = 60 * secondsPerMinute;
        const int secondsPerDay = 24 * secondsPerHour;

        AtlasHandler atlasHandler = structuredCausalModel.atlasHandler;

        SimulationTicket ticket = structuredCausalModel.simulationTicket;

        try
        {
            #region Generate the Social Potential dataset
            Console.WriteLine("Starting Generating Social Potential");
            ticket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Information, "Generating Social Potential")
            );
            atlasHandler.UpdateTicket(ticket);

            var socialPotential = new SocialPotential(ticket.id);
            for (int day = 0; day < ticket.DaysToSimulate; day++)
            {
                int numberOfActiveAgents = 0;
                int numberOfEncounteringAgents = 0;
                foreach (Agent agent in structuredCausalModel.agents)
                {
                    DayCalendar dayCalendar = agent.calendar[day];
                    if (dayCalendar.activities.Count > 0)
                    {
                        numberOfActiveAgents++;
                    }
                    bool encountering = false;
                    foreach (Agent otherAgent in agent.agentInteractionsDictionary.Keys)
                    {
                        foreach (
                            IInteraction interaction in agent.agentInteractionsDictionary[
                                otherAgent
                            ]
                        )
                        {
                            if ((int)interaction.startTime.TotalDays == day)
                            {
                                encountering = true;
                                break;
                            }
                        }
                        if (encountering)
                            break;
                    }
                    if (encountering)
                        numberOfEncounteringAgents++;
                }
                socialPotential.dailyData.Add(
                    new SocialEvent(day * secondsPerDay, (int)numberOfActiveAgents, "Active")
                );
                socialPotential.dailyData.Add(
                    new SocialEvent(
                        day * secondsPerDay,
                        (int)numberOfEncounteringAgents,
                        "Encountering"
                    )
                );
            }

            using (StreamWriter streamWriter = new StreamWriter("SocialPotential.json"))
            {
                streamWriter.Write(JsonConvert.SerializeObject(socialPotential));
            }
            #endregion

            #region Generate the Agent Encounters dataset
            Console.WriteLine("Generating Agent Encounters");
            ticket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Information, "Generating Agent Encounters")
            );
            atlasHandler.UpdateTicket(ticket);
            AgentEncounters agentEncounters = new AgentEncounters(ticket.id);

            #region Group dwellings by elevation
            SortedDictionary<float, List<Place>> dwellingsByElevation =
                new SortedDictionary<float, List<Place>>();
            foreach (Place pointOfInterest in structuredCausalModel.dwellPoints)
            {
                if (pointOfInterest is Place)
                {
                    bool found = false;
                    foreach (float dwellingElevation in dwellingsByElevation.Keys)
                    {
                        if (
                            Mathf.Abs(pointOfInterest.transform.position.y - dwellingElevation)
                            < 0.1f
                        ) //meters
                        {
                            dwellingsByElevation[dwellingElevation].Add(pointOfInterest);
                            found = true;
                            break;
                        }
                    }
                    if (found == false)
                    {
                        dwellingsByElevation[pointOfInterest.transform.position.y] =
                            new List<Place>();
                        dwellingsByElevation[pointOfInterest.transform.position.y].Add(
                            pointOfInterest
                        );
                    }
                }
            }
            #endregion

            const int buildingIdentifier = 0;
            agentEncounters.hierarchy.Add(new HierarchyNode(buildingIdentifier, "building"));
            int floor = 1;
            Dictionary<Agent, int> agentReferenceDictionary = new Dictionary<Agent, int>();
            foreach (float elevation in dwellingsByElevation.Keys)
            {
                int elevationIdentifier = agentEncounters.hierarchy.Count;
                agentEncounters.hierarchy.Add(
                    new HierarchyNode(
                        elevationIdentifier,
                        "Floor " + floor + "(" + elevation + " m elevation)",
                        buildingIdentifier
                    )
                );
                int unit = 1;
                foreach (Place dwelling in dwellingsByElevation[elevation])
                {
                    int dwellingIdentifier = agentEncounters.hierarchy.Count;
                    agentEncounters.hierarchy.Add(
                        new HierarchyNode(
                            dwellingIdentifier,
                            "Unit " + (floor * 100 + unit),
                            elevationIdentifier
                        )
                    );
                    foreach (Agent agent in structuredCausalModel.agents)
                    {
                        if (agent.suite == dwelling)
                        {
                            int agentIdentifier = agentEncounters.hierarchy.Count;
                            agentEncounters.hierarchy.Add(
                                new HierarchyNode(
                                    agentIdentifier,
                                    agent.agentType.name + "(" + agent.id + ")",
                                    dwellingIdentifier
                                )
                            );
                            agentReferenceDictionary[agent] = agentIdentifier;
                        }
                    }
                    unit++;
                }
                floor++;
            }

            foreach (Agent agent in structuredCausalModel.agents)
            {
                foreach (Agent otherAgent in agent.agentInteractionsDictionary.Keys)
                {
                    foreach (
                        IInteraction interaction in agent.agentInteractionsDictionary[otherAgent]
                    )
                    {
                        agentEncounters.encounters.Add(
                            new Siting(
                                agentReferenceDictionary[agent],
                                agentReferenceDictionary[otherAgent],
                                (int)interaction.startTime.TotalSeconds,
                                (int)(interaction.startTime.TotalSeconds + 1)
                            )
                        );
                    }
                }
            }

            using (StreamWriter streamWriter = new StreamWriter("AgentEncounters.json"))
            {
                streamWriter.Write(JsonConvert.SerializeObject(agentEncounters));
            }
            #endregion

            #region Generate the Encounter Densities dataset
            Console.WriteLine("Generating Encounter Densities");
            ticket.eventLog.Add(
                new SimulationEvent(
                    SimulationEventType.Information,
                    "Generating Encounter Densities"
                )
            );
            atlasHandler.UpdateTicket(ticket);
            EncounterDensities encounterDensities = new EncounterDensities(ticket.id);

            if (structuredCausalModel.totalJourneySpace > 0)
            {
                encounterDensities.inventory.Add(
                    new SpaceTypeStatistics(
                        0,
                        "circulation",
                        structuredCausalModel.totalJourneySpace
                    )
                );
            }
            if (structuredCausalModel.totalSemiPrivateSpace > 0)
            {
                encounterDensities.inventory.Add(
                    new SpaceTypeStatistics(
                        1,
                        "semi-private",
                        structuredCausalModel.totalSemiPrivateSpace
                    )
                );
            }
            if (structuredCausalModel.totalPauseSpace > 0)
            {
                encounterDensities.inventory.Add(
                    new SpaceTypeStatistics(2, "pause", structuredCausalModel.totalPauseSpace)
                );
            }
            if (structuredCausalModel.totalPrivateSpace > 0)
            {
                encounterDensities.inventory.Add(
                    new SpaceTypeStatistics(3, "private", structuredCausalModel.totalPrivateSpace)
                );
            }

            for (int day = 0; day < ticket.DaysToSimulate; day++)
            {
                SortedDictionary<int, TimeSpan> hourBasedTotalCirculationSpaceSeconds =
                    new SortedDictionary<int, TimeSpan>();
                SortedDictionary<int, TimeSpan> hourBasedTotalSemiPrivateSpaceSeconds =
                    new SortedDictionary<int, TimeSpan>();
                SortedDictionary<int, TimeSpan> hourBasedTotalPauseSpaceSeconds =
                    new SortedDictionary<int, TimeSpan>();
                SortedDictionary<int, TimeSpan> hourBasedTotalPrivateSpaceSeconds =
                    new SortedDictionary<int, TimeSpan>();

                for (int hour = 0; hour < 24; hour++)
                {
                    hourBasedTotalCirculationSpaceSeconds.Add(hour, TimeSpan.Zero);
                    hourBasedTotalSemiPrivateSpaceSeconds.Add(hour, TimeSpan.Zero);
                    hourBasedTotalPauseSpaceSeconds.Add(hour, TimeSpan.Zero);
                    hourBasedTotalPrivateSpaceSeconds.Add(hour, TimeSpan.Zero);
                }

                foreach (Agent agent in structuredCausalModel.agents)
                {
                    DayCalendar dayCalendar = agent.calendar[day];
                    foreach (Activity activity in dayCalendar.activities)
                    {
                        for (
                            int second = (int)activity.startTime.TotalSeconds;
                            second < (int)activity.finishTime.TotalSeconds;
                            second++
                        )
                        {
                            int hour = (second % secondsPerDay) / secondsPerHour;
                            if (hour < 0 || hour > 23)
                                Console.WriteLine("Hour is wrong " + hour);
                            TimeSpan timeOfDay = TimeSpan.FromSeconds(second);
                            IAction action = activity.chosenPlan.GetActionAtTime(timeOfDay);
                            if (action != null) // Check for quantization error..
                            {
                                Vector3 location;
                                if (action.GetLocationAtTime(timeOfDay, out location))
                                {
                                    int sampleXIndex = (int)Mathf.Round(
                                        (location.x - structuredCausalModel.bounds.min.x)
                                            / structuredCausalModel.spaceStep.x
                                    );
                                    int sampleYIndex = (int)Mathf.Round(
                                        (location.y - structuredCausalModel.bounds.min.y)
                                            / structuredCausalModel.spaceStep.y
                                    );
                                    int sampleZIndex = (int)Mathf.Round(
                                        (location.z - structuredCausalModel.bounds.min.z)
                                            / structuredCausalModel.spaceStep.z
                                    );

                                    if (
                                        sampleXIndex < 0
                                        || sampleXIndex
                                            >= structuredCausalModel.generalVoxelGrid.Width
                                    )
                                        Console.WriteLine("X is wrong " + sampleXIndex);
                                    if (
                                        sampleYIndex < 0
                                        || sampleYIndex
                                            >= structuredCausalModel.generalVoxelGrid.Height
                                    )
                                        Console.WriteLine("Y is wrong " + sampleYIndex);
                                    if (
                                        sampleZIndex < 0
                                        || sampleZIndex
                                            >= structuredCausalModel.generalVoxelGrid.Depth
                                    )
                                        Console.WriteLine("Z is wrong " + sampleZIndex);

                                    short spaceTypeMask =
                                        structuredCausalModel.generalVoxelGrid.GetAt(
                                            sampleXIndex,
                                            sampleYIndex,
                                            sampleZIndex,
                                            StructuredCausalModel.journeySpaceBit
                                                | StructuredCausalModel.semiPrivateSpaceBit
                                                | StructuredCausalModel.pauseSpaceBit
                                                | StructuredCausalModel.privateSpaceBit
                                        );
                                    if (
                                        (spaceTypeMask & StructuredCausalModel.journeySpaceBit)
                                        != 0x0000
                                    )
                                    {
                                        hourBasedTotalCirculationSpaceSeconds[hour] +=
                                            TimeSpan.FromSeconds(1);
                                    }
                                    if (
                                        (spaceTypeMask & StructuredCausalModel.semiPrivateSpaceBit)
                                        != 0x0000
                                    )
                                    {
                                        hourBasedTotalSemiPrivateSpaceSeconds[hour] +=
                                            TimeSpan.FromSeconds(1);
                                    }
                                    if (
                                        (spaceTypeMask & StructuredCausalModel.pauseSpaceBit)
                                        != 0x0000
                                    )
                                    {
                                        hourBasedTotalPauseSpaceSeconds[hour] +=
                                            TimeSpan.FromSeconds(1);
                                    }
                                    if (
                                        (spaceTypeMask & StructuredCausalModel.privateSpaceBit)
                                        != 0x0000
                                    )
                                    {
                                        hourBasedTotalPrivateSpaceSeconds[hour] +=
                                            TimeSpan.FromSeconds(1);
                                    }
                                }
                            }
                        }
                    }
                }
                for (int hour = 0; hour < 24; hour++)
                {
                    if (hourBasedTotalCirculationSpaceSeconds[hour].TotalSeconds > 0)
                    {
                        encounterDensities.tallies.Add(
                            new EncounterTally(
                                0,
                                (int)hourBasedTotalCirculationSpaceSeconds[hour].TotalSeconds,
                                day * secondsPerDay + hour * secondsPerHour,
                                day * secondsPerDay + (hour + 1) * secondsPerHour
                            )
                        );
                    }
                    if (hourBasedTotalSemiPrivateSpaceSeconds[hour].TotalSeconds > 0)
                    {
                        encounterDensities.tallies.Add(
                            new EncounterTally(
                                1,
                                (int)hourBasedTotalSemiPrivateSpaceSeconds[hour].TotalSeconds,
                                day * secondsPerDay + hour * secondsPerHour,
                                day * secondsPerDay + (hour + 1) * secondsPerHour
                            )
                        );
                    }
                    if (hourBasedTotalPauseSpaceSeconds[hour].TotalSeconds > 0)
                    {
                        encounterDensities.tallies.Add(
                            new EncounterTally(
                                2,
                                (int)hourBasedTotalPauseSpaceSeconds[hour].TotalSeconds,
                                day * secondsPerDay + hour * secondsPerHour,
                                day * secondsPerDay + (hour + 1) * secondsPerHour
                            )
                        );
                    }
                    if (hourBasedTotalPrivateSpaceSeconds[hour].TotalSeconds > 0)
                    {
                        encounterDensities.tallies.Add(
                            new EncounterTally(
                                3,
                                (int)hourBasedTotalPrivateSpaceSeconds[hour].TotalSeconds,
                                day * secondsPerDay + hour * secondsPerHour,
                                day * secondsPerDay + (hour + 1) * secondsPerHour
                            )
                        );
                    }
                }
            }
            using (StreamWriter streamWriter = new StreamWriter("EncounterDensities.json"))
            {
                streamWriter.Write(JsonConvert.SerializeObject(encounterDensities));
            }
            #endregion

            #region Generate the Trip Count dataset
            Console.WriteLine("Generating Trip Count");
            ticket.eventLog.Add(
                new SimulationEvent(
                    SimulationEventType.Information,
                    "Generating Trip Count dataset"
                )
            );
            atlasHandler.UpdateTicket(ticket);
            int hoursOfSimulation = (int)ticket.DaysToSimulate * 24;
            List<HourlyInteraction> encountersByHour = new List<HourlyInteraction>();
            List<HourlyInteraction> greetingsByHour = new List<HourlyInteraction>();
            List<HourlyInteraction> conversationsByHour = new List<HourlyInteraction>();
            for (int hour = 0; hour < hoursOfSimulation; hour++)
            {
                encountersByHour.Add(new HourlyInteraction());
                encountersByHour[hour].ts = (int)hour * 3600;
                encountersByHour[hour].te = (int)(hour + 1) * 3600 - 1;
                encountersByHour[hour].c = "encounter";

                greetingsByHour.Add(new HourlyInteraction());
                greetingsByHour[hour].ts = (int)hour * 3600;
                greetingsByHour[hour].te = (int)(hour + 1) * 3600 - 1;
                greetingsByHour[hour].c = "greeting";

                conversationsByHour.Add(new HourlyInteraction());
                conversationsByHour[hour].ts = (int)hour * 3600;
                conversationsByHour[hour].te = (int)(hour + 1) * 3600 - 1;
                conversationsByHour[hour].c = "conversation";
            }

            foreach (Agent agent in structuredCausalModel.agents)
            {
                foreach (Agent otherAgent in agent.agentInteractionsDictionary.Keys)
                {
                    foreach (
                        IInteraction interaction in agent.agentInteractionsDictionary[otherAgent]
                    )
                    {
                        int hour = (int)interaction.startTime.TotalSeconds / 3600;
                        if (interaction is Encounter)
                        {
                            encountersByHour[hour].n++;
                        }
                        else if (interaction is Greeting)
                        {
                            greetingsByHour[hour].n++;
                        }
                        else if (interaction is Conversation)
                        {
                            conversationsByHour[hour].n++;
                        }
                    }
                }
            }
            HourlyInteractions hourlyInteractions = new HourlyInteractions();
            for (int hour = 0; hour < hoursOfSimulation; hour++)
            {
                if (encountersByHour[hour].n > 0)
                {
                    hourlyInteractions.samples.Add(encountersByHour[hour]);
                }
                if (greetingsByHour[hour].n > 0)
                {
                    hourlyInteractions.samples.Add(greetingsByHour[hour]);
                }
                if (conversationsByHour[hour].n > 0)
                {
                    hourlyInteractions.samples.Add(conversationsByHour[hour]);
                }
            }
            using (StreamWriter streamWriter = new StreamWriter("HourlyInteractions.json"))
            {
                streamWriter.Write(JsonConvert.SerializeObject(hourlyInteractions));
            }
            #endregion

            #region the Activity Counts dataset
            if (false)
            {
                Console.WriteLine("Generating Activity Counts dataset");
                ticket.eventLog.Add(
                    new SimulationEvent(
                        SimulationEventType.Information,
                        "Generating Activity Counts dataset"
                    )
                );
                atlasHandler.UpdateTicket(ticket);
                ActiviyCounts activityCounts = new ActiviyCounts();
                Dictionary<string, List<ActivityCount>> activityCountsByPlace =
                    new Dictionary<string, List<ActivityCount>>();

                foreach (Place place in structuredCausalModel.dwellPoints)
                {
                    string placeName = place.name + " " + place.BottomCenter;
                    #region Set up the activity counts for each time step so they are ready to accumulate statistics.
                    // if (activityCountsByPlace.ContainsKey(placeName) == false)
                    {
                        activityCountsByPlace[placeName] = new List<ActivityCount>();

                        for (
                            int seconds = 0;
                            seconds < ticket.DaysToSimulate * secondsPerDay;
                            seconds += secondsPerHour
                        )
                        {
                            foreach (UseCase useCase in structuredCausalModel.useCases)
                            {
                                ActivityCount activityCount = new ActivityCount();
                                activityCount.useCase = useCase.name;
                                activityCount.startedAt = seconds;
                                activityCount.endedAt =
                                    activityCount.startedAt + secondsPerHour - 1;
                                activityCount.trips = 0;
                                activityCount.encounters = 0;
                                activityCount.greetings = 0;
                                activityCount.conversations = 0;
                                activityCount.destinationIdentity =
                                    place.name + " " + place.BottomCenter;

                                activityCountsByPlace[placeName].Add(activityCount);
                            }
                        }
                    }
                    #endregion
                }
                // Dictionary<UseCase, >
                foreach (Agent agent in structuredCausalModel.agents)
                {
                    for (int day = 0; day < agent.calendar.Count; day++)
                    {
                        DayCalendar dayCalendar = agent.calendar[day];
                        foreach (Activity activity in dayCalendar.activities)
                        {
                            string placeName =
                                activity.departing.name + " " + activity.departing.BottomCenter;
                            int hourOfInterest =
                                (int)activity.startTime.TotalSeconds / secondsPerHour;
                            int offset = day * 24 + hourOfInterest;
                            ActivityCount relevantActivityCount = activityCountsByPlace[placeName][
                                hourOfInterest
                            ];

                            // We now have the day, the hour, the place and the activityCount .. now we need to find the encounters.
                            foreach (Agent otherAgent in agent.agentInteractionsDictionary.Keys)
                            {
                                foreach (
                                    IInteraction interaction in agent.agentInteractionsDictionary[
                                        otherAgent
                                    ]
                                )
                                {
                                    // if the interaction happened in the current hour..
                                    if (
                                        (int)interaction.startTime.TotalSeconds / secondsPerHour
                                        == hourOfInterest
                                    )
                                    {
                                        if (interaction is Encounter)
                                        {
                                            Encounter encounter = (Encounter)interaction;
                                            if (encounter.forgotten == false)
                                            {
                                                relevantActivityCount.encounters++;
                                            }
                                        }
                                        else if (interaction is Greeting)
                                        {
                                            relevantActivityCount.greetings++;
                                        }
                                        else if (interaction is Conversation)
                                        {
                                            relevantActivityCount.conversations++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                {
                    int timeSliceIndex = 0;
                    foreach (string place in activityCountsByPlace.Keys)
                    {
                        foreach (ActivityCount activityCount in activityCountsByPlace[place])
                        {
                            //if (activityCount.trips > 0 || activityCount.encounters > 0 || activityCount.greetings > 0)
                            {
                                activityCounts.activities.Add(activityCount);
                            }
                            timeSliceIndex++;
                        }
                    }
                }
                using (StreamWriter streamWriter = new StreamWriter("ActivityCounts.json"))
                {
                    streamWriter.Write(JsonConvert.SerializeObject(activityCounts));
                }
            }
            #endregion
        }
        catch (Exception exception)
        {
            Console.WriteLine("Exception: " + exception);
            Debug.LogError("Exception: " + exception);
            ticket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Error, "Exception: " + exception)
            );
            atlasHandler.UpdateTicket(ticket);
        }
    }
}
