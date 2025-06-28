namespace Juice.Utils
{
    public class TimeUtils
    {
        /// <summary>
        /// Return delay time in millisecond based on attempts
        /// </summary>
        /// <param name="attempts"></param>
        /// <param name="maxtimeOut"></param>
        /// <returns></returns>
        public static int NextDelayMs(int attempts, int maxtimeOut = 30000)
        {
            var rand = new Random();
            var baseDelay = (int)Math.Pow(2, attempts) * 1000;
            var jitter = rand.Next(0, 1000);
            return Math.Min(baseDelay + jitter, maxtimeOut); // cap at 30s
        }
    }
}
