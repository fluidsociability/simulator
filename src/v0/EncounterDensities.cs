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
using System;
using System.Collections.Generic;

namespace FLUID_Simulator
{
    public class SpaceTypeStatistics
    {
        public int id { get; set; }

        public string name { get; set; }

        public List<Object> area { get; set; }

        public SpaceTypeStatistics(int id, string name, float area)
        {
            this.id = id;
            this.name = name;
            this.area = new List<object>() { area, "m**2" };
        }
    }

    public class EncounterTally
    {
        public int id { get; set; }

        public int count { get; set; }

        public int startedAt { get; set; }

        public int endedAt { get; set; }

        public EncounterTally(int spaceTypeId, int count, int startedAt, int endedAt)
        {
            this.id = spaceTypeId;
            this.count = count;
            this.startedAt = startedAt;
            this.endedAt = endedAt;
        }
    }

    public class EncounterDensities
    {
        public ObjectId ticketId { get; set; }

        public List<SpaceTypeStatistics> inventory = new List<SpaceTypeStatistics>();

        public List<EncounterTally> tallies = new List<EncounterTally>();

        public EncounterDensities(ObjectId ticketId)
        {
            this.ticketId = ticketId;
        }
    }
}
