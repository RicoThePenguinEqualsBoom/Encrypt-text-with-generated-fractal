using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SteganoTool
{
    internal class NewtonPoly
    {
        public int Degree { get; }
        public Complex[] Coefficients { get; }
        public Complex[] Roots { get; }

        internal NewtonPoly(int degree, Complex[] coefficients, Complex[] roots)
        {
            Degree = degree;
            Coefficients = coefficients;
            Roots = roots;
        }

        internal Complex Evaluate(Complex z)
        {
            Complex sum = Complex.Zero;
            for (int i = 0; i < Coefficients.Length; i++)
                sum += Coefficients[i] * Complex.Pow(z, Degree - i);

            return sum;
        }

        internal Complex Derivative(Complex z)
        {
            Complex sum = Complex.Zero;
            for (int i = 0; i < Coefficients.Length - 1; i++)
            {
                int power = Degree - i;
                sum += power * Coefficients[i] * Complex.Pow(z, power - 1);
            }

            return sum;
        }

        public static NewtonPoly Generate()
        {
            var rng = new Random();
            int degree = RandomNumberGenerator.GetInt32(3, 6);
            Complex[] roots = new Complex[degree];
            for (int i = 0; i < degree; i++)
            {
                double angle = rng.NextDouble() * 2 * Math.PI;
                double radius = 0.5 + rng.NextDouble();
                roots[i] = Complex.FromPolarCoordinates(radius, angle);
            }

            Complex[] coefficients = new Complex[degree + 1];
            coefficients[0] = Complex.One;
            for (int i = 1; i <= degree; i++)
            {
                coefficients[i] = Complex.Zero;
                foreach (var combo in GetRootCombinations(roots, i))
                {
                    Complex product = Complex.One;
                    foreach (var r in combo)
                        product *= -r;
                    coefficients[i] += product;
                }
            }

            return new NewtonPoly(degree, coefficients, roots);
        }

        private static IEnumerable<IEnumerable<Complex>> GetRootCombinations(Complex[] roots, int k)
        {
            int n = roots.Length;
            int[] indices = new int[k];
            for (int i = 0; i < k; i++)
                indices[i] = i;

            while (indices[0] <= n - k)
            {
                yield return indices.Select(idx => roots[idx]);
                int t = k - 1;
                while (t != 0 && indices[t] == n - k + t) t--;
                indices[t]++;
                for (int i = t + 1; i < k; i++) indices[i] = indices[i - 1] + 1;
                if (indices[0] > n - k) break;
            }
        }
    }
}
