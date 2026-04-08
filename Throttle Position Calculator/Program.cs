#pragma warning disable IDE2001

namespace Throttle_Position_Calculator;

using static Throttle_Position_Calculator.MathHelper;

internal static class Program
{
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    private static readonly string[] accelerator = File.ReadAllLines("accelerator.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    private static readonly string[] throttle = File.ReadAllLines("throttle.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    private static readonly string[] boost = File.ReadAllLines("boost.csv").RemoveEmpty();

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];

    /// <summary>
    /// There are several assumptions made about the format of the data.
    /// 1. The top row must be the headers, sorted ascending.
    /// 2. The leftmost column must be the RPM, sorted ascending.
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args)
    {
        try {
            finalCalculation[0] = new float[accelerator[0].Split('\t').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) { // Pre-populate the finalCalculation headers
                finalCalculation[0][j] = float.Parse(accelerator[0].Split('\t')[j]);
            }

            float maxSensitivity = 0;
            for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split('\t'), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length]; // Initialize the row in finalCalculation
                finalCalculation[i][0] = rpm; // Set the first column to the RPM

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Starting at 1 to ignore the RPM column
                    finalCalculation[i][j] = throttle.LookupValueInTable(rpm, torqueValuesAtRpm[j]);
                }

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Target Boost
                    finalCalculation[i][j] += finalCalculation[i][j] * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation[i][j]) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER);

                    if (finalCalculation[i][j] > maxSensitivity)
                    {
                        maxSensitivity = finalCalculation[i][j];
                    }
                }
            }

            // Normalize so that max sensitivity is 100
            for (int i = 1; i < finalCalculation.Length; i++) {
                for (int j = 1; j < finalCalculation[i].Length; j++)
                {
                    finalCalculation[i][j] = (finalCalculation[i][j] / maxSensitivity) * 100;
                }
            }

            File.WriteAllLines("desired_sensitivity.csv", finalCalculation.Select(row => string.Join("\t", row).Replace("-∞", "0")).Prepend("[Selection3D]"));
            
            Console.WriteLine("Successfully wrote desired_sensitivity.csv");
            Console.WriteLine("Press ENTER to close.");
            Console.ReadLine();
        }
        catch(Exception exception) {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }
    }
}
