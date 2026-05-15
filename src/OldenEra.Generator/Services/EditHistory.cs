using System.Collections.Generic;

namespace OldenEra.Generator.Services
{
    /// <summary>
    /// Bounded undo / redo stack for arbitrary snapshot values (T-802).
    /// <para>
    /// Hosts capture a snapshot of <c>SettingsFile</c> after every change
    /// event (the same point that already triggers validate/persist) and
    /// hand it to <see cref="Push"/>. Consecutive identical snapshots are
    /// deduplicated so a textbox burst that re-emits the same string does
    /// not flood the stack. The undo stack is capped at <see cref="Cap"/>;
    /// pushing beyond the cap drops the oldest entry.
    /// </para>
    /// <para>
    /// The class is generic and storage-agnostic — callers typically use
    /// <c>EditHistory&lt;string&gt;</c> with a serialized JSON payload so the
    /// snapshot is immutable and trivially comparable. This matches the
    /// acceptance criterion that snapshot serialization is identical to
    /// the in-memory clone path.
    /// </para>
    /// </summary>
    public sealed class EditHistory<T>
    {
        public const int DefaultCap = 50;

        private readonly LinkedList<T> _undo = new();
        private readonly Stack<T> _redo = new();
        private readonly IEqualityComparer<T> _comparer;

        public int Cap { get; }

        public EditHistory(int cap = DefaultCap, IEqualityComparer<T>? comparer = null)
        {
            Cap = cap > 0 ? cap : DefaultCap;
            _comparer = comparer ?? EqualityComparer<T>.Default;
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        /// <summary>
        /// Push a new prior-state snapshot onto the undo stack and clear
        /// any pending redo entries (a fresh edit invalidates the redo
        /// branch). Consecutive duplicates are coalesced.
        /// </summary>
        public void Push(T snapshot)
        {
            if (_undo.Count > 0 && _comparer.Equals(_undo.Last!.Value, snapshot))
            {
                // Dedupe consecutive identical states.
                _redo.Clear();
                return;
            }

            _undo.AddLast(snapshot);
            while (_undo.Count > Cap)
                _undo.RemoveFirst();

            _redo.Clear();
        }

        /// <summary>
        /// Pop the most recent undo snapshot, archiving <paramref name="current"/>
        /// onto the redo stack so it can be re-applied via <see cref="TryRedo"/>.
        /// </summary>
        public bool TryUndo(T current, out T previous)
        {
            if (_undo.Count == 0)
            {
                previous = default!;
                return false;
            }

            previous = _undo.Last!.Value;
            _undo.RemoveLast();
            _redo.Push(current);
            return true;
        }

        /// <summary>
        /// Pop the most recent redo snapshot, archiving <paramref name="current"/>
        /// back onto the undo stack.
        /// </summary>
        public bool TryRedo(T current, out T next)
        {
            if (_redo.Count == 0)
            {
                next = default!;
                return false;
            }

            next = _redo.Pop();
            _undo.AddLast(current);
            while (_undo.Count > Cap)
                _undo.RemoveFirst();
            return true;
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }
    }
}
