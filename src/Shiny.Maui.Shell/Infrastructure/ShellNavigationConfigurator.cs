namespace Shiny.Infrastructure;

/// <summary>
/// Holds pre-resolved viewmodel instances queued by
/// <see cref="INavigator.NavigateTo{TViewModel}"/> and
/// <see cref="INavigationBuilder.Navigate"/> so that the apply sites
/// (<c>ShinyRouteFactory.GetOrCreate</c>, <see cref="ShinyShell"/>'s
/// <c>OnNavigated</c>, and <c>ShinyShellNavigator.AppOnPageAppearing</c>)
/// bind the same instance the navigator pinned, instead of resolving a
/// fresh instance from DI on each path.
/// </summary>
/// <remarks>
/// Pinning the viewmodel before <c>Shell.GoToAsync</c> is awaited eliminates
/// the race where the navigator's post-await BindingContext check could fire
/// before <c>Shell.OnNavigated</c> or <c>Application.PageAppearing</c> ran
/// (most visibly on Android cross-Section absolute navigation between
/// <c>ShellContent</c>-declared routes).
///
/// Entries are stored as a FIFO linked-list queue. <see cref="TryConsume"/>
/// pops the first entry whose declared type is assignable from the requested
/// viewmodel type, so interleaved navigations to different viewmodel types
/// don't disturb each other. Same-type rapid navigations stay FIFO.
///
/// <see cref="EnqueueResolved{T}"/> (and the non-generic
/// <see cref="EnqueueResolved(Type, object)"/>) return an
/// <see cref="IDisposable"/> the navigator must dispose after navigation
/// completes — if the entry has not yet been consumed (e.g. the navigation
/// threw before an apply site fired), <see cref="IDisposable.Dispose"/>
/// removes it from the queue so it cannot leak onto a later, unrelated
/// navigation to the same type.
/// </remarks>
public class ShellNavigationConfigurator
{
    readonly Lock sync = new();
    readonly LinkedList<PendingResolved> queue = new();

    /// <summary>
    /// Applies <paramref name="configure"/> to <paramref name="instance"/>
    /// synchronously, then queues the instance for the next apply site that
    /// resolves a viewmodel assignable from <typeparamref name="T"/>.
    /// Dispose the returned handle after the navigation has finished to roll
    /// back the entry if it was never applied.
    /// </summary>
    public IDisposable EnqueueResolved<T>(T instance, Action<T>? configure = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(instance);

        // Configure synchronously so every apply site and every lifecycle
        // hook downstream (OnAppearing, INPC subscribers) observes a fully
        // initialised viewmodel. If the callback throws, nothing is enqueued
        // and the exception propagates to the navigator caller.
        configure?.Invoke(instance);

        return this.EnqueueResolved(typeof(T), instance);
    }

    /// <summary>
    /// Non-generic counterpart to <see cref="EnqueueResolved{T}"/> for code
    /// paths (e.g. <see cref="INavigationBuilder"/>) that hold the viewmodel
    /// type as a runtime <see cref="Type"/>.
    /// </summary>
    public IDisposable EnqueueResolved(Type viewModelType, object instance)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(instance);

        var entry = new PendingResolved(viewModelType, instance);
        LinkedListNode<PendingResolved> node;
        lock (this.sync)
            node = this.queue.AddLast(entry);

        return new Subscription(this, node);
    }

    /// <summary>
    /// Pops and returns the first queued viewmodel whose declared type is
    /// assignable from <paramref name="viewModelType"/>. Returns <c>null</c>
    /// when no entry matches (e.g., the initial-page case where the user
    /// never called <see cref="INavigator.NavigateTo{TViewModel}"/>).
    /// </summary>
    public object? TryConsume(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        lock (this.sync)
        {
            var node = this.queue.First;
            while (node != null)
            {
                if (node.Value.Type.IsAssignableFrom(viewModelType))
                {
                    var instance = node.Value.Instance;
                    this.queue.Remove(node);
                    return instance;
                }
                node = node.Next;
            }
        }
        return null;
    }

    /// <summary>Discards every pending entry.</summary>
    public void Clear()
    {
        lock (this.sync)
            this.queue.Clear();
    }

    record PendingResolved(Type Type, object Instance);

    sealed class Subscription(ShellNavigationConfigurator owner, LinkedListNode<PendingResolved> node) : IDisposable
    {
        bool disposed;

        public void Dispose()
        {
            if (this.disposed)
                return;
            this.disposed = true;

            lock (owner.sync)
            {
                // Node may already have been removed by TryConsume; check
                // List ownership before attempting removal
                // (LinkedListNode.List is null once detached).
                if (node.List == owner.queue)
                    owner.queue.Remove(node);
            }
        }
    }
}
