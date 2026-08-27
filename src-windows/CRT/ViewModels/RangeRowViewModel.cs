using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CRT.ViewModels;

/// <summary>One inline-editable row in the loads/segments sidebar.</summary>
public sealed partial class RangeRowViewModel : ObservableObject
{
    private readonly SessionViewModel _parent;

    public RangeRowViewModel(SessionViewModel parent, int index, string title, string durationText, int startFrame, int endFrame)
    {
        _parent = parent;
        Index = index;
        Title = title;
        DurationText = durationText;
        _startText = startFrame.ToString();
        _endText = endFrame.ToString();
    }

    public int Index { get; }

    /// <summary>"Load N" / "Segment N".</summary>
    public string Title { get; }

    /// <summary>ISO duration chip text.</summary>
    public string DurationText { get; }

    /// <summary>Caption for the per-field Paste buttons on the segment rows.</summary>
    public string PasteLabel => CRT.Services.AppServices.Loc["Paste"];

    [ObservableProperty]
    private string _startText;

    [ObservableProperty]
    private string _endText;

    [RelayCommand]
    private void Delete() => _parent.DeleteRow(Index);

    [RelayCommand]
    private Task PasteStartAsync() => _parent.PasteRowFieldAsync(Index, start: true);

    [RelayCommand]
    private Task PasteEndAsync() => _parent.PasteRowFieldAsync(Index, start: false);

    /// <summary>Commits the inline edit (Enter / focus loss).</summary>
    public void Commit() => _parent.CommitRow(Index, StartText, EndText);
}
