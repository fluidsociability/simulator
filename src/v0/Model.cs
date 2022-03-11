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

using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FLUID
{
    public enum SocialSyntaxEnum
    {
        Circulation,
        Mandatory,
        Optional,
        SemiPrivate,
        Suite
    }

    public class Component
    {
        public string name { get; private set; }

        //Brief Description
        //Building Usage types it could be in
        public SocialSyntaxEnum socialSyntax { get; private set; }
        public int preferedDestinationMaximum { get; private set; }
        public double visibility { get; private set; }
        public double baseOccupancy { get; private set; }
        public double variance { get; private set; }
        public double vacancyRate { get; private set; }
        public double agentOpenness { get; private set; }

        public Component(string line)
        {
            string[] lexemes = line.Split('\t');
            name = lexemes[0];
            switch (lexemes[3])
            {
                case "Circulation":
                    socialSyntax = SocialSyntaxEnum.Circulation;
                    break;

                case "Mandatory Destination":
                    socialSyntax = SocialSyntaxEnum.Mandatory;
                    break;

                case "Optional Destination":
                    socialSyntax = SocialSyntaxEnum.Optional;
                    break;

                case "Suite":
                    socialSyntax = SocialSyntaxEnum.Suite;
                    break;

                case "Semi-private Space":
                    socialSyntax = SocialSyntaxEnum.SemiPrivate;
                    break;

                default:
                    throw new Exception($"Bad social syntax '{lexemes[3]}");
            }
            preferedDestinationMaximum = int.Parse(lexemes[4]);
            visibility = double.Parse(lexemes[5].Substring(0, lexemes[5].IndexOf('%'))) / 100;
            baseOccupancy = double.Parse(lexemes[6]);
            variance = double.Parse(lexemes[7]);
            vacancyRate = double.Parse(lexemes[8].Substring(0, lexemes[8].IndexOf('%'))) / 100;
            agentOpenness = double.Parse(lexemes[9].Substring(0, lexemes[9].IndexOf('%'))) / 100;
        }

        private Component()
        {
            // The default constructore
            name = "Default";
            socialSyntax = SocialSyntaxEnum.Suite;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} : {name}";
        }

        static private Component _defaultComponent = null;
        static public Component defaultComponent
        {
            get
            {
                if (_defaultComponent == null)
                {
                    _defaultComponent = new Component();
                }
                return _defaultComponent;
            }
        }
    }

    public class AgentType
    {
        public string name { get; private set; }
        public string description { get; private set; }
        public string usage { get; private set; }
        public double velocityNorm { get; private set; }
        public double velocityVariance { get; private set; }
        public double cautionBaseline { get; private set; }
        public double cautionVariance { get; private set; }
        public double openness { get; private set; }
        public double opennessVariance { get; private set; }
        public IList<UseCase> useCases { get; private set; }

        public AgentType(string line)
        {
            string[] lexemes = line.Split('\t');

            name = lexemes[0];
            description = lexemes[1];
            usage = lexemes[2];
            velocityNorm = double.Parse(lexemes[3].Substring(0, lexemes[3].IndexOf('%'))) / 100;
            velocityVariance = double.Parse(lexemes[4].Substring(0, lexemes[4].IndexOf('%'))) / 100;
            cautionBaseline = float.Parse(lexemes[5].Substring(0, lexemes[5].IndexOf('%'))) / 100;
            cautionVariance = float.Parse(lexemes[6].Substring(0, lexemes[6].IndexOf('%'))) / 100;
            openness = float.Parse(lexemes[7].Substring(0, lexemes[7].IndexOf('%'))) / 100;
            opennessVariance = float.Parse(lexemes[8].Substring(0, lexemes[8].IndexOf('%'))) / 100;
        }

        public void BindUseCases(IList<UseCase> allUseCases)
        {
            useCases = new List<UseCase>();
            foreach (UseCase useCase in allUseCases)
            {
                if (useCase.applicableAgentTypes.Contains(this))
                {
                    useCases.Add(useCase);
                }
            }
        }

        public override string ToString()
        {
            return $"{this.GetType().Name} : {name}";
        }
    }

    public class UnitOccupancy
    {
        public int population { get; private set; }
        public IList<AgentType> agentMix { get; private set; }
        public bool isFamily { get; private set; }
        public double propensity { get; private set; }

        public UnitOccupancy(string line, IList<AgentType> agentTypes)
        {
            string[] lexemes = line.Split('\t');

            population = int.Parse(lexemes[0]);
            agentMix = new List<AgentType>();
            foreach (string agentTypeName in lexemes[1].Split('|'))
            {
                bool found = false;
                foreach (AgentType agentType in agentTypes)
                {
                    if (agentTypeName.Trim() == agentType.name)
                    {
                        agentMix.Add(agentType);
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    Debug.Break();
                    throw new Exception("Could not find agent type: " + agentTypeName);
                }
            }
            if (population != agentMix.Count)
            {
                Debug.Break();
                throw new Exception(
                    "Did find " + population + " agent types in \"" + lexemes[1] + "\""
                );
            }
            isFamily = lexemes[2] == "Yes";
            propensity = double.Parse(lexemes[3].Substring(0, lexemes[3].IndexOf('%'))) / 100;
        }
    }

    public enum OpportunityFunction
    {
        ClearCalendar,
        BlockCalendar
    }

    public class UseCase
    {
        public string name { get; private set; }
        public IList<AgentType> applicableAgentTypes { get; private set; }
        public string description { get; private set; }
        public IList<Component> startingPointComponents { get; private set; }
        public List<double> startingPointComponentPropensities { get; private set; }
        public IList<Component> destinationComponents { get; private set; }
        public IList<double> destinationComponentPropensities { get; private set; }
        public bool grouping { get; private set; }
        public IList<UseCase> parentUseCases { get; private set; }
        public TimeSpan dwellTime { get; private set; }
        public bool shouldReturn { get; private set; }
        public OpportunityFunction opportunityFunction { get; private set; }
        public IList<TimeSpan> typicalTimes { get; private set; }
        public TimeSpan hourDistribution { get; private set; }
        public IList<double> propensityOnADayOff { get; private set; }
        public IList<double> propensityOnAWorkDay { get; private set; }

        public bool isRootUseCase { get; set; }

        // public List<Place> dwellPoints { get; private set; }
        public Dictionary<Component, double> startingComponentPropensityDictionary
        {
            get;
            private set;
        }
        public Dictionary<Component, double> destinationComponentPropensityDictionary
        {
            get;
            private set;
        }

        private string[] parentUseCaseNames;

        //public bool startsAtDefault { get; private set; }
        //public bool dwellsAtDefault { get; private set; }

        public UseCase(string line, IList<Component> components, IList<AgentType> agentTypes)
        {
            const int agentUseCaseColumnIndex = 0; // Agent Use case
            const int applicableAgentTypesColumnIndex = 1; // Applicable Agent types
            const int briefDescriptionColumnIndex = 2; // Brief description
            const int startingComponentsColumnIndex = 3; // Start Position
            const int startingComponentPropensitiesColumnIndex = 4; // Start Propensity
            const int destinationTypesColumnIndex = 5; // Destination
            const int destinationPropensitiesColumnIndex = 6; // Destination Propensity
            const int groupingColumnIndex = 7; // Grouping?
            const int parentUseCasesColumnIndex = 8; // Parent Use Cases
            const int dwellTimeColumnIndex = 9; // Dwell time (hh:mm)
            const int returnColumnIndex = 10; // Return?
            const int opportunityFunctionColumnIndex = 11; // Opportunity Function
            const int typicalTimeColumnIndex = 12; // Typical Time
            const int hourDistributionColumnIndex = 13; // Hour distribution
            const int propensityOnADayOffColumnIndex = 14; // Propensity on a Day Off
            const int propensityOnAWorkColumnIndex = 15; // Propensity on a Work Day

            string[] lexemes = line.Split('\t');

            name = lexemes[agentUseCaseColumnIndex].Trim();
            applicableAgentTypes = new List<AgentType>();
            foreach (
                string applicableAgentTypeName in lexemes[applicableAgentTypesColumnIndex].Split(
                    '|'
                )
            )
            {
                foreach (AgentType agentType in agentTypes)
                {
                    if (applicableAgentTypeName.Trim() == agentType.name)
                    {
                        applicableAgentTypes.Add(agentType);
                        break;
                    }
                }
            }

            description = lexemes[briefDescriptionColumnIndex].Trim();

            startingPointComponents = new List<Component>();
            foreach (string startPositionType in lexemes[startingComponentsColumnIndex].Split('|'))
            {
                bool found = false;
                foreach (Component component in components)
                {
                    if (startPositionType.Trim() == component.name)
                    {
                        startingPointComponents.Add(component);
                        found = true;
                        break;
                    }
                }
                if (found == false)
                    throw new Exception(
                        $"Could not find a component with the name '{startPositionType}'"
                    );
            }

            try
            {
                startingPointComponentPropensities = new List<double>();
                foreach (
                    string startPositionPropensity in lexemes[
                        startingComponentPropensitiesColumnIndex
                    ].Split('|')
                )
                {
                    startingPointComponentPropensities.Add(
                        double.Parse(
                            startPositionPropensity
                                .Substring(0, startPositionPropensity.IndexOf('%'))
                                .Trim()
                        ) / 100
                    );
                }
                if (startingPointComponents.Count != startingPointComponentPropensities.Count)
                    throw new Exception(
                        $"Mismatch between starting component and propensity counts for use case '{name}'"
                    );
            }
            catch (Exception e) { }

            startingComponentPropensityDictionary = new Dictionary<Component, double>();
            for (int index = 0; index < startingPointComponents.Count; index++)
            {
                startingComponentPropensityDictionary[startingPointComponents[index]] =
                    startingPointComponentPropensities[index];
            }

            destinationComponents = new List<Component>();
            foreach (string destinationType in lexemes[destinationTypesColumnIndex].Split('|'))
            {
                bool found = false;
                foreach (Component component in components)
                {
                    if (destinationType.Trim() == component.name)
                    {
                        destinationComponents.Add(component);
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    throw new Exception(
                        $"Could not find a component with the name '{destinationType}'"
                    );
                }
            }

            destinationComponentPropensities = new List<double>();
            foreach (
                string destinationPropensity in lexemes[destinationPropensitiesColumnIndex].Split(
                    '|'
                )
            )
            {
                destinationComponentPropensities.Add(
                    double.Parse(
                        destinationPropensity
                            .Substring(0, destinationPropensity.IndexOf('%'))
                            .Trim()
                    ) / 100
                );
            }

            destinationComponentPropensityDictionary = new Dictionary<Component, double>();
            for (int index = 0; index < destinationComponents.Count; index++)
            {
                destinationComponentPropensityDictionary[destinationComponents[index]] =
                    destinationComponentPropensities[index];
            }

            grouping = lexemes[groupingColumnIndex] == "Yes";

            parentUseCaseNames = lexemes[parentUseCasesColumnIndex].Split('|');

            dwellTime = TimeSpan.Parse(lexemes[dwellTimeColumnIndex]);

            shouldReturn = lexemes[returnColumnIndex] == "Yes";

            switch (lexemes[opportunityFunctionColumnIndex].Trim())
            {
                case "Clear Calendar":
                    opportunityFunction = OpportunityFunction.ClearCalendar;
                    break;

                case "Block Calendar":
                    opportunityFunction = OpportunityFunction.BlockCalendar;
                    break;

                default:
                    Debug.Break();
                    throw new Exception(
                        "Could not parse \""
                            + lexemes[opportunityFunctionColumnIndex]
                            + "\" as a Opportunity"
                    );
            }

            typicalTimes = new List<TimeSpan>();
            foreach (string hourMinute in lexemes[typicalTimeColumnIndex].Split('|'))
            {
                typicalTimes.Add(TimeSpan.Parse(hourMinute.Trim()));
            }
            hourDistribution = TimeSpan.Parse(lexemes[hourDistributionColumnIndex]);

            propensityOnADayOff = new List<double>();
            foreach (string propensityString in lexemes[propensityOnADayOffColumnIndex].Split('|'))
            {
                propensityOnADayOff.Add(
                    double.Parse(
                        propensityString.Substring(0, propensityString.IndexOf('%')).Trim()
                    ) / 100
                );
            }

            propensityOnAWorkDay = new List<double>();
            foreach (string propensityString in lexemes[propensityOnAWorkColumnIndex].Split('|'))
            {
                propensityOnAWorkDay.Add(
                    double.Parse(
                        propensityString.Substring(0, propensityString.IndexOf('%')).Trim()
                    ) / 100
                );
            }

            if (
                typicalTimes.Count != propensityOnADayOff.Count
                || typicalTimes.Count != propensityOnAWorkDay.Count
            )
            {
                throw new Exception(
                    $"Use Case: {name} does not have the same number of typical times and work day/day off propensities"
                );
            }

            // dwellPoints = new List<Place>();
        }

        public void ResolveParentUseCases(IList<UseCase> useCases)
        {
            parentUseCases = new List<UseCase>();
            foreach (string parentUseCaseName in parentUseCaseNames)
            {
                foreach (UseCase useCase in useCases)
                {
                    if (
                        parentUseCaseName.Trim() == useCase.name
                        && parentUseCases.Contains(useCase) == false
                    )
                    {
                        parentUseCases.Add(useCase);
                        break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return "Usecase name:"
                + name
                + ", "
                + typicalTimes
                + ", dwellTime = "
                + dwellTime
                + ", "
                + hourDistribution;
        }
    }

    public class Plan
    {
        public Plan(string label, double propensity, List<IAction> actions)
        {
            this.label = label;
            this.propensity = propensity;
            this.actions = actions;
        }

        public string label { get; private set; }

        public double propensity { get; set; }

        public TimeSpan duration
        {
            get
            {
                TimeSpan result = TimeSpan.Zero;
                foreach (IAction action in actions)
                {
                    result += action.duration;
                }
                return result;
            }
        }

        public List<IAction> actions { get; private set; }

        public IAction GetActionAtTime(TimeSpan timeOfDay)
        {
            IAction result = null;
            foreach (IAction action in actions)
            {
                if (action.isActiveAt(timeOfDay))
                {
                    result = action;
                    break;
                }
            }
            return result;
        }
    }

    public class Activity
    {
        public Agent agent;
        public UseCase useCase;
        public Place departing;
        public Place arriving;

        public bool allocated;
        public Plan chosenPlan = null;
        public float speed;
        public IList<Activity> childActivities = new List<Activity>();
        TimeSpan _startTime;

        public TimeSpan startTime
        {
            get { return _startTime; }
            set
            {
                _startTime = value;
                TimeSpan cumulativeStartingTime = _startTime;
                foreach (IAction action in chosenPlan.actions)
                {
                    action.startTime = cumulativeStartingTime;
                    cumulativeStartingTime += action.duration;
                }
            }
        }
        public TimeSpan finishTime
        {
            get
            {
                IAction lastAction = chosenPlan.actions[chosenPlan.actions.Count - 1];
                return lastAction.startTime + lastAction.duration;
            }
        }

        public override string ToString()
        {
            string result = $"Activity: Usecase: {useCase.name}";
            TimeSpan time = startTime;
            if (chosenPlan.actions != null)
            {
                foreach (IAction action in chosenPlan.actions)
                {
                    if (action.duration.TotalSeconds > 0)
                    {
                        result += $" Action: {action.GetType().Name}({action.duration})";
                    }
                }
            }
            return result;
        }

        public static float CalculateDistance(Vector3[] path)
        {
            float distance = 0;
            // Calculate the distance along the path
            for (int index = 1; index < path.Length; index++)
            {
                distance += (path[index] - path[index - 1]).magnitude;
            }

            return distance;
        }
    }

    public class DayCalendar
    {
        public List<Activity> activities = new List<Activity>();
        public BitArray sociable = new BitArray(24 * 60 * 60, false);
        public BitArray busy = new BitArray(24 * 60 * 60, true);
    }

    public class VoxelEventAccumulator
    {
        // The lower, left, rear corner of a voxel.
        public Vector3 min;

        // Thhe upper, right, front corner of the voxel
        public Vector3 max;

        // A collection of movement event counts broken up into some set of sequential time periods
        public Dictionary<TimeSpan, int> movementEventsByTime = new Dictionary<TimeSpan, int>();

        // A collection of movement event counts broken up into some set of sequential time period
        public Dictionary<TimeSpan, int> lineOfSightEventCounterByTime =
            new Dictionary<TimeSpan, int>();

        public VoxelEventAccumulator(Vector3 min, Vector3 max)
        {
            this.min = min;
            this.max = max;
        }
    }

    public enum SpaceType
    {
        Private,
        SemiPrivate,
        Pause,
        Public
    }

    public class Acquaintance
    {
        public Agent otherAgent { get; set; }
        public TimeSpan startTime { get; set; }
    }

    public class Associate
    {
        public Agent otherAgent { get; set; }
        public TimeSpan startTime { get; set; }
    }

    public interface IInteraction
    {
        TimeSpan startTime { get; set; }

        TimeSpan cooldown { get; set; }
        Vector3 myPosition { get; set; }
        Vector3 theirPosition { get; set; }
    }

    public class Encounter : IInteraction
    {
        public TimeSpan startTime { get; set; }
        public TimeSpan cooldown { get; set; }
        public Vector3 myPosition { get; set; }
        public Vector3 theirPosition { get; set; }
        public bool forgotten { get; set; }
    }

    public class Greeting : IInteraction
    {
        public TimeSpan startTime { get; set; }
        public TimeSpan cooldown { get; set; }
        public Vector3 myPosition { get; set; }
        public Vector3 theirPosition { get; set; }
    }

    public class Conversation : IInteraction
    {
        public TimeSpan startTime { get; set; }
        public TimeSpan cooldown { get; set; }
        public Vector3 myPosition { get; set; }
        public Vector3 theirPosition { get; set; }
        public Vector3 conversationPosition { get; set; }
    }

    public class Relationship
    {
        public Agent otherAgent;

        public List<IInteraction> interactions = new List<IInteraction>();
    }

    public class ActivitySummary
    {
        public string useCaseName;
        public TimeSpan startTime;
        public TimeSpan duration;

        public ActivitySummary(string useCaseName, TimeSpan startTime, TimeSpan duration)
        {
            this.useCaseName = useCaseName;
            this.startTime = startTime;
            this.duration = duration;
        }
    }

    //public class ActivityRecord
    //{
    //    public string agentName { get; set; }

    //    public ActivityRecord(string agentName)
    //    {
    //        this.agentName = agentName;
    //    }

    //    public List<List<ActivitySummary>> dailyActivitySummaries = new List<List<ActivitySummary>>();
    //}

    public class AgentProfile
    {
        public string type { get; set; }
        public double caution { get; set; }

        public List<ActivitySummary> activitySummaries = new List<ActivitySummary>();
    }

    public class Household
    {
        public Component component { get; private set; }
        public bool isFamily { get; private set; }
        public List<AgentProfile> agentProfiles = new List<AgentProfile>();

        public Household(Component component, bool isFamily)
        {
            this.component = component;
            this.isFamily = isFamily;
        }
    }

    public class PathSegment
    {
        public PathSegment(Vector3 endA, Vector3 endB)
        {
            ends.Add(endA);
            ends.Add(endB);
        }

        public List<Vector3> ends = new List<Vector3>();
        public int traversals;
        public List<Encounter> encounters = new List<Encounter>();
        public List<Greeting> greetings = new List<Greeting>();
        public List<Conversation> conversations = new List<Conversation>();
    }

    public class PathSegmentCollection
    {
        public List<PathSegment> pathSegments = new List<PathSegment>();
        Dictionary<Vector3, List<PathSegment>> pathSegmentIndex =
            new Dictionary<Vector3, List<PathSegment>>();

        public int AddTraversal(Vector3 endA, Vector3 endB)
        {
            int traversals = 0;
            // The index is not "perfect" as there is a tiny chance
            // two segments will have the same center point but different end points.
            // Thus it is used to speed up search, and does not represent a true "hash".
            Vector3 mid = (endA + endB) / 2;
            #region If the index does not contain the segment "hash", add it
            if (pathSegmentIndex.ContainsKey(mid) == false)
            {
                pathSegmentIndex[mid] = new List<PathSegment>();
            }
            #endregion

            bool found = false;
            foreach (PathSegment pathSegment in pathSegmentIndex[mid])
            {
                if (pathSegment.ends.Contains(endA) && pathSegment.ends.Contains(endB))
                {
                    // We found it
                    pathSegment.traversals++;
                    traversals = pathSegment.traversals;
                    found = true;
                    break;
                }
            }
            if (found == false)
            {
                // We have never seen this segment before.. add it
                PathSegment pathSegment = new PathSegment(endA, endB);
                pathSegments.Add(pathSegment);
                pathSegmentIndex[mid].Add(pathSegment);
                pathSegment.traversals++;
                traversals = pathSegment.traversals;
            }

            return traversals;
        }

        public PathSegment Find(Vector3 endA, Vector3 endB)
        {
            PathSegment result = null;
            Vector3 mid = (endA + endB) / 2;
            foreach (PathSegment pathSegment in pathSegmentIndex[mid])
            {
                if (pathSegment.ends.Contains(endA) && pathSegment.ends.Contains(endB))
                {
                    result = pathSegment;
                    break;
                }
            }
            return result;
        }
    }

    public class HourlyInteraction
    {
        public int ts { get; set; }
        public int te { get; set; }
        public int n { get; set; }
        public string c { get; set; }
    }

    public class HourlyInteractions
    {
        public List<HourlyInteraction> samples = new List<HourlyInteraction>();
    }

    public class ActivityCount
    {
        public string useCase { get; set; }
        public string destinationIdentity { get; set; }
        public int trips { get; set; }
        public int encounters { get; set; }
        public int greetings { get; set; }
        public int conversations { get; set; }
        public int startedAt { get; set; }
        public int endedAt { get; set; }
    }

    public class ActiviyCounts
    {
        public List<ActivityCount> activities = new List<ActivityCount>();
    }

    public class Anchor
    {
        public enum Familiarity
        {
            Stranger,
            Acquaintance,
            Associate
        }

        public Agent perceivedAgent { get; set; }
        public Familiarity familiarity { get; set; }
        public TimeSpan firstPerceivedAt { get; set; }
        public TimeSpan lastPerceivedAt { get; set; }

        public float interest;

        public bool considerGreeting { get; set; }
        public TimeSpan? lastAwkwardnessBasedGreetingAt { get; set; }
        public bool collisionBasedGreetingAttempted { get; set; }
        public bool distanceBasedGreetingAttempted { get; set; }

        public bool considerConversation { get; set; }
    }

    public class SpaceTypeWeights
    {
        public SpaceTypeWeights(float outside, float undercover, float inside)
        {
            this.outside = outside;
            this.undercover = undercover;
            this.inside = inside;
        }

        public float outside { get; }
        public float undercover { get; }
        public float inside { get; }
    }

    public class WeatherAsJson
    {
        public string isHotCold { get; set; }
        public string isRaining { get; set; }
        public string name { get; set; }
    }

    public class Weather
    {
        public BitArray isRainingByHour { get; set; }
        public BitArray isHotColdByHour { get; set; }
    }
}
