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
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];
    
    public static void Main(string[] args)
    {
        try {
            finalCalculation[0] = new float[accelerator[0].Split(',').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) { // Pre-populate the finalCalculation headers
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
            }
            
            float maxSensitivity = 1;
            for (int i = 1; i < accelerator.Length; i++)
            {
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM
                
                finalCalculation[i] = new float[torqueValuesAtRpm.Length]; // Initialize the row in finalCalculation
                finalCalculation[i][0] = rpm; // Set the first column to the RPM
                
                for (int j = 1; j < torqueValuesAtRpm; j++)
                {
                    float desiredSensitivity = float.Parse(sensitivity[i].Split(',')[j]);
                    float testValue = 0F;
                    while (!GetCalculatedValue(rpm, testValue, maxSensitivity).IsAround(desiredSensitivity)) {
                        testValue += 0.01F;
                    }
                    
                    finalCalculation[i][j] = testValue;
                    if (testValue > maxSensitivity)
                    {
                        maxSensitivity = testValue;
                    }
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
    /// <param name="maxSensitivity"></param>
    /// <returns></returns>
    [Pure]
    private static float GetCalculatedValue(float rpm, float requestedTorque, float maxSensitivity)
    {
        float finalCalculation;
        finalCalculation = throttle.LookupValueInTable(rpm, requestedTorque);
        finalCalculation += finalCalculation * ((float)Math.Tanh((boost.LookupValueInTable(rpm, finalCalculation) / TANH_DIVISOR)) * (float)TANH_MULTIPLIER); // Divide by 14.7 to get a multiplier related to atmospheric pressure

        return (finalCalculation / maxSensitivity) * 100;
    }
}

