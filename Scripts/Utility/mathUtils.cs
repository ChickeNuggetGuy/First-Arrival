using Godot;
using System;

public partial class mathUtils
{
	private static readonly (float Accuracy, float MaxDeg)[] WorstCaseByAccuracy =
	{
		(0f, 28.4f),
		(10f, 26.5f),
		(20f, 24.6f),
		(30f, 22.7f),
		(40f, 20.8f),
		(50f, 18.9f),
		(60f, 17.0f),
		(70f, 15.1f),
		(80f, 13.2f),
		(90f, 11.2f),
		(100f, 1.7f),
	};

	/// Returns the maximum horizontal and vertical deviation for a shot, as a Vector2.
	/// - accuracy: stated ranged accuracy in [0, 100]
	/// - verticalScale: multiplier for vertical deviation (default 1.0 = same as horizontal)
	/// - outputInDegrees: if true, returns degrees; if false, returns radians
	public static Vector2 GetMaxDeviation(
		float accuracy,
		float verticalScale = 1.0f,
		bool outputInDegrees = true
	)
	{
		// Clamp input
		float a = MathF.Max(0f, MathF.Min(100f, accuracy));

		// Find bracketing points for interpolation
		float maxDeg = WorstCaseByAccuracy[0].MaxDeg;

		for (int i = 0; i < WorstCaseByAccuracy.Length - 1; i++)
		{
			var p0 = WorstCaseByAccuracy[i];
			var p1 = WorstCaseByAccuracy[i + 1];

			if (a >= p0.Accuracy && a <= p1.Accuracy)
			{
				float t =
					(a - p0.Accuracy) / MathF.Max(1e-6f, (p1.Accuracy - p0.Accuracy));
				maxDeg = Mathf.Lerp(p0.MaxDeg, p1.MaxDeg, t);
				break;
			}

			// If above the last bracket, use the last value
			if (a > WorstCaseByAccuracy[^1].Accuracy)
			{
				maxDeg = WorstCaseByAccuracy[^1].MaxDeg;
			}
		}

		float horiz = maxDeg;
		float vert = maxDeg * verticalScale;

		if (!outputInDegrees)
		{
			float toRad = MathF.PI / 180f;
			horiz *= toRad;
			vert *= toRad;
		}

		return new Vector2(horiz, vert);
	}
}
