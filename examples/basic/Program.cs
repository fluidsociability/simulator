// See https://aka.ms/new-console-template for more information

using System;

namespace Basic
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting simulation");

            var seed = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var state = new FLUID.SimulationState(seed);
            state.start();

            do
            {
                if (!state.schedule.step(state))
                    break;
            } while (state.schedule.getSteps() < 20);

            state.finish();

            Console.WriteLine("Ending simulation");
        }
    }
}
