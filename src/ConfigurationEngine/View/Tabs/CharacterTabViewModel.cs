namespace ConfigurationEngine.View.Tabs;

using System.Collections.ObjectModel;
using MonkeyStack.Core.Character;

/// <summary>
/// A „Karakter” fül (view.md, "1. Karakter mérete" és "2. Mozgási animációk"): felül a karakter mérete, alatta
/// mozgásállapotonként egy fix sor, a <see cref="MovementState"/> sorrendjében.
/// </summary>
public sealed class CharacterTabViewModel : TabViewModel
{
    private CharacterSizeViewModel? _size;

    public CharacterTabViewModel(ConfigurationWindowViewModel owner)
        : base(owner)
    {
    }

    public override int TabIndex => ConfigurationWindowViewModel.CharacterTabIndex;

    /// <summary>A karakter mérete; <c>null</c>, amíg nincs betöltött másolat.</summary>
    public CharacterSizeViewModel? Size
    {
        get => _size;
        private set => SetProperty(ref _size, value);
    }

    /// <summary>A mozgási animációk sorai: Idle, Walking, Falling, Dragging.</summary>
    public ObservableCollection<MovementRowViewModel> MovementRows { get; } = [];

    /// <summary>A forrásválasztó lista elemei (a szerkesztett másolat forrásai).</summary>
    public ObservableCollection<SourceOption> SourceOptions => Owner.SourceOptions;

    protected override IEnumerable<EditableItemViewModel> Items =>
        Size is null ? MovementRows : MovementRows.Prepend<EditableItemViewModel>(Size);

    protected override void Rebuild(DataReloadReason reason)
    {
        MovementRows.Clear();
        var data = Owner.Data;
        if (data is null)
        {
            Size = null;
            return;
        }

        Size = new CharacterSizeViewModel(Owner, data.Character);

        // A tároló minden állapothoz ad sort (a hiányzót a szállított alapanimációval), így mindig négy sor van.
        foreach (var state in Enum.GetValues<MovementState>())
        {
            if (data.MovementAnimations.FirstOrDefault(m => m.MovementState == state) is { } movement)
            {
                MovementRows.Add(new MovementRowViewModel(Owner, movement));
            }
        }
    }

    protected override void RefreshPreviews()
    {
        foreach (var row in MovementRows)
        {
            row.RefreshPreview();
        }
    }
}
