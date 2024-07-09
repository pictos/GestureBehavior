using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using System.Numerics;
using WGestureRecognizer = Microsoft.UI.Input.GestureRecognizer;
using WGestureSettings = Microsoft.UI.Input.GestureSettings;
using WWindow = Microsoft.UI.Xaml.Window;

namespace GestureBehavior.GestureBehavior;
partial class GestureBehavior
{
	WGestureRecognizer gestureRecognizer = new();
	bool isGestureSucceed, isScrolling;
	IMauiContext? mauiContext;
	FrameworkElement? touchableView;
	VisualElement view = default!;
	bool isDoubleTap;
	WWindow Window => mauiContext!.Services.GetRequiredService<WWindow>();

	public GestureBehavior()
	{
		gestureRecognizer.GestureSettings = WGestureSettings.Tap
			| WGestureSettings.Hold
			| WGestureSettings.HoldWithMouse
			| WGestureSettings.ManipulationTranslateX
			| WGestureSettings.ManipulationTranslateY
			| WGestureSettings.RightTap;
	}

	protected override void OnAttachedTo(VisualElement bindable, FrameworkElement platformView)
	{
		mauiContext = bindable.Handler!.MauiContext!;
		touchableView = platformView;
		view = bindable;

		gestureRecognizer.ManipulationStarted += OnManipulationStarted;
		gestureRecognizer.ManipulationUpdated += OnManipulationUpdated;
		gestureRecognizer.ManipulationCompleted += OnManipulationCompleted;
		gestureRecognizer.Holding += OnHolding;
		gestureRecognizer.Tapped += OnTapped;
		gestureRecognizer.RightTapped += OnRightTapped;

		platformView.PointerPressed += OnPointerPressed;
		platformView.PointerMoved += OnPointerMoved;
		platformView.PointerReleased += OnPointerReleased;
		platformView.PointerCanceled += OnPointerCanceled;
		platformView.DoubleTapped += OnDoubleTapped;
	}

	protected override void OnDetachedFrom(VisualElement bindable, FrameworkElement platformView)
	{
		gestureRecognizer.ManipulationStarted -= OnManipulationStarted;
		gestureRecognizer.ManipulationUpdated -= OnManipulationUpdated;
		gestureRecognizer.ManipulationCompleted -= OnManipulationCompleted;
		gestureRecognizer.Holding -= OnHolding;
		gestureRecognizer.RightTapped -= OnRightTapped;
		gestureRecognizer.Tapped -= OnTapped;

		platformView.PointerPressed -= OnPointerPressed;
		platformView.PointerMoved -= OnPointerMoved;
		platformView.PointerReleased -= OnPointerReleased;
		platformView.PointerCanceled -= OnPointerCanceled;
		platformView.DoubleTapped -= OnDoubleTapped;

		//gestureRecognizer = default!;
		view = default!;
	}

	internal void OnPointerCanceled(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (!e.Pointer.IsInRange)
			return;
		var uiElement = (FrameworkElement?)sender ?? touchableView;

		if (uiElement is null)
			return;

		gestureRecognizer.CompleteGesture();
		uiElement.ReleasePointerCapture(e.Pointer);

		if (!isGestureSucceed)
		{
			if (isScrolling)
			{
				var touch = e.GetCurrentPoint(uiElement).Position.ToMauiPoint();

				var rect = CalculateElementRect(touchableView);

				var arg = new PanEventArgs([touch], Vector2.Zero, rect, Direction.Unknow, GestureStatus.Canceled);
				PanFire(arg);
				isScrolling = false;
			}
		}

		isGestureSucceed = false;
		FlowGestureToInnerView(e, EventType.PointerCanceled);
	}

	internal void OnPointerReleased(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (!e.Pointer.IsInRange)
			return;
		var uiElement = (FrameworkElement?)sender ?? touchableView;

		if (uiElement is null)
			return;
		isGestureSucceed = true;
		gestureRecognizer.ProcessUpEvent(e.GetCurrentPoint(uiElement));
		uiElement.ReleasePointerCapture(e.Pointer);

		FlowGestureToInnerView(e, EventType.PointerReleased);
	}

	internal void OnPointerMoved(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (!e.Pointer.IsInRange)
			return;
		var uiElement = (FrameworkElement?)sender ?? touchableView;

		if (uiElement is null)
			return;
		try
		{
			gestureRecognizer.ProcessMoveEvents(e.GetIntermediatePoints(uiElement));
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"{ex} : {ex.Message}");
		}

		FlowGestureToInnerView(e, EventType.PointerMoved);
	}

	internal void OnPointerPressed(object? sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		if (!e.Pointer.IsInRange)
			return;
		var uiElement = (FrameworkElement?)sender ?? touchableView;

		if (uiElement is null)
			return;

		try
		{
			uiElement.CapturePointer(e.Pointer);
			isGestureSucceed = false;
			var point = e.GetCurrentPoint(uiElement);
			gestureRecognizer.ProcessDownEvent(point);
		}
		catch (ArgumentException ex)
		{
			Trace.WriteLine($"Error: {ex} : {ex.Message}");
		}
		e.Handled = true;

		FlowGestureToInnerView(e, EventType.PointerPressed);
	}

	internal void OnDoubleTapped(object? sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
	{
		doubleTapCompletionSource.SetResult(true);
		doubleTapCompletionSource = new();
		isDoubleTap = true;

		Logger();
		var uiElement = (FrameworkElement?)sender ?? touchableView;

		if (uiElement is null)
			return;

		var touch = e.GetPosition(uiElement);

		var rect = CalculateElementRect(uiElement);
		var args = new TapEventArgs(new(touch.X, touch.Y), rect);
		DoubleTapFire(args);
		e.Handled = true;
		FlowGestureToInnerView(e, EventType.DoubleTapped);
	}

	void OnHolding(WGestureRecognizer sender, Microsoft.UI.Input.HoldingEventArgs args)
	{
		if (args.HoldingState != Microsoft.UI.Input.HoldingState.Started)
			return;

		var rect = CalculateElementRect(touchableView);
		var touch = args.Position.ToMauiPoint();
		var arg = new LongPressEventArgs(touch, rect);
		LongPressFire(arg);
	}

	void OnManipulationCompleted(WGestureRecognizer sender, Microsoft.UI.Input.ManipulationCompletedEventArgs args)
	{
		Logger();

		var dX = args.Cumulative.Translation.X;
		var dY = args.Cumulative.Translation.Y;

		var distance = args.Cumulative.ToMauiDistance();

		var touch = args.Position.ToMauiPoint();

		var rect = CalculateElementRect(touchableView);
		var direction = ComputeDirection(dX, dY);

		if (HandlesSwipe)
		{
			var velocities = args.Velocities.Linear;
			const double velocityThreshold = 0.5;

			if (Math.Abs(velocities.X) > velocityThreshold || Math.Abs(velocities.Y) > velocityThreshold)
			{
				var swipeArgs = new SwipeEventArgs([touch], distance, new((float)velocities.X, (float)velocities.Y), rect, direction);
				SwipeFire(swipeArgs);
				goto END;
			}
		}

		var arg = new PanEventArgs([touch], distance, rect, direction, GestureStatus.Completed);
		PanFire(arg);

		END:
		isScrolling = false;
	}

	void OnManipulationUpdated(WGestureRecognizer sender, Microsoft.UI.Input.ManipulationUpdatedEventArgs args)
	{
		Logger();
		var dX = args.Delta.Translation.X;
		var dY = args.Delta.Translation.Y;

		var distance = args.Delta.ToMauiDistance();

		var touch = args.Position.ToMauiPoint();

		var rect = CalculateElementRect(touchableView);
		var direction = ComputeDirection(dX, dY);

		var arg = new PanEventArgs([touch], distance, rect, direction, GestureStatus.Running);
		PanFire(arg);
	}

	void OnManipulationStarted(WGestureRecognizer sender, Microsoft.UI.Input.ManipulationStartedEventArgs args)
	{
		isScrolling = true;
		var touch = args.Position.ToMauiPoint();

		var rect = CalculateElementRect(touchableView);

		var arg = new PanEventArgs([touch], Vector2.Zero, rect, Direction.Unknow, GestureStatus.Started);
		PanFire(arg);
	}

	void OnRightTapped(WGestureRecognizer sender, Microsoft.UI.Input.RightTappedEventArgs args)
	{
		Logger();

		var rect = CalculateElementRect(touchableView);
		var touch = args.Position.ToMauiPoint();
		var arg = new LongPressEventArgs(touch, rect);
		LongPressFire(arg);
	}

	//TODO add the machinere created on Android do handle doubleTap here
	void OnTapped(WGestureRecognizer sender, Microsoft.UI.Input.TappedEventArgs args)
	{
		// Need this, because it will be executed twice during a doubleTap
		if (isDoubleTap)
		{
			isDoubleTap = false;
			return;
		}

		Logger();
		cts.Dispose();
		cts = RegisterNewCts();

		var touch = args.Position.ToMauiPoint();
		var rect = CalculateElementRect(touchableView);
		var arg = new TapEventArgs(touch, rect);

		Task.Run(async () => await doubleTapCompletionSource.Task.WaitAsync(cts.Token)).ContinueWith(t =>
		{
			Debug.Assert(MainThread.IsMainThread, "It should run on UI thread");

			if (t.Status == TaskStatus.Canceled)
				TapFire(arg);

		}, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
	}

	static Direction ComputeDirection(double dX, double dY)
	{
		Direction direction;
		if (Math.Abs(dX) > Math.Abs(dY))
		{
			direction = dX > 0 ? Direction.Right : Direction.Left;
		}
		else
		{
			direction = dY > 0 ? Direction.Down : Direction.Up;
		}

		return direction;
	}

	Rect CalculateElementRect(FrameworkElement? uiElement)
	{
		if (uiElement is null)
			return Rect.Zero;

		var transform = uiElement.TransformToVisual(Window.Content);
		var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
		return new(position.X, position.Y, uiElement.ActualWidth, uiElement.ActualHeight);
	}

	void FlowGestureToInnerView(RoutedEventArgs args, EventType eventType)
	{
		if (!FlowGesture)
			return;

		foreach (var behavior in view.HandleGestureOnParents())
		{
			switch (eventType)
			{
				case EventType.PointerPressed:
					behavior.OnPointerPressed(null, (PointerRoutedEventArgs)args);
					break;
				case EventType.PointerMoved:
					behavior.OnPointerMoved(null, (PointerRoutedEventArgs)args);
					break;
				case EventType.PointerReleased:
					behavior.OnPointerReleased(null, (PointerRoutedEventArgs)args);
					break;
				case EventType.PointerCanceled:
					behavior.OnPointerCanceled(null, (PointerRoutedEventArgs)args);
					break;
				case EventType.DoubleTapped:
					behavior.OnDoubleTapped(null, (DoubleTappedRoutedEventArgs)args);
					break;
			}
		}
	}



	enum EventType
	{
		PointerPressed,
		PointerMoved,
		PointerReleased,
		PointerCanceled,
		DoubleTapped
	}
}


//IF in the future there's a need to handle more fingers in touch
// here a snippet on how that can be done

//using Microsoft.UI.Xaml;
//using Microsoft.UI.Xaml.Input;
//using System.Collections.Generic;

//public sealed partial class MainPage : Page
//{
//    private Dictionary<uint, Point> activePointers = new Dictionary<uint, Point>();

//    public MainPage()
//    {
//        this.InitializeComponent();
//        // Subscribe to pointer events
//        this.PointerPressed += OnPointerPressed;
//        this.PointerMoved += OnPointerMoved;
//        this.PointerReleased += OnPointerReleased;
//    }

//    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
//    {
//        var pointer = e.GetCurrentPoint(this);
//        if (pointer.PointerDevice.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Touch)
//        {
//            // Add the pointer ID and its initial position to the tracking dictionary
//            activePointers[pointer.PointerId] = pointer.Position;
//        }
//    }

//    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
//    {
//        var pointer = e.GetCurrentPoint(this);
//        if (activePointers.ContainsKey(pointer.PointerId))
//        {
//            // Update the position of the moving pointer
//            activePointers[pointer.PointerId] = pointer.Position;

//            // Example: If you're specifically looking for two fingers
//            if (activePointers.Count == 2)
//            {
//                // You can access the positions of the two fingers here
//                // For example, logging their positions
//                foreach (var position in activePointers.Values)
//                {
//                    System.Diagnostics.Debug.WriteLine($"Pointer Position: {position}");
//                }
//            }
//        }
//    }

//    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
//    {
//        var pointer = e.GetCurrentPoint(this);
//        // Remove the pointer from the tracking dictionary when it's released
//        if (activePointers.ContainsKey(pointer.PointerId))
//        {
//            activePointers.Remove(pointer.PointerId);
//        }
//    }
//}
