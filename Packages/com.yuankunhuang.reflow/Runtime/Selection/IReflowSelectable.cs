using System;

namespace Reflow
{
    /// <summary> An <see cref="IReflowItem"/> that <see cref="ReflowSelection"/> can select. </summary>
    public interface IReflowSelectable
    {
        /// <summary> Raise with the item itself when the user clicks it. </summary>
        event Action<IReflowSelectable> Clicked;

        /// <summary> False: clicks are ignored and the selection stays where it is. </summary>
        bool IsSelectable { get; }

        /// <summary> Show the selected / normal state. Called on spawn and whenever the selection changes. </summary>
        void SetSelected(bool pSelected);
    }
}
