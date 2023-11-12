using System;
using System.Collections.Generic;
using System.Linq;

namespace TextParse.Commands
{
    public static class EnumHelper<TEnum> where TEnum : struct
    {
        /// <summary>
        /// Returns a list that contains the values of the constants in enumType.
        /// </summary>
        /// <returns>List of constants in enumTypes</returns>
        public static IList<TEnum> GetValues(bool limitFlagEnumsToPrimaryBinary = false)
        {
            List<TEnum> list = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToList();

            if (!limitFlagEnumsToPrimaryBinary)
            {
                return list;
            }

            List<TEnum> filteredList = new List<TEnum>();

            foreach (TEnum enumValue in list)
            {
                int bits = Convert.ToInt32(enumValue);

                if ((bits & (bits - 1)) == 0)
                {
                    filteredList.Add(enumValue);
                }
            }

            return filteredList;
        }

        /// <summary>
        /// Test a Flag Enum to determine whether it is a primary binary
        /// </summary>
        /// <param name="value">Value to test</param>
        /// <returns>True if value is primary</returns>
        public static bool IsPrimaryBinary(TEnum value)
        {
            IList<TEnum> primaryList = GetValues(true);

            return primaryList.Contains(value);
        }

        public static TEnum Parse(string value)
        {
            return (TEnum) Enum.Parse(typeof(TEnum), value, true);
        }
    }
}