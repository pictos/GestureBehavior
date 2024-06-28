using Microsoft.UI.Input;
using System.Numerics;
using WPoint = Windows.Foundation.Point;

namespace GestureBehavior.GestureBehavior;


static partial class Helpers
{
	public static Point ToMauiPoint(this WPoint wpoint) =>
		new(wpoint.X, wpoint.Y);

	public static Vector2 ToMauiDistance(this ManipulationDelta delta) =>
		new((float)delta.Translation.X, (float)delta.Translation.Y);


}
