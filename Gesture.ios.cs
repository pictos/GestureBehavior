using System.Diagnostics;
using System.Numerics;
using CoreGraphics;
using UIKit;

namespace GestureBehavior.GestureBehavior;

sealed class CustomGestureRecognizerDelegate : UIGestureRecognizerDelegate
{
	public override bool ShouldRecognizeSimultaneously(UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
	{
		return true;
	}
}

partial class GestureBehavior
{
	static CustomGestureRecognizerDelegate multipleTouchesDelegate = new();
	CGPoint previous = CGPoint.Empty;
	const int swipeVelocityThreshold = 800;

	UITapGestureRecognizer tapGestureRecognizer;
	UITapGestureRecognizer doubleTapGestureRecognizer;
	UIPanGestureRecognizer panGestureRecognizer;
	UILongPressGestureRecognizer longPressGestureRecognizer;

	public GestureBehavior()
	{
		tapGestureRecognizer = new(SingleTapHandler);
		doubleTapGestureRecognizer = new(DoubleTapHandler) { NumberOfTapsRequired = 2 };
		panGestureRecognizer = new(PanGestureHandler);
		longPressGestureRecognizer = new(LongPressHandler);
	}


	protected override void OnAttachedTo(VisualElement bindable, UIView platformView)
	{
		if (FlowGesture)
		{
			tapGestureRecognizer.Delegate = multipleTouchesDelegate;
			doubleTapGestureRecognizer.Delegate = multipleTouchesDelegate;
			panGestureRecognizer.Delegate = multipleTouchesDelegate;
			longPressGestureRecognizer.Delegate = multipleTouchesDelegate;
		}

		platformView.AddGestureRecognizer(tapGestureRecognizer);
		platformView.AddGestureRecognizer(doubleTapGestureRecognizer);
		platformView.AddGestureRecognizer(panGestureRecognizer);
		platformView.AddGestureRecognizer(longPressGestureRecognizer);
	}

	protected override void OnDetachedFrom(VisualElement bindable, UIView platformView)
	{
		platformView.RemoveGestureRecognizer(tapGestureRecognizer);
		platformView.RemoveGestureRecognizer(doubleTapGestureRecognizer);
		platformView.RemoveGestureRecognizer(panGestureRecognizer);
		platformView.RemoveGestureRecognizer(longPressGestureRecognizer);
	}

	void LongPressHandler(UILongPressGestureRecognizer gesture)
	{
		if (gesture.State != UIGestureRecognizerState.Began)
			return;
		
		var view = gesture.View;
		var rect = CalculateViewPosition(view);
		var touch = CalculateTouch(gesture, view);

		var args = new LongPressEventArgs(touch, rect);
		LongPressFire(args);
	}

	// Handle over gesture on this later
	void SingleTapHandler(UITapGestureRecognizer gesture)
	{
		cts.Dispose();
		cts = RegisterNewCts();

		var view = gesture.View;
		var rect = CalculateViewPosition(view);
		var touch = CalculateTouch(gesture, view);

		Task.Run(async () => await doubleTapCompletionSource.Task.WaitAsync(cts.Token)).ContinueWith(t =>
		{
			Debug.Assert(MainThread.IsMainThread, "It should run on UI thread");
			if (t.Status == TaskStatus.Canceled)
			{
				var args = new TapEventArgs(touch, rect);

				TapFire(args);
			}
		}, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
	}

	public void PanGestureHandler(UIPanGestureRecognizer gesture)
	{
		var status = gesture.State.ToMauiStatus();
		var view = gesture.View;
		var translation = gesture.TranslationInView(view);
		var velocity = gesture.VelocityInView(view);
		var distance = new Vector2((float)translation.X, (float)translation.Y);

		var touches = CalculateTouches(gesture, view);
		var rect = CalculateViewPosition(view);
		var direction = Direction.Unknow;

		if (status is GestureStatus.Completed or GestureStatus.Canceled & HandlesSwipe)
		{
			if (previous == CGPoint.Empty)
				return;

			var isSwipeX = Math.Abs(velocity.X) > swipeVelocityThreshold;
			var isSwipeY = Math.Abs(velocity.Y) > swipeVelocityThreshold;

			if (isSwipeX || isSwipeY)
			{
				if (isSwipeX)
				{
					direction = velocity.X > 0 ? Direction.Right : Direction.Left;
				}
				else if (isSwipeY)
				{
					direction = velocity.Y > 0 ? Direction.Down : Direction.Up;
				}

				var panArgs = new PanEventArgs(touches, distance, rect, direction, GestureStatus.Canceled);
				PanFire(panArgs);

				var swipeArgs =
					new SwipeEventArgs(touches, distance, new((float)velocity.X, (float)velocity.Y), rect, direction);
				SwipeFire(swipeArgs);
				gesture.CancelsTouchesInView = true;

				goto FINISH;
			}
		}

		direction = CalculateDirection(translation, previous);
		var args = new PanEventArgs(touches, distance, rect, direction, status);
		PanFire(args);

		previous = translation;
		FINISH:
		if (gesture.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled)
		{
			gesture.SetTranslation(CGPoint.Empty, view);
			previous = CGPoint.Empty;
		}
	}

	void DoubleTapHandler(UITapGestureRecognizer gesture)
	{
		doubleTapCompletionSource.SetResult(true);
		doubleTapCompletionSource = new();
		var view = gesture.View;
		var rect = CalculateViewPosition(view);
		var touch = CalculateTouch(gesture, view);

		var args = new TapEventArgs(touch, rect);
		DoubleTapFire(args);
	}


	static Rect CalculateViewPosition(UIView view)
	{
		var viewBounds = view.Bounds;
		return new Rect(viewBounds.X, viewBounds.Y, viewBounds.Width, viewBounds.Height);
	}

	static Point CalculateTouch(UIGestureRecognizer gesture, UIView view)
	{
		var location = gesture.LocationInView(view);
		return new Point(location.X, location.Y);
	}

	static Point[] CalculateTouches(UIGestureRecognizer gesture, UIView view)
	{
		var numberOfTouches = gesture.NumberOfTouches;

		if (numberOfTouches <= 1)
		{
			var point = CalculateTouch(gesture, view);
			return [point];
		}

		var points = new Point[numberOfTouches];

		for (var i = 0; i < numberOfTouches; i++)
		{
			var location = gesture.LocationOfTouch(i, view);
			points[i] = new Point(location.X, location.Y);
		}

		return points;
	}

	static Direction CalculateDirection(CGPoint translation, CGPoint previous)
	{
		var dX = translation.X - previous.X;
		var dY = translation.Y - previous.Y;

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
}