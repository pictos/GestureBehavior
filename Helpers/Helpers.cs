
namespace GestureBehavior.GestureBehavior;
static partial class Helpers
{
	internal static bool TryFindParentElementWithParentOfType<T>(this VisualElement? element, out VisualElement? result, out T? parent) where T : VisualElement
	{
		result = null;
		parent = null;

		while (element?.Parent is not null)
		{
			if (element.Parent is not T parentElement)
			{
				element = element.Parent as VisualElement;
				continue;
			}

			result = element;
			parent = parentElement;

			return true;
		}

		return false;
	}

	internal static bool TryFindParentOfType<T>(this VisualElement? element, out T? parent) where T : VisualElement
		=> TryFindParentElementWithParentOfType(element, out _, out parent);

	public static IEnumerable<GestureBehavior> HandleGestureOnParents(this VisualElement visualElement)
	{

		var p = visualElement.Parent;
		while (p is VisualElement parent)
		{
			foreach (var b in parent.Behaviors.OfType<GestureBehavior>())
				yield return b;

			p = p.Parent;
		}
	}
}

