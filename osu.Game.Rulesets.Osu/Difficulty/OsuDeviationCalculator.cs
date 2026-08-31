using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty
{
    /// <summary>
    /// Calculates the estimated tapping deviation from the proportion of 100s assuming that the player taps follow a contaminated normal distribution
    /// This distribution can be written as: w * N(0, sigma^2) + (1-w) * N(0, k^2*sigma^2)
    /// We can't find sigma from this distribution in a closed form solution, so rootfinding must be used
    /// </summary>
    static class OsuDeviationCalculator
    {
        /// <summary>
        /// 1 / sqrt(pi)
        /// </summary>
        const double gauss_norm_const = 0.56418958354;
        static double k = 2;
        static double w = 0.5;
        /// <summary>
        /// Initial guess for newtonian method. Most players are around 90ish UR, so a u (1 / sigma) of 9 is probably not too far
        /// </summary>
        const double initial_guess = 1 / 9;
        /// <summary>
        /// How incorrect can we accept
        /// </summary>
        static double precision = 0.01;
        /// <summary>
        /// There is no closed form solution for sigma with a contaminated distribution, even using erfinv
        /// The equation is slightly easier if instead of solving for sigma we solve for u = 1/sigma, which gives the equation:
        /// f(u) = w * erf(z * u / sqrt(2)) + (1-w) * erf(z * u / (k * sqrt(2)) - (2p - 1)
        static double q;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="n300">How many relevant Greats the player got on accuracy objects. This (and all the other n variables) can, and should, be rescaled to avoid possible 0 cases and to account for slider accuracy</param>
        /// <param name="n100">as above but for Goods</param>
        /// <param name="n50">as above but for Mehs</param>
        /// <param name="n0">as above but for Misses</param>
        /// <param name="z">The size of the 300 hitwindow</param>
        /// <returns></returns>
        public static double CalculateDeviation(double n300, double n100, double n50, double n0, double z)
        {
            // probability of getting a 300
            double p300 = n300 / (n300 + n100 + n50 + n0);
            // check 
            if (p300 == 1)
                return 0;
            if (p300 == 0)
                return Double.PositiveInfinity;
            // Halve the hitwindow and probability to work with P(X < z) rather than P(-z < X < z) to make the maths a little nicer
            z /= 2;
            p300 /= 2;
            double u = initial_guess;
            double y = deviation_equation(u, z, p300);
            // Newtonian method to solve the equation for u
            while (y > precision)
            {
                u -= distribution_derivative(u, v) / y;
                y = deviation_equation(u, z, p300);
            }
            // sigma = 1 / u
            return 1 / u;
        }
        /// <summary>
        /// With the contamination, eUR is no longer just 10 times deviation.
        /// This is not directly useful, but is helpful for testing
        /// </summary>
        /// <param name="sigma">tapping deviation</param>
        /// <returns>The estimated unstable rate of a player with deviation sigma</returns>
        public static double EstimatedUnstableRate(double sigma)
        {
            return w * sigma + (1-w) * k * sigma;
        }
        /// <summary>
        /// value of the normal distribution at some value x. Required for the gradient descent
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static double normal(double x)
        {
            return gauss_norm_const * Math.Exp( - x*x);
        }
        /// <summary>
        /// Closed form derivative of f(u) = w * erf(z * u / sqrt(2)) + (1-w) * erf(z * u / (k * sqrt(2))) - (2p - 1)
        /// Define g(x) as normal(x)
        /// f'(u) = w * (z / sqrt(2)) * g(z * u / sqrt(2)) + (1-w) * (z / (k * sqrt(2))) * g(z * u / (k * sqrt(2)))
        /// </summary>
        /// <param name="u"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        private static double distribution_derivative(double u, double z)
        {
            double x = z * u / sqrt(2);
            return 2 * w * (z / sqrt(2)) * normal(x) + 2 * (1-w) * (z / (k * sqrt(2))) * normal(x / k);
        }

        /// <summary>
        /// The function that we rootfind to solve for our sigma value
        /// </summary>
        /// <param name="u">1 / sigma</param>
        /// <param name="z">half of the great hitwindow</param>
        /// <param name="p">half of the probability of a 300</param>
        /// <returns></returns>
        private static double deviation_equation(double u, double z, double p)
        {
            double x = z * u / sqrt(2);
            return w * DiffUtils.Erf(x) + (1-w) * Erf(x / k) - 2 * p + 1;
        }
    }
}