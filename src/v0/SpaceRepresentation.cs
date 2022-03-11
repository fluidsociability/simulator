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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IVoxelGridA
{
    short GetAt(int x, int y, int z, short mask);
    void SetAt(int x, int y, int z, short newValue, short mask);

    int Width { get; }

    int Height { get; }

    int Depth { get; }
}

public class SparseVoxelGrid : IVoxelGridA
{
    long width;
    long height;
    long depth;
    public Dictionary<long, short> valuesByEncodedIndex = new Dictionary<long, short>();
    public SortedDictionary<int, int> yCounts = new SortedDictionary<int, int>();

    // public Dictionary<int, int> yMap = new Dictionary<int, int>();
    public float elevationShift = 0;

    public SparseVoxelGrid(int width, int height, int depth)
    {
        this.width = width;
        this.height = height;
        this.depth = depth;
    }

    public long EncodeIndex(int x, int y, int z)
    {
        return (long)z * width * height + (long)y * width + (long)x;
    }

    public void DecodeKey(long key, out int xIndex, out int yIndex, out int zIndex)
    {
        xIndex = (int)(key % width);
        yIndex = (int)((key / width) % height);
        zIndex = (int)((key / (width * height)));
    }

    public short GetAt(long encodedIndex, short mask)
    {
        //if (0 > x || x > width - 1 || 0 > z || z > depth - 1)
        //{
        //    throw new System.IndexOutOfRangeException();
        //}
        short value = 0;
        if (valuesByEncodedIndex.ContainsKey(encodedIndex))
        {
            value = (short)(valuesByEncodedIndex[encodedIndex] & mask);
        }
        return value;
    }

    public short GetAt(int x, int y, int z, short mask)
    {
        long encodedIndex = EncodeIndex(x, y, z);
        return GetAt(encodedIndex, mask);
    }

    public void SetAt(long encodedIndex, short newValue, short mask)
    {
        //if (0 > x || x > width - 1 || 0 > z || z > depth - 1)
        //{
        //    throw new System.IndexOutOfRangeException();
        //}
        int x,
            y,
            z;
        DecodeKey(encodedIndex, out x, out y, out z);
        if (valuesByEncodedIndex.ContainsKey(encodedIndex))
        {
            valuesByEncodedIndex[encodedIndex] = (short)(
                (valuesByEncodedIndex[encodedIndex] & ~mask) | (newValue & mask)
            );
            if (valuesByEncodedIndex[encodedIndex] == 0x0000)
            {
                valuesByEncodedIndex.Remove(encodedIndex);
            }
            //else
            //{
            //    yCounts[y]++;
            //}
        }
        else
        {
            if (newValue != 0)
            {
                valuesByEncodedIndex[encodedIndex] = (short)(newValue & mask);
                if (yCounts.ContainsKey(y) == false)
                {
                    yCounts[y] = 0;
                }
                yCounts[y]++;
            }
        }
    }

    public void SetAt(int x, int y, int z, short newValue, short mask)
    {
        //if (0 > x || x > width - 1 || 0 > z || z > depth - 1)
        //{
        //    throw new System.IndexOutOfRangeException();
        //}
        long encodedIndex = EncodeIndex(x, y, z);
        SetAt(encodedIndex, newValue, mask);
    }

    public int Width
    {
        get { return (int)width; }
    }

    public int Height
    {
        get { return (int)height; }
    }

    public int Depth
    {
        get { return (int)depth; }
    }
}
