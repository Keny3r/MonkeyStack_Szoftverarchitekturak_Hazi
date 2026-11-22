namespace ConfigurationEngine.View.Tabs;

using System.Collections.ObjectModel;
using MonkeyStack.Core.Events;

/// <summary>
/// A „Mintavételezés” fül (gui.md, "5. Mintavételezés"): minden <see cref="Metric"/> értékhez pontosan egy, fix sor;
/// sort hozzáadni vagy törölni nem lehet. A módosítások Mentéskor tárolódnak; az OS esemény értesítő motor ezután a
/// <c>PollingSettingsChanged</c> eseményből értesül róluk, a View vele közvetlenül nem kommunikál.
/// </summary>
public sealed class PollingTabViewModel : TabViewModel
{
    public PollingTabViewModel(ConfigurationWindowViewModel owner)
        : base(owner)
    {
        ResetDefaultsCommand = new RelayCommand(ResetDefaults, () => Rows.Count > 0);
    }

    public override int TabIndex => ConfigurationWindowViewModel.PollingTabIndex;

    /// <summary>A metrikák sorai, a <see cref="Metric"/> sorrendjében.</summary>
    public ObservableCollection<PollingRowViewModel> Rows { get; } = [];

    /// <summary>„Alapértékek visszaállítása”: minden sor a séma alapértékeire a szerkesztett másolatban.</summary>
    public RelayCommand ResetDefaultsCommand { get; }

    protected override IEnumerable<EditableItemViewModel> Items => Rows;

    protected override void Rebuild(DataReloadReason reason)
    {
        Rows.Clear();
        if (Owner.Data is { } data)
        {
            // A tároló minden metrikához ad sort (a hiányzót alapértékekkel), így mindig hét sor van.
            foreach (var metric in Enum.GetValues<Metric>())
            {
                if (data.MetricPollings.FirstOrDefault(p => p.Metric == metric) is { } polling)
                {
                    Rows.Add(new PollingRowViewModel(Owner, polling));
                }
            }
        }

        ResetDefaultsCommand.RaiseCanExecuteChanged();
    }

    protected override void RefreshPreviews()
    {
        foreach (var row in Rows)
        {
            row.RefreshSummary();
        }
    }

    private void ResetDefaults()
    {
        foreach (var row in Rows)
        {
            row.ResetToDefaults();
        }

        Owner.NotifyEdited();
    }
}
