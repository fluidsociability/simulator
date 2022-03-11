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

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FLUID_Simulator
{
    public enum SimulationEventType
    {
        Information,
        Warning,
        Error
    }

    [Serializable]
    public class SimulationEvent
    {
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime time;
        public SimulationEventType eventType;
        public string message;

        public SimulationEvent(SimulationEventType eventType, string message)
        {
            if (message.StartsWith("Shut")) { }
            this.eventType = eventType;
            this.message = message;
            this.time = DateTime.Now;
        }
    }

    [Serializable]
    public class LabelledPopulation
    {
        public LabelledPopulation(string label, int population)
        {
            this.label = label;
            this.population = population;
        }

        public string label { get; set; }
        public int population { get; set; }
    }

    [Serializable]
    [BsonIgnoreExtraElements]
    public class SimulationTicket
    {
        public string version { get; set; }

        public event EventHandler ProgressUpdated;

        [BsonId]
        public ObjectId id { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime createdOn { get; set; }

        public ObjectId UserId { get; set; }

        public int maximumUnitsToSimulate;

        [Category("Simulation"), DisplayName("Model Name"), Description("Name of the fbx file.")]
        public string ModelName { get; set; }

        [Category("Simulation"), Description("An optional description of the simulation.")]
        public string Description { get; set; }

        [
            Category("Simulation"),
            Description(
                "The ramdom \"seed\" used to vary the starting conditions of the simulation."
            )
        ]
        public int Seed { get; set; }

        [
            Category("Simulation"),
            DisplayName("Path To Model"),
            Description("Full path of the glb file.")
        ]
        public string PathToModelFile { get; set; }

        [Category("Simulation"), Description("How will the building be used?")]
        public string buildingType { get; set; }

        [
            Category("Simulation"),
            DisplayName("Days To Simulate"),
            Description("Number of days to simulate.")
        ]
        public uint DaysToSimulate { get; set; }

        [
            Category("Simulation"),
            DisplayName("Start Time"),
            Description("The time of day the simulation should start.")
        ]
        public string StartTime { get; set; }

        [
            Category("Simulation"),
            DisplayName("End Time"),
            Description("The time of day the simulation should end.")
        ]
        public string EndTime { get; set; }

        [
            Category("Simulation"),
            DisplayName("Start Date"),
            Description("The date of day the simulation should start.")
        ]
        public string startDate { get; set; }

        [
            Category("Simulation"),
            DisplayName("Location"),
            Description("The airport name(code) of the simulation")
        ]
        public string Location { get; set; }

        public ObjectId glTFIdentifier { get; set; }

        public float modelImportProgress { get; set; }

        public float simulationProgress { get; set; }

        public float outputProgress { get; set; }

        public BsonDateTime lastUpdate { get; set; }

        public List<SimulationEvent> eventLog = new List<SimulationEvent>();

        public int agentPopulation { get; set; }

        public float areaPerAgent { get; set; }

        public List<LabelledPopulation> agentTypePopulations = new List<LabelledPopulation>();

        public List<LabelledPopulation> dwellingTypePopulations = new List<LabelledPopulation>();

        public int elevatorBankCount;

        public List<LabelledPopulation> mandatoryDestinationPopulations =
            new List<LabelledPopulation>();

        public List<LabelledPopulation> optionalDestinationPopulations =
            new List<LabelledPopulation>();

        public int acquaintanceships { get; set; }
        public int associations { get; set; }
        public int encounters { get; set; }
        public int greetings { get; set; }
        public int conversations { get; set; }
        public int numberOfFloors { get; set; }
        public int mostEncountersOnAnyGivenDay { get; set; }
        public int leastEncountersOnAnyGivenDay { get; set; }
        public int mostGreetingsOnAnyGivenDay { get; set; }
        public int leastGreetingsOnAnyGivenDay { get; set; }
        public int mostConversationsOnAnyGivenDay { get; set; }
        public int leastConversationsOnAnyGivenDay { get; set; }
    }
}
