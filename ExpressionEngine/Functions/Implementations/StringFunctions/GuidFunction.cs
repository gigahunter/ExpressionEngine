﻿using System;
using System.Linq;
using ExpressionEngine;
using ExpressionEngine.Functions.Base;
using ExpressionEngine.Functions.CustomException;
using ExpressionEngine.Functions.Implementations.StringFunctions;

namespace Parser.ExpressionParser.Functions.Implementations.StringFunctions
{
    public class GuidFunction : Function
    {
        public GuidFunction() : base("guid")
        {
        }

        public override ValueContainer ExecuteFunction(params ValueContainer[] parameters)
        {
            if (parameters.Length > 1)
            {
                throw new ArgumentError("Too many arguments");
            }

            if (parameters.Length < 1) return new ValueContainer(Guid.NewGuid().ToString("D"));

            var format = AuxiliaryMethods.VcIsString(parameters[0]);
            if (!new[] {"n", "d", "b", "p", "x"}.Contains(format.ToLower()))
            {
                throw new PowerAutomateMockUpException($"The given format, {format}, is not recognized.");
            }

            return new ValueContainer(Guid.NewGuid().ToString(format.ToUpper()));
        }
    }
}