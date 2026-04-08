using System;
using System.Collections.Generic;
using System.Text;

namespace Throttle_Position_Calculator;
public static class MathHelper
{
    private const float TANH_DIVISOR = 55F;
    private const float TANH_MULTIPLIER = 3.3F;
    
    /// <summary>
    /// Gets the Throttle Plate Opening Angle for a given <paramref name="yValue"/> at a specified <paramref name="rpm"/>.
    /// </summary>
    /// <param name="yValue"></param>
    /// <param name="values"></param>
    /// <param name="rpm"></param>
    /// <returns></returns>
    public static float LookupValueInTable(this string[] values, float rpm, float yValue) {
        float[] tableHeaders = Array.ConvertAll(values[0].Split(','), theValue => string.IsNullOrWhiteSpace(theValue) ? -1 : float.Parse(theValue));
        short[] rpmList = values.Select(line => short.Parse(string.IsNullOrWhiteSpace(line.Split(',')[0]) ? "-1" : line.Split(',')[0])).ToArray();
        
        for(int i = 1; i < rpmList.Length; i++) {
            if(rpmList[i] >= rpm || i == rpmList.Length - 1) { // We have found the matching y columns
                for(int j = 1; j < tableHeaders.Length; j++) { // Starting at 1 to ignore the blank column
                    if(tableHeaders[j] >= yValue || j == tableHeaders.Length - 1) { // We have found the matching x columns
                        // Get the four values
                        float rpmCeilingValueCeiling = float.Parse(values[i].Split(',')[j]);
                        
                        float rpmFloorValueCeiling;
                        if(i == 1) {
                            rpmFloorValueCeiling = rpmCeilingValueCeiling;
                        }
                        else {
                            rpmFloorValueCeiling = float.Parse(values[i - 1].Split(',')[j]);
                        }
                        
                        float rpmCeilingValueFloor;
                        if(j == 1) {
                            rpmCeilingValueFloor = rpmCeilingValueCeiling;
                        }
                        else {
                            rpmCeilingValueFloor = float.Parse(values[i].Split(',')[j - 1]);
                        }

                        float rpmFloorValueFloor;
                        if(i == 1 && j == 1) { // If both RPM and Requested Torque are at their minimums, no need to interpolate
                            rpmFloorValueFloor = rpmCeilingValueCeiling; // All four values are the same in this instance
                        }
                        else if(i != 1 && j == 1) { // If only the Requested Torque is minimum value, set to the next Requested Torque
                            rpmFloorValueFloor = rpmFloorValueCeiling; // Previous torque and next torque are the same value
                        }
                        else if(i == 1 && j != 1) { // If only the RPM is minimum value, set to the next RPM
                            rpmFloorValueFloor = rpmCeilingValueFloor; // Previous RPM and next RPM are the same value
                        }
                        else {
                            rpmFloorValueFloor = float.Parse(values[i - 1].Split(',')[j - 1]);
                        }

                        // Get the percentage change in RPM from closest floor
                        float distanceFromRpmCeiling = rpmList[i] - rpm;
                        float totalRpmGap = rpmList[i] - rpmList[i - 1];

                        float rpmPercentageChangeFromCeiling;
                        if(totalRpmGap == 0 || (i == rpmList.Length - 1 && rpm > rpmList[i])) {
                            rpmPercentageChangeFromCeiling = 0;
                        }
                        else {
                            rpmPercentageChangeFromCeiling = distanceFromRpmCeiling / totalRpmGap;
                        }

                        // Get the percentage change in yValue from closest floor in the table
                        float distanceFromCeilingValue = tableHeaders[j] - yValue;
                        float totalValueGap = tableHeaders[j] - tableHeaders[j - 1];

                        float valuePercentageChangeFromCeiling;
                        if(totalValueGap == 0 || (j == tableHeaders.Length - 1 && yValue > tableHeaders[j])) {
                            valuePercentageChangeFromCeiling = 0;
                        }
                        else {
                            valuePercentageChangeFromCeiling = distanceFromCeilingValue / totalValueGap;
                        }

                        // Calculate the interpolated throttle position using the RPM ceiling
                        float rpmCeilingFinalValueGap = rpmCeilingValueCeiling - rpmCeilingValueFloor;
                        float rpmCeilingFinalValue = rpmCeilingValueCeiling - (rpmCeilingFinalValueGap * valuePercentageChangeFromCeiling);

                        // Calculate the interpolated throttle position using the RPM floor
                        float rpmFloorFinalValueGap = rpmFloorValueCeiling - rpmFloorValueFloor;
                        float rpmFloorFinalValue = rpmFloorValueCeiling - (rpmFloorFinalValueGap * valuePercentageChangeFromCeiling);

                        // Calculate the final interpolated throttle position using the two RPM-based values
                        return rpmCeilingFinalValue - ((rpmCeilingFinalValue - rpmFloorFinalValue) * rpmPercentageChangeFromCeiling);
                    }
                }
            }
        }

        throw new Exception("The fabric of spacetime is unraveling.");
    }
}

