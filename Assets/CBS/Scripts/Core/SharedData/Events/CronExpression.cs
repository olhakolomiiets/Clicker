using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class CronExpression
{
    public const string EVERY_HOUR = "0 * * * *";
    public const string EVERY_3_HOUR = "0 */3 * * *";
    public const string EVERY_6_HOUR = "0 */6 * * *";
    public const string EVERY_12_HOUR = "0 */12 * * *";
    public const string EVERY_DAY_AT_12_00_AM = "0 0 * * *";
    public const string EVERY_DAY_AT_2_00_AM = "0 2 * * *";
    public const string AT_12_00_AM_ONLY_ON_FRIDAY = "0 0 * * FRI";
    public const string AT_12_00_AM_ONLY_ON_SUNDAY = "0 0 * * SUN";
    public const string AT_12_00_AM_ONLY_ON_MONDAY = "0 0 * * MON";
    public const string AT_12_00_AM_ONLY_ON_THURSDAY = "0 0 * * THU";
    public const string AT_12_00_AM_ONLY_ON_WEDNESDAY = "0 0 * * WED";
    public const string AT_12_00_AM_ONLY_ON_TUESDAY = "0 0 * * TUE";
    public const string AT_12_00_AM_ONLY_ON_SATURDAY = "0 0 * * SAT";
    public const string AT_12_00_EVERY_DAY_FROM_MONDAY_TO_FRIDAY = "0 0 * * 1-5";
    public const string AT_12_00_EVERY_SATURDAY_AND_SUNDAY = "0 0 * * 6,0";
    public const string AT_12_00_AM_ON_DAY_1_OF_THE_MONTH = "0 0 1 * *";
    public const string EVERY_QUARTER = "0 0 1 */3 *";
    public const string EVERY_YEAR = "0 0 1 1 *";

    public static List<string> AllCronsTitle
    {
        get
        {
            return typeof(CronExpression).GetAllPublicConstantNames<string>();
        }
    }

    public static List<string> AllCronsValues
    {
        get
        {
            return typeof(CronExpression).GetAllPublicConstantValues<string>();
        }
    }

    private static List<string> GetAllPublicConstantNames<T>(this Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T))
            .Select(x => x.Name)
            .ToList();
    }

    private static List<string> GetAllPublicConstantValues<T>(this Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(T))
            .Select(x => (string)x.GetRawConstantValue())
            .ToList();
    }
}
