namespace Shiny.Infrastructure;

/// <summary>
/// Holds pending viewmodel-configuration callbacks supplied by
/// <see cref="INavigator.NavigateTo{TViewModel}"/> so that property values can be
/// applied to the resolved viewmodel BEFORE
/// <see cref="IPageLifecycleAware.OnAppearing"/> fires.
/// </summary>
/// <remarks>
/// Entries are stored as a single linked-list queue. <see cref="TryApply"/>
/// consumes the first entry whose declared type is assignable from the
/// viewmodel being bound, which makes interleaved navigations to different
/// viewmodel types safe — each type pops its own FIFO entry without disturbing
/// the others. Same-type rapid navigations stay FIFO, which produces the same
/// observable state per page since each viewmodel instance is configured exactly
/// once with one of the queued actions.
///
/// <see cref="Enqueue"/> returns an <see cref="IDisposable"/> the navigator
/// must dispose after the navigation completes — if the entry has not yet been
/// applied (e.g. the navigation threw), <see cref="IDisposable.Dispose"/>
/// removes it from the queue so it cannot leak onto a later, unrelated
/// navigation to the same type.
/// </remarks>
public class ShellNavigationConfigurator
{
    readonly Lock sync = new();
    readonly LinkedList<PendingConfigure> queue = new();

    /// <summary>
    /// Enqueues a configuration callback for the next viewmodel of type
    /// <typeparamref name="T"/> bound by Shiny's navigation pipeline.
    /// Dispose the returned handle once the navigation has finished to roll
    /// back the entry if it was never applied.
    /// </summary>
    public IDisposable Enqueue<T>(Action<T> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var entry = new PendingConfigure(typeof(T), obj => configure((T)obj));
        LinkedListNode<PendingConfigure> node;
        lock (this.sync)
            node = this.queue.AddLast(entry);

        return new Subscription(this, node);
    }

    /// <summary>
    /// Applies the first queued callback whose declared type is assignable
    /// from <paramref name="viewModel"/>. Returns <c>true</c> when a callback
    /// was applied and removed from the queue.
    /// </summary>
    public bool TryApply(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Action<object>? toApply = null;

        lock (this.sync)
        {
            var node = this.queue.First;
            while (node != null)
            {
                if (node.Value.Type.IsInstanceOfType(viewModel))
                {
                    toApply = node.Value.Apply;
                    this.queue.Remove(node);
                    break;
                }
                node = node.Next;
            }
        }

        toApply?.Invoke(viewModel);
        return toApply != null;
    }

    /// <summary>Discards every pending callback.</summary>
    public void Clear()
    {
        lock (this.sync)
            this.queue.Clear();
    }

    record PendingConfigure(Type Type, Action<object> Apply);

    sealed class Subscription(ShellNavigationConfigurator owner, LinkedListNode<PendingConfigure> node) : IDisposable
    {
        bool disposed;

        public void Dispose()
        {
            if (this.disposed)
                return;
            this.disposed = true;

            lock (owner.sync)
            {
                // Node may already have been removed by TryApply; check List
                // ownership before attempting removal (LinkedListNode.List is
                // null once detached).
                if (node.List == owner.queue)
                    owner.queue.Remove(node);
            }
        }
    }
}
