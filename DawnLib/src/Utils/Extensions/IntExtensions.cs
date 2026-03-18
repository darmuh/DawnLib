using System;
using System.Collections.Generic;
using System.Text;

namespace Dawn.Utils;
public static class IntExtensions
{
    /// <summary>
    /// An alternative to clamping that will cycle a value in a looped range rather than always returning the minimum or maximum value.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns>The next int in a looped range</returns>
    /// <remarks>
    /// A value larger than the maximum will be set to the minimum value. A value below the minimum will be set to the maximum value.
    /// </remarks>
    public static int Cycle(this int value, int min, int max)
    {
        if (value < min)
            return max;

        if (value > max)
            return min;

        return value;
    }
}
