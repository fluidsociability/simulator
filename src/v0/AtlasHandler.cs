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
using MongoDB.Bson;
using MongoDB.Driver;

namespace FLUID_Simulator
{
    public class AtlasHandler
    {
        MongoClient client;
        IMongoDatabase database;

        IMongoCollection<SimulationTicket> simTicketCollection;

        FilterDefinition<SimulationTicket> ticketFilter;

        public AtlasHandler(bool isProduction)
        {
            if (isProduction)
            {
                client = new MongoClient("mongodb connection string");
            }
            else
            {
                client = new MongoClient("mongodb connection string");
            }
            database = client.GetDatabase("Fluid");

            simTicketCollection = database.GetCollection<SimulationTicket>("simTicket");
        }

        public SimulationTicket GetTicket(ObjectId ticketIdentifer)
        {
            SimulationTicket ticket = null;

            ticketFilter = Builders<SimulationTicket>.Filter.Eq("id", ticketIdentifer);

            var matchingTickets = simTicketCollection.Find<SimulationTicket>(ticketFilter).ToList();

            if (matchingTickets.Count == 1)
            {
                ticket = matchingTickets[0];
            }

            return ticket;
        }

        public void UpdateTimeStamp()
        {
            var update = Builders<SimulationTicket>.Update.Set(
                "lastUpdate",
                new BsonDateTime(DateTime.Now)
            );

            simTicketCollection.UpdateOne(ticketFilter, update);
        }

        public void UpdateTicket(SimulationTicket simulationTicket)
        {
            var update = Builders<SimulationTicket>.Update
                .Set("lastUpdate", new BsonDateTime(DateTime.Now))
                .Set("version", simulationTicket.version)
                .Set("modelImportProgress", simulationTicket.modelImportProgress)
                .Set("simulationProgress", simulationTicket.simulationProgress)
                .Set("outputProgress", simulationTicket.outputProgress)
                .Set("eventLog", simulationTicket.eventLog)
                .Set("agentPopulation", simulationTicket.agentPopulation)
                .Set("areaPerAgent", simulationTicket.areaPerAgent)
                .Set("dwellingTypePopulations", simulationTicket.dwellingTypePopulations)
                .Set("agentTypePopulations", simulationTicket.agentTypePopulations)
                .Set("elevatorBankCount", simulationTicket.elevatorBankCount)
                .Set(
                    "mandatoryDestinationPopulations",
                    simulationTicket.mandatoryDestinationPopulations
                )
                .Set(
                    "optionalDestinationPopulations",
                    simulationTicket.optionalDestinationPopulations
                )
                .Set("acquaintanceships", simulationTicket.acquaintanceships)
                .Set("associations", simulationTicket.associations)
                .Set("encounters", simulationTicket.encounters)
                .Set("greetings", simulationTicket.greetings)
                .Set("conversations", simulationTicket.conversations)
                .Set("numberOfFloors", simulationTicket.numberOfFloors)
                .Set("mostEncountersOnAnyGivenDay", simulationTicket.mostEncountersOnAnyGivenDay)
                .Set("leastEncountersOnAnyGivenDay", simulationTicket.leastEncountersOnAnyGivenDay)
                .Set("mostGreetingsOnAnyGivenDay", simulationTicket.mostGreetingsOnAnyGivenDay)
                .Set("leastGreetingsOnAnyGivenDay", simulationTicket.leastGreetingsOnAnyGivenDay)
                .Set(
                    "mostConversationsOnAnyGivenDay",
                    simulationTicket.mostConversationsOnAnyGivenDay
                )
                .Set(
                    "leastConversationsOnAnyGivenDay",
                    simulationTicket.leastConversationsOnAnyGivenDay
                );

            simTicketCollection.UpdateOne(ticketFilter, update);
        }
    }
}
