namespace YARG.Core
{
    public static class YargMath
    {
        #region Lerp
        public static int Lerp(int start, int end, float percent)
        {
            return (int) (start + (end - start) * percent);
        }

        public static int Lerp(int start, int end, double percent)
        {
            return (int) (start + (end - start) * percent);
        }

        public static uint Lerp(uint start, uint end, float percent)
        {
            return (uint) (start + (end - start) * percent);
        }

        public static uint Lerp(uint start, uint end, double percent)
        {
            return (uint) (start + (end - start) * percent);
        }

        public static float Lerp(float start, float end, float percent)
        {
            return (float) (start + (end - start) * percent);
        }

        public static float Lerp(float start, float end, double percent)
        {
            return (float) (start + (end - start) * percent);
        }

        public static double Lerp(double start, double end, float percent)
        {
            return start + (end - start) * percent;
        }

        public static double Lerp(double start, double end, double percent)
        {
            return start + (end - start) * percent;
        }
        #endregion

        #region Lerp with range
        public static int Lerp(int valueStart, int valueEnd, float rangeStart, float rangeEnd, float rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static int Lerp(int valueStart, int valueEnd, double rangeStart, double rangeEnd, double rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static uint Lerp(uint valueStart, uint valueEnd, float rangeStart, float rangeEnd, float rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static uint Lerp(uint valueStart, uint valueEnd, double rangeStart, double rangeEnd, double rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static float Lerp(float valueStart, float valueEnd, float rangeStart, float rangeEnd, float rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static float Lerp(float valueStart, float valueEnd, double rangeStart, double rangeEnd, double rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static double Lerp(double valueStart, double valueEnd, float rangeStart, float rangeEnd, float rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }

        public static double Lerp(double valueStart, double valueEnd, double rangeStart, double rangeEnd, double rangeTarget)
        {
            double percent = (rangeTarget - rangeStart) / (rangeEnd - rangeStart);
            return Lerp(valueStart, valueEnd, percent);
        }
        #endregion
    }
}