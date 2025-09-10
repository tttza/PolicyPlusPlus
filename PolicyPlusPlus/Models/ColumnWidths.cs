using Microsoft.UI.Xaml;

namespace PolicyPlusPlus.Models
{
    public sealed class ColumnWidths : DependencyObject
    {
        public GridLength NameWidth
        {
            get => (GridLength)GetValue(NameWidthProperty);
            set => SetValue(NameWidthProperty, value);
        }
        public static readonly DependencyProperty NameWidthProperty =
            DependencyProperty.Register(nameof(NameWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(3, GridUnitType.Star)));

        public GridLength IdWidth
        {
            get => (GridLength)GetValue(IdWidthProperty);
            set => SetValue(IdWidthProperty, value);
        }
        public static readonly DependencyProperty IdWidthProperty =
            DependencyProperty.Register(nameof(IdWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(1.5, GridUnitType.Star)));

        public GridLength CategoryWidth
        {
            get => (GridLength)GetValue(CategoryWidthProperty);
            set => SetValue(CategoryWidthProperty, value);
        }
        public static readonly DependencyProperty CategoryWidthProperty =
            DependencyProperty.Register(nameof(CategoryWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(2, GridUnitType.Star)));

        public GridLength AppliesWidth
        {
            get => (GridLength)GetValue(AppliesWidthProperty);
            set => SetValue(AppliesWidthProperty, value);
        }
        public static readonly DependencyProperty AppliesWidthProperty =
            DependencyProperty.Register(nameof(AppliesWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

        public GridLength SupportedWidth
        {
            get => (GridLength)GetValue(SupportedWidthProperty);
            set => SetValue(SupportedWidthProperty, value);
        }
        public static readonly DependencyProperty SupportedWidthProperty =
            DependencyProperty.Register(nameof(SupportedWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(2, GridUnitType.Star)));

        public GridLength UserWidth
        {
            get => (GridLength)GetValue(UserWidthProperty);
            set => SetValue(UserWidthProperty, value);
        }
        public static readonly DependencyProperty UserWidthProperty =
            DependencyProperty.Register(nameof(UserWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));

        public GridLength ComputerWidth
        {
            get => (GridLength)GetValue(ComputerWidthProperty);
            set => SetValue(ComputerWidthProperty, value);
        }
        public static readonly DependencyProperty ComputerWidthProperty =
            DependencyProperty.Register(nameof(ComputerWidth), typeof(GridLength), typeof(ColumnWidths), new PropertyMetadata(new GridLength(1, GridUnitType.Star)));
    }
}
