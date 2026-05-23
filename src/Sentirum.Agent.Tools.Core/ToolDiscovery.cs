using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.AI;

namespace Sentirum.Agent.Tools;

/// <summary>
/// Discovers <see cref="ToolAttribute"/>-decorated methods on a type and
/// turns each one into an <see cref="AIFunction"/> via
/// <see cref="AIFunctionFactory.Create(System.Delegate, AIFunctionFactoryOptions?)"/>.
/// </summary>
public static class ToolDiscovery
{
    /// <summary>
    /// Discovers tools defined on <paramref name="toolsetInstance"/> and
    /// returns them as a sequence of <see cref="AIFunction"/> instances.
    /// </summary>
    public static IEnumerable<AIFunction> Discover(object toolsetInstance)
    {
        ArgumentNullException.ThrowIfNull(toolsetInstance);

        var type = toolsetInstance.GetType();

        const BindingFlags MethodFlags = BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static;

        foreach (var method in type.GetMethods(MethodFlags))
        {
            var attribute = method.GetCustomAttribute<ToolAttribute>(inherit: true);
            if (attribute is null)
            {
                continue;
            }

            yield return ToAIFunction(method, attribute, toolsetInstance);
        }
    }

    private static AIFunction ToAIFunction(
        MethodInfo method,
        ToolAttribute attribute,
        object toolsetInstance)
    {
        ValidateToolMethod(method);

        var resolvedName = attribute.Name ?? StripAsyncSuffix(method.Name);

        var options = new AIFunctionFactoryOptions
        {
            Name = resolvedName,
            Description = attribute.Description,
        };

        // Static methods don't bind to an instance.
        var target = method.IsStatic ? null : toolsetInstance;

        // CreateDelegate over the method bound to the target instance gives
        // AIFunctionFactory a strongly-typed callable so it can generate the
        // JSON schema with full parameter metadata.
        var delegateType = GetDelegateType(method);
        var boundDelegate = method.CreateDelegate(delegateType, target);

        return AIFunctionFactory.Create(boundDelegate, options);
    }

    /// <summary>
    /// Validates that <paramref name="method"/> can be exposed as a tool.
    /// Rejects unsupported signatures with clear messages instead of letting
    /// the reflection layer throw deep inside <c>AIFunctionFactory</c>.
    /// </summary>
    private static void ValidateToolMethod(MethodInfo method)
    {
        if (method.ContainsGenericParameters)
        {
            throw new InvalidOperationException(
                $"Tool method '{method.DeclaringType?.FullName}.{method.Name}' is generic; " +
                "generic tools are not supported. Provide a non-generic wrapper.");
        }

        if (method.ReturnType == typeof(void) && IsAsyncStateMachine(method))
        {
            throw new InvalidOperationException(
                $"Tool method '{method.DeclaringType?.FullName}.{method.Name}' is async void; " +
                "return Task or Task<T> instead.");
        }

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType.IsByRef || parameter.IsOut || parameter.IsIn)
            {
                throw new InvalidOperationException(
                    $"Tool method '{method.DeclaringType?.FullName}.{method.Name}' parameter " +
                    $"'{parameter.Name}' is ref/out/in; tool parameters must be passed by value.");
            }
        }
    }

    private static bool IsAsyncStateMachine(MethodInfo method) =>
        method.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() is not null;

    private static string StripAsyncSuffix(string methodName) =>
        methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > "Async".Length
            ? methodName[..^"Async".Length]
            : methodName;

    private static Type GetDelegateType(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var parameterTypes = new Type[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            parameterTypes[i] = parameters[i].ParameterType;
        }

        if (method.ReturnType == typeof(void))
        {
            return parameters.Length == 0
                ? typeof(Action)
                : Expression.GetActionType(parameterTypes);
        }

        var funcTypes = new Type[parameterTypes.Length + 1];
        Array.Copy(parameterTypes, funcTypes, parameterTypes.Length);
        funcTypes[^1] = method.ReturnType;
        return Expression.GetFuncType(funcTypes);
    }
}
