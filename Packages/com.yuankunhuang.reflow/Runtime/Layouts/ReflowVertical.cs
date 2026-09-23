using UnityEngine;

namespace Reflow
{
    [AddComponentMenu("Layout/Reflow/Vertical")]
    public class ReflowVertical : ReflowLinear
    {
        protected override int MainAxis => 1;
    }
}
