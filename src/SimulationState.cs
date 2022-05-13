using System;

namespace FLUID
{
    public class SimulationState
    {
        long _seed;
        public Schedule schedule { get; }

        public SimulationState(long seed)
        {
            _seed = seed;
            schedule = new Schedule();
            Console.WriteLine("INITIALIZING SIM STATE");
        }

        public void start()
        {
            Console.WriteLine(_seed);
        }

        public void finish() { }
    }
}
