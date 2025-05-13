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
using System.Drawing;

namespace SteganoTool
{
    //This file contains GPU fractal code using the ILGPU library
    internal class GPU : ProcessFractal
    {
        //Declare the constants for the fractal calculations
        private const double Epsilon = 1e-6f;
        private const int MaxIterations = 200_000;
        private const int ChunkSize = 1024;
        private const double xMin = -1.5f, yMin = -1.5f;
        private const double xMax = 1.5f, yMax = 1.5f;

        //Declare the fractal functions and the delegate for them
        private delegate (double zx, double zy) FractalFunction(double zx, double zy, double cReal, double cImag);

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
            //This method is mostly the same as the cpu version' tail handling
            FractalFunction func = fractalChoice switch
            {
                0 => JuliaFunc,
                1 => NewtonFunc,
                2 => NovaFunc,
                _ => JuliaFunc
            };

            /*This method uses a 1D array just like the cpu version, however, it uses the index for x, y instead of just 
             * width and height*/
            int x = index % width;
            int y = index / width;

            if (x >= width || y >= height) 
                return;

            double zx = xMin + (xMax - xMin) * x / (width - 1);
            double zy = yMin + (yMax - yMin) * y / (height - 1);
            double zx2 = zx, zy2 = zy;
            double escapeRadius2 = escapeRadius * escapeRadius;
            int iteration = 0;

            while (iteration <= MaxIterations && (zx2 * zx2 + zy2 * zy2) < escapeRadius2)
            {
                (zx2, zy2) = func(zx2, zy2, cReal, cImag);
                iteration++;
            }

            if (iteration < MaxIterations)
            {
                double mod = XMath.Sqrt(zx2 * zx2 + zy2 * zy2);
                double logZn = XMath.Log(mod) / XMath.Log(2.0f);
                double nu = XMath.Log(logZn) / XMath.Log(2.0f);
                output[index] = iteration + 1 - nu;
            }
            else
            {
                output[index] = MaxIterations;
            }
        }

        internal static void ColorKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> iterations, double maxIt,
            ArrayView1D<int, Stride1D.Dense> palette, ArrayView1D<int, Stride1D.Dense> pixels)
        {
            //This method is also mostly the same as the cpu version
            int paletteLen = (int)palette.Length;
            double norm = maxIt > 0 ? XMath.Pow(iterations[index] / maxIt, 0.7) : 0.0;
            int colorIdx = XMath.Clamp((int)(norm * (paletteLen - 1)) % paletteLen, 0, paletteLen - 1);
            pixels[index] = palette[colorIdx];
        }

        internal static void ReductionKernel(Index1D index, ArrayView1D<double, Stride1D.Dense> input,
            ArrayView1D<double, Stride1D.Dense> partialMax, int length)
        {
            //This method is used to find the maximum value in the input array
            int start = index * ChunkSize;
            int end = XMath.Min(start + ChunkSize, length);
            double maxVal = double.MinValue;
            for (int i = start; i < end; i++)
            {
                maxVal = XMath.Max(maxVal, input[i]);
            }
            partialMax[index] = maxVal;
        }
    }
}
