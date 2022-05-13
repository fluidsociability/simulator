using System;

namespace FLUID
{
    public class Schedule
    {
        int _stepCounter = 0;

        public bool step(SimulationState s)
        {
            Console.WriteLine("step {0}", _stepCounter);
            _stepCounter++;
            return true;
        }

        public int getSteps()
        {
            return _stepCounter;
        }
    }
}
