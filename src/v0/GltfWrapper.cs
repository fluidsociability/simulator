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
using System.Text;
using System.Threading.Tasks;
using glTFLoader;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace FLUID_Simulator
{
    public class GltfWrapper
    {
        [BsonId]
        public ObjectId id { get; set; }

        public ObjectId ticketId { get; set; }

        public glTFLoader.Schema.Gltf glTF { get; set; }
    }
}
