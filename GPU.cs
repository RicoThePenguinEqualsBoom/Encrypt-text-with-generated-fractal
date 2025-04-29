using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;
using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.OpenCL;
using ILGPU.Runtime.CPU;

namespace SteganoTool
{
    internal class GPU : ProcessFractal
    {
        private const double Epsilon = 1e-6;
        private const int MaxIterations = 200_000;
        private const double xMin = -1.5, yMin = -1.5;
        private const double xMax = 1.5, yMax = 1.5;

        private static (double zx, double zy) NewtonFunc(double zx, double zy, double cReal, double cImag)
        {
            return (zx, zy);
        }

        private static (double zx, double zy) NovaFunc(double zx, double zy, double cReal, double cImag)
        {
            return (zx, zy);
        }

        private static (double zx, double zy) JuliaFunc(double zx, double zy, double cReal, double cImag)
        {
            double newZx = zx * zx - zy * zy + cReal;
            double newZy = 2 * zx * zy + cImag;
            return (newZx, newZy);
        }

        internal static void JuliaKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> output, double cReal, double cImag,
            double escapeRadius, int width, int height, int fractalChoice)
        {
            if (index >= width * height) return;

            int x = index % width;
            int y = index / width;

            double zx = xMin + (xMax - xMin) * x / (width - 1);
            double zy = yMin + (yMax - yMin) * y / (height - 1);
            double zx2 = zx, zy2 = zy;
            int iteration = 0;


            while (iteration <= MaxIterations && (zx2 * zx2 + zy2 * zy2) < escapeRadius * escapeRadius)
            {
                (zx2, zy2) = JuliaFunc(zx2, zy2, cReal, cImag);
                iteration++;
            }

            if (iteration < MaxIterations)
            {
                double mod = Math.Sqrt(zx2 * zx2 + zy2 * zy2);
                double logZn = Math.Log(mod) / Math.Log(2);
                double nu = Math.Log(logZn) / Math.Log(2);
                output[index] = iteration + 1 - nu;
            }
            else
            {
                output[index] = MaxIterations;
            }
        }

        static void ReductionKernel(ArrayView2D<double, Stride2D.DenseY> input, ArrayView<double> output, int width, int height)
        {
            int y = Grid.IdxX;
            if (y >= height) return;
            double max = double.MinValue;
            for (int x = 0; x < width; x++)
            {
                double val = input[x, y];
                max = XMath.Max(max, val);
            }
            output[y] = max;
        }
    }
}
