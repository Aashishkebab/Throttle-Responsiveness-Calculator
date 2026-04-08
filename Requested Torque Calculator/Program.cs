using System.Diagnostics.Contracts;
using System.Runtime.Intrinsics.Arm;

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
    private static readonly string[] accelerator = File.ReadAllLines("accelerator.txt").RemoveEmpty();
    
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to an arbitrary torque value (sensitivity).
    /// </summary>
    private static readonly string[] sensitivity = File.ReadAllLines("sensitivity.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    private static readonly string[] throttle = File.ReadAllLines("throttle.txt").RemoveEmpty();

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    private static readonly string[] boost = File.ReadAllLines("boost.txt").RemoveEmpty();
    
    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];
    
    public static void Main(string[] args)
    {
        try {
            finalCalculation[0] = new float[accelerator[0].Split('\t').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) { // Pre-populate the finalCalculation headers
                finalCalculation[0][j] = float.Parse(accelerator[0].Split('\t')[j]);
            }
            
            // Get the max sensitivity to un-normalize (weirdize?)
            float maxSensitivity = 0;
            for (int i = 1; i < finalCalculation.Length; i++) {
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split('\t'), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM
                
                maxSensitivity = GetCalculatedValue(rpm, MAX_REQUESTED_TORQUE); // Calculate maximum possible value for the RPM
            }
            
            for (int i = 1; i < accelerator.Length; i++)
            {
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split('\t'), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM
                
                finalCalculation[i] = new float[torqueValuesAtRpm.Length]; // Initialize the row in finalCalculation
                finalCalculation[i][0] = rpm; // Set the first column to the RPM
                
                for (int j = 1; j < torqueValuesAtRpm.Length; j++)
                {
                    float desiredSensitivity = float.Parse(sensitivity[i].Split(',')[j]);
                    float testValue = 0F;
                    while (!((GetCalculatedValue(rpm, testValue) / maxSensitivity) * 100).IsAround(desiredSensitivity) && testValue < MAX_REQUESTED_TORQUE) {
                        testValue += 0.01F;
                    }
                    
                    finalCalculation[i][j] = testValue;
                    Console.WriteLine($"Calculated value {testValue} for RPM {rpm}");
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

    /// <summary>
    /// Gets the final calculated value from the <paramref name="rpm"/> and <paramref name="requestedTorque"/>.
    /// </summary>
    /// <param name="rpm"></param>
    /// <param name="requestedTorque"></param>
    /// <returns></returns>
    [Pure]
    private static float GetCalculatedValue(float rpm, float requestedTorque)
    {
        float finalCalculation;
        finalCalculation = throttle.LookupValueInTable(rpm, requestedTorque);
        finalCalculation += finalCalculation * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER); // Divide by 14.7 to get a multiplier related to atmospheric pressure

        return finalCalculation;
    }
}

