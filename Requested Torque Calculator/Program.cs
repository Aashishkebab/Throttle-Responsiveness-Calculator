using System.Diagnostics.Contracts;

using static Throttle_Position_Calculator.MathHelper;

namespace Requested_Torque_Calculator;

internal static class Program {
    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to a Requested Torque value.
    /// </summary>
    /// <remarks>
    /// Still required to get the headers.
    /// </remarks>
    private static readonly string[] accelerator = File.ReadAllLines("accelerator.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Accelerator Pedal Angle at different RPMs to an arbitrary torque value (sensitivity).
    /// </summary>
    private static readonly string[] sensitivity = File.ReadAllLines("sensitivity.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Requested Torque at different RPMs to a Throttle Opening Angle.
    /// </summary>
    private static readonly string[] throttle = File.ReadAllLines("throttle.csv").RemoveEmpty();

    /// <summary>
    /// Table mapping Throttle Opening Angle at different RPMs to Target Boost.
    /// </summary>
    private static readonly string[] manifoldPressure = File.ReadAllLines("map.csv").RemoveEmpty();

    private const float MAXIMUM_VACUUM = 14.7f;

    /// <summary>
    /// The final calculated "torque" in arbitrary units.
    /// </summary>
    private static readonly float[][] finalCalculation = new float[accelerator.Length][];

    public static void Main(string[] args) {
        try {
            finalCalculation[0] = new float[accelerator[0].Split(',').Length];
            for(int j = 1; j < finalCalculation[0].Length; j++) { // Pre-populate the finalCalculation headers
                finalCalculation[0][j] = float.Parse(accelerator[0].Split(',')[j]);
            }

            // Get the max sensitivity to un-normalize (weirdize?)
            float maxSensitivity = 0;
            for(int i = 1; i < finalCalculation.Length; i++) {
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                float rowMaxSensitivity = GetCalculatedValue(rpm, MAX_REQUESTED_TORQUE); // Calculate maximum possible value for the RPM
                if(rowMaxSensitivity > maxSensitivity) {
                    maxSensitivity = rowMaxSensitivity;
                }
            }

            for(int i = 1; i < accelerator.Length; i++) {
                float[] torqueValuesAtRpm = Array.ConvertAll(accelerator[i].Split(','), float.Parse); // The current row
                float rpm = torqueValuesAtRpm[0]; // First column is the actual RPM

                finalCalculation[i] = new float[torqueValuesAtRpm.Length]; // Initialize the row in finalCalculation
                finalCalculation[i][0] = rpm; // Set the first column to the RPM

                for(int j = 1; j < torqueValuesAtRpm.Length; j++) {
                    float desiredSensitivity = float.Parse(sensitivity[i].Split(',')[j]);
                    float requestedTorque = GetRequestedTorque(rpm, desiredSensitivity, maxSensitivity);

                    if(requestedTorque > MAX_REQUESTED_TORQUE) {
                        requestedTorque = MAX_REQUESTED_TORQUE;
                    }

                    finalCalculation[i][j] = requestedTorque;
                    Console.WriteLine($"Calculated value {requestedTorque} for RPM {rpm}");
                }
            }

            File.WriteAllLines("desired_acceleration.csv", finalCalculation.Select(row => string.Join(",", row).Replace("-∞", "0")));
            File.WriteAllLines("desired_acceleration.txt", finalCalculation.Skip(1).Select(row => string.Join("\t", row.Skip(1)).Replace("-∞", "0")).Prepend("[Selection3D]"));

            Console.WriteLine("Successfully wrote desired_acceleration.csv and desired_acceleration.txt");
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
    private static float GetCalculatedValue(float rpm, float requestedTorque) {
        if(rpm == 0 || requestedTorque == 0) {
            return 0;
        }

        float finalCalculation;

        float mapCalculation = manifoldPressure.LookupValueInTable(rpm, throttle.LookupValueInTable(rpm, requestedTorque));
        finalCalculation = (mapCalculation > 0 ? 1f + (float)Math.Tanh(mapCalculation / TANH_DIVISOR) * TANH_MULTIPLIER : (MAXIMUM_VACUUM + mapCalculation) / MAXIMUM_VACUUM);

        finalCalculation += finalCalculation * EngineTorque.LookupValueInList(rpm);

        return finalCalculation;
    }

    /// <summary>
    /// Reverses <see cref="GetCalculatedValue"/> to find the requested torque that produces the desired sensitivity.
    /// </summary>
    [Pure]
    private static float GetRequestedTorque(float rpm, float desiredSensitivity, float maxSensitivity) {
        if(desiredSensitivity == 0) {
            return 0;
        }
        if(desiredSensitivity == 100) {
            return MAX_REQUESTED_TORQUE;
        }

        // Un-normalize
        float targetRaw = desiredSensitivity * maxSensitivity / 100f;

        // Remove engine torque multiplier
        float pressureRatio = targetRaw / (1f + EngineTorque.LookupValueInList(rpm));

        // Reverse pressure ratio to MAP
        float map;
        if(pressureRatio < 1f) {
            map = (pressureRatio * 14.7f) - MAXIMUM_VACUUM;
        }
        else {
            float tanhInput = (pressureRatio - 1f) / TANH_MULTIPLIER;
            if(tanhInput >= 1f) {
                tanhInput = 0.9999f; // atanh is undefined at 1
            }
            map = TANH_DIVISOR * (float)Math.Atanh(tanhInput);
        }

        // Reverse-lookup MAP in manifold pressure table to get throttle angle
        float throttleAngle = manifoldPressure.LookupHeaderInTable(rpm, map);

        // Reverse-lookup throttle angle in throttle table to get requested torque
        float requestedTorque = throttle.LookupHeaderInTable(rpm, throttleAngle);

        return requestedTorque;
    }
}