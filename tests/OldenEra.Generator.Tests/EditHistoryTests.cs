using OldenEra.Generator.Services;

namespace OldenEra.Generator.Tests;

public class EditHistoryTests
{
    [Fact]
    public void Push_ThenUndo_RestoresPreviousAndArchivesCurrentToRedo()
    {
        var h = new EditHistory<string>();
        h.Push("a");

        Assert.True(h.CanUndo);
        Assert.False(h.CanRedo);

        Assert.True(h.TryUndo(current: "b", out var prev));
        Assert.Equal("a", prev);
        Assert.False(h.CanUndo);
        Assert.True(h.CanRedo);

        Assert.True(h.TryRedo(current: prev, out var next));
        Assert.Equal("b", next);
        Assert.True(h.CanUndo);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void TryUndo_OnEmptyStack_ReturnsFalse()
    {
        var h = new EditHistory<string>();
        Assert.False(h.TryUndo(current: "x", out _));
        Assert.False(h.TryRedo(current: "x", out _));
    }

    [Fact]
    public void Push_DedupesConsecutiveIdenticalSnapshots()
    {
        var h = new EditHistory<string>();
        h.Push("a");
        h.Push("a");
        h.Push("a");

        Assert.Equal(1, h.UndoCount);
    }

    [Fact]
    public void Push_DistinctSnapshots_StackIndependently()
    {
        var h = new EditHistory<string>();
        h.Push("a");
        h.Push("b");
        h.Push("a"); // not consecutive duplicate of last → distinct entry

        Assert.Equal(3, h.UndoCount);
    }

    [Fact]
    public void Push_BeyondCap_DropsOldestEntry()
    {
        var h = new EditHistory<int>(cap: 50);
        for (int i = 0; i < 75; i++)
            h.Push(i);

        Assert.Equal(50, h.UndoCount);

        // Newest entry on top is 74; popping 50 times must reach 25, not 0.
        for (int expected = 74; expected >= 25; expected--)
        {
            Assert.True(h.TryUndo(current: -1, out var v));
            Assert.Equal(expected, v);
        }
        Assert.False(h.CanUndo);
    }

    [Fact]
    public void DefaultCap_IsFifty()
    {
        Assert.Equal(50, EditHistory<int>.DefaultCap);
        var h = new EditHistory<int>();
        Assert.Equal(50, h.Cap);
    }

    [Fact]
    public void Push_AfterUndo_ClearsRedoStack()
    {
        var h = new EditHistory<string>();
        h.Push("a");
        h.TryUndo("b", out _);
        Assert.True(h.CanRedo);

        h.Push("c");
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var h = new EditHistory<string>();
        h.Push("a");
        h.TryUndo("b", out _);
        h.Clear();

        Assert.False(h.CanUndo);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void NonPositiveCap_FallsBackToDefault()
    {
        Assert.Equal(EditHistory<int>.DefaultCap, new EditHistory<int>(cap: 0).Cap);
        Assert.Equal(EditHistory<int>.DefaultCap, new EditHistory<int>(cap: -5).Cap);
    }

    [Fact]
    public void UndoRedoSequence_IsReversible()
    {
        var h = new EditHistory<int>();
        h.Push(1);
        h.Push(2);
        h.Push(3);

        // current state = 4 (live, not yet pushed)
        Assert.True(h.TryUndo(4, out var s)); Assert.Equal(3, s);
        Assert.True(h.TryUndo(s, out s));     Assert.Equal(2, s);
        Assert.True(h.TryUndo(s, out s));     Assert.Equal(1, s);
        Assert.False(h.TryUndo(s, out _));

        Assert.True(h.TryRedo(s, out s));     Assert.Equal(2, s);
        Assert.True(h.TryRedo(s, out s));     Assert.Equal(3, s);
        Assert.True(h.TryRedo(s, out s));     Assert.Equal(4, s);
        Assert.False(h.TryRedo(s, out _));
    }
}
