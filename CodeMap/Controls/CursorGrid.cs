using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeMap.Controls;

/// <summary>
/// Grid with a configurable pointer cursor for resize handles.
/// </summary>
public sealed class CursorGrid : Grid
{
    public static readonly DependencyProperty CursorShapeProperty = DependencyProperty.Register(
        nameof(CursorShape),
        typeof(InputSystemCursorShape),
        typeof(CursorGrid),
        new PropertyMetadata(InputSystemCursorShape.Arrow, OnCursorShapeChanged));

    public InputSystemCursorShape CursorShape
    {
        get => (InputSystemCursorShape)GetValue(CursorShapeProperty);
        set => SetValue(CursorShapeProperty, value);
    }

    public CursorGrid()
    {
        UpdateProtectedCursor(CursorShape);
    }

    private static void OnCursorShapeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not CursorGrid grid || args.NewValue is not InputSystemCursorShape shape)
        {
            return;
        }

        grid.UpdateProtectedCursor(shape);
    }

    private void UpdateProtectedCursor(InputSystemCursorShape shape)
    {
        ProtectedCursor = InputSystemCursor.Create(shape);
    }
}
