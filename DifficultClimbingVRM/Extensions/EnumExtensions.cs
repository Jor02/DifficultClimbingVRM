using System;
using System.Linq;

namespace DifficultClimbingVRM.Extensions
{
    internal static class EnumExtensions
    {
        public static bool IsOneOf<T>(this T enumeration, params T[] enums) where T : Enum
        {
            return enums.Contains(enumeration);
        }
    }
}
