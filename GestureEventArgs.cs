using System.Numerics;

namespace GestureBehavior.GestureBehavior;

public sealed class PanEventArgs(Point[] touches, Vector2 distance, Rect viewPosition, Direction direction, GestureStatus gestureStatus)
	: MotionEventArgs(touches, viewPosition, direction)
{
	public Vector2 Distance { get; } = distance;
	public GestureStatus GestureStatus { get; } = gestureStatus;
}

public sealed class SwipeEventArgs(Point[] touches, Vector2 distance, Vector2 velocity, Rect viewPosition, Direction direction)
	: MotionEventArgs(touches, viewPosition, direction)
{
	public Vector2 Distance { get; } = distance;
	public Vector2 Velocity { get; } = velocity;
}

public sealed class LongPressEventArgs(Point touch, Rect viewPosition) : SingleTapEventArgs(touch, viewPosition)
{
}

public sealed class TapEventArgs(Point touch, Rect viewPosition) : SingleTapEventArgs(touch, viewPosition)
{
}

public abstract class MotionEventArgs(Point[] touches, Rect viewPosition, Direction direction) : BaseEventArgs(viewPosition)
{
	public Point[] Touches { get; } = touches;
	public Direction Direction { get; } = direction;
	public Point Center { get; } = GetCenter(touches);
}

public abstract class SingleTapEventArgs(Point touch, Rect viewPosition) : BaseEventArgs(viewPosition)
{
	public Point Touch { get; } = touch;
}

public enum Direction
{
	Unknow,
	Up,
	Down,
	Right,
	Left
}


public abstract class BaseEventArgs(Rect viewPosition) : EventArgs
{
	public Rect ViewPosition { get; } = viewPosition;

	protected static Point GetCenter(Point[] touches)
	{
		var size = touches.Length;
		switch (size)
		{
			case 0: return Point.Zero;
			case 1: return touches[0];
			default:
				double x = 0, y = 0;
				for (int i = 0; i < size; i++)
				{
					x += touches[i].X;
					y += touches[i].Y;
				}
				return new Point(x / size, y / size);
		}
	}
}
