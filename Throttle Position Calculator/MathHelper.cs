using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Throttle_Position_Calculator;
public static class MathHelper
{
    public const float MAX_REQUESTED_TORQUE = 320F;

    public const float TANH_DIVISOR = 33F;
    public const float TANH_MULTIPLIER = 2.2F;

    public static SortedList<float, float> EngineTorque {
        get {
            return new() {
                { 0F, 0F },
                { 800F, 90F },
                { 1200F, 130F },
                { 1600F, 150F },
                { 2000F, 160F },
                { 2400F, 180F },
                { 2800F, 220F },
                { 3200F, 260F },
                { 3600F, 310F },
                { 4000F, 325F },
                { 4400F, 337F },
                { 4800F, 331F },
                { 5200F, 319F },
                { 5600F, 300F },
                { 6000F, 280F },
                { 6400F, 260F }
            };
        }
    }

    /// <summary>
    /// Gets the Throttle Plate Opening Angle for a given <paramref name="yValue"/> at a specified <paramref name="rpm"/>.
    /// </summary>
    /// <param name="yValue"></param>
    /// <param name="values"></param>
    /// <param name="rpm"></param>
    /// <returns></returns>
    [Pure]
    public static float LookupValueInTable(this string[] values, float rpm, float yValue) {
        if(rpm == 0 || yValue == 0) {
            return 0;
        }

        float[] tableHeaders = Array.ConvertAll(values[0].Split(','), theValue => string.IsNullOrWhiteSpace(theValue) ? -1 : float.Parse(theValue));
        float[] rpmList = values.Select(line => float.Parse(string.IsNullOrWhiteSpace(line.Split(',')[0]) ? "-1" : line.Split(',')[0])).ToArray();
        
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

    /// <summary>
    /// Looks up the corresponding value based on the RPM and interpolates if necessary.
    /// </summary>
    /// <param name="rpm"></param>
    /// <returns></returns>
    public static float LookupValueInList(this SortedList<float, float> list, float rpm) {
        for(int i = 0; i < list.Count; i++) {
            if(list.Keys[i] >= rpm) {
                if(list.Keys[i] == rpm) {
                    return list.Values[i];
                }
                else {
                    if(i == 0) {
                        return list.Values[0];
                    }
                    else {
                        float floorRpm = list.Keys[i - 1];
                        float ceilingRpm = list.Keys[i];
                        float rpmGap = ceilingRpm - floorRpm;

                        float rpmDistanceFromCeiling = ceilingRpm - rpm;
                        float rpmPercentageChange = rpmDistanceFromCeiling / rpmGap;

                        float valueGap = list.Values[i] - list.Values[i - 1];
                        return list.Values[i] - ((valueGap) * rpmPercentageChange);
                    }
                }
            }
        }

        return list.Values[list.Count - 1];
    }

    /// <summary>
    /// Checks whether two <see cref="float"/> values are within a certain <paramref name="leeway"/> of each other.
    /// </summary>
    /// <param name="float1"></param>
    /// <param name="float2"></param>
    /// <param name="leeway"></param>
    /// <returns></returns>
    [Pure]
    public static bool IsAround(this float float1, float float2, float leeway = 0.5F) {
        return Math.Abs(float1 - float2) < leeway;
    }
    
    /// <summary>
    /// Gets rid of empty (based on <c>.ToString</c>) and null elements and returns a new array.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    /// <returns></returns>
    [Pure]
    public static T[] RemoveEmpty<T>(this T[] array)
    {
        List<T> shortenedList = [];

        for(int i = 0; i < array.Length; i++)
        {
            if(array[i]?.ToString()?.Length > 0)
            {
                shortenedList.Add(array[i]);
            }
        }

        return shortenedList.ToArray();
    }

    /// <summary>
    /// Removes the <c>[Selection3D]</c> nonsense if it is present.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    /// <returns></returns>
    public static T[] RemoveRomRaiderNonsense<T>(this T[] array) {
        List<T> shortenedList = [];

        for(int i = 0; i < array.Length; i++) {
            if((array[i]?.ToString() ?? string.Empty).Contains("[Selection3D]")) {
                continue;
            }
            else {
                shortenedList.Add(array[i]);
            }
        }

        return shortenedList.ToArray();
    }
}

