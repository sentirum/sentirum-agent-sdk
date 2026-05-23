using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Sentirum.Agent.Builder;

/// <summary>
/// AsyncLocal carrier that surfaces the active <see cref="IServiceProvider"/>
/// to deferred builder hooks. Set by <see cref="SentirumAgentFactory"/> while
/// it materializes an <see cref="ISentirumAgent"/>; cleared on the way out.
/// </summary>
/// <remarks>
/// <para>
/// Extensions that need to resolve services during the options pipeline
/// (most notably <c>WithTools&lt;T&gt;()</c> in
/// <c>Sentirum.Agent.Tools.Core</c>) read the provider from
/// <see cref="Current"/>. Anything that runs outside the factory scope
/// will observe <see langword="null"/>.
/// </para>
/// <para>
/// The implementation uses a <see cref="ConcurrentStack{T}"/> per async
/// local so nested Push/Dispose pairs and concurrent factory calls in
/// different contexts do not clobber each other.
/// </para>
/// </remarks>
public static class SentirumServiceProviderAccessor
{
    private static readonly AsyncLocal<ConcurrentStack<IServiceProvider>> s_stack = new();

    /// <summary>
    /// Gets the active service provider, or <see langword="null"/> when no
    /// Sentirum factory is on the stack.
    /// </summary>
    public static IServiceProvider? Current
    {
        get
        {
            var stack = s_stack.Value;
            return stack is not null && stack.TryPeek(out var provider)
                ? provider
                : null;
        }
    }

    /// <summary>
    /// Pushes a service provider onto the AsyncLocal scope and returns an
    /// <see cref="IDisposable"/> that pops it when disposed.
    /// </summary>
    public static IDisposable Push(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var stack = s_stack.Value ??= new ConcurrentStack<IServiceProvider>();
        stack.Push(serviceProvider);

        return new Popper();
    }

    private sealed class Popper : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var stack = s_stack.Value;
            stack?.TryPop(out _);
            _disposed = true;
        }
    }
}
