namespace ConfigurationEngine.View;

using ConfigurationEngine.Validation;

/// <summary>
/// A fülek nézetmodelljeinek közös alapja: a szerkesztett másolat lecserélésekor újraépíti a sorokat, minden
/// módosítás után kiosztja a hibákat a soroknak és frissíti az előnézeteket, és az „N hiba” feliratra kattintáskor
/// kijelöli a hibás sort és a hibás mezőre viszi a fókuszt (view.md, "Hibajelzés" és "Élő előnézet").
/// </summary>
public abstract class TabViewModel : ObservableObject
{
    protected TabViewModel(ConfigurationWindowViewModel owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        owner.DataReloaded += (_, e) =>
        {
            Rebuild(e.Reason);
            ApplyErrors(owner.Errors);
            RefreshPreviews();
        };
        owner.ErrorsUpdated += (_, _) => ApplyErrors(owner.Errors);
        owner.Edited += (_, _) => RefreshPreviews();
        owner.ErrorNavigationRequested += (_, error) =>
        {
            if (ConfigurationWindowViewModel.TabOf(error.Kind) == TabIndex)
            {
                Navigate(error);
            }
        };
    }

    /// <summary>A fül indexe (<see cref="ConfigurationWindowViewModel.CharacterTabIndex"/>, …).</summary>
    public abstract int TabIndex { get; }

    protected ConfigurationWindowViewModel Owner { get; }

    /// <summary>A fül összes szerkeszthető eleme.</summary>
    protected abstract IEnumerable<EditableItemViewModel> Items { get; }

    /// <summary>
    /// A sorok újraépítése a <see cref="ConfigurationWindowViewModel.Data"/>-ból. Mentés után az új sorok előnézetei
    /// átveszik a régiekét (a <see cref="PreviewManager"/> intézi), Elvetés után újak készülnek.
    /// </summary>
    protected abstract void Rebuild(DataReloadReason reason);

    /// <summary>Minden módosítás után: a sorok előnézetei a szerkesztett forrást és sebességet mutassák.</summary>
    protected virtual void RefreshPreviews()
    {
    }

    /// <summary>A hibás elem kijelölése (lista + részletek elrendezésű füleken).</summary>
    protected virtual void Select(EditableItemViewModel item)
    {
    }

    private void ApplyErrors(IReadOnlyList<ValidationError> errors)
    {
        // Az összes hiba (beviteli, validátor, média) egy ValidationResult-ban, hogy a sorok az ErrorsFor-ral válogassanak.
        var result = new ValidationResult(errors);
        foreach (var item in Items)
        {
            item.ApplyErrors(result);
        }
    }

    private void Navigate(ValidationError error)
    {
        var item = Items.FirstOrDefault(i => i.Kind == error.Kind && i.ItemKey == error.ItemKey);
        if (item is null)
        {
            return;
        }

        Select(item);
        item.RequestFocus(error.PropertyName);
    }
}
