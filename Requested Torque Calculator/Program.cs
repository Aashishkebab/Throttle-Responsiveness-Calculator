namespace Requested_Torque_Calculator;

using static Throttle_Position_Calculator.MathHelper;

internal static class Program
{
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    /// <remarks>
    /// Still required to get the headers.
    /// </remarks>
    private static readonly string[] accelerator = File.ReadAllLines("accelerator.csv");
    
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to an arbitrary torque value (sensitivity).
    /// </summary>
    private static readonly string[] sensitivity = File.ReadAllLines("sensitivity.csv");

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    private static readonly string[] throttle = File.ReadAllLines("throttle.csv");

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    private static readonly string[] boost = File.ReadAllLines("boost.csv");

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    private static float[][] finalCalculation = new float[sensitivity.Length][];
    
    public static void Main(string[] args)
    {
        try {
            finalCalculation[0] = new float[accelerator[0].Split(',').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) {
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
            }

            for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse);

                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length];
                finalCalculation[i][0] = rpm;

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Starting at 1 to ignore the RPM column
                    finalCalculation[i][j] = throttle.LookupValueInTable(rpm, torqueValuesAtRpm[j]);
                }

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Target Boost
                    finalCalculation[i][j] += finalCalculation[i][j] * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation[i][j]) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER); // Divide by 14.7 to get a multiplier related to atmospheric pressure
                }
            }

            File.WriteAllLines("desired_acceleration.csv", finalCalculation.Select(row => string.Join("\t", row).Replace("-∞", "0")).Prepend("[Selection3D]"));
            
            Console.WriteLine("Successfully wrote desired_acceleration.csv");
            Console.WriteLine("Press ENTER to close.");
            Console.ReadLine();
        }
        catch(Exception exception) {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }
    }
}

