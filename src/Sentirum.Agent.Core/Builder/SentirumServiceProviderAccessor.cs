using System;
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
/// </remarks>
public static class SentirumServiceProviderAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentProvider = new();

    /// <summary>
    /// Gets the active service provider, or <see langword="null"/> when no
    /// Sentirum factory is on the stack.
    /// </summary>
    public static IServiceProvider? Current => CurrentProvider.Value;

    /// <summary>
    /// Pushes a service provider onto the AsyncLocal scope and returns an
    /// <see cref="IDisposable"/> that restores the previous value when
    /// disposed.
    /// </summary>
    public static IDisposable Push(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var previous = CurrentProvider.Value;
        CurrentProvider.Value = serviceProvider;

        return new Restorer(previous);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly IServiceProvider? _previous;
        private bool _disposed;

        public Restorer(IServiceProvider? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentProvider.Value = _previous;
            _disposed = true;
        }
    }
}
