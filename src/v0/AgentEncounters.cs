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
using System.Collections.Generic;

namespace FLUID_Simulator
{
    public class HierarchyNode
    {
        public int id { get; set; }

        public string name { get; set; }

        public int? parent { get; set; }

        public HierarchyNode(int id, string name, int? parent = null)
        {
            this.id = id;
            this.name = name;
            this.parent = parent;
        }
    }

    //encounters = [
    //    { ids: [1, 2], startedAt: 100, endedAt: 110 },
    //    { ids:[3, 4], startedAt: 110, endedAt: 120 },
    //    ,,,
    //];
    public class Siting
    {
        public List<int> ids = new List<int>();
        public int startedAt;
        public int endedAt;

        public Siting(int idA, int idB, int startedAt, int endedAt)
        {
            this.ids.Add(idA);
            this.ids.Add(idB);
            this.startedAt = startedAt;
            this.endedAt = endedAt;
        }
    }

    public class AgentEncounters
    {
        public ObjectId ticketId { get; set; }

        public List<HierarchyNode> hierarchy = new List<HierarchyNode>();

        public List<Siting> encounters = new List<Siting>();

        public AgentEncounters(ObjectId ticketId)
        {
            this.ticketId = ticketId;
        }
    }
}
