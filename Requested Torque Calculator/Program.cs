namespace Requested_Torque_Calculator;

internal static class Program
{
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    static readonly string[] sensitivity = File.ReadAllLines("sensitivity.csv");

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    static readonly string[] throttle = File.ReadAllLines("throttle.csv");

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    static string[]? boost;

    /// <summary>
    /// The Requested Torque headers in the <see cref="throttle"/> table.
    /// </summary>
    static readonly float[] throttleRequestedTorqueHeaders = Array.ConvertAll(throttle[0].Split(','), theValue => string.IsNullOrWhiteSpace(theValue) ? -1 : float.Parse(theValue));

    /// <summary>
    /// The RPMs in the throttle table.
    /// </summary>
    static readonly short[] throttleRpmList = throttle.Skip(1).Select(line => short.Parse(line.Split(',')[0])).ToArray();

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    static float[][] finalCalculation = new float[sensitivity.Length][];
    
    static void Main(string[] args)
    {
        try {
            try {
                boost = File.ReadAllLines("boost.csv");
            }
            catch(Exception) {
                // Do nothing, let boost remain null
            }

            finalCalculation[0] = new float[sensitivity[0].Split(',').Length];
            for(int j = 1; j < finalCalculation.Length; j++) {
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
            }

            for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse);

                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length];
                finalCalculation[i][0] = rpm;

                for(int j = 1; j < torqueValuesAtRpm.Length; j++) { // Starting at 1 to ignore the RPM column
                    finalCalculation[i][j] = rpm.LookupThrottlePlateOpeningAngle(torqueValuesAtRpm[j]);
                }

                if(boost != null) { // Boost calculations are optional
                    for(int j = 1; j < torqueValuesAtRpm.Length; j++) { // Target Boost
                        // TODO
                    }
                }
            }

            File.WriteAllLines("sensitivity.csv", finalCalculation.Select(row => string.Join(",", row)));
        }
        catch(Exception exception) {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }

        Console.WriteLine("Successfully wrote sensitivity.csv");
        Console.WriteLine("Press ENTER to close.");
        Console.ReadLine();
    }
}

