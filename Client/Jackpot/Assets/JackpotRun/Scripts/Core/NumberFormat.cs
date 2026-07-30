using System.Globalization;

namespace JackpotRun.Core
{
    public static class NumberFormat
    {
        public static string Fmt(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public static string Comma(long v)
        {
            return v.ToString("#,##0", CultureInfo.InvariantCulture);
        }

        public static string Comma(int v)
        {
            return v.ToString("#,##0", CultureInfo.InvariantCulture);
        }
    }
}
