namespace Torque_calculator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string[] accelerator = File.ReadAllLines("accelerator.csv");
            string[] throttle = File.ReadAllLines("throttle.csv");
            string[] boost = File.ReadAllLines("boost.csv");
            string[] finalCalculation = new string[accelerator.Length];

            for(int i = 0; i < accelerator.Length; i++){

            }
        }
    }
}
