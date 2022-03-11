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
using UnityEngine;
using UnityEngine.AI;
using FLUID;
using FLUID_Simulator;
using System.Collections;

public class Agent //: MonoBehaviour
{
    public int id;

    //public Vector3 lastLocation;
    //public StructuredCausalModel structuredCausalModel;
    //public Dictionary<String, object> memory = new Dictionary<string, object>();

    public AgentType agentType;
    public Place suite;
    public double caution;

    //public double healthiness;
    public List<DayCalendar> calendar = new List<DayCalendar>();
    public Activity currentActivity;

    //    public IAction currentAction;
    public Dictionary<int, int> encountersByDay = new Dictionary<int, int>();
    public Dictionary<int, int> greetingsByDay = new Dictionary<int, int>();
    public Dictionary<int, int> conversationsByDay = new Dictionary<int, int>();
    public Dictionary<Agent, Anchor> otherAgentsInMind = new Dictionary<Agent, Anchor>();
    public Dictionary<Agent, float> otherAgentFamilarity = new Dictionary<Agent, float>();
    public Dictionary<Agent, Acquaintance> acquaintanceDictionary =
        new Dictionary<Agent, Acquaintance>();
    public Dictionary<Agent, Associate> associateDictionary = new Dictionary<Agent, Associate>();
    public Dictionary<Agent, List<IInteraction>> agentInteractionsDictionary =
        new Dictionary<Agent, List<IInteraction>>();
    public SortedDictionary<TimeSpan, UseCase> sortedUseCases =
        new SortedDictionary<TimeSpan, UseCase>();
    public List<BitArray> visibilityByDay = new List<BitArray>();
    public AgentProfile agentProfile { get; set; }

    TimeSpan singleDayDuration = TimeSpan.FromSeconds(24 * 60 * 60);
    TimeSpan timeStep = TimeSpan.FromSeconds(1);
    TimeSpan maximumConversationDuration = TimeSpan.FromSeconds(120);

    public DateTime AssignActivities(
        StructuredCausalModel structuredCausalModel,
        PathManager pathManager,
        DateTime lastHeartBeat,
        List<DayCalendar> calendar,
        int day
    )
    {
        // What second did the day start in the year?
        int dayStartSecond =
            (structuredCausalModel.startSecond + day * StructuredCausalModel.secondsPerDay)
            % StructuredCausalModel.secondsPerYear;
        DateTime dateOfSimulatedDay =
            structuredCausalModel.newYearMidnight
            + TimeSpan.FromSeconds(structuredCausalModel.startSecond)
            + TimeSpan.FromDays(day);
        int totalDays = (int)TimeSpan.FromTicks(dateOfSimulatedDay.Ticks).TotalDays;

        visibilityByDay.Add(new BitArray(StructuredCausalModel.secondsPerDay));

        // From longest duration use case to shortest..
        // Duration is used as a metric of priority.. longer tasks preempt shorter ones
        foreach (var entry in sortedUseCases.Reverse())
        {
            UseCase useCase = entry.Value;
            Dictionary<Place, Vector3[]> placesReachableOnFoot =
                pathManager.GetPlacesReachableOnFoot(suite, useCase);

            #region Where will this activity start?
            Place chosenStartPoint = null;
            if (
                useCase.startingComponentPropensityDictionary.ContainsKey(
                    FLUID.Component.defaultComponent
                )
            )
            {
                chosenStartPoint = suite;
            }
            else if (
                useCase.destinationComponentPropensityDictionary.ContainsKey(
                    FLUID.Component.defaultComponent
                )
            )
            {
                double totalLikelihood = 0;
                Dictionary<Place, double> startPointPropensities = new Dictionary<Place, double>();
                foreach (Place startPoint in placesReachableOnFoot.Keys)
                {
                    startPointPropensities[startPoint] =
                        useCase.startingComponentPropensityDictionary[startPoint.component];
                    totalLikelihood += startPointPropensities[startPoint];
                }

                // Then roll a die, and then march through each type's propensity until a type of affordace is selected
                string selectedAffordanceName = string.Empty;
                double sample = structuredCausalModel.random.NextDouble();
                double cumulativePropensity = 0;
                foreach (Place candidateStartPoint in startPointPropensities.Keys)
                {
                    cumulativePropensity +=
                        startPointPropensities[candidateStartPoint] / totalLikelihood;
                    if (sample < cumulativePropensity)
                    {
                        chosenStartPoint = candidateStartPoint;
                        break;
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
            #endregion

            #region Where will this activity dwell?
            Place chosenDwellPoint = null;
            if (
                useCase.destinationComponentPropensityDictionary.ContainsKey(
                    FLUID.Component.defaultComponent
                )
            )
            {
                chosenDwellPoint = suite;
            }
            else if (
                useCase.startingComponentPropensityDictionary.ContainsKey(
                    FLUID.Component.defaultComponent
                )
            )
            {
                double totalLikelihood = 0;
                Dictionary<Place, double> dwellPointPropensities = new Dictionary<Place, double>();
                foreach (Place dwellPoint in placesReachableOnFoot.Keys)
                {
                    if (useCase.destinationComponents.Contains(dwellPoint.component))
                    {
                        dwellPointPropensities[dwellPoint] =
                            useCase.destinationComponentPropensityDictionary[dwellPoint.component];
                        totalLikelihood += dwellPointPropensities[dwellPoint];
                    }
                }

                // Then roll a die, and then march through each type's propensity until a type of affordace is selected
                string selectedAffordanceName = string.Empty;
                double sample = structuredCausalModel.random.NextDouble();
                double cumulativePropensity = 0;
                foreach (Place candidateDwellPoint in dwellPointPropensities.Keys)
                {
                    cumulativePropensity +=
                        dwellPointPropensities[candidateDwellPoint] / totalLikelihood;
                    if (sample < cumulativePropensity)
                    {
                        chosenDwellPoint = candidateDwellPoint;
                        break;
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }
            #endregion

            // Loop through all the time periods the activity may occur in
            for (
                int typicalTimeIndex = 0;
                typicalTimeIndex < useCase.typicalTimes.Count;
                typicalTimeIndex++
            )
            {
                try
                {
                    TimeSpan typicalTime = useCase.typicalTimes[typicalTimeIndex];
                    double dayOffBasePropensity = useCase.propensityOnADayOff[typicalTimeIndex];
                    double workDayBasePropensity = useCase.propensityOnAWorkDay[typicalTimeIndex];

                    if (chosenStartPoint != null && chosenDwellPoint != null)
                    {
                        bool activityAdded = false;

                        #region Create a placeholder activity
                        Activity activity = new Activity();
                        activity.departing = chosenStartPoint;
                        activity.arriving = chosenDwellPoint;
                        activity.agent = this;
                        activity.useCase = useCase;
                        activity.speed = (float)(
                            agentType.velocityNorm
                            + agentType.velocityVariance
                                * (2 * structuredCausalModel.random.NextDouble() - 1)
                        );
                        #endregion

                        TimeSpan earliestTimeActivityCouldStart;
                        TimeSpan latestTimeActivityCouldStart;
                        int timeStepsReqired = 0;

                        List<Plan> enteringPlans = GeneratePlans(
                            structuredCausalModel,
                            pathManager,
                            chosenStartPoint,
                            chosenDwellPoint
                        );
                        List<Plan> exitingPlans = GeneratePlans(
                            structuredCausalModel,
                            pathManager,
                            chosenDwellPoint,
                            chosenStartPoint
                        );

                        // Create a list of exiting plans.
                        if (enteringPlans.Count > 0 && exitingPlans.Count > 0)
                        {
                            #region Select an entering plan..
                            Plan enteringPlan = null;
                            foreach (Plan candidatePlan in enteringPlans)
                            {
                                if (enteringPlan == null)
                                {
                                    enteringPlan = candidatePlan;
                                }
                                else if (
                                    candidatePlan.duration
                                    < enteringPlan.duration
                                        + TimeSpan.FromSeconds(
                                            10 * (structuredCausalModel.random.NextDouble() - 5)
                                        )
                                )
                                {
                                    enteringPlan = candidatePlan;
                                }
                            }
                            #endregion

                            #region Select an exiting plan..
                            Plan exitingPlan = null;
                            foreach (Plan candidatePlan in exitingPlans)
                            {
                                if (exitingPlan == null)
                                {
                                    exitingPlan = candidatePlan;
                                }
                                else if (
                                    candidatePlan.duration
                                    < exitingPlan.duration
                                        + TimeSpan.FromSeconds(
                                            10 * (structuredCausalModel.random.NextDouble() - 0.5)
                                        )
                                )
                                {
                                    exitingPlan = candidatePlan;
                                }
                            }
                            #endregion

                            #region Form a complete activity plan from the actions within the inbound, outbound and dwelling components
                            List<IAction> actions = new List<IAction>();
                            actions.AddRange(enteringPlan.actions);
                            if (chosenDwellPoint.component.visibility > 0)
                            {
                                actions.Add(
                                    new Motionless(
                                        activity.useCase.dwellTime,
                                        activity.arriving.BottomCenter,
                                        (float)chosenDwellPoint.component.visibility,
                                        structuredCausalModel.random
                                    )
                                );
                            }
                            else
                            {
                                actions.Add(
                                    new Absent(activity.departing.name, activity.useCase.dwellTime)
                                );
                            }
                            actions.AddRange(exitingPlan.actions);

                            activity.chosenPlan = new Plan(
                                $"{enteringPlan.label} <-> {exitingPlan.label}",
                                1.0,
                                actions
                            );
                            #endregion

                            #region Work out the range of possible times this activity could start that would allow the the activity to be completed before midnight.
                            timeStepsReqired = (int)(
                                (
                                    activity.chosenPlan.duration.TotalSeconds
                                    + maximumConversationDuration.TotalSeconds
                                ) / timeStep.TotalSeconds
                            );

                            // Set the window to be the "no conflict" optimisitic value
                            earliestTimeActivityCouldStart = typicalTime - useCase.hourDistribution;
                            latestTimeActivityCouldStart = typicalTime + useCase.hourDistribution;

                            //// If the acivity ends after midnight, just lop of the duration to make it "fit".
                            //latestTimeActivityCouldStart = singleDayDuration - activity.chosenPlan.duration - timeStep;
                            //if (latestTimeActivityCouldStart + activity.chosenPlan.duration < latestTimeActivityCouldStart)
                            //{
                            //    latestTimeActivityCouldStart = singleDayDuration - activity.chosenPlan.duration - timeStep;
                            //}
                            #endregion

                            // Establish the start and end times from the parent use case activity.
                            // Look through the available time slots and collect start / end times where the child activity could be placed.
                            // Randomly pick one adjusting for longer gaps having a higher probability that shorter ones.
                            // If a valid time is found, place the new activity in the the calendar and mask it out in the bit mask.
                            // Rinse and repeat.
                            // The logic needs to be "invertable" so it works for both Clearing parents / Blocking children works and
                            // the reverse Blocking parent / Clearing children also works.

                            if (useCase.isRootUseCase)
                            {
                                if (
                                    LikelihoodOfOccurence(
                                        totalDays,
                                        useCase,
                                        dayOffBasePropensity,
                                        workDayBasePropensity,
                                        (int)useCase.dwellTime.TotalSeconds
                                    ) > structuredCausalModel.random.NextDouble()
                                )
                                {
                                    activity.startTime =
                                        earliestTimeActivityCouldStart
                                        + TimeSpan.FromSeconds(
                                            2
                                                * useCase.hourDistribution.TotalSeconds
                                                * structuredCausalModel.random.NextDouble()
                                        );

                                    PlaceClearingActivityInCalendar(
                                        calendar,
                                        day,
                                        activity,
                                        activity.startTime
                                    );
                                }
                                else
                                {
                                    // No activities on this day
                                }
                            }
                            else
                            {
                                #region Now try to "fit" the activity into the agent's calendar that may already contain conflicting activities.
                                TimeSpan lastTransitionToAvailable;
                                int runLength = 0;
                                lastTransitionToAvailable = earliestTimeActivityCouldStart;

                                Vector3[] pathFromAToB = pathManager.GetPath(
                                    chosenStartPoint,
                                    chosenDwellPoint,
                                    false,
                                    false
                                );
                                if (pathFromAToB != null)
                                {
                                    // Scan through the start times available for this activity..
                                    bool first = true;
                                    for (
                                        TimeSpan timeOfDay =
                                            earliestTimeActivityCouldStart + timeStep;
                                        timeOfDay
                                            < latestTimeActivityCouldStart
                                                + activity.chosenPlan.duration;
                                        timeOfDay += timeStep
                                    )
                                    {
                                        try
                                        {
                                            #region Test and record if this time step represents the transition from unavailable to available
                                            int bitMaskIndex = (int)(
                                                timeOfDay.TotalSeconds / timeStep.TotalSeconds
                                            );
                                            switch (useCase.opportunityFunction)
                                            {
                                                case OpportunityFunction.BlockCalendar:
                                                    // Detect transitions from true (booked) to false (available) in the bitmask
                                                    if (
                                                        (
                                                            first
                                                            || calendar[day].busy[bitMaskIndex - 1]
                                                                == true
                                                        ) // Was either not busy or just finished something
                                                        && calendar[day].busy[bitMaskIndex] == false
                                                    ) // Not busy now
                                                    {
                                                        #region Assert that the runlength is correctly set to 0 as the previous time step was "busy".
                                                        if (runLength != 0)
                                                        {
                                                            // This can now occur due to weather
                                                            //throw new Exception("Logical flaw.");
                                                        }
                                                        #endregion

                                                        // Record this timestep as being the start of a block that has availability
                                                        lastTransitionToAvailable = timeOfDay;
                                                    }
                                                    break;

                                                case OpportunityFunction.ClearCalendar:

                                                    // Detect transitions from false (available) to true (busy) in the bitmask
                                                    {
                                                        #region Assert that the runlength is correctly set to 0 as the previous time step was "busy".
                                                        if (runLength != 0)
                                                        {
                                                            throw new Exception("Logical flaw.");
                                                        }
                                                        #endregion

                                                        // Record this timestep as being the start of a block that has availability
                                                        lastTransitionToAvailable = timeOfDay;
                                                    }
                                                    break;
                                            }
                                            #endregion

                                            #region Test if this is this time step represents the transition from unavailable to available
                                            // Detect transitions from false (available) to true (booked) in the bitmask
                                            bool transitionToUnavailable = (
                                                calendar[day].busy[bitMaskIndex - 1] == false // The previous bit was off
                                                && calendar[day].busy[bitMaskIndex] == true
                                            ); // This bit is on
                                            #endregion

                                            #region Consider the effect of weather
                                            switch (chosenDwellPoint.component.socialSyntax)
                                            {
                                                case SocialSyntaxEnum.Mandatory:
                                                case SocialSyntaxEnum.Suite:
                                                    break;

                                                case SocialSyntaxEnum.Optional:

                                                    {
                                                        int hour = (int)(
                                                            (
                                                                dayStartSecond
                                                                + timeOfDay.TotalSeconds
                                                            ) / StructuredCausalModel.secondsPerHour
                                                        );
                                                        bool isHotCold =
                                                            structuredCausalModel.weatherByLocation[
                                                                structuredCausalModel
                                                                    .simulationTicket
                                                                    .Location
                                                            ].isHotColdByHour[hour];
                                                        bool isRaining =
                                                            structuredCausalModel.weatherByLocation[
                                                                structuredCausalModel
                                                                    .simulationTicket
                                                                    .Location
                                                            ].isRainingByHour[hour];
                                                        switch (chosenDwellPoint.exposure)
                                                        {
                                                            case ExposureEnum.Inside:
                                                                break;

                                                            case ExposureEnum.Undercover:
                                                                transitionToUnavailable = isHotCold;
                                                                break;

                                                            case ExposureEnum.Outside:
                                                                transitionToUnavailable =
                                                                    isHotCold || isRaining;
                                                                if (transitionToUnavailable) { }
                                                                break;
                                                        }
                                                    }
                                                    break;
                                            }
                                            #endregion

                                            if (transitionToUnavailable)
                                            {
                                                // Do we have enough time to perform the activity in this block of time?
                                                if (runLength >= timeStepsReqired)
                                                {
                                                    // We have enough time to do this activity.

                                                    // We now roll the die to get a sample to see if the agent will perform this activity.
                                                    if (
                                                        LikelihoodOfOccurence(
                                                            totalDays,
                                                            useCase,
                                                            dayOffBasePropensity,
                                                            workDayBasePropensity,
                                                            runLength
                                                        )
                                                        > structuredCausalModel.random.NextDouble()
                                                    )
                                                    {
                                                        // Success! The agent can and will perform this activity.

                                                        TimeSpan startTime =
                                                            lastTransitionToAvailable
                                                            + TimeSpan.FromSeconds(
                                                                (runLength - timeStepsReqired)
                                                                    * structuredCausalModel.random.NextDouble()
                                                            );

                                                        #region Build a new activity based on the weather of the start time.
                                                        #region Get the weather appropriate set start -> dwell paths
                                                        string outboundElevatorBank = string.Empty;
                                                        string inboundElevatorBank = string.Empty;
                                                        if (
                                                            activity.chosenPlan.actions[2]
                                                            is InElevator
                                                        )
                                                        {
                                                            outboundElevatorBank = (
                                                                (InElevator)activity
                                                                    .chosenPlan
                                                                    .actions[2]
                                                            ).bank;
                                                        }

                                                        if (
                                                            activity.chosenPlan.actions[
                                                                activity.chosenPlan.actions.Count
                                                                    - 2
                                                            ] is InElevator
                                                        )
                                                        {
                                                            inboundElevatorBank = (
                                                                (InElevator)activity
                                                                    .chosenPlan
                                                                    .actions[
                                                                    activity
                                                                        .chosenPlan
                                                                        .actions
                                                                        .Count - 2
                                                                ]
                                                            ).bank;
                                                        }

                                                        int hour = (int)(
                                                            (
                                                                dayStartSecond
                                                                + timeOfDay.TotalSeconds
                                                            ) / StructuredCausalModel.secondsPerHour
                                                        );
                                                        bool isHotCold =
                                                            structuredCausalModel.weatherByLocation[
                                                                structuredCausalModel
                                                                    .simulationTicket
                                                                    .Location
                                                            ].isHotColdByHour[hour];
                                                        bool isRaining =
                                                            structuredCausalModel.weatherByLocation[
                                                                structuredCausalModel
                                                                    .simulationTicket
                                                                    .Location
                                                            ].isRainingByHour[hour];

                                                        #endregion

                                                        if (
                                                            string.IsNullOrEmpty(
                                                                outboundElevatorBank
                                                            )
                                                        )
                                                        {
                                                            Vector3[] pathFromStartPointToDwellPoint =
                                                                pathManager.GetPath(
                                                                    chosenStartPoint,
                                                                    chosenDwellPoint,
                                                                    isHotCold,
                                                                    isRaining
                                                                );
                                                            (
                                                                (Journey)activity
                                                                    .chosenPlan
                                                                    .actions[0]
                                                            ).Path = pathFromStartPointToDwellPoint;
                                                        }
                                                        else
                                                        {
                                                            Vector3[] pathFromStartPointToNearestElevator =
                                                                pathManager.GetPathToClosestElevator(
                                                                    chosenStartPoint,
                                                                    outboundElevatorBank,
                                                                    isHotCold,
                                                                    isRaining
                                                                );
                                                            Vector3[] pathFromNearestElevatorBankToDwellPoint =
                                                                pathManager.GetPathToClosestElevator(
                                                                    chosenStartPoint,
                                                                    outboundElevatorBank,
                                                                    isHotCold,
                                                                    isRaining,
                                                                    reverse: true
                                                                );
                                                            (
                                                                (Journey)activity
                                                                    .chosenPlan
                                                                    .actions[0]
                                                            ).Path =
                                                                pathFromStartPointToNearestElevator;
                                                            (
                                                                (Journey)activity
                                                                    .chosenPlan
                                                                    .actions[0]
                                                            ).Path =
                                                                pathFromNearestElevatorBankToDwellPoint;
                                                        }

                                                        if (
                                                            string.IsNullOrEmpty(
                                                                inboundElevatorBank
                                                            )
                                                        )
                                                        {
                                                            try
                                                            {
                                                                Vector3[] pathFromStartPointToDwellPoint =
                                                                    pathManager.GetPath(
                                                                        chosenStartPoint,
                                                                        chosenDwellPoint,
                                                                        isHotCold,
                                                                        isRaining
                                                                    );
                                                                (
                                                                    (Journey)activity
                                                                        .chosenPlan
                                                                        .actions[
                                                                        activity
                                                                            .chosenPlan
                                                                            .actions
                                                                            .Count - 1
                                                                    ]
                                                                ).Path = Reverse(
                                                                    pathFromStartPointToDwellPoint
                                                                );
                                                            }
                                                            catch (Exception a) { }
                                                        }
                                                        else
                                                        {
                                                            // Walk to elevator : Wait : Ride elevator : Walk to dwelling
                                                            Vector3[] pathFromStartPointToNearestElevator =
                                                                pathManager.GetPathToClosestElevator(
                                                                    chosenStartPoint,
                                                                    inboundElevatorBank,
                                                                    isHotCold,
                                                                    isRaining
                                                                );
                                                            Vector3[] pathFromNearestElevatorBankToDwellPoint =
                                                                pathManager.GetPathToClosestElevator(
                                                                    chosenStartPoint,
                                                                    inboundElevatorBank,
                                                                    isHotCold,
                                                                    isRaining,
                                                                    reverse: true
                                                                );
                                                            (
                                                                (Journey)activity
                                                                    .chosenPlan
                                                                    .actions[
                                                                    activity
                                                                        .chosenPlan
                                                                        .actions
                                                                        .Count - 4
                                                                ]
                                                            ).Path = Reverse(
                                                                pathFromNearestElevatorBankToDwellPoint
                                                            );
                                                            (
                                                                (Journey)activity
                                                                    .chosenPlan
                                                                    .actions[
                                                                    activity
                                                                        .chosenPlan
                                                                        .actions
                                                                        .Count - 1
                                                                ]
                                                            ).Path = Reverse(
                                                                pathFromStartPointToNearestElevator
                                                            );
                                                        }
                                                        #endregion

                                                        PlaceBlockingActivityInCalendar(
                                                            calendar,
                                                            day,
                                                            activity,
                                                            startTime
                                                        );
                                                        activityAdded = true;
                                                        break;
                                                    }
                                                }
                                                runLength = 0;
                                            }
                                            else if (calendar[day].busy[bitMaskIndex] == false)
                                            {
                                                runLength++;
                                            }

                                            if (
                                                DateTime.Now - lastHeartBeat
                                                > TimeSpan.FromSeconds(60)
                                            )
                                            {
                                                GC.Collect();
                                                structuredCausalModel.atlasHandler.UpdateTimeStamp();
                                                lastHeartBeat = DateTime.Now;
                                                // Debug.Log("Calendar generation is taking a long time");
                                            }
                                        }
                                        catch (Exception e) { }
                                        first = false;
                                    }
                                }

                                if (activityAdded == false)
                                {
                                    // The loop completed and may have a risidual time slot a long enough for the activity.
                                    if (runLength >= timeStepsReqired)
                                    {
                                        // We have enough time to do this activity.

                                        // We now roll the die to get a sample to see if the agent will perform this activity.
                                        if (
                                            LikelihoodOfOccurence(
                                                totalDays,
                                                useCase,
                                                dayOffBasePropensity,
                                                workDayBasePropensity,
                                                runLength
                                            ) > structuredCausalModel.random.NextDouble()
                                        )
                                        {
                                            // Success! The agent can and will perform this activity.

                                            TimeSpan startTime =
                                                lastTransitionToAvailable
                                                + TimeSpan.FromSeconds(
                                                    (runLength - timeStepsReqired)
                                                        * structuredCausalModel.random.NextDouble()
                                                );

                                            PlaceBlockingActivityInCalendar(
                                                calendar,
                                                day,
                                                activity,
                                                startTime
                                            );
                                        }
                                    }
                                }
                                #endregion
                            }
                        }
                    }
                    else
                    {
                        // Nothing to do here.
                        // Debug.Log("Use case " + useCase.name + " could not find affordance");
                    }

                    if (DateTime.Now - lastHeartBeat > TimeSpan.FromMinutes(1))
                    {
                        structuredCausalModel.atlasHandler.UpdateTimeStamp();
                        lastHeartBeat = DateTime.Now;
                        GC.Collect();
                    }
                }
                catch (Exception e) { }
            }
        }

        return lastHeartBeat;
    }

    private void PlaceBlockingActivityInCalendar(
        List<DayCalendar> calendar,
        int day,
        Activity activity,
        TimeSpan startTime
    )
    {
        activity.startTime = startTime;

        foreach (IAction action in activity.chosenPlan.actions)
        {
            for (
                TimeSpan time = action.startTime;
                time < action.startTime + action.duration;
                time += timeStep
            )
            {
                int seconds = (int)time.TotalSeconds;
                calendar[day].busy[seconds] = true;
                calendar[day].sociable[seconds] = action is Absent == false;
                visibilityByDay[day].Set(seconds, action.isActiveAt(time));
            }
        }

        calendar[day].activities.Add(activity);
    }

    private void PlaceClearingActivityInCalendar(
        List<DayCalendar> calendar,
        int day,
        Activity activity,
        TimeSpan startTime
    )
    {
        activity.startTime = startTime;

        foreach (IAction action in activity.chosenPlan.actions)
        {
            for (
                TimeSpan time = action.startTime;
                time < action.startTime + action.duration;
                time += timeStep
            )
            {
                int seconds = (int)time.TotalSeconds;
                bool dormant = action is Absent;
                calendar[day].busy[(int)time.TotalSeconds] = !dormant;
                calendar[day].sociable[(int)time.TotalSeconds] = !dormant;
                visibilityByDay[day].Set(seconds, action.isActiveAt(time) && !dormant);
            }
        }

        calendar[day].activities.Add(activity);
    }

    private double LikelihoodOfOccurence(
        int day,
        UseCase useCase,
        double dayOffBasePropensity,
        double workDayBasePropensity,
        int runLength
    )
    {
        #region What is the likelihood of this activity occuring?
        // Now the tricky question of mapping the propensity correctly into the run length available.
        // Consider that there is a uniform sample distribution across the entire window.
        // If the run length only goes for half the window then the likelihood of the activity in this time perioud should be halved.
        // We can achieve this by dividing the runLength by the full window size.

        double likelihood;

        // Is it a workday?
        if (day % 7 <= 4)
        {
            // Its a workday, so the acitivity's propensity comes from the workDayBasePropensity hyperparameter
            likelihood = workDayBasePropensity; //* runLength * timeStep.TotalSeconds / useCase.hourDistribution.TotalSeconds; // Weekday
        }
        else
        {
            // Its a day off, so the acitivity's propensity comes from the dayOffBasePropensity hyperparameter
            likelihood = dayOffBasePropensity; // * runLength * timeStep.TotalSeconds / useCase.hourDistribution.TotalSeconds; // Weekend
        }
        #endregion
        return likelihood;
    }

    public List<Plan> GeneratePlans(
        StructuredCausalModel structuredCausalModel,
        PathManager pathManager,
        Place from,
        Place to
    )
    //Dictionary<string,Vector3[]> pathFromAToNearestElevatorBankDictionary,
    //Dictionary<string, Vector3[]> pathFromNearestElevatorBankToBDictionary,
    //Vector3[] pathFromAToB)
    {
        List<Plan> plans = new List<Plan>();

        if (from == to)
        {
            // The agent is already there..
            Journey nullJourney = new Journey(new Vector3[] { }, agentType.velocityNorm);
            Plan nullPlan = new Plan("Null Path", 1.0, new List<IAction>() { nullJourney });
            plans.Add(nullPlan);

            return plans;
        }

        Vector3[] pathFromAToB = pathManager.GetPath(from, to, false, false);

        // 1. Shortest path
        if (pathFromAToB != null)
        {
            Journey shortestJourney = new Journey(pathFromAToB, agentType.velocityNorm);
            Plan shortestPathPlan = new Plan(
                "Shortest Path",
                1.0,
                new List<IAction>() { shortestJourney }
            );
            plans.Add(shortestPathPlan);
        }

        // 2. Elevator mediated shortest path
        foreach (string bank in structuredCausalModel.elevatorBankDictionary.Keys)
        {
            Vector3[] pathToNearestElevator = pathManager.GetPathToClosestElevator(
                from,
                bank,
                false,
                false
            );

            if (pathToNearestElevator != null)
            {
                Journey journeyToNearestElevator = new Journey(
                    pathToNearestElevator,
                    agentType.velocityNorm
                );
                if (journeyToNearestElevator != null)
                {
                    Vector3[] pathFromNearestElevator = pathManager.GetPathToClosestElevator(
                        to,
                        bank,
                        false,
                        false
                    );
                    if (pathFromNearestElevator != null)
                    {
                        Journey journeyFromElevator = new Journey(
                            pathFromNearestElevator,
                            agentType.velocityNorm
                        );
                        if (journeyFromElevator != null)
                        {
                            // How long should we wait for the elevator?
                            // Given the elevator speed, lets get a rough sense.
                            double maximumAcceleration = 0.5; // meters per second per second
                            TimeSpan doorCycleTime = TimeSpan.FromSeconds(10);
                            Vector3 fromA = pathToNearestElevator[pathToNearestElevator.Length - 1];
                            Vector3 toB = pathFromNearestElevator[
                                pathFromNearestElevator.Length - 1
                            ];
                            double midpointVelocity = Math.Sqrt(
                                Math.Abs(maximumAcceleration * (toB.y - fromA.y))
                            );
                            double secondsToMidPoint = midpointVelocity / maximumAcceleration;
                            TimeSpan timeInElevatorDuration =
                                TimeSpan.FromSeconds(2 * secondsToMidPoint) + doorCycleTime;
                            TimeSpan waitForElevatorDuration =
                                TimeSpan.FromSeconds(15 * structuredCausalModel.random.NextDouble())
                                + doorCycleTime;

                            Motionless waitForElevator = new Motionless(
                                waitForElevatorDuration,
                                pathToNearestElevator[pathToNearestElevator.Length - 1],
                                1,
                                structuredCausalModel.random
                            );
                            InElevator inElevator = new InElevator(bank, 0, timeInElevatorDuration);
                            List<IAction> actions = new List<IAction>()
                            {
                                journeyToNearestElevator,
                                waitForElevator,
                                inElevator,
                                journeyFromElevator
                            };
                            Plan elevatorPlan = new Plan("Elevator", 1.0, actions);
                            plans.Add(elevatorPlan);
                        }
                    }
                }
            }
        }
        // 3. Magnetism mediated shortest path
        // 4. Elevator and magnetism mediated shortest path
        // 5. Chained magnets

        return plans;
    }

    public Vector3[] Reverse(Vector3[] path)
    {
        Vector3[] result = new Vector3[path.Length];
        Array.Copy(path, result, path.Length);
        Array.Reverse(result);
        return result;
    }
}
