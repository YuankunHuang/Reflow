using UnityEngine;

namespace Reflow
{
    [AddComponentMenu("Layout/Reflow/Horizontal")]
    public class ReflowHorizontal : ReflowLinear
    {
        public ReflowHorizontal()
        {
            // Rows usually center their children vertically.
            _childAlignment = TextAnchor.MiddleLeft;
        }

        protected override int MainAxis => 0;
    }
}
