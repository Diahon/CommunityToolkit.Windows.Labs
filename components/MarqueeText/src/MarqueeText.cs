// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace CommunityToolkit.Labs.WinUI.MarqueeTextRns;

/// <summary>
/// A Control that displays Text in a Marquee style.
/// </summary>
[TemplatePart(Name = MarqueeContainerPartName, Type = typeof(Panel))]
[TemplatePart(Name = Segment1PartName, Type = typeof(FrameworkTemplate))]
[TemplatePart(Name = Segment2PartName, Type = typeof(FrameworkTemplate))]
[TemplatePart(Name = Segment2PartName, Type = typeof(FrameworkTemplate))]
[TemplatePart(Name = MarqueeTransformPartName, Type = typeof(TranslateTransform))]
[TemplateVisualState(GroupName = DirectionVisualStateGroupName, Name = LeftwardsVisualStateName)]
[TemplateVisualState(GroupName = DirectionVisualStateGroupName, Name = RightwardsVisualStateName)]
[TemplateVisualState(GroupName = DirectionVisualStateGroupName, Name = UpwardsVisualStateName)]
[TemplateVisualState(GroupName = DirectionVisualStateGroupName, Name = DownwardsVisualStateName)]
[TemplateVisualState(GroupName = BehaviorVisualStateGroupName, Name = TickerVisualStateName)]
[TemplateVisualState(GroupName = BehaviorVisualStateGroupName, Name = LoopingVisualStateName)]
[TemplateVisualState(GroupName = BehaviorVisualStateGroupName, Name = BouncingVisualStateName)]
[ContentProperty(Name = nameof(Text))]

#if HAS_UNO
// See: https://github.com/CommunityToolkit/Labs-Windows/pull/275#issuecomment-1331113635
#pragma warning disable CA1001
#endif
public partial class MarqueeText : Control
{
    private const string MarqueeContainerPartName = "MarqueeContainer";
    private const string Segment1PartName = "Segment1";
    private const string Segment2PartName = "Segment2";
    private const string MarqueeTransformPartName = "MarqueeTransform";

    private const string MarqueeActiveState = "MarqueeActive";
    private const string MarqueeStoppedState = "MarqueeStopped";

    private const string DirectionVisualStateGroupName = "DirectionStateGroup";
    private const string LeftwardsVisualStateName = "Leftwards";
    private const string RightwardsVisualStateName = "Rightwards";
    private const string UpwardsVisualStateName = "Upwards";
    private const string DownwardsVisualStateName = "Downwards";

    private const string BehaviorVisualStateGroupName = "BehaviorStateGroup";
    private const string TickerVisualStateName = "Ticker";
    private const string LoopingVisualStateName = "Looping";
    private const string BouncingVisualStateName = "Bouncing";

    private Panel? _marqueeContainer;
    private FrameworkElement? _segment1;
    private FrameworkElement? _segment2;
    private TranslateTransform? _marqueeTransform;
    private Storyboard? _marqueeStoryboard;

    private bool _isActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarqueeText"/> class.
    /// </summary>
    public MarqueeText()
    {
        DefaultStyleKey = typeof(MarqueeText);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Implicit casting throws early when parts are missing from the template
        _marqueeContainer = (Panel)GetTemplateChild(MarqueeContainerPartName);
        _segment1 = (FrameworkElement)GetTemplateChild(Segment1PartName);
        _segment2 = (FrameworkElement)GetTemplateChild(Segment2PartName);
        _marqueeTransform = (TranslateTransform)GetTemplateChild(MarqueeTransformPartName);

        _marqueeContainer.SizeChanged += Container_SizeChanged;

        // Swapping tabs in TabView caused errors where the control would unload and never reattach events.
        // Hotfix: Don't detach events. This should be fine because the GC will handle it.
        // However, more research is required.
        //Unloaded += MarqueeText_Unloaded;

        VisualStateManager.GoToState(this, GetVisualStateName(Direction), false);
        VisualStateManager.GoToState(this, GetVisualStateName(Behavior), false);
    }

    private static string GetVisualStateName(MarqueeDirection direction)
    {
        return direction switch
        {
            MarqueeDirection.Left => LeftwardsVisualStateName,
            MarqueeDirection.Right => RightwardsVisualStateName,
            MarqueeDirection.Up => UpwardsVisualStateName,
            MarqueeDirection.Down => DownwardsVisualStateName,
            _ => LeftwardsVisualStateName,
        };
    }

    private static string GetVisualStateName(MarqueeBehavior behavior)
    {
        return behavior switch
        {
            MarqueeBehavior.Ticker => TickerVisualStateName,
            MarqueeBehavior.Looping => LoopingVisualStateName,
#if !HAS_UNO
            MarqueeBehavior.Bouncing => BouncingVisualStateName,
#endif
            _ => TickerVisualStateName,
        };
    }

    /// <summary>
    /// Begins the Marquee animation if not running.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when template parts are not supplied.</exception>
    public void StartMarquee()
    {
        bool initial = _isActive;
        _isActive = true;
        bool playing = UpdateAnimation(initial);

        // Invoke MarqueeBegan if Marquee is now playing and was not before
        if (playing && !initial)
        {
            MarqueeBegan?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Stops the Marquee animation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when template parts are not supplied.</exception>
    public void StopMarquee()
    {
        StopMarquee(_isActive);
    }

    private void StopMarquee(bool initial)
    {
        // Set _isActive and update the animation to match
        _isActive = false;
        bool playing = UpdateAnimation(false);

        // Invoke MarqueeStopped if Marquee is not playing and was before
        if (!playing && initial)
        {
            MarqueeStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Updates the animation to match the current control state.
    /// </summary>
    /// <param name="resume">True if animation should resume from its current position, false if it should restart.</param>
    /// <exception cref="InvalidOperationException">Thrown when template parts are not supplied.</exception>
    /// <returns>True if the Animation is now playing.</returns>
    private bool UpdateAnimation(bool resume = true)
    {
        // Crucial template parts are missing!
        // This should never happen because an exception should have been thrown in OnApplyTemplate.
        // However, should this happen, just return false meaning the animation could not play.
        if (_marqueeContainer is null ||
            _marqueeTransform is null ||
            _segment1 is null ||
            _segment2 is null)
        {
            return false;
        }

        // The marquee is stopped.
        // Update the animation to the stopped position.
        if (!_isActive)
        {
            VisualStateManager.GoToState(this, MarqueeStoppedState, false);

            return false;
        }

        // Get the size of the container and segment, based on the orientation.
        // Also track the property to adjust, also based on the orientation.
        double containerSize;
        double segmentSize;
        double value;
        string targetProperty;

        if (IsDirectionHorizontal)
        {
            // The direction is horizontal, so the sizes, value, and properties
            // are defined by width and X coordinates.
            containerSize = _marqueeContainer.ActualWidth;
            segmentSize = _segment1.ActualWidth;
            value = _marqueeTransform.X;
            targetProperty = "(TranslateTransform.X)";
        }
        else
        {
            // The direction is vertical, so the sizes, value, and properties
            // are defined by height and Y coordinates.
            containerSize = _marqueeContainer.ActualHeight;
            segmentSize = _segment1.ActualHeight;
            value = _marqueeTransform.Y;
            targetProperty = "(TranslateTransform.Y)";
        }

        if (IsLooping && segmentSize < containerSize)
        {
            // If the marquee is in looping mode and the segment is smaller
            // than the container then the animation does not not need to play.

            // NOTE: Use resume as initial because _isActive is updated before
            // calling update animation. If _isActive were passed, it would allow for
            // MarqueeStopped to be invoked when the marquee was already stopped.

            StopMarquee(resume);
            _segment2.Visibility = Visibility.Collapsed;
            return false;
        }

        // The start position is offset 100% if in ticker mode
        // Otherwise it's 0
        double start = IsTicker ? containerSize : 0;

        // The end is when the end of the text reaches the border if in bouncing mode
        // Otherwise it is when the first set of text is 100% out of view
        double end = IsBouncing ? containerSize - segmentSize : -segmentSize;

        // The distance is used for calculating the duration and the previous
        // animation progress if resuming
        double distance = Math.Abs(start - end);

        // If the distance is zero, don't play an animation
        if (distance is 0)
        {
            return false;
        }

        // Swap the start and end to inverse direction for right or upwards
        if (IsDirectionInverse)
        {
            (start, end) = (end, start);
        }

        // The second segment of text should be hidden if the marquee is not in looping mode
        _segment2.Visibility = IsLooping ? Visibility.Visible : Visibility.Collapsed;

        // Calculate the animation duration by dividing the distance by the speed
        TimeSpan duration = TimeSpan.FromSeconds(distance / Speed);

        // Unbind events from the old storyboard
        if (_marqueeStoryboard is not null)
        {
            _marqueeStoryboard.Completed -= StoryBoard_Completed;
        }

        // Create new storyboard and animation
        _marqueeStoryboard = CreateMarqueeStoryboardAnimation(start, end, duration, targetProperty);

        // Bind the storyboard completed event
        _marqueeStoryboard.Completed += StoryBoard_Completed;

        // Set the visual state to active and begin the animation
        VisualStateManager.GoToState(this, MarqueeActiveState, true);
        _marqueeStoryboard.Begin();
        
        // If resuming, seek the animation so the text resumes from its current position.
        if (resume)
        {
            double progress = Math.Abs(start - value) / distance;
            _marqueeStoryboard.Seek(TimeSpan.FromTicks((long)(duration.Ticks * progress)));
        }

        return true;
    }

    private Storyboard CreateMarqueeStoryboardAnimation(double start, double end, TimeSpan duration, string targetProperty)
    {   
        // Initialize the new storyboard
        var marqueeStoryboard = new Storyboard
        {
            Duration = duration,
            RepeatBehavior = RepeatBehavior,
#if !HAS_UNO
            AutoReverse = IsBouncing,
#endif
        };
        
        // Create a new double animation, moving from [start] to [end] positions in [duration] time.
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = duration,
            RepeatBehavior = RepeatBehavior,
#if !HAS_UNO
            AutoReverse = IsBouncing,
#endif
        };

        // Create the key frames
        var frame1 = new DiscreteDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),
            Value = start,
        };
        var frame2 = new EasingDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(duration),
            Value = end,
        };

        // Add the key frames to the animation
        animation.KeyFrames.Add(frame1);
        animation.KeyFrames.Add(frame2);

        // Add the double animation to the storyboard
        marqueeStoryboard.Children.Add(animation);
        
        // Set the storyboard target and target property
        Storyboard.SetTarget(animation, _marqueeTransform);
        Storyboard.SetTargetProperty(animation, targetProperty);

        return marqueeStoryboard;
    }
}

#if HAS_UNO
#pragma warning restore CA1001
#endif
