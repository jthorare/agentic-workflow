using System.Reflection;

namespace Aw.Architecture.Tests.Rules;

/// <summary>One call site in a method body.</summary>
/// <param name="Caller">The method whose body holds the call.</param>
/// <param name="Callee">The method called or constructor constructed.</param>
/// <param name="ConstantFalseArgument">The instruction before the call pushes the constant 0, so a bool last argument is false.</param>
public sealed record IlCall(MethodBase Caller, MethodBase Callee, bool ConstantFalseArgument);
