namespace ConfigurationEngine.View.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

/// <summary>
/// Az „N hiba” feliratra kattintáskor a hibás mezőre viszi a fókuszt (view.md, "Mezőnkénti hibajelzés").
/// A vezérlőn megadott mezőnév (<c>controls:FocusTarget.Property="Threshold"</c>) a modell mezőjének neve; ha a vezérlő
/// <see cref="EditableItemViewModel"/> adatkörnyezete ezt a mezőt kéri (<see cref="EditableItemViewModel.PendingFocus"/>),
/// a vezérlő megkapja a fókuszt. Ha a vezérlő még nem látható (pl. most váltott a fül), a betöltése után kapja meg.
/// </summary>
public static class FocusTarget
{
    public static readonly DependencyProperty PropertyProperty = DependencyProperty.RegisterAttached(
        "Property",
        typeof(string),
        typeof(FocusTarget),
        new PropertyMetadata(null, OnPropertyChanged));

    private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
        "Subscription",
        typeof(Subscription),
        typeof(FocusTarget),
        new PropertyMetadata(null));

    public static string? GetProperty(DependencyObject element) => (string?)element.GetValue(PropertyProperty);

    public static void SetProperty(DependencyObject element, string? value) => element.SetValue(PropertyProperty, value);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.OldValue is not null)
        {
            return;
        }

        element.Loaded += (_, _) => Attach(element);
        element.Unloaded += (_, _) => Detach(element);
        element.DataContextChanged += (_, _) =>
        {
            Detach(element);
            if (element.IsLoaded)
            {
                Attach(element);
            }
        };

        if (element.IsLoaded)
        {
            Attach(element);
        }
    }

    private static void Attach(FrameworkElement element)
    {
        Detach(element);
        if (element.DataContext is not EditableItemViewModel item)
        {
            return;
        }

        var subscription = new Subscription(element, item);
        item.FocusRequested += subscription.OnFocusRequested;
        element.SetValue(SubscriptionProperty, subscription);
        TryFocus(element, item);
    }

    private static void Detach(FrameworkElement element)
    {
        if (element.GetValue(SubscriptionProperty) is Subscription subscription)
        {
            subscription.Item.FocusRequested -= subscription.OnFocusRequested;
            element.ClearValue(SubscriptionProperty);
        }
    }

    private static void TryFocus(FrameworkElement element, EditableItemViewModel item)
    {
        if (item.PendingFocus is null || item.PendingFocus != GetProperty(element))
        {
            return;
        }

        // A fülváltás utáni elrendezés és kirajzolás után.
        element.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!element.IsVisible || item.PendingFocus != GetProperty(element))
            {
                return;
            }

            element.Focus();
            if (element is TextBox textBox)
            {
                textBox.SelectAll();
            }

            element.BringIntoView();
            item.PendingFocus = null;
        });
    }

    private sealed class Subscription(FrameworkElement element, EditableItemViewModel item)
    {
        public EditableItemViewModel Item { get; } = item;

        public void OnFocusRequested(object? sender, EventArgs e) => TryFocus(element, Item);
    }
}
