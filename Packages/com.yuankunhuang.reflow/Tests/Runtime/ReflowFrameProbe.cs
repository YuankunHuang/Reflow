using System;
using UnityEngine;

namespace Reflow.Tests
{
    /// <summary>
    /// Runs one callback in the next Update and one in the LateUpdate of that same frame. A component added during a
    /// frame gets LateUpdate before its first Update, so LateUpdate waits for an Update in the same frame.
    /// </summary>
    public sealed class ReflowFrameProbe : MonoBehaviour
    {
        public Action onUpdate;
        public Action onLateUpdate;

        private int _updateFrame = -1;

        private void Update()
        {
            _updateFrame = Time.frameCount;
            Action action = onUpdate;
            onUpdate = null;
            action?.Invoke();
        }

        private void LateUpdate()
        {
            if (_updateFrame != Time.frameCount)
                return;
            Action action = onLateUpdate;
            onLateUpdate = null;
            action?.Invoke();
        }
    }
}
