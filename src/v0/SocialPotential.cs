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
    public class SocialEvent
    {
        public int startedAt { get; set; }

        public int count { get; set; }

        public string kind { get; set; }

        public SocialEvent(int startedAt, int count, string kind)
        {
            this.startedAt = startedAt;
            this.count = count;
            this.kind = kind;
        }
    }

    public class SocialPotential
    {
        const int secondsPerDay = 24 * 60 * 60;

        public ObjectId ticketId { get; set; }

        public List<SocialEvent> dailyData = new List<SocialEvent>();

        public SocialPotential(ObjectId ticketId)
        {
            this.ticketId = ticketId;
        }
    }
}
