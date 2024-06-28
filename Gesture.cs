using System.Runtime.CompilerServices;

namespace GestureBehavior.GestureBehavior;

public sealed partial class GestureBehavior : PlatformBehavior<VisualElement>
{
	TaskCompletionSource<bool> doubleTapCompletionSource = new();
	CancellationTokenSource cts = RegisterNewCts();

	public bool FlowGesture { get; set; }

	public static float SwipeVelocityThreshold { get; set; } = 0.1F;

	public event EventHandler<TapEventArgs>? Tap;

	public event EventHandler<TapEventArgs>? DoubleTap;

	public event EventHandler<LongPressEventArgs>? LongPress;

	public event EventHandler<PanEventArgs>? Pan;

	public event EventHandler<SwipeEventArgs>? Swipe;

	bool HandlesSwipe
	{
		get
		{
			var count = Swipe?.GetInvocationList() ?? [];
			return count.Length > 0;
		}
	}

	internal void TapFire(TapEventArgs args) => Tap?.Invoke(this, args);
	internal void LongPressFire(LongPressEventArgs args) => LongPress?.Invoke(this, args);
	internal void PanFire(PanEventArgs args) => Pan?.Invoke(this, args);
	internal void SwipeFire(SwipeEventArgs args) => Swipe?.Invoke(this, args);
	internal void DoubleTapFire(TapEventArgs args) => DoubleTap?.Invoke(this, args);

	static void Logger([CallerMemberName] string name = "", [CallerLineNumber] int number = 0)
	{
		System.Diagnostics.Debug.WriteLine(" ##############################");
		System.Diagnostics.Debug.WriteLine($"Called from {name} at line: {number}");
	}

	static CancellationTokenSource RegisterNewCts() =>
		new(TimeSpan.FromMilliseconds(100));
}
