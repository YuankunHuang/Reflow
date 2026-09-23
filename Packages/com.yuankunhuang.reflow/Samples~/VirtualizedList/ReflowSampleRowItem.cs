using TMPro;

namespace Reflow.Samples
{
    public sealed class ReflowSampleRow
    {
        public int id;
        public string text;
    }

    /// <summary> List item: a card whose height follows its wrapped text. </summary>
    public sealed class ReflowSampleRowItem : ReflowItemBehaviour<ReflowSampleRow>
    {
        public TextMeshProUGUI label;

        protected override void OnShow(ReflowSampleRow pData)
        {
            label.text = $"<b>#{pData.id}</b>  {pData.text}";
        }
    }
}
