using Android.Content;
using Android.Views;
using Microsoft.Maui.Platform;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using AView = Android.Views.View;

namespace GestureBehavior.GestureBehavior;

static partial class Helpers
{
	internal static Rect GetViewPosition(this AView? view)
	{
		if (view is null)
			return Rect.Zero;

		var location = ArrayPool<int>.Shared.Rent(2);

		view.GetLocationInWindow(location);

		var x = location[0];
		var y = location[1];
		var width = view.Width;
		var height = view.Height;

		ArrayPool<int>.Shared.Return(location);
		return DIP.ToRect(x, y, width, height);
	}

	internal static Vector2 CalculateDistances(MotionEvent? e1, MotionEvent? e2, Context context)
	{
		if (e1 is null && e2 is null)
			return Vector2.Zero;

		float x, y = 0;

		if (e1 is null && e2 is not null)
		{
			x = (float)context.FromPixels(e2.GetX());
			y = (float)context.FromPixels(e2.GetY());
			return new(x, y);
		}

		if (e1 is not null && e2 is null)
		{
			x = (float)context.FromPixels(e1.GetX());
			y = (float)context.FromPixels(e1.GetY());
			return new(x, y);
		}

		Debug.Assert(e2 is not null, "e2 MotionEvent should be null");
		Debug.Assert(e1 is not null, "e1 MotionEvent should be null");

		var dX = e2.GetX() - e1.GetX();
		var dY = e2.GetY() - e1.GetY();


		x = (float)context.FromPixels(dX);
		y = (float)context.FromPixels(dY);

		return new(x, y);

	}

	internal static Vector2 GetVelocity(MotionEvent previous, MotionEvent current, Vector2 distance)
	{
		if (previous is null)
			return Vector2.Zero;

		var ms = current.EventTime - previous.EventTime;

		return new((distance.X * 1000 / ms), distance.Y * 1000 / ms);
	}

	internal static GestureStatus ToGestureStatus(this MotionEventActions actions) => actions switch
	{
		MotionEventActions.ButtonPress => GestureStatus.Started,
		MotionEventActions.ButtonRelease => GestureStatus.Completed,
		MotionEventActions.Cancel => GestureStatus.Canceled,
		MotionEventActions.Down => GestureStatus.Started,
		MotionEventActions.Move => GestureStatus.Running,
		MotionEventActions.Outside => GestureStatus.Running,
		MotionEventActions.Pointer1Down => GestureStatus.Started,
		MotionEventActions.Pointer1Up => GestureStatus.Completed,
		MotionEventActions.Pointer2Down => GestureStatus.Started,
		MotionEventActions.Pointer2Up => GestureStatus.Completed,
		MotionEventActions.Pointer3Down => GestureStatus.Started,
		MotionEventActions.Pointer3Up => GestureStatus.Completed,
		MotionEventActions.Up => GestureStatus.Completed,
		_ => GestureStatus.Canceled
	};
}


internal static class DIP
{
	internal static readonly double Density = DeviceDisplay.MainDisplayInfo.Density;

	internal static Point ToPoint(double dipX, double dipY)
	{
		return new Point(dipX / Density, dipY / Density);
	}

	internal static Rect ToRect(double dipX, double dipY, double dipWidth, double dipHeight)
	{
		return new Rect(dipX / Density, dipY / Density, dipWidth / Density, dipHeight / Density);
	}
}
