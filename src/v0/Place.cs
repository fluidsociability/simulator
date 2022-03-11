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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum ExposureEnum
{
    Outside,
    Undercover,
    Inside
}

public class Place : MonoBehaviour
{
    // public Occupancy occupancy;
    public UnitOccupancy unitOccupancy;

    public Vector3 BottomCenter
    {
        get { return StructuredCausalModel.FindBottomCenter(gameObject); }
    }

    public FLUID.Component component { get; set; }

    public List<Place> semiPrivateSpaces = new List<Place>();

    public ExposureEnum exposure { get; set; }
}
