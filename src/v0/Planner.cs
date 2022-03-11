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
using UnityEngine;
using UnityEngine.AI;

namespace FLUID
{
    public interface ISample
    {
        TimeSpan time { get; set; }
        Vector3 location { get; set; }
    }

    public class PathSample : ISample
    {
        public TimeSpan time { get; set; }
        public Vector3 segmentStart;
        public Vector3 segmentEnd;
        public Vector3 location { get; set; }
        public double proportion;
    }

    public class DwellSample : ISample
    {
        public TimeSpan time { get; set; }
        public Vector3 location { get; set; }
    }

    public interface IAction
    {
        TimeSpan startTime { get; set; }
        TimeSpan duration { get; }

        bool isActiveAt(TimeSpan sampleTime);

        bool GetLocationAtTime(TimeSpan sampleTime, out Vector3 location);

        ISample GetSampleAtTime(TimeSpan sampleTime);
    }

    public class Journey : IAction
    {
        TimeSpan _startTime = TimeSpan.Zero;
        TimeSpan _duration;
        Vector3[] _path = null;
        public double walkingSpeedScale;

        public Journey(Vector3[] path, double walkingSpeedScale)
        {
            _path = path;
            _duration = TimeSpan.Zero;
            for (int index = 1; index < path.Length; index++)
            {
                _duration += TimeSpan.FromSeconds(
                    (path[index] - path[index - 1]).magnitude * 1.4 * walkingSpeedScale
                );
            }
            this.walkingSpeedScale = walkingSpeedScale;
        }

        public TimeSpan startTime
        {
            get { return _startTime; }
            set { _startTime = value; }
        }

        public TimeSpan duration
        {
            get { return _duration; }
        }

        public bool isActiveAt(TimeSpan sampleTime)
        {
            bool result = startTime <= sampleTime && sampleTime <= startTime + _duration;
            return result;
        }

        public bool GetLocationAtTime(TimeSpan sampleTime, out Vector3 location)
        {
            bool found = false;
            location = Vector3.zero;
            if (startTime <= sampleTime && sampleTime <= startTime + duration)
            {
                double sampleDistance = (sampleTime - startTime).TotalSeconds * walkingSpeedScale;
                double totalDistance = 0;
                for (int index = 1; index < _path.Length; index++)
                {
                    double segmentDistance = (_path[index] - _path[index - 1]).magnitude;
                    if (
                        totalDistance <= sampleDistance
                        && sampleDistance < totalDistance + segmentDistance
                    )
                    {
                        // We have found the correct segment in the journey
                        // Work out the amount of the segment traversed
                        float proportion = (float)(
                            (sampleDistance - totalDistance) / segmentDistance
                        );
                        location =
                            _path[index - 1] + proportion * (_path[index] - _path[index - 1]);
                        found = true;
                        break;
                    }
                    totalDistance += segmentDistance;
                }
            }

            return found;
        }

        public ISample GetSampleAtTime(TimeSpan sampleTime)
        {
            ISample actionSample = null;
            if (startTime <= sampleTime && sampleTime <= startTime + duration)
            {
                double sampleDistance = (sampleTime - startTime).TotalSeconds * walkingSpeedScale;
                double totalDistance = 0;
                for (int index = 1; index < _path.Length; index++)
                {
                    double segmentDistance = (_path[index] - _path[index - 1]).magnitude;
                    if (
                        totalDistance <= sampleDistance
                        && sampleDistance < totalDistance + segmentDistance
                    )
                    {
                        // We have found the correct segment in the journey
                        PathSample pathSample = new PathSample();
                        pathSample.time = sampleTime;
                        pathSample.segmentStart = _path[index - 1];
                        pathSample.segmentEnd = _path[index];

                        // Work out the amount of the segment traversed
                        pathSample.proportion = (sampleDistance - totalDistance) / segmentDistance;
                        pathSample.location =
                            pathSample.segmentStart
                            + (float)pathSample.proportion
                                * (pathSample.segmentEnd - pathSample.segmentStart);

                        actionSample = pathSample;
                        break;
                    }
                    totalDistance += segmentDistance;
                }
            }

            return actionSample;
        }

        public Vector3[] Path
        {
            get { return _path; }
            set { _path = value; }
        }
    }

    public class Motionless : IAction
    {
        public Motionless(
            TimeSpan duration,
            Vector3 location,
            float visibility,
            System.Random random
        )
        {
            this.duration = duration;
            this.location = location;
            this.visibility = visibility;
            this.random = random;
        }

        public float visibility { get; set; }
        public TimeSpan startTime { get; set; }

        public bool isActiveAt(TimeSpan sampleTime)
        {
            bool result = startTime <= sampleTime && sampleTime <= startTime + duration;
            return result && (random.NextDouble() < visibility);
        }

        public TimeSpan duration { get; private set; }
        public Vector3 location { get; private set; }
        System.Random random;

        public bool GetLocationAtTime(TimeSpan sampleTime, out Vector3 location)
        {
            location = Vector3.zero;
            bool found = false;
            if (startTime <= sampleTime && sampleTime <= startTime + duration)
            {
                location = this.location;
                found = true;
            }

            return found;
        }

        public ISample GetSampleAtTime(TimeSpan sampleTime)
        {
            ISample sample = null;
            if (startTime <= sampleTime && sampleTime <= startTime + duration)
            {
                DwellSample dwellSample = new DwellSample();
                dwellSample.time = sampleTime;
                dwellSample.location = location;
                sample = dwellSample;
            }

            return sample;
        }
    }

    public class Absent : IAction
    {
        TimeSpan _startTime = TimeSpan.Zero;
        string _label;
        TimeSpan _duration;

        public Absent(string label, TimeSpan duration)
        {
            _label = label;
            _duration = duration;
        }

        public TimeSpan startTime
        {
            get { return _startTime; }
            set { _startTime = value; }
        }

        public string label
        {
            get { return _label; }
        }

        public bool isActiveAt(TimeSpan sampleTime)
        {
            return startTime <= sampleTime && sampleTime <= startTime + _duration;
        }

        public TimeSpan duration
        {
            get { return _duration; }
        }

        public bool GetLocationAtTime(TimeSpan sampleTime, out Vector3 location)
        {
            location = Vector3.zero;
            return false;
        }

        public ISample GetSampleAtTime(TimeSpan sampleTime)
        {
            return null;
        }
    }

    public class InElevator : IAction
    {
        TimeSpan _startTime = TimeSpan.Zero;
        string _bank;
        int _car;
        TimeSpan _duration;

        public InElevator(string bank, int car, TimeSpan duration)
        {
            _bank = bank;
            _car = car;
            _duration = duration;
        }

        public TimeSpan startTime
        {
            get { return _startTime; }
            set { _startTime = value; }
        }

        public string bank
        {
            get { return _bank; }
        }

        public int car
        {
            get { return _car; }
        }

        public bool isActiveAt(TimeSpan sampleTime)
        {
            return startTime <= sampleTime && sampleTime <= startTime + _duration;
        }

        public TimeSpan duration
        {
            get { return _duration; }
        }

        public bool GetLocationAtTime(TimeSpan sampleTime, out Vector3 location)
        {
            location = Vector3.zero;
            return false;
        }

        public ISample GetSampleAtTime(TimeSpan sampleTime)
        {
            return null;
        }
    }
}
