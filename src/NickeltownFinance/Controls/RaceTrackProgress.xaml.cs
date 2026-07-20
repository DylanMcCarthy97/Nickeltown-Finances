using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace NickeltownFinance.Controls;

public partial class RaceTrackProgress : UserControl
{
    private const double CarWidth = 34;
    private const double TrackPadding = 8;
    private const double FinishReserve = 22;

    private Storyboard? _cruiseStoryboard;

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(RaceTrackProgress),
            new PropertyMetadata(0d, OnVisualStateChanged));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            nameof(IsIndeterminate),
            typeof(bool),
            typeof(RaceTrackProgress),
            new PropertyMetadata(false, OnVisualStateChanged));

    public static readonly DependencyProperty StageLabelProperty =
        DependencyProperty.Register(
            nameof(StageLabel),
            typeof(string),
            typeof(RaceTrackProgress),
            new PropertyMetadata("On track"));

    public static readonly DependencyProperty ProgressLabelProperty =
        DependencyProperty.Register(
            nameof(ProgressLabel),
            typeof(string),
            typeof(RaceTrackProgress),
            new PropertyMetadata("0%"));

    public static readonly DependencyProperty DetailTextProperty =
        DependencyProperty.Register(
            nameof(DetailText),
            typeof(string),
            typeof(RaceTrackProgress),
            new PropertyMetadata(string.Empty));

    public RaceTrackProgress()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisuals();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
                StopCruise();
            else
                UpdateVisuals();
        };
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public string StageLabel
    {
        get => (string)GetValue(StageLabelProperty);
        set => SetValue(StageLabelProperty, value);
    }

    public string ProgressLabel
    {
        get => (string)GetValue(ProgressLabelProperty);
        set => SetValue(ProgressLabelProperty, value);
    }

    public string DetailText
    {
        get => (string)GetValue(DetailTextProperty);
        set => SetValue(DetailTextProperty, value);
    }

    private static void OnVisualStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RaceTrackProgress control)
            control.UpdateVisuals();
    }

    private void TrackHost_OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisuals();

    private void UpdateVisuals()
    {
        if (!IsLoaded || TrackHost.ActualWidth <= 0)
            return;

        var travel = Math.Max(0, TrackHost.ActualWidth - CarWidth - FinishReserve - TrackPadding);

        if (IsIndeterminate)
        {
            Trail.Width = 0;
            StartCruise(travel);
            return;
        }

        StopCruise();

        var percent = Math.Clamp(Progress, 0, 100) / 100.0;
        var x = TrackPadding + (travel * percent);
        CarTransform.X = x;
        Trail.Width = Math.Max(0, x + (CarWidth * 0.45));
    }

    private void StartCruise(double travel)
    {
        StopCruise();

        var to = Math.Max(TrackPadding + 24, travel);
        var animation = new DoubleAnimation
        {
            From = TrackPadding,
            To = to,
            Duration = TimeSpan.FromSeconds(1.55),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            AccelerationRatio = 0.25,
            DecelerationRatio = 0.25
        };

        _cruiseStoryboard = new Storyboard();
        Storyboard.SetTarget(animation, CarTransform);
        Storyboard.SetTargetProperty(animation, new PropertyPath(System.Windows.Media.TranslateTransform.XProperty));
        _cruiseStoryboard.Children.Add(animation);
        _cruiseStoryboard.Begin();
    }

    private void StopCruise()
    {
        if (_cruiseStoryboard is null)
            return;

        _cruiseStoryboard.Stop();
        _cruiseStoryboard.Remove();
        _cruiseStoryboard = null;
    }
}