// Eigen.cpp : Definisce le funzioni esportate per l'applicazione DLL.
//

#include "stdafx.h"
#include <Eigen/Dense>
#define EXPORT_API __declspec(dllexport)
using namespace Eigen;
extern "C" {
	EXPORT_API float* solveSystem(float * A, float * b, const int nrows, const int ncols) {
		MatrixXd coefficientMatrix = MatrixXd::Matrix(nrows, ncols);
		VectorXd rightHandSideVector(nrows);
		VectorXd x;
		float *s = new float[ncols];

		for (int j = 0; j < ncols; j++)
			for (int i = 0; i < nrows; i++)
				coefficientMatrix(i, j) = A[i*ncols + j];

		for (int i = 0; i < nrows; i++)
			rightHandSideVector(i) = b[i];
		x = coefficientMatrix.bdcSvd(ComputeThinU | ComputeThinV).solve(rightHandSideVector);
		for (int i = 0; i < ncols; i++)
			s[i] = x(i);
		return s;
	}

	EXPORT_API int releaseMemory(int* pArray)
	{
		delete[] pArray;
		return 0;
	}
}