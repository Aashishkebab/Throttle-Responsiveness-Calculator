using static Throttle_Position_Calculator.MathHelper;
using static Throttle_Position_Calculator.Program;

namespace Throttle_Position_Calculator;

internal static class Program {
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
    private static readonly float[][] rawFinalCalculation = new float[accelerator.Length][];

    /// <summary>
    /// The final calculated torque normalized to 100 as max available torque.
    /// </summary>
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];

    private static readonly float[][] pedalCalculation = new float[accelerator.Length][];

    /// <summary>
    /// There are several assumptions made about the format of the data.
    /// 1. The top row must be the headers, sorted ascending.
    /// 2. The leftmost column must be the RPM, sorted ascending.
    /// </summary>
    /// <param name="args"></param>
    public static void Main(string[] args) {
        try {
            finalCalculation[0] = new float[accelerator[0].Split(',').Length];
            rawFinalCalculation[0] = new float[finalCalculation[0].Length];
            pedalCalculation[0] = new float[finalCalculation[0].Length];

            for(int j = 1; j < finalCalculation[0].Length; j++) { // Pre-populate the finalCalculation headers
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
                rawFinalCalculation[0][j] = finalCalculation[0][j];
                pedalCalculation[0][j] = finalCalculation[0][j];
            }

            float maxSensitivity = 0;
            for(int i = 1; i < accelerator.Length; i++) { // Starting at 1 to ignore the headers
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length]; // Initialize the row in finalCalculation
                finalCalculation[i][0] = rpm; // Set the first column to the RPM
                rawFinalCalculation[i] = new float[torqueValuesAtRpm.Length];
                rawFinalCalculation[i][0] = rpm;
                pedalCalculation[i] = new float[torqueValuesAtRpm.Length];
                pedalCalculation[i][0] = rpm;

                // Calculate the throttle output from the accelerator pedal using the throttle table
                for(int j = 1; j < finalCalculation[i].Length; j++) { // Starting at 1 to ignore the RPM column
                    finalCalculation[i][j] = throttle.LookupValueInTable(rpm, torqueValuesAtRpm[j]);
                    rawFinalCalculation[i][j] = finalCalculation[i][j];
                    pedalCalculation[i][j] = finalCalculation[i][j];
                }

                // Multiply the throttle angles with Target Boost
                for(int j = 1; j < finalCalculation[i].Length; j++) {
                    finalCalculation[i][j] += finalCalculation[i][j] * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation[i][j]) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER); // Account for boost efficiency
                    rawFinalCalculation[i][j] = finalCalculation[i][j];
                }

                // Multiple the sensitivity/torque values with a relatively standard engine torque curve
                for(int j = 1; j < finalCalculation[i].Length; j++) {
                    finalCalculation[i][j] += finalCalculation[i][j] * EngineTorque.LookupValueInList(rpm);
                    rawFinalCalculation[i][j] = finalCalculation[i][j];
                }

                for(int j = 1; j < finalCalculation[i].Length; j++) { // Calculate max sensitivity
                    if(finalCalculation[i][j] > maxSensitivity) {
                        maxSensitivity = finalCalculation[i][j];
                    }
                }
            }

            for(int i = 0; i < finalCalculation.Length; i++) {
                for(int j = 1; j < finalCalculation[i].Length; j++) { // Normalize values to be out of 100
                    finalCalculation[i][j] = (finalCalculation[i][j] / maxSensitivity) * 100;
                }
            }

            File.WriteAllLines("pedal_to_throttle.csv", pedalCalculation.Select(row => string.Join(",", row).Replace("-∞", "0")));
            File.WriteAllLines("calculated_torque.csv", finalCalculation.Select(row => string.Join(",", row).Replace("-∞", "0")));
            File.WriteAllLines("calculated_torque_raw.csv", rawFinalCalculation.Select(row => string.Join(",", row).Replace("-∞", "0")));

            Console.WriteLine("Successfully wrote calculated_torque.csv and calculated_torque_raw.csv and pedal_to_throttle.csv");
            Console.WriteLine("Press ENTER to close.");
            Console.ReadLine();
        }
        catch(Exception exception) {
            Console.WriteLine(exception.ToString());
            Console.ReadLine();
        }
    }
}
