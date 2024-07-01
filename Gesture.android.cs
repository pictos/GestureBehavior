using Android.Content;
using Android.Views;
using Microsoft.Maui.Platform;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using AView = Android.Views.View;

namespace GestureBehavior.GestureBehavior;

partial class GestureBehavior
{
	internal GestureDetector? gestureDetector;
	internal AView? PlatformView { get; private set; }
	VisualElement visualElement = default!;

	protected override void OnAttachedTo(VisualElement bindable, Android.Views.View platformView)
	{
		BindingContext = bindable.BindingContext;
		visualElement = bindable;
		PlatformView = platformView;
		gestureDetector = new MyGestureDetector(platformView.Context, new SimpleGestureListener(this, platformView.Context!));
		platformView.Touch += OnPlatformTouch;
	}

	protected override void OnDetachedFrom(VisualElement bindable, Android.Views.View platformView)
	{
		Debug.Assert(gestureDetector is not null, "GestureDetector shouldn't be null here");
		platformView.Touch -= OnPlatformTouch;
		PlatformView = null;
		visualElement = default!;
		gestureDetector.Dispose();
	}

	void OnPlatformTouch(object? sender, Android.Views.View.TouchEventArgs e)
	{
		Debug.Assert(gestureDetector is not null, "GestureDetector shouldn't be null here");

		gestureDetector.OnTouchEvent(e.Event!);
		var motion = MotionEvent.Obtain(e.Event);

		HandleFlowGesture(motion);
		motion?.Recycle();
	}


	void HandleFlowGesture(MotionEvent? e)
	{
		if (!FlowGesture || e is null)
			return;

		foreach (var b in visualElement.HandleGestureOnParents())
		{
			b.gestureDetector?.OnTouchEvent(e);
		}
	}

	public void FireTouchEvent(MotionEvent e)
	{
		gestureDetector?.OnTouchEvent(e);
	}
}


sealed class SimpleGestureListener : GestureDetector.SimpleOnGestureListener, IDisposable
{
	GestureBehavior behavior;
	private readonly Context context;
	//TaskCompletionSource<bool> doubleTapCompletionSource = new();
	//CancellationTokenSource cts = RegisterNewCts();
	bool isScrolling;

	int scaledMaximumFlingVelocity;
	public SimpleGestureListener(GestureBehavior behavior, Context context)
	{
		this.behavior = behavior;
		this.context = context;
		var settings = ViewConfiguration.Get(context);
		scaledMaximumFlingVelocity = settings!.ScaledMaximumFlingVelocity;
	}

	static CancellationTokenSource RegisterNewCts()
	{
		return new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
	}

	// This event is fired even during a doubleTap, so added this Task machinery to
	// cancel it if a doubleTap is registered. The threshold is 100ms.
	//public override bool OnSingleTapUp(MotionEvent e)
	//{
	//	var motionEvent = MotionEvent.Obtain(e)!;
	//	cts.Dispose();
	//	cts = RegisterNewCts();
	//	var task = Task.Run(async () =>
	//	{
	//		return await doubleTapCompletionSource.Task.WaitAsync(cts.Token);
	//	})
	//	.ContinueWith(t =>
	//	{
	//		Debug.Assert(MainThread.IsMainThread, "It should run on UI thread");
	//		if (t.Status == TaskStatus.Canceled)
	//		{
	//			Logger();
	//			var result = base.OnSingleTapUp(motionEvent);
	//			var x = motionEvent.GetX();
	//			var y = motionEvent.GetY();

	//			var args = new TapEventArgs(new(x, y), new Point(x / 2, y / 2));

	//			behavior.TapFire(args);
	//			motionEvent.Recycle();
	//			return result;
	//		}

	//		return false;

	//	}, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());

	//	return false;
	//}

	// Can use this instead, but it takes longer than my implementation of OnSingleTapUp
	// Not sure which one to use...

	public override bool OnSingleTapConfirmed(MotionEvent e)
	{
		Logger();

		var args = GenerateTapEventArgs(e);

		behavior.TapFire(args);
		return base.OnSingleTapConfirmed(e);
	}


	TapEventArgs GenerateTapEventArgs(MotionEvent e)
	{
		var x = context.FromPixels(e.GetX());
		var y = context.FromPixels(e.GetY());
		return new TapEventArgs(new Point(x, y), behavior.PlatformView.GetViewPosition());
	}

	public override void OnLongPress(MotionEvent e)
	{
		Logger();
		var x = context.FromPixels(e.GetX());
		var y = context.FromPixels(e.GetY());
		var args = new LongPressEventArgs(new(x, y), behavior.PlatformView.GetViewPosition());

		behavior.LongPressFire(args);
		base.OnLongPress(e);
	}

	// Use this to move Views on the screen

	MotionEvent? previous;
	MotionEvent? Previous
	{
		get => previous;
		set
		{
			previous?.Recycle();
			previous = value is null ? null : MotionEvent.Obtain(value);
		}
	}


	// To next time: Implement Up action inside OnFling
	public override bool OnScroll(MotionEvent? e1, MotionEvent e2, float distanceX, float distanceY)
	{
		isScrolling = true;

		if (e1 is null)
			return false;

		var distance = new Vector2((float)context.FromPixels(distanceX), (float)context.FromPixels(distanceY));

		var touches = ComputeTouches(e2, context);

		GestureStatus status;

		// If Previous is null I should infeer that we're hadling the start gesture and it should be zeroed args
		if (Previous is null)
			status = e1.Action.ToGestureStatus();
		else
			status = e2.Action.ToGestureStatus();

		var direction = ComputeDirection(distanceX, distanceY);
		var args = new PanEventArgs(touches, distance, behavior.PlatformView.GetViewPosition(), direction, status);
		behavior.PanFire(args);

		if (e2.Action == MotionEventActions.Up)
			HandleOnScrollUp(e2);

		Previous = e2;
		Logger();
		//System.Diagnostics.Debug.WriteLine($"Action: {e2.Action}");
		return base.OnScroll(e1, e2, distanceX, distanceY);
	}


	static Point[] ComputeTouches(MotionEvent current, Context context)
	{
		var pointers = current.PointerCount;
		var touches = new Point[pointers];
		var coordenates = new MotionEvent.PointerCoords();

		for (var i = 0; i < pointers; i++)
		{
			current.GetPointerCoords(i, coordenates);
			touches[i] = new((float)context.FromPixels(coordenates.X), (float)context.FromPixels(coordenates.Y));
		}

		return touches;
	}

	static Direction ComputeDirection(float dX, float dY)
	{
		Direction direction;

		if (Math.Abs(dX) > Math.Abs(dY))
		{
			direction = dX > 0 ? Direction.Left : Direction.Right;
		}
		else
		{
			direction = dY > 0 ? Direction.Up : Direction.Down;
		}

		return direction;
	}

	static Direction ComputeSwipeDirection(float dX, float dY)
	{
		Direction direction;

		if (Math.Abs(dX) > Math.Abs(dY))
		{
			direction = dX < 0 ? Direction.Left : Direction.Right;
		}
		else
		{
			direction = dY < 0 ? Direction.Up : Direction.Down;
		}

		return direction;
	}

	// This method is always fired after OnScroll interaction, based on the velocity of the gesture
	// which can be difficult to determine which one to use
	void HandleOnScrollUp(MotionEvent currentEvent)
	{
		if (!isScrolling)
			return;

		var cX = currentEvent.GetX();
		var cY = currentEvent.GetY();
		var pX = Previous?.GetX() ?? 0;
		var pY = Previous?.GetY() ?? 0;

		var dX = cX - pX;
		var dY = cY - pY;

		var distance = Helpers.CalculateDistances(currentEvent, Previous, context);

		var touches = ComputeTouches(currentEvent, context);

		var direction = ComputeDirection(dX, dY);

		var args = new PanEventArgs(touches, distance, behavior.PlatformView.GetViewPosition(), direction, GestureStatus.Completed);

		behavior.PanFire(args);
		isScrolling = false;
	}

	public override bool OnFling(MotionEvent? e1, MotionEvent e2, float velocityX, float velocityY)
	{
		var relativeVx = velocityX / scaledMaximumFlingVelocity;
		var relativeVy = velocityY / scaledMaximumFlingVelocity;

		var swipedX = Math.Abs(relativeVx) > GestureBehavior.SwipeVelocityThreshold;
		var swipedY = Math.Abs(relativeVy) > GestureBehavior.SwipeVelocityThreshold;


		HandleOnScrollUp(e2);

		if (swipedX || swipedY)
		{
			var distance = Helpers.CalculateDistances(e1, e2, context);
			var velocity = new Vector2((float)context.FromPixels(velocityX), (float)context.FromPixels(velocityY));

			var touches = ComputeTouches(e2, context);

			var direction = ComputeSwipeDirection(velocityX, velocityY);

			var args = new SwipeEventArgs(touches, distance, velocity, behavior.PlatformView.GetViewPosition(), direction);
			behavior.SwipeFire(args);
		}

		isScrolling = false;
		Previous = null;
		return false;
	}

	public override bool OnDoubleTap(MotionEvent e)
	{
		//doubleTapCompletionSource.SetResult(true);
		//doubleTapCompletionSource = new();
		var args = GenerateTapEventArgs(e);
		Logger();
		behavior.DoubleTapFire(args);
		return base.OnDoubleTap(e);
	}

	public override bool OnDoubleTapEvent(MotionEvent e)
	{
		Logger();
		return base.OnDoubleTapEvent(e);
	}

	public override void OnShowPress(MotionEvent e)
	{
		Logger();
		base.OnShowPress(e);
	}

	public override bool OnDown(MotionEvent e)
	{
		Logger();
		return true;
	}

	static void Logger([CallerMemberName] string name = "", [CallerLineNumber] int number = 0)
	{
		//System.Diagnostics.Debug.WriteLine(" ##############################");
		//System.Diagnostics.Debug.WriteLine($"Called from {name} at line: {number}");
	}

	protected override void Dispose(bool disposing)
	{
		behavior = null!;
		//cts?.Dispose();
		base.Dispose(disposing);
	}
}


// Create the custom GestureDetector type, just to override the Dispose method
// and dipose the listener.
sealed class MyGestureDetector : GestureDetector
{
	private readonly IOnGestureListener listener;

	[Obsolete]
	public MyGestureDetector(IOnGestureListener listener) : base(listener)
	{
		this.listener = listener;
	}

	public MyGestureDetector(Context? context, IOnGestureListener listener) : base(context, listener)
	{
		this.listener = listener;
	}

	[Obsolete]
	public MyGestureDetector(IOnGestureListener listener, Android.OS.Handler? handler) : base(listener, handler)
	{
		this.listener = listener;
	}

	public MyGestureDetector(Context? context, IOnGestureListener listener, Android.OS.Handler? handler) : base(context, listener, handler)
	{
		this.listener = listener;
	}

	public MyGestureDetector(Context? context, IOnGestureListener listener, Android.OS.Handler? handler, bool unused) : base(context, listener, handler, unused)
	{
		this.listener = listener;
	}

	protected override void Dispose(bool disposing)
	{
		listener.Dispose();
		base.Dispose(disposing);
	}
}